// CS0162 (unreachable code) is expected and intentional here: EnableCombatPerfTiming is a
// compile-time const that defaults false, so every toggle-guarded branch is const-folded away.
// That IS the design (zero-cost disabled path).
#pragma warning disable CS0162

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

/// <summary>
/// DEBUG-ONLY combat performance timing harness.
///
/// PURPOSE:
///   Measures wall-clock time spent in hot mod code paths during combat so that
///   stutter can be MEASURED (not guessed). All measurements are main-thread only;
///   no locking is used or needed.
///
/// TOGGLE:
///   Set <see cref="EnableCombatPerfTiming"/> to <c>true</c> at compile time to activate.
///   When <c>false</c> (the default) every call site is a no-op:
///     - <see cref="Measure"/> returns <c>default(PerfScope)</c> immediately.
///     - <see cref="PerfScope.Dispose"/> on a default instance is a no-op (label is null).
///     - The JIT const-folds the entire <c>if (EnableCombatPerfTiming)</c> branch away.
///     - <see cref="Reset"/> and <see cref="Dump"/> are no-ops.
///   There is therefore ZERO behavioral and near-zero performance difference when the flag
///   is off. NEVER ship with the flag enabled.
///
/// TIMING SEMANTICS:
///   Each <see cref="PerfScope"/> times from construction to <see cref="PerfScope.Dispose"/>.
///   For <c>async</c> methods the scope times only to the first <c>await</c> suspension point
///   (i.e. the synchronous preamble plus whatever runs before the first await yields).
///   If you want end-to-end async timing you must place the scope outside the awaited call.
///   A comment at each async instrumentation site notes this caveat.
///
/// SCOPE:
///   Measures mod code only. Timings include any synchronous child work that runs inside the
///   measured method body. Awaited child work is included only up to the first suspension.
/// </summary>
internal static class CardEditorDebugPerfTimer
{
    /// <summary>
    /// Master compile-time toggle. <c>false</c> = zero cost. Never ship as <c>true</c>.
    /// </summary>
    internal const bool EnableCombatPerfTiming = false;

    // ── storage ──────────────────────────────────────────────────────────────────

    internal sealed class Entry
    {
        public long TotalTicks;
        public long Calls;
        public long MaxTicks;
    }

    private static Dictionary<string, Entry> _data = new Dictionary<string, Entry>();

    // ── public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a timing scope for <paramref name="label"/>.
    /// When <see cref="EnableCombatPerfTiming"/> is <c>false</c> this returns
    /// <c>default(PerfScope)</c> immediately and the JIT eliminates the whole call.
    /// Usage: <c>using PerfScope _ = CardEditorDebugPerfTimer.Measure("MyLabel");</c>
    /// </summary>
    internal static PerfScope Measure(string label)
    {
        if (!EnableCombatPerfTiming)
        {
            return default;
        }
        return new PerfScope(label);
    }

    /// <summary>
    /// Clears all accumulated data. Call at combat start (inside
    /// <see cref="EnableCombatPerfTiming"/> guard).
    /// </summary>
    internal static void Reset()
    {
        if (!EnableCombatPerfTiming)
        {
            return;
        }
        _data.Clear();
    }

    /// <summary>
    /// Emits a summary log. Call at combat end (inside
    /// <see cref="EnableCombatPerfTiming"/> guard).
    /// Skips labels whose total is under 0.5 ms. Shows the top 12 by total time.
    /// </summary>
    internal static void Dump()
    {
        if (!EnableCombatPerfTiming)
        {
            return;
        }

        double ticksPerMs = Stopwatch.Frequency / 1000.0;

        var lines = _data
            .Where(kv => kv.Value.TotalTicks / ticksPerMs >= 0.5)
            .OrderByDescending(kv => kv.Value.TotalTicks)
            .Take(12)
            .Select(kv =>
            {
                double totalMs = kv.Value.TotalTicks / ticksPerMs;
                double maxMs   = kv.Value.MaxTicks   / ticksPerMs;
                return $"  {kv.Key}: total={totalMs:F1}ms calls={kv.Value.Calls} max={maxMs:F2}ms";
            })
            .ToList();

        if (lines.Count == 0)
        {
            Log.Info("[CardEditor][PerfTimer] ===== combat summary ===== (no labels >= 0.5 ms)");
            return;
        }

        string summary = "[CardEditor][PerfTimer] ===== combat summary =====\n" +
                         string.Join("\n", lines);
        Log.Info(summary);
    }

    // ── PerfScope ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight zero-allocation timing scope.
    /// A <c>default</c> instance (label == null) disposes as a complete no-op.
    /// </summary>
    internal readonly struct PerfScope : IDisposable
    {
        private readonly string? _label;
        private readonly long    _start;

        internal PerfScope(string label)
        {
            _label = label;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            // default(PerfScope) has _label == null — fast no-op path.
            if (_label == null)
            {
                return;
            }

            try
            {
                long elapsed = Stopwatch.GetTimestamp() - _start;

                if (!_data.TryGetValue(_label, out Entry? entry))
                {
                    entry = new Entry();
                    _data[_label] = entry;
                }

                entry.TotalTicks += elapsed;
                entry.Calls      += 1;
                if (elapsed > entry.MaxTicks)
                {
                    entry.MaxTicks = elapsed;
                }
            }
            catch
            {
                // Never let perf instrumentation break the game loop.
            }
        }
    }
}
