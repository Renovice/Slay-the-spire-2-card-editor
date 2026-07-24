using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace SlayTheSpire2Mod.CardEditor;

// CS0162 (unreachable code) is expected and intentional here: SimulateDummyHost / SimulateVersionMismatch
// are compile-time consts that default false, so the compiler folds the toggle-guarded branches away.
// That IS the design (zero-cost disabled path). Suppress only for this debug file.
#pragma warning disable CS0162

/// <summary>
/// DEBUG-ONLY helper that lets a solo developer verify the co-op ready-check end-to-end
/// (used after the manifest-version fix). Nothing here runs unless one of the two consts below is
/// flipped to <c>true</c> in source; with both <c>false</c> (the shipping default) this type is
/// completely inert - no patches, no nodes, no per-frame cost, no behavioral change for players.
///
/// APPROACH A (<see cref="SimulateDummyHost"/>): spins up an in-process ENet host lobby on
/// 127.0.0.1:33771 that has readied a local host player, then pumps it every frame. The developer
/// launches a SECOND game instance (e.g. with the 'fastmp' arg), joins by IP 127.0.0.1:33771, and
/// readies up to confirm the run begins - proving the ready-check works without needing a partner.
///
/// APPROACH C (<see cref="SimulateVersionMismatch"/>): documented helper only. The mod does not own
/// a client join path (joins go through the game's own multiplayer screens), so this cannot be
/// force-wired without invasive changes. Instead <see cref="BuildMismatchedJoinFlow"/> builds a
/// <see cref="JoinFlow"/> carrying a deliberately-wrong version so a developer who does have a join
/// entry point can prove the parity check rejects the connection. See that method's docs.
/// </summary>
internal static class CardEditorDebugDummyLobby
{
	// ---- Master toggles. BOTH DEFAULT false. Flip in source to use; never ship enabled. ----

	/// <summary>
	/// APPROACH A master toggle. When <c>true</c>, at main-menu ready the mod starts ONE in-process
	/// dummy co-op host on 127.0.0.1:33771 with a readied local host player, and pumps it each frame.
	/// </summary>
	internal const bool SimulateDummyHost = false;

	/// <summary>
	/// APPROACH C master toggle. When <c>true</c>, logs a reminder at startup pointing at
	/// <see cref="BuildMismatchedJoinFlow"/>. This const does NOT auto-wire a join (the mod has no
	/// join entry point of its own); it only surfaces the helper. See <see cref="BuildMismatchedJoinFlow"/>.
	/// </summary>
	internal const bool SimulateVersionMismatch = false;

	private const ushort DummyHostPort = 33771;
	private const int DummyHostMaxClients = 4;
	private const string DummyHostIp = "127.0.0.1";

	// NetId for the local debug client. Host is NetId=1; NMultiplayerTest's default client is 1000, so
	// 2000 is safely distinct and won't collide with either.
	private const ulong DummyClientNetId = 2000UL;

	private static bool _started;
	private static bool _dummyClientConnected;
	private static NetHostGameService? _dummyHost;
	private static StartRunLobby? _dummyLobby;
	private static CardEditorDebugDummyLobbyPump? _pump;

	// The character the dummy host picked at startup, captured so it can be re-affirmed (re-broadcast) when a
	// real client actually connects. See OnDummyLobbyPlayerConnected for why re-affirming is needed.
	private static CharacterModel? _dummyCharacter;

	// The dummy host's OWN NetId (NetHostGameService.NetId, which is 1). Captured after the host starts so the
	// PlayerConnected handler can distinguish the host's own local player (fired at AddLocalHostPlayerInternal)
	// from a real remote client joining.
	private static ulong _dummyHostNetId;

	// The live main menu captured at ready-time (only on the enabled path). Kept as a diagnostic handle;
	// the actual join is driven from the fake friend-list entry (see NJoinFriendScreen_ShowFriends_DummyEntry_Patch).
	private static NMainMenu? _mainMenu;

