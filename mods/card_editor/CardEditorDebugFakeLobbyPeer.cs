// CS0162 (unreachable code) is expected and intentional here: SimulateFakeLobbyPeer /
// TreatLocalAsClientForReadyGate are compile-time consts that default false, so the compiler folds
// the toggle-guarded branches away. That IS the design (zero-cost disabled path).
#pragma warning disable CS0162

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Unlocks;

namespace SlayTheSpire2Mod.CardEditor;

/// <summary>
/// DEBUG-ONLY fake-lobby-peer test harness.
///
/// PURPOSE:
///   Lets a solo developer verify that clicking READY in the StartRunLobby works end-to-end
///   without a second player and without any real networking. It works by injecting one synthetic,
///   already-ready <see cref="LobbyPlayer"/> directly into the lobby's Players list so that
///   <see cref="StartRunLobby.IsAboutToBeginGame"/> sees two players, both ready.
///
/// WHAT IT IS NOT:
///   This does NOT test real networking, peer-to-peer sync, or actual multiplayer run start with
///   a second human. The fake peer is a local in-memory struct; no ENet socket is opened, no
///   message is sent or received for the fake player, and no second process is involved.
///   It is purely for verifying the local ready-check gate logic.
///
/// NEVER SHIP ENABLED:
///   A fake peer in a real multiplayer session would corrupt run start: the host would attempt to
///   begin a run for a player id (4242) that has no real connection, causing BeginRunForAllPlayers
///   to broadcast a LobbyBeginRunMessage to a nonexistent peer. Both consts MUST remain false in
///   any build that ships to players.
///
/// TOGGLES (both default false):
///   <see cref="SimulateFakeLobbyPeer"/>        — inject a ready fake peer after AddLocalHostPlayer.
///   <see cref="TreatLocalAsClientForReadyGate"/>— force the mod's ready gate to behave as if local
///                                                  player were a CLIENT, exercising that code path solo.
///
/// DIAGNOSTICS (active when EITHER const is true):
///   Postfix on IsAboutToBeginGame logs all three gate conditions.
///   Postfix on SetReady logs entry/exit so you can see if the click reached the game.
/// </summary>
internal static class CardEditorDebugFakeLobbyPeer
{
    // ---- Master toggles. BOTH DEFAULT false. Flip in source to test; NEVER ship enabled. ----

    /// <summary>
    /// When true: after AddLocalHostPlayer runs, inject one synthetic already-ready LobbyPlayer
    /// (netId=4242) into the lobby's Players list so IsAboutToBeginGame can return true on
    /// a single local player readying up. Zero networking occurs; this is a pure list manipulation.
    /// </summary>
    internal const bool SimulateFakeLobbyPeer = false;

    /// <summary>
    /// When true: sets <see cref="CardEditorMultiplayerSync.ForceClientReadyGateForTesting"/> = true,
    /// making the mod's AllowClientReady behave as if the local player were a CLIENT. This exercises
    /// the client-side ready gate code path in a solo hosting session without a second player.
    /// Must be false in any real multiplayer session.
    /// </summary>
    internal const bool TreatLocalAsClientForReadyGate = false;

    // NetId for the injected fake peer. Host is 1; DummyLobby client uses 2000 - 4242 is distinct.
    // internal so the patch classes (top-level types in this file) can reference it.
    internal const ulong FakePeerNetId = 4242UL;

    // Whether EITHER diagnostic toggle is active (drives the diagnostic postfixes).
    // internal so the patch classes (which are top-level in this file) can access it.
    internal static bool DiagnosticsActive => SimulateFakeLobbyPeer || TreatLocalAsClientForReadyGate;

