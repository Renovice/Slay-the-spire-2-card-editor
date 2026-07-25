// CS0162 (unreachable code) is expected and intentional here: SimulateFakeLobbyPeer /
// TreatLocalAsClientForReadyGate / SimulateFakeJoinAsClient are compile-time consts that default
// false, so the compiler folds the toggle-guarded branches away. That IS the design (zero-cost
// disabled path).
#pragma warning disable CS0162

using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Quality;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace SlayTheSpire2Mod.CardEditor;

/// <summary>
/// DEBUG-ONLY fake-lobby-peer test harness.
///
/// PURPOSE:
///   Lets a solo developer verify that clicking READY in the StartRunLobby works end-to-end
///   without a second player and without any real networking.
///
/// TOGGLE SUMMARY (all default false — no effect when all are false):
///   <see cref="SimulateFakeLobbyPeer"/>          — injects a ready fake peer into the HOST lobby
///                                                    so IsAboutToBeginGame sees two players.
///                                                    Tests the HOST-side ready path only.
///   <see cref="TreatLocalAsClientForReadyGate"/> — forces AllowClientReady to behave as if the
///                                                    local session is a CLIENT; exercises the client
///                                                    ready-gate code path in a solo hosting session.
///   <see cref="SimulateFakeJoinAsClient"/>        — injects a fake "Fake Lobby (client test) - debug"
///                                                    entry in the Join Friend list. Clicking it builds
///                                                    a synthetic CLIENT lobby (no real networking)
///                                                    and pushes CharacterSelect AS A CLIENT with a
///                                                    pre-readied fake host peer. This is the JOINER
///                                                    (CLIENT) side test: it exercises the path where
///                                                    AllowClientReady must let the click through, SetReady
///                                                    runs, localPlayer.isReady becomes true, and the
///                                                    fake service logs that LobbyPlayerSetReadyMessage
///                                                    would be sent. Because a CLIENT never calls
///                                                    BeginRunForAllPlayers, the run will NOT begin —
///                                                    that is correct; we are testing the ready-register
///                                                    path, not the run-start path.
///
/// NEVER SHIP ENABLED:
///   Any of these consts being true in a shipped build will corrupt or fake real multiplayer state.
///   All MUST remain false in any build delivered to players.
///
/// DIAGNOSTICS (active when ANY of the three consts is true):
///   Postfix on IsAboutToBeginGame logs all three gate conditions.
///   Postfix on SetReady logs whether the click reached the game and the resulting isReady state.
/// </summary>
internal static class CardEditorDebugFakeLobbyPeer
{
    // ---- Master toggles. ALL DEFAULT false. Flip in source to test; NEVER ship enabled. ----

    /// <summary>
    /// When true: after AddLocalHostPlayer runs, inject one synthetic already-ready LobbyPlayer
    /// (netId=4242) into the lobby's Players list so IsAboutToBeginGame can return true on
    /// a single local player readying up. Zero networking occurs; this is a pure list manipulation.
    /// Tests the HOST-side ready path only; does NOT reproduce the "joiner can't click ready" bug.
    /// </summary>
    internal const bool SimulateFakeLobbyPeer = false;

    /// <summary>
    /// When true: sets <see cref="CardEditorMultiplayerSync.ForceClientReadyGateForTesting"/> = true,
    /// making the mod's AllowClientReady behave as if the local player were a CLIENT. This exercises
    /// the client-side ready gate code path in a solo hosting session without a second player.
    /// Must be false in any real multiplayer session.
    /// </summary>
    internal const bool TreatLocalAsClientForReadyGate = false;

    /// <summary>
    /// When true: injects a "Fake Lobby (client test) - debug" entry into the Join Friend list.
    /// Clicking it builds a fully fake CLIENT-side lobby (no real ENet socket, no networking)
    /// and pushes NCharacterSelectScreen via InitializeMultiplayerAsClient with a pre-readied
    /// fake host player (netId=1, isReady=true) and the local player (netId=2000, isReady=false).
    /// The user picks a character and clicks Ready to test whether:
    ///   - AllowClientReady lets the click through (the reported bug: it did not)
    ///   - SetReady runs and localPlayer.isReady becomes true
    ///   - The fake service logs that LobbyPlayerSetReadyMessage WOULD be sent to the host
    /// The run will NOT begin (BeginRunForAllPlayers only runs for Host/Singleplayer) — correct.
    /// NEVER SHIP ENABLED: fakes a client lobby for local ready-path testing only; no real
    /// networking or sync; never deploy to players.
    /// </summary>
    internal const bool SimulateFakeJoinAsClient = false;