	/// <summary>
	/// Called once from <c>MainMenu_Ready_Patch.Postfix</c>. Returns immediately (zero cost) unless
	/// <see cref="SimulateDummyHost"/> is enabled. Never throws into game code.
	/// </summary>
	/// <param name="mainMenu">
	/// The live <see cref="NMainMenu"/> from the ready patch. Captured only on the enabled path as a
	/// diagnostic handle.
	/// </param>
	internal static void TryStartDummyHostOnce(NMainMenu? mainMenu = null)
	{
		// Fast, allocation-free no-op on the shipping path: the JIT folds this const check so the
		// whole body is dead code when the toggle is false.
		if (!SimulateDummyHost)
		{
			if (SimulateVersionMismatch)
			{
				Log.Info("[CardEditor][DummyLobby] SimulateVersionMismatch is ON. This does not auto-join; " +
					"call CardEditorDebugDummyLobby.BuildMismatchedJoinFlow(...) from your join path to prove the parity check rejects a bad version.");
			}
			return;
		}

		// Capture the live menu even if we've already started, so a re-fired _Ready keeps a valid handle.
		if (mainMenu != null)
		{
			_mainMenu = mainMenu;
		}

		if (_started)
		{
			return;
		}
		_started = true;

		try
		{
			SceneTree? tree = Engine.GetMainLoop() as SceneTree;
			if (tree?.Root == null)
			{
				Log.Warn("[CardEditor][DummyLobby] No SceneTree/root available; cannot start the dummy host.");
				return;
			}

			NetHostGameService host = new NetHostGameService();

			// Mirror NMultiplayerTest.StartHost: null return means success.
			NetErrorInfo? error = host.StartENetHost(DummyHostPort, DummyHostMaxClients);
			if (error.HasValue)
			{
				Log.Warn($"[CardEditor][DummyLobby] Failed to start dummy ENet host on 127.0.0.1:{DummyHostPort}: {error}");
				return;
			}

			// Same shape as NMultiplayerTest: Standard lobby, this listener stub, 4 max players.
			StartRunLobby lobby = new StartRunLobby(GameMode.Standard, host, new DummyLobbyListener(), DummyHostMaxClients);

			// Exactly as NMultiplayerTest.StartHost L262.
			lobby.AddLocalHostPlayer(new UnlockState(SaveManager.Instance.Progress), SaveManager.Instance.Progress.MaxMultiplayerAscension);

			// Pick the first available character, matching how NMultiplayerTestCharacterPaginator
			// enumerates (index 0 = Ironclad). Captured so it can be re-affirmed when a client connects.
			_dummyCharacter = ModelDb.Character<Ironclad>();
			if (_dummyCharacter != null)
			{
				lobby.SetLocalCharacter(_dummyCharacter);
			}
			else
			{
				Log.Warn("[CardEditor][DummyLobby] ModelDb.Character<Ironclad>() returned null; skipping initial SetLocalCharacter.");
			}

			// Ready the host player so the only thing gating run-start is the joining client readying.
			lobby.SetReady(true);

			// Capture the host's own NetId (== 1) so the PlayerConnected handler can ignore the host's own
			// local player and only re-affirm when a REMOTE client connects. Safe to read here: the host has
			// started successfully, so NetHostGameService.NetId will not throw.
			_dummyHostNetId = host.NetId;

			// When a client actually connects, re-affirm character + ready so the (now-connected) client sees
			// a ready partner. The initial SetLocalCharacter/SetReady above broadcast to nobody (no peer yet).
			// Guarded so a double-subscribe cannot happen: this whole block only runs once (see _started guard
			// at the top of the method) and only on the enabled SimulateDummyHost path.
			lobby.PlayerConnected += OnDummyLobbyPlayerConnected;

			_dummyHost = host;
			_dummyLobby = lobby;

			// Dedicated mod-owned pump node: only ever created on this enabled path, so with the const
			// false there is genuinely no extra node in the tree.
			_pump = new CardEditorDebugDummyLobbyPump();
			tree.Root.CallDeferred(Node.MethodName.AddChild, _pump);

			// One-click join: since the dummy host started, the ShowFriends postfix patch injects a fake
			// "Dummy Host [debug]" entry into the game's own "Choose Friend to Join" list (NJoinFriendScreen).
			// Clicking that entry fires the real join flow against 127.0.0.1:33771 - no 'fastmp' arg or manual
			// IP entry required. The F9 key fallback (in the pump node) still works if that screen isn't open.
			Log.Info($"[CardEditor][DummyLobby] Dummy co-op host listening on 127.0.0.1:{DummyHostPort} (ready). " +
				"Open 'Join Friend' and click the 'Dummy Host [debug]' entry (or press F9) to join as a client and " +
				"test the ready-check. The 'fastmp' arg / manual Join-by-IP path still works too.");
		}
		catch (Exception ex)
		{
			// A debug toggle must NEVER crash the game.
			Log.Warn($"[CardEditor][DummyLobby] Failed starting dummy co-op host: {ex}");
		}
	}