    /// <summary>
    /// Called once during mod Prepare, before Harmony patches are applied. Sets the client-gate
    /// test hook if <see cref="TreatLocalAsClientForReadyGate"/> is enabled.
    /// No-op (and zero cost) when both consts are false.
    /// </summary>
    internal static void Prepare()
    {
        // Const-folded: JIT eliminates this body when both toggles are false.
        if (!SimulateFakeLobbyPeer && !TreatLocalAsClientForReadyGate)
        {
            return;
        }

        if (TreatLocalAsClientForReadyGate)
        {
            CardEditorMultiplayerSync.ForceClientReadyGateForTesting = true;
            Log.Info("[CardEditor][FakePeer] TreatLocalAsClientForReadyGate=true: AllowClientReady will " +
                     "behave as if local player is a CLIENT (test-only; ForceClientReadyGateForTesting set).");
        }

        if (SimulateFakeLobbyPeer)
        {
            Log.Info("[CardEditor][FakePeer] SimulateFakeLobbyPeer=true: a synthetic ready peer (netId=4242) " +
                     "will be injected after AddLocalHostPlayer. NOT real networking.");
        }
    }
}

// ---------------------------------------------------------------------------
// PATCH 1: Inject fake peer after AddLocalHostPlayer (SimulateFakeLobbyPeer only)
// ---------------------------------------------------------------------------

/// <summary>
/// Harmony postfix on <see cref="StartRunLobby.AddLocalHostPlayer"/>. After the real host player
/// is added, injects one synthetic already-ready <see cref="LobbyPlayer"/> (netId=4242) directly
/// into <c>Players</c> so that <see cref="StartRunLobby.IsAboutToBeginGame"/> sees two players.
///
/// <para><b>Prepare() returns false when <see cref="CardEditorDebugFakeLobbyPeer.SimulateFakeLobbyPeer"/>
/// is false</b> — the patch is never applied and has zero runtime cost when the toggle is off.</para>
///
/// <para>LobbyPlayer is a struct stored in a List, so modifying isReady requires writing back the
/// modified copy (see how SetReady does it at StartRunLobby.cs L705-707).</para>
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.AddLocalHostPlayer))]
internal static class CardEditorFakePeer_AddLocalHostPlayer_Patch
{
    // Harmony calls Prepare() before applying the patch; returning false skips it entirely.
    // With the const false the compiler folds the body to `return false` — truly zero cost.
    private static bool Prepare()
    {
        return CardEditorDebugFakeLobbyPeer.SimulateFakeLobbyPeer;
    }

    private static void Postfix(StartRunLobby __instance)
    {
        try
        {
            List<LobbyPlayer> players = __instance.Players;

            // Guard: only inject if we are not already in the list (idempotent).
            if (players.FindIndex(p => p.id == CardEditorDebugFakeLobbyPeer.FakePeerNetId) >= 0)
            {
                Log.Warn("[CardEditor][FakePeer] Fake peer (netId=4242) already present; skipping injection.");
                return;
            }

            // Determine a slot id that doesn't collide with existing players.
            int slotId = 1;
            while (players.FindIndex(p => p.slotId == slotId) >= 0)
            {
                slotId++;
            }

            // Build the fake peer. Use the same character the game defaults to in
            // TryAddPlayerInFirstAvailableSlot (Ironclad, L794-803 of StartRunLobby.cs).
            // UnlockState: use an empty SerializableUnlockState; the fake peer only exists to
            // satisfy the IsAboutToBeginGame headcount + allReady check, not to actually run.
            CharacterModel character = ModelDb.Character<Ironclad>();

            LobbyPlayer fakePeer = new LobbyPlayer
            {
                id = CardEditorDebugFakeLobbyPeer.FakePeerNetId,
                slotId = slotId,
                character = character,
                unlockState = new SerializableUnlockState(),
                maxMultiplayerAscensionUnlocked = 0,
                isReady = true   // Already ready so AllReady() passes after local player readies.
            };

            // Add directly to Players — mirrors what HandleClientLobbyJoinRequestMessage does after
            // TryAddPlayerInFirstAvailableSlot (Players.Add at L248 of StartRunLobby.cs).
            players.Add(fakePeer);

            Log.Info($"[CardEditor][FakePeer] Injected fake ready peer (netId={CardEditorDebugFakeLobbyPeer.FakePeerNetId}, " +
                     $"slot={slotId}, char={character?.Id.Entry ?? "null"}). " +
                     $"Players={players.Count}. Click READY to test IsAboutToBeginGame.");
        }
        catch (Exception ex)
        {
            // A debug harness must NEVER crash the lobby.
            Log.Warn($"[CardEditor][FakePeer] Failed injecting fake ready peer: {ex}");
        }
    }
}

