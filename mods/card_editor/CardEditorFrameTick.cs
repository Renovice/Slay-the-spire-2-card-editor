using System;
using Godot;

namespace SlayTheSpire2Mod.CardEditor;

// card_editor builds without Godot's C# source generators, so Godot never dispatches engine virtuals
// (_Ready, _Process, _Input, _Notification, ...) to overrides declared in mod classes that derive directly
// from Godot types: GodotSharp's InvokeGodotClassMethod only calls an override when the generated
// HasGodotClassMethod reports it. (Mod subclasses of game types such as NButton/NSubmenu still receive the
// virtuals the game class itself overrides.) Signal-connected delegate Callables do work, so non-UI per-frame
// work runs from SceneTree.ProcessFrame through this helper.
internal sealed class CardEditorFrameTick
{
	// A long hitch (loading, alt-tab) should not turn into one huge delta on the next tick.
	private const double MaxDeltaSeconds = 0.25;

	private readonly Action<double> _tick;
	private readonly Callable _callable;
	private SceneTree? _tree;
	private ulong _lastTickUsec;

	public CardEditorFrameTick(Action<double> tick)
	{
		_tick = tick;
		_callable = Callable.From(OnProcessFrame);
	}

	public bool IsRunning => _tree != null
		&& GodotObject.IsInstanceValid(_tree)
		&& _tree.IsConnected(SceneTree.SignalName.ProcessFrame, _callable);

	public void Start()
	{
		if (IsRunning || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}

		tree.Connect(SceneTree.SignalName.ProcessFrame, _callable);
		_tree = tree;
		_lastTickUsec = Time.GetTicksUsec();
	}

	public void Stop()
	{
		if (IsRunning)
		{
			_tree!.Disconnect(SceneTree.SignalName.ProcessFrame, _callable);
		}
		_tree = null;
	}

	private void OnProcessFrame()
	{
		ulong now = Time.GetTicksUsec();
		double delta = Math.Min(MaxDeltaSeconds, (now - _lastTickUsec) / 1_000_000.0);
		_lastTickUsec = now;
		try
		{
			_tick(delta);
		}
		catch (Exception ex)
		{
			MegaCrit.Sts2.Core.Logging.Log.Warn($"[CardEditor] Frame tick failed: {ex.GetType().Name}: {ex.Message}");
		}
	}
}