	/// <summary>
	/// Pumps the dummy host once per frame. Called from <see cref="CardEditorDebugDummyLobbyPump"/>.
	/// </summary>
	internal static void PumpDummyHost()
	{
		if (!SimulateDummyHost)
		{
			return;
		}

		try
		{
			NetHostGameService? host = _dummyHost;
			if (host == null)
			{
				return;
			}

			if (_dummyClientConnected)
			{
				// A client is connected; a single non-blocking service per frame is enough.
				host.Update();
				return;
			}

			// No client yet. The ENet connect handshake (CONNECT -> VERIFY_CONNECT -> Connect event)
			// can't complete in one process because both ends do isolated 0ms service passes at coarse,
			// unsynchronized cadences (the client only polls ~10x/sec behind Task.Delay(100)). Burst-drain
			// the host for ~one frame with 1ms gaps so the OS delivers the CONNECT between passes and the
			// host flushes VERIFY_CONNECT inside the client's 100ms poll window. Stops once connected.
			for (int i = 0; i < 16; i++)
			{
				host.Update();
				System.Threading.Thread.Sleep(1);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][DummyLobby] Dummy host pump failed: {ex}");
		}
	}

	/// <summary>
	/// Handles <see cref="StartRunLobby.PlayerConnected"/> for the dummy host. This event fires both for the
	/// host's OWN local player (from <c>AddLocalHostPlayerInternal</c>, id == host NetId) and for a real remote
	/// client joining (from <c>HandleClientLobbyJoinRequestMessage</c>, AFTER the join response is sent and the
	/// peer is marked ready for broadcasting). We ignore the former and, for the latter, re-affirm the dummy
	/// host's character + ready so those state messages actually reach the now-connected client - the initial
	/// SetLocalCharacter/SetReady at startup broadcast to nobody because no peer existed yet.
	///
	/// Re-affirming is safe: the joining client is not yet ready, so <c>IsAboutToBeginGame</c> is false and
	/// re-calling SetReady(true) will not prematurely start the run. Fully guarded; must never crash the lobby.
	/// </summary>
	private static void OnDummyLobbyPlayerConnected(LobbyPlayer p)
	{
		if (!SimulateDummyHost)
		{
			return;
		}

		try
		{
			// Ignore the host's own local player (fired when AddLocalHostPlayer added us before any client).
			if (p.id == _dummyHostNetId)
			{
				return;
			}

			StartRunLobby? lobby = _dummyLobby;
			if (lobby == null)
			{
				return;
			}

			// A real remote client is now connected - stop the burst-pump (see PumpDummyHost) and
			// fall back to a single service per frame.
			_dummyClientConnected = true;
			Log.Info("[CardEditor][DummyLobby] Client connected - re-affirming dummy character + ready");

			// Re-affirm character only if we captured a valid one; always re-affirm ready.
			if (_dummyCharacter != null)
			{
				lobby.SetLocalCharacter(_dummyCharacter);
			}
			lobby.SetReady(true);
		}
		catch (Exception ex)
		{
			// A debug re-affirm must NEVER crash the lobby.
			Log.Warn($"[CardEditor][DummyLobby] Failed re-affirming dummy character/ready on client connect: {ex}");
		}
	}

