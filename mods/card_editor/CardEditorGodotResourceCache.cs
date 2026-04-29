using System;
using Godot;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorGodotResourceCache
{
	public static T? Load<T>(ref T? cached, string path) where T : GodotObject
	{
		return GetOrReload(ref cached, () => GD.Load<T>(path));
	}

	public static T? TryLoad<T>(ref T? cached, string path) where T : GodotObject
	{
		return GetOrReload(ref cached, () =>
		{
			try
			{
				return GD.Load<T>(path);
			}
			catch
			{
				return null;
			}
		});
	}

	public static T? GetOrReload<T>(ref T? cached, Func<T?> load) where T : GodotObject
	{
		if (IsAlive(cached))
		{
			return cached;
		}

		cached = load();
		return cached;
	}

	public static bool IsAlive(GodotObject? resource)
	{
		if (resource == null)
		{
			return false;
		}

		try
		{
			return GodotObject.IsInstanceValid(resource);
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