    // NetId for the injected fake peer (host-side test). Host is 1; DummyLobby client uses 2000;
    // 4242 is distinct from both. internal so patch classes (top-level in this file) can reference it.
    internal const ulong FakePeerNetId = 4242UL;

    // NetId used for the fake CLIENT in the SimulateFakeJoinAsClient test.
    // Matches CardEditorDebugDummyLobby.DummyClientNetId (2000) so logs are consistent.
    internal const ulong FakeClientNetId = 2000UL;

    // NetId of the synthetic host peer in the SimulateFakeJoinAsClient test.
    internal const ulong FakeHostNetId = 1UL;

    // Whether ANY diagnostic toggle is active (drives the diagnostic postfixes).
    // internal so the patch classes can access it.
    internal static bool DiagnosticsActive =>
        SimulateFakeLobbyPeer || TreatLocalAsClientForReadyGate || SimulateFakeJoinAsClient;

    /// <summary>
    /// Called once during mod Prepare, before Harmony patches are applied. Sets the client-gate
    /// test hook if <see cref="TreatLocalAsClientForReadyGate"/> is enabled.
    /// No-op (and zero cost) when all three consts are false.
    /// </summary>
    internal static void Prepare()
    {
        // Const-folded: JIT eliminates this body when all three toggles are false.
        if (!SimulateFakeLobbyPeer && !TreatLocalAsClientForReadyGate && !SimulateFakeJoinAsClient)
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

        if (SimulateFakeJoinAsClient)
        {
            Log.Info("[CardEditor][FakeJoin] SimulateFakeJoinAsClient=true: a 'Fake Lobby (client test) - debug' " +
                     "entry will appear in Join Friend. Clicking it fakes a CLIENT lobby with a pre-readied host " +
                     "peer (id=1). No real networking. Use to test the joiner-side ready path.");
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
// PATCH 2: Diagnostic postfix on IsAboutToBeginGame (active when ANY toggle is true)
// ---------------------------------------------------------------------------

/// <summary>
/// Harmony postfix on <see cref="StartRunLobby.IsAboutToBeginGame"/>. Logs all three gate
/// conditions and the result so the developer can see which condition blocks the run start.
///
/// <para>Format: <c>[CardEditor][FakePeer] IsAboutToBeginGame -> {result} |
/// connectingPlayers={n} players={n} allReady={bool} (unready: id=...)</c></para>
///
/// <para>Prepare() returns false when all three consts are false — never applied when disabled.</para>
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
// PATCH 3: Diagnostic postfix on SetReady (active when ANY toggle is true)
// ---------------------------------------------------------------------------

/// <summary>
/// Harmony postfix on <see cref="StartRunLobby.SetReady"/>. Logs that the click reached the game
/// and what the local player's isReady state is after the call, so the developer can confirm the
/// ready click was not eaten by an earlier patch or gate.
///
/// <para>Format: <c>[CardEditor][FakePeer] SetReady({ready}) called for netId={n} ->
/// localPlayer.isReady={bool}</c></para>
///
/// <para>Prepare() returns false when all three consts are false — never applied when disabled.</para>
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

// ---------------------------------------------------------------------------
// PATCH 4: Inject a "Fake Lobby (client test) - debug" entry into NJoinFriendScreen
//          (SimulateFakeJoinAsClient only)
// ---------------------------------------------------------------------------

/// <summary>
/// Minimal fake <see cref="INetGameService"/> implementation whose <see cref="Type"/> is
/// <see cref="NetGameType.Client"/>. Used exclusively by
/// <see cref="CardEditorFakeJoin_ShowFriends_Patch"/> to satisfy the type check in
/// <see cref="NCharacterSelectScreen.InitializeMultiplayerAsClient"/>.
///
/// <para>All send/broadcast members are no-ops that log what WOULD be sent to the host. This is
/// intentionally valuable: it surfaces whether <c>LobbyPlayerSetReadyMessage</c> is emitted when
/// the joiner clicks Ready, which is the core of the reported bug.</para>
///
/// <para>
/// <b>NEVER SHIP ENABLED.</b> This class should only be instantiated when
/// <see cref="CardEditorDebugFakeLobbyPeer.SimulateFakeJoinAsClient"/> is true.
/// </para>
/// </summary>
internal sealed class FakeClientNetGameService : INetGameService
{
    private bool _isLoading;

    public ulong NetId { get; }

    public bool IsConnected => true;

    public bool IsGameLoading => _isLoading;

    /// <summary>Type is Client — required by InitializeMultiplayerAsClient's guard.</summary>
    public NetGameType Type => NetGameType.Client;

    public PlatformType Platform => PlatformType.None;

    public event Action<NetErrorInfo>? Disconnected;

    public FakeClientNetGameService(ulong netId)
    {
        NetId = netId;
    }

    /// <summary>
    /// Logs the message type so the developer can see that LobbyPlayerSetReadyMessage WOULD be sent.
    /// This is the key observable: if this logs when the user clicks Ready, the ready path works.
    /// </summary>
    public void SendMessage<T>(T message, ulong playerId) where T : INetMessage
    {
        Log.Info($"[FakeJoin][FakeNetService] SendMessage<{typeof(T).Name}> to peer {playerId} (no-op: fake client, no real networking).");
    }

    /// <summary>
    /// Logs the message type — critically logs LobbyPlayerSetReadyMessage if SetReady fires it.
    /// </summary>
    public void SendMessage<T>(T message) where T : INetMessage
    {
        Log.Info($"[FakeJoin][FakeNetService] SendMessage<{typeof(T).Name}> (no-op: fake client, no real networking).");
    }

    public void RegisterMessageHandler<T>(MessageHandlerDelegate<T> messageHandlerDelegate) where T : INetMessage
    {
        // No-op: no real network means no messages will arrive. Safe to skip.
    }

    public void UnregisterMessageHandler<T>(MessageHandlerDelegate<T> messageHandlerDelegate) where T : INetMessage
    {
        // No-op.
    }

    public void Update()
    {
        // No-op: no ENet socket to service.
    }

    public void Disconnect(NetError reason, bool now = false)
    {
        Log.Info($"[FakeJoin][FakeNetService] Disconnect({reason}, now={now}) called (no-op: fake client).");
        try { Disconnected?.Invoke(new NetErrorInfo(reason, selfInitiated: true)); }
        catch (Exception ex) { Log.Warn($"[FakeJoin][FakeNetService] Disconnected event threw: {ex}"); }
    }

    public ConnectionStats? GetStatsForPeer(ulong peerId)
    {
        return null;
    }

    public void SetGameLoading(bool isLoading)
    {
        _isLoading = isLoading;
    }

    public void SetBufferMessages(bool bufferMessages)
    {
        // No-op.
    }

    public string? GetRawLobbyIdentifier()
    {
        return null;
    }
}

/// <summary>
/// DEBUG-ONLY Harmony postfix on <see cref="NJoinFriendScreen"/>.ShowFriends that injects a fake
/// "Fake Lobby (client test) - debug" entry into the game's own "Choose Friend to Join" list.
/// Clicking it builds a synthetic CLIENT-side lobby — no real ENet socket or networking — and pushes
/// <see cref="NCharacterSelectScreen"/> via
/// <see cref="NCharacterSelectScreen.InitializeMultiplayerAsClient"/> exactly as the real join path
/// does (NJoinFriendScreen.cs L208-210).
///
/// <para>
/// WHAT TO OBSERVE: the user opens Multiplayer → Join Friend, clicks this fake entry, lands in the
/// character-select lobby AS A CLIENT with a ready fake host (id=1, isReady=true), picks a character,
/// and clicks Ready. Because a CLIENT never calls BeginRunForAllPlayers, the run will NOT begin —
/// that is CORRECT. What is being tested:
/// <list type="bullet">
///   <item>AllowClientReady must let the click through (the reported bug was that it did not).</item>
///   <item>StartRunLobby.SetReady must run — logged by the PATCH 3 diagnostic.</item>
///   <item>localPlayer.isReady must become true — logged by the PATCH 3 diagnostic.</item>
///   <item>The fake service must log that LobbyPlayerSetReadyMessage WOULD be sent to the host —
///         logged by <see cref="FakeClientNetGameService.SendMessage{T}(T)"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Zero-cost when disabled:</b> <see cref="Prepare"/> returns
/// <see cref="CardEditorDebugFakeLobbyPeer.SimulateFakeJoinAsClient"/>. When the const is
/// <c>false</c>, Harmony skips the patch entirely — ShowFriends runs completely unmodified.
/// Do NOT modify or re-enable the dummy-host entry
/// (<see cref="NJoinFriendScreen_ShowFriends_DummyEntry_Patch"/> in CardEditorDebugDummyLobby.cs).
/// </para>
///
/// <para>
/// <b>NEVER SHIP ENABLED.</b> Fakes a client lobby for local ready-path testing only; no real
/// networking or sync; never deploy to players.
/// </para>
/// </summary>
[HarmonyPatch(typeof(NJoinFriendScreen), "ShowFriends")]
internal static class CardEditorFakeJoin_ShowFriends_Patch
{
    // The fake entry's player-id in the button list. Distinct from the DummyLobby entry (2000 is
    // DummyClientNetId there); we also use 2000 as our local CLIENT netId — same value, consistent
    // with the existing DummyClientNetId constant and the FakeClientNetId in FakePeer above.
    private const ulong FakeJoinEntryPlayerId = CardEditorDebugFakeLobbyPeer.FakeClientNetId;

    // _buttonContainer: same field the DummyLobby patch uses.
    private static readonly FieldInfo? _buttonContainerField =
        AccessTools.Field(typeof(NJoinFriendScreen), "_buttonContainer");

    // _noFriendsLabel: same field.
    private static readonly FieldInfo? _noFriendsLabelField =
        AccessTools.Field(typeof(NJoinFriendScreen), "_noFriendsLabel");

    // _stack: protected field on NSubmenu (the base class). AccessTools searches base classes.
    private static readonly FieldInfo? _stackField =
        AccessTools.Field(typeof(NSubmenu), "_stack");

    // When SimulateFakeJoinAsClient is false, Harmony NEVER applies the patch.
    private static bool Prepare()
    {
        return CardEditorDebugFakeLobbyPeer.SimulateFakeJoinAsClient;
    }

    private static void Postfix(NJoinFriendScreen __instance)
    {
        try
        {
            if (_buttonContainerField?.GetValue(__instance) is not Control buttonContainer)
            {
                Log.Warn("[FakeJoin] ShowFriends postfix: _buttonContainer unavailable; cannot add fake entry.");
                return;
            }

            // Build the button using the same factory the real friend entries use.
            NJoinFriendButton btn = NJoinFriendButton.Create(FakeJoinEntryPlayerId);
            buttonContainer.AddChildSafely(btn);

            // Override the label text (which defaults to a blank/garbage Steam display name for this id)
            // after _Ready runs, deferred to end-of-frame so the button's own _Ready has set it first.
            Callable.From(delegate
            {
                try
                {
                    if (GodotObject.IsInstanceValid(btn))
                    {
                        MegaRichTextLabel label = btn.GetNode<MegaRichTextLabel>("%Text");
                        label.Text = "[center]Fake Lobby (client test) - debug[/center]";
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[FakeJoin] ShowFriends postfix: failed overriding fake entry label: {ex}");
                }
            }).CallDeferred();

            // Wire the click: build the fake client lobby and push CharacterSelect as a CLIENT.
            btn.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(delegate
            {
                try
                {
                    PerformFakeClientJoin(__instance);
                }
                catch (Exception ex)
                {
                    Log.Warn($"[FakeJoin] Fake lobby entry click failed: {ex}");
                }
            }));

            // Hide the "No friends" label: with a joinable (fake) entry the list is not empty.
            if (_noFriendsLabelField?.GetValue(__instance) is MegaLabel noFriendsLabel
                && GodotObject.IsInstanceValid(noFriendsLabel))
            {
                noFriendsLabel.Visible = false;
            }
        }
        catch (Exception ex)
        {
            // A debug entry must NEVER crash the join screen.
            Log.Warn($"[FakeJoin] Failed injecting fake client-test friend-list entry: {ex}");
        }
    }

    /// <summary>
    /// Performs the fake client join: builds a synthetic <see cref="FakeClientNetGameService"/> and
    /// <see cref="ClientLobbyJoinResponseMessage"/>, then pushes
    /// <see cref="NCharacterSelectScreen"/> exactly as NJoinFriendScreen.JoinGameAsync does
    /// at L208-210 (reading <c>_stack</c> from the <see cref="NJoinFriendScreen"/> instance via
    /// AccessTools, calling GetSubmenuType, InitializeMultiplayerAsClient, then Push).
    /// </summary>
    private static void PerformFakeClientJoin(NJoinFriendScreen joinScreen)
    {
        Log.Info("[FakeJoin] Building fake client service (Type=Client, NetId=2000).");

        // Step a: build the fake INetGameService.
        FakeClientNetGameService fakeService = new FakeClientNetGameService(CardEditorDebugFakeLobbyPeer.FakeClientNetId);

        // Step b: build the fake ClientLobbyJoinResponseMessage.
        // HOST player (id=1, isReady=true, Ironclad, slot 0).
        CharacterModel hostCharacter = ModelDb.Character<Ironclad>();
        UnlockState localProgress = new UnlockState(SaveManager.Instance.Progress);
        SerializableUnlockState hostUnlockState = localProgress.ToSerializable();

        LobbyPlayer fakeHost = new LobbyPlayer
        {
            id = CardEditorDebugFakeLobbyPeer.FakeHostNetId,
            slotId = 0,
            character = hostCharacter,
            unlockState = hostUnlockState,
            maxMultiplayerAscensionUnlocked = 0,
            isReady = true   // The fake host is already ready; we are testing the joiner side.
        };

        // LOCAL player (id=2000, isReady=false, no character yet — picked on the char-select screen).
        LobbyPlayer fakeLocal = new LobbyPlayer
        {
            id = CardEditorDebugFakeLobbyPeer.FakeClientNetId,
            slotId = 1,
            // Same character as the fake host: the field is non-nullable and the user re-picks on the
            // char-select screen anyway, so seeding it avoids a null the lobby would have to survive.
            character = fakeHost.character,
            unlockState = hostUnlockState,   // reuse; the local player's unlock state is fine.
            maxMultiplayerAscensionUnlocked = 0,
            isReady = false
        };

        ClientLobbyJoinResponseMessage fakeResponse = new ClientLobbyJoinResponseMessage
        {
            playersInLobby = new List<LobbyPlayer> { fakeHost, fakeLocal },
            ascension = 0,
            seed = null,
            modifiers = new List<SerializableModifier>(),
            dailyTime = null
        };

        Log.Info("[FakeJoin] Pushing character select as CLIENT (host peer id=1 ready=true, local id=2000 ready=false).");

        // Step c: get _stack off the NJoinFriendScreen, then mirror NJoinFriendScreen.JoinGameAsync L208-210.
        NSubmenuStack? stack = null;
        if (_stackField != null)
        {
            stack = _stackField.GetValue(joinScreen) as NSubmenuStack;
        }

        if (stack == null)
        {
            Log.Warn("[FakeJoin] Could not read _stack from NJoinFriendScreen; cannot push CharacterSelectScreen.");
            return;
        }

        NCharacterSelectScreen charSelectScreen = stack.GetSubmenuType<NCharacterSelectScreen>();
        charSelectScreen.InitializeMultiplayerAsClient(fakeService, fakeResponse);
        stack.Push(charSelectScreen);

        Log.Info("[FakeJoin] CharacterSelectScreen pushed as CLIENT. Pick a character and click Ready to test the joiner-side ready path. " +
                 "The run will NOT begin (correct — BeginRunForAllPlayers only runs on Host/Singleplayer). " +
                 "Watch for: SetReady log, localPlayer.isReady=true, and FakeNetService SendMessage<LobbyPlayerSetReadyMessage>.");
    }
}

#pragma warning restore CS0162