	/// <summary>
	/// Fires the REAL join flow: <see cref="NMainMenu.JoinGame"/> with an
	/// <see cref="ENetClientConnectionInitializer"/> pointed at the in-process dummy host
	/// (<see cref="DummyHostIp"/>:<see cref="DummyHostPort"/>). Routed through
	/// <see cref="TaskHelper.RunSafely"/> so the async task's exceptions are logged, and fully try/catch
	/// guarded so it can never crash the game. Invoked by the F9 key fallback in
	/// <see cref="CardEditorDebugDummyLobbyPump"/> (the primary path is the fake friend-list entry, which
	/// drives NJoinFriendScreen's own JoinGame instead).
	/// </summary>
	/// <param name="source">Where the join was triggered from ("key"), for the log line.</param>
	internal static void JoinDummyHost(string source)
	{
		if (!SimulateDummyHost)
		{
			return;
		}

		try
		{
			NMainMenu? menu = _mainMenu;
			if (menu == null || !GodotObject.IsInstanceValid(menu))
			{
				Log.Warn("[CardEditor][DummyLobby] Join Dummy Host requested but no valid NMainMenu was captured; cannot join.");
				return;
			}

			Log.Info($"[CardEditor][DummyLobby] Join Dummy Host clicked ({source}) -> connecting to {DummyHostIp}:{DummyHostPort}");

			// JoinGame is public async Task; RunSafely fires it without awaiting and logs any exception.
			TaskHelper.RunSafely(menu.JoinGame(BuildDummyClientInitializer()));
		}
		catch (Exception ex)
		{
			// A debug join must NEVER crash the game.
			Log.Warn($"[CardEditor][DummyLobby] Failed firing Join Dummy Host: {ex}");
		}
	}

	/// <summary>
	/// APPROACH C helper (not auto-wired). Builds a <see cref="JoinFlow"/> whose local identity carries
	/// a deliberately-wrong version string, so the client-side parity check in
	/// <c>JoinFlow.Begin</c> throws <c>ClientConnectionFailedException</c> with
	/// <c>ConnectionFailureReason.VersionMismatch</c> when it connects to a real host.
	///
	/// The mod does not own a client join path, so you must drive this yourself from wherever you can
	/// obtain an <c>IClientConnectionInitializer</c> and a <c>SceneTree</c>. Example:
	/// <code>
	/// var flow = CardEditorDebugDummyLobby.BuildMismatchedJoinFlow("v0.000-WRONG");
	/// var initializer = new ENetClientConnectionInitializer(netId: 1000, ip: "127.0.0.1", port: 33771);
	/// try { await flow.Begin(initializer, GetTree()); }
	/// catch (Exception ex) { Log.Info($"[CardEditor][DummyLobby] Expected rejection: {ex}"); }
	/// </code>
	/// Feed it a version that differs from the host's real version to force the mismatch; the
	/// <c>hash</c>/<c>branch</c>/mod lists are left at neutral defaults so the version check is the one
	/// that trips first (see JoinFlow.Begin L123-141).
	/// </summary>
	/// <param name="wrongVersion">A version string that intentionally does not match the host's.</param>
	/// <returns>A single-use <see cref="JoinFlow"/> preloaded with the mismatched identity.</returns>
	internal static JoinFlow BuildMismatchedJoinFlow(string wrongVersion = "v0.000-DEBUG-MISMATCH")
	{
		JoinFlow.MockInfo mockInfo = new JoinFlow.MockInfo
		{
			version = wrongVersion,
			hash = 0u,
			branch = default(PlatformBranch),
			gameplayAffectingMods = new List<string>(),
			nonGameplayAffectingMods = new List<string>()
		};

		return new JoinFlow(new NetClientGameService(), mockInfo);
	}

