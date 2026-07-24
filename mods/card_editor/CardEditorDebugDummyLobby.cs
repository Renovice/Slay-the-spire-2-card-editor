using System;
using System.Collections.Generic;
using Godot;
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
	private static NetHostGameService? _dummyHost;
	private static StartRunLobby? _dummyLobby;
	private static CardEditorDebugDummyLobbyPump? _pump;

	// The live main menu captured at ready-time (only on the enabled path); source of the JoinGame call.
	private static NMainMenu? _mainMenu;

	// Mod-owned CanvasLayer hosting the one-click debug join button. Only ever created on the enabled path.
	private static CanvasLayer? _joinButtonLayer;

	/// <summary>
	/// Called once from <c>MainMenu_Ready_Patch.Postfix</c>. Returns immediately (zero cost) unless
	/// <see cref="SimulateDummyHost"/> is enabled. Never throws into game code.
	/// </summary>
	/// <param name="mainMenu">
	/// The live <see cref="NMainMenu"/> from the ready patch. Captured only on the enabled path so the
	/// one-click debug join button can drive the real join flow (<see cref="NMainMenu.JoinGame"/>).
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
			// enumerates (index 0 = Ironclad).
			lobby.SetLocalCharacter(ModelDb.Character<Ironclad>());

			// Ready the host player so the only thing gating run-start is the joining client readying.
			lobby.SetReady(true);

			_dummyHost = host;
			_dummyLobby = lobby;

			// Dedicated mod-owned pump node: only ever created on this enabled path, so with the const
			// false there is genuinely no extra node in the tree.
			_pump = new CardEditorDebugDummyLobbyPump();
			tree.Root.CallDeferred(Node.MethodName.AddChild, _pump);

			// One-click join: since the dummy host started, add an always-visible debug button that fires
			// the real join flow against 127.0.0.1:33771 - no 'fastmp' arg or manual IP entry required.
			TryAddJoinButton(tree);

			Log.Info($"[CardEditor][DummyLobby] Dummy co-op host listening on 127.0.0.1:{DummyHostPort} (ready). " +
				"Click the 'Join Dummy Host [debug]' button (top-left) or press F9 to join as a client and test the ready-check. " +
				"The 'fastmp' arg / manual Join-by-IP path still works too.");
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
			_dummyHost?.Update();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][DummyLobby] Dummy host pump failed: {ex}");
		}
	}

	/// <summary>
	/// Builds a mod-owned <see cref="CanvasLayer"/> (high layer, added straight to the SceneTree root) with
	/// a single Godot <see cref="Button"/> anchored top-left. Living on its own layer at the root makes the
	/// button robust against the main menu's own layout/containers. Pressing it fires <see cref="JoinDummyHost"/>.
	/// Only ever called on the enabled path (guarded by the const at the call site).
	/// </summary>
	private static void TryAddJoinButton(SceneTree tree)
	{
		try
		{
			if (_joinButtonLayer != null)
			{
				return;
			}

			CanvasLayer layer = new CanvasLayer
			{
				Name = "CardEditorDebugDummyLobbyJoinLayer",
				Layer = 128
			};

			Button button = new Button
			{
				Name = "CardEditorDebugDummyLobbyJoinButton",
				Text = "Join Dummy Host [debug]",
				Position = new Vector2(20, 20)
			};

			button.Pressed += () => JoinDummyHost("button");
			layer.AddChild(button);

			_joinButtonLayer = layer;
			tree.Root.CallDeferred(Node.MethodName.AddChild, layer);
		}
		catch (Exception ex)
		{
			// A debug button must NEVER crash the game.
			Log.Warn($"[CardEditor][DummyLobby] Failed adding Join Dummy Host button: {ex}");
		}
	}

	/// <summary>
	/// Fires the REAL join flow: <see cref="NMainMenu.JoinGame"/> with an
	/// <see cref="ENetClientConnectionInitializer"/> pointed at the in-process dummy host
	/// (<see cref="DummyHostIp"/>:<see cref="DummyHostPort"/>). Routed through
	/// <see cref="TaskHelper.RunSafely"/> so the async task's exceptions are logged, and fully try/catch
	/// guarded so neither the button nor the key fallback can crash the game. Invoked by the button and by
	/// the F9 key fallback in <see cref="CardEditorDebugDummyLobbyPump"/>.
	/// </summary>
	/// <param name="source">Where the join was triggered from ("button" or "key"), for the log line.</param>
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

			ENetClientConnectionInitializer initializer = new ENetClientConnectionInitializer(DummyClientNetId, DummyHostIp, DummyHostPort);

			// JoinGame is public async Task; RunSafely fires it without awaiting and logs any exception.
			TaskHelper.RunSafely(menu.JoinGame(initializer));
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
	/// Key fallback for the one-click join: pressing F9 fires the same real join flow as the debug button,
	/// in case the button is obscured by menu UI. Fully guarded; a debug key must never crash the game.
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

#pragma warning restore CS0162