// ---------------------------------------------------------------------------
// PATCH 2: Diagnostic postfix on IsAboutToBeginGame (active when EITHER toggle is true)
// ---------------------------------------------------------------------------

/// <summary>
/// Harmony postfix on <see cref="StartRunLobby.IsAboutToBeginGame"/>. Logs all three gate
/// conditions and the result so the developer can see which condition blocks the run start.
///
/// <para>Format: <c>[CardEditor][FakePeer] IsAboutToBeginGame -> {result} |
/// connectingPlayers={n} players={n} allReady={bool} (unready: id=...)</c></para>
///
/// <para>Prepare() returns false when both consts are false — never applied when disabled.</para>
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.IsAboutToBeginGame))]
internal static class CardEditorFakePeer_IsAboutToBeginGame_Patch
{
    private static bool Prepare()
    {
        return CardEditorDebugFakeLobbyPeer.DiagnosticsActive;
    }

    // Cache the _connectingPlayers field reflectively; null if unavailable (gracefully degraded log).
    private static readonly FieldInfo? _connectingPlayersField =
        AccessTools.Field(typeof(StartRunLobby), "_connectingPlayers");

    private static void Postfix(StartRunLobby __instance, bool __result)
    {
        try
        {
            // Read _connectingPlayers via reflection so we can log its count.
            int connectingCount = 0;
            if (_connectingPlayersField?.GetValue(__instance) is System.Collections.ICollection col)
            {
                connectingCount = col.Count;
            }

            List<LobbyPlayer> players = __instance.Players;
            int playerCount = players.Count;

            bool allReady = true;
            var unreadyIds = new System.Text.StringBuilder();
            foreach (LobbyPlayer p in players)
            {
                if (!p.isReady)
                {
                    allReady = false;
                    if (unreadyIds.Length > 0) unreadyIds.Append(", ");
                    unreadyIds.Append($"id={p.id}");
                }
            }

            string unreadyStr = unreadyIds.Length > 0 ? $" (unready: {unreadyIds})" : "";
            Log.Info($"[CardEditor][FakePeer] IsAboutToBeginGame -> {__result} | " +
                     $"connectingPlayers={connectingCount} players={playerCount} allReady={allReady}{unreadyStr}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[CardEditor][FakePeer] IsAboutToBeginGame diagnostic failed: {ex}");
        }
    }
}

// ---------------------------------------------------------------------------
// PATCH 3: Diagnostic postfix on SetReady (active when EITHER toggle is true)
// ---------------------------------------------------------------------------

/// <summary>
/// Harmony postfix on <see cref="StartRunLobby.SetReady"/>. Logs that the click reached the game
/// and what the local player's isReady state is after the call, so the developer can confirm the
/// ready click was not eaten by an earlier patch or gate.
///
/// <para>Format: <c>[CardEditor][FakePeer] SetReady({ready}) called for netId={n} ->
/// localPlayer.isReady={bool}</c></para>
///
/// <para>Prepare() returns false when both consts are false — never applied when disabled.</para>
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetReady))]
internal static class CardEditorFakePeer_SetReady_Patch
{
    private static bool Prepare()
    {
        return CardEditorDebugFakeLobbyPeer.DiagnosticsActive;
    }

    private static void Postfix(StartRunLobby __instance, bool ready)
    {
        try
        {
            ulong localNetId = __instance.NetService?.NetId ?? 0UL;
            LobbyPlayer localPlayer = __instance.LocalPlayer;

            Log.Info($"[CardEditor][FakePeer] SetReady({ready}) called for netId={localNetId} " +
                     $"-> localPlayer.isReady={localPlayer.isReady}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[CardEditor][FakePeer] SetReady diagnostic failed: {ex}");
        }
    }
}

#pragma warning restore CS0162
