using System.Threading;

namespace SlayTheSpire2Mod.CardEditor;

// Central invalidation counter for all runtime caches in the mod.
//
// Every mutation seam that can change a card's EFFECTIVE effect surface (stored overrides,
// draft overrides, instance overrides, created-card definitions, custom keyword/status
// definitions, temporary/granted extra effects, matching-card auras, stateful transform
// markers, self-scaling payloads, preset loads) calls Bump(). Caches capture Current when
// they build and rebuild whenever the captured value no longer matches — so a stale cache
// is structurally impossible as long as every mutation seam bumps.
//
// Over-bumping is always safe (worst case: an unnecessary rebuild). Under-bumping is a
// gameplay-corruption bug, so when in doubt a seam bumps.
internal static class CardEditorRuntimeCacheVersion
{
	private static long _current;

	internal static long Current => Interlocked.Read(ref _current);

	internal static void Bump()
	{
		Interlocked.Increment(ref _current);
	}
}