	/// <summary>
	/// Builds the <see cref="ENetClientConnectionInitializer"/> that points at the in-process dummy host
	/// (<see cref="DummyHostIp"/>:<see cref="DummyHostPort"/> as <see cref="DummyClientNetId"/>). Used by
	/// both the F9 key fallback and the fake friend-list entry so the connection details live in one place.
	/// </summary>
	internal static ENetClientConnectionInitializer BuildDummyClientInitializer()
	{
		return new ENetClientConnectionInitializer(DummyClientNetId, DummyHostIp, DummyHostPort);
	}

	/// <summary>
	/// Minimal no-op / log-only <see cref="IStartRunLobbyListener"/> for the dummy host. The dummy host
	/// only needs to accept a client and let the ready-check fire; it has no UI, so every callback is a
	/// no-op or a debug log.
	/// </summary>
	private sealed class DummyLobbyListener : IStartRunLobbyListener
	{
		public void PlayerConnected(LobbyPlayer player)
		{
			Log.Info($"[CardEditor][DummyLobby] Player connected: {player.id} (slot {player.slotId}). Ready={player.isReady}");
		}

		public void PlayerChanged(LobbyPlayer player, bool isRandomCharacterResolution)
		{
			Log.Info($"[CardEditor][DummyLobby] Player changed: {player.id} Ready={player.isReady}");
		}

		public void AscensionChanged()
		{
		}

		public void SeedChanged()
		{
		}

		public void ModifiersChanged()
		{
		}

		public void MaxAscensionChanged()
		{
		}

		public void RemotePlayerDisconnected(LobbyPlayer player)
		{
			Log.Info($"[CardEditor][DummyLobby] Remote player disconnected: {player.id}");
		}

		public void BeginRun(string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers)
		{
			// This firing is the whole point: it proves the ready-check passed and the run started.
			Log.Info($"[CardEditor][DummyLobby] BeginRun fired (seed={seed}). Ready-check PASSED - the co-op ready flow works.");
		}

		public void LocalPlayerDisconnected(NetErrorInfo info)
		{
			Log.Info($"[CardEditor][DummyLobby] Local player disconnected: {info}");
		}
	}
}

/// <summary>
/// Dedicated mod-owned pump node for the DEBUG dummy host. Only ever instantiated when
/// <see cref="CardEditorDebugDummyLobby.SimulateDummyHost"/> is enabled; on the shipping path it is
/// never created, so it adds nothing to the scene tree.
/// </summary>
internal sealed partial class CardEditorDebugDummyLobbyPump : Node
{
	public override void _Ready()
	{
		Name = "CardEditorDebugDummyLobbyPump";
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _Process(double delta)
	{
		CardEditorDebugDummyLobby.PumpDummyHost();
	}

	/// <summary>
	/// Key fallback for the one-click join: pressing F9 fires the same real join flow as the fake
	/// friend-list entry, in case the "Join Friend" screen isn't open. Fully guarded; a debug key must
	/// never crash the game.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		try
		{
			if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 })
			{
				CardEditorDebugDummyLobby.JoinDummyHost("key");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][DummyLobby] Dummy host key fallback failed: {ex}");
		}
	}
}

/// <summary>
/// DEBUG-ONLY Harmony postfix on <see cref="NJoinFriendScreen"/>.ShowFriends that injects a fake
/// "Dummy Host [debug]" entry into the game's own "Choose Friend to Join" list, wired to join the
/// in-process dummy host on 127.0.0.1:33771. This replaces the old floating CanvasLayer button so the
/// entry lives IN the friend list, exactly where a real joinable friend would appear.
///
/// <para>
/// <b>Zero-cost when disabled:</b> <see cref="Prepare"/> returns
/// <see cref="CardEditorDebugDummyLobby.SimulateDummyHost"/>. When the const is <c>false</c> Harmony
/// skips the patch entirely - it is never applied, so ShowFriends runs completely unmodified and there
/// is no runtime cost or behavioral change whatsoever.
/// </para>
///
/// <para>
/// The postfix runs at ShowFriends' first <c>await</c>, which is AFTER the synchronous clear of
/// <c>_buttonContainer</c> (NJoinFriendScreen L149-152) - so the fake entry survives that clear and
/// coexists with any real Steam friends added later in the same method.
/// </para>
/// </summary>
[HarmonyPatch(typeof(NJoinFriendScreen), "ShowFriends")]
internal static class NJoinFriendScreen_ShowFriends_DummyEntry_Patch
{
	// NetId used for the fake list entry's NJoinFriendButton. Distinct from real Steam ids (2000UL is the
	// same debug client id the dummy host expects) so its blank Steam name is overridden below.
	private const ulong DummyEntryPlayerId = 2000UL;

	private static readonly FieldInfo? _buttonContainerField =
		AccessTools.Field(typeof(NJoinFriendScreen), "_buttonContainer");

	private static readonly FieldInfo? _noFriendsLabelField =
		AccessTools.Field(typeof(NJoinFriendScreen), "_noFriendsLabel");

	private static readonly MethodInfo? _joinGameMethod =
		AccessTools.Method(typeof(NJoinFriendScreen), "JoinGame", new[] { typeof(IClientConnectionInitializer) });

	// When SimulateDummyHost is false this returns false, so Harmony NEVER applies the patch: truly inert.
	private static bool Prepare()
	{
		return CardEditorDebugDummyLobby.SimulateDummyHost;
	}

	private static void Postfix(NJoinFriendScreen __instance)
	{
		try
		{
			if (_buttonContainerField?.GetValue(__instance) is not Control buttonContainer)
			{
				Log.Warn("[CardEditor][DummyLobby] ShowFriends postfix: _buttonContainer unavailable; cannot add fake entry.");
				return;
			}

			// Build the same NJoinFriendButton scene the real friend entries use, then parent it into the
			// list. AddChildSafely mirrors what ShowFriends itself does for real friends.
			NJoinFriendButton btn = NJoinFriendButton.Create(DummyEntryPlayerId);
			buttonContainer.AddChildSafely(btn);

			// The button's _Ready sets its %Text label to the (blank/garbage) Steam name for id 2000. Override
			// it AFTER _Ready runs by deferring the label write to the end of the current frame.
			Callable.From(delegate
			{
				try
				{
					if (GodotObject.IsInstanceValid(btn))
					{
						MegaRichTextLabel label = btn.GetNode<MegaRichTextLabel>("%Text");
						label.Text = "[center]Dummy Host [debug][/center]";
					}
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor][DummyLobby] ShowFriends postfix: failed overriding fake entry label: {ex}");
				}
			}).CallDeferred();

			// Wire the click to the screen's own private JoinGame(IClientConnectionInitializer), driving the
			// REAL join flow against the in-process dummy host - identical to how real friend entries join.
			btn.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(delegate
			{
				try
				{
					Log.Info("[CardEditor][DummyLobby] Fake lobby entry clicked -> joining 127.0.0.1:33771");
					ENetClientConnectionInitializer initializer = CardEditorDebugDummyLobby.BuildDummyClientInitializer();
					if (_joinGameMethod != null)
					{
						_joinGameMethod.Invoke(__instance, new object[] { initializer });
					}
					else
					{
						// Fallback: JoinGame is a one-liner over the public JoinGameAsync.
						TaskHelper.RunSafely(__instance.JoinGameAsync(initializer));
					}
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor][DummyLobby] Fake lobby entry click failed: {ex}");
				}
			}));

			// Hide the "No friends" label: with a joinable (fake) entry present the list is not empty.
			if (_noFriendsLabelField?.GetValue(__instance) is MegaLabel noFriendsLabel
				&& GodotObject.IsInstanceValid(noFriendsLabel))
			{
				noFriendsLabel.Visible = false;
			}
		}
		catch (Exception ex)
		{
			// A debug entry must NEVER crash the join screen.
			Log.Warn($"[CardEditor][DummyLobby] Failed injecting fake friend-list entry: {ex}");
		}
	}
}

#pragma warning restore CS0162
