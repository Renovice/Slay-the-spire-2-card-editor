using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCustomIconLoader
{
	private static readonly object _lock = new();
	private static readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _warnedFailures = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg",
		".webp"
	};

	private const string CustomIconsFolderName = "custom icons";
	private const string CustomIconsFolderAltName = "custom_icons";

	public static void EnsureCustomIconDirectory()
	{
		try
		{
			string dir = GetPreferredCustomIconDirectory();
			if (!string.IsNullOrWhiteSpace(dir))
			{
				Directory.CreateDirectory(dir);
			}
		}
		catch
		{
		}
	}

	public static string GetCustomIconDirectoryPath()
	{
		EnsureCustomIconDirectory();
		return GetPreferredCustomIconDirectory();
	}

	public static IReadOnlyList<string> ListCustomIconFiles()
	{
		try
		{
			string dir = GetPreferredCustomIconDirectory();
			if (string.IsNullOrWhiteSpace(dir))
			{
				return Array.Empty<string>();
			}

			Directory.CreateDirectory(dir);

			return Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
				.Where(path => _allowedExtensions.Contains(Path.GetExtension(path) ?? string.Empty))
				.Select(Path.GetFileName)
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList()!;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed listing custom icon files: {ex}");
			return Array.Empty<string>();
		}
	}

	public static Texture2D? LoadTexture(string? pathOrFileName)
	{
		if (string.IsNullOrWhiteSpace(pathOrFileName))
		{
			return null;
		}

		string input = pathOrFileName.Trim();
		string cacheKey = ResolveCacheKey(input);

		lock (_lock)
		{
			if (_cache.TryGetValue(cacheKey, out Texture2D? cached))
			{
				return cached;
			}
		}

		Texture2D? loaded = TryLoadTextureInternal(input);
		lock (_lock)
		{
			_cache[cacheKey] = loaded;
		}

		if (loaded == null)
		{
			WarnOnce($"Failed loading custom icon '{input}'.");
		}

		return loaded;
	}

	private static Texture2D? TryLoadTextureInternal(string input)
	{
		if (LooksLikeResourcePath(input))
		{
			try
			{
				if (ResourceLoader.Exists(input))
				{
					return ResourceLoader.Load<Texture2D>(input);
				}
			}
			catch
			{
			}
		}

		if (!TryResolveAbsolutePath(input, out string absolutePath))
		{
			return null;
		}

		return TryLoadTextureFromFile(absolutePath);
	}

	private static string ResolveCacheKey(string input)
	{
		if (LooksLikeResourcePath(input))
		{
			return input;
		}

		return TryResolveAbsolutePath(input, out string absolutePath)
			? absolutePath
			: input;
	}

	private static bool LooksLikeResourcePath(string input)
	{
		return input.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
			|| input.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryResolveAbsolutePath(string input, out string absolutePath)
	{
		absolutePath = string.Empty;

		try
		{
			if (Path.IsPathRooted(input))
			{
				if (!File.Exists(input))
				{
					return false;
				}

				absolutePath = Path.GetFullPath(input);
				return true;
			}

			string normalizedRelative = input
				.Replace('/', Path.DirectorySeparatorChar)
				.Replace('\\', Path.DirectorySeparatorChar);

			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (!string.IsNullOrWhiteSpace(modDir))
			{
				string directCandidate = Path.Combine(modDir, normalizedRelative);
				if (File.Exists(directCandidate))
				{
					absolutePath = directCandidate;
					return true;
				}
			}

			string customDir = GetPreferredCustomIconDirectory();
			if (!string.IsNullOrWhiteSpace(customDir))
			{
				string customCandidate = Path.Combine(customDir, normalizedRelative);
				if (File.Exists(customCandidate))
				{
					absolutePath = customCandidate;
					return true;
				}

				string fileNameOnly = Path.GetFileName(normalizedRelative);
				if (!string.IsNullOrWhiteSpace(fileNameOnly))
				{
					string fileOnlyCandidate = Path.Combine(customDir, fileNameOnly);
					if (File.Exists(fileOnlyCandidate))
					{
						absolutePath = fileOnlyCandidate;
						return true;
					}
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private static string GetPreferredCustomIconDirectory()
	{
		try
		{
			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (string.IsNullOrWhiteSpace(modDir))
			{
				return string.Empty;
			}

			string primary = Path.Combine(modDir, CustomIconsFolderName);
			if (Directory.Exists(primary))
			{
				return primary;
			}

			string alt = Path.Combine(modDir, CustomIconsFolderAltName);
			if (Directory.Exists(alt))
			{
				return alt;
			}

			return primary;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static Texture2D? TryLoadTextureFromFile(string absolutePath)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
			{
				return null;
			}

			string extension = (Path.GetExtension(absolutePath) ?? string.Empty).ToLowerInvariant();
			byte[] bytes = File.ReadAllBytes(absolutePath);
			if (bytes.Length == 0)
			{
				return null;
			}

			Image image = new Image();
			Error err = Error.Failed;

			if (extension == ".png")
			{
				err = image.LoadPngFromBuffer(bytes);
			}
			else if (extension == ".jpg" || extension == ".jpeg")
			{
				err = image.LoadJpgFromBuffer(bytes);
			}
			else if (extension == ".webp")
			{
				err = image.LoadWebpFromBuffer(bytes);
			}

			if (err != Error.Ok || image.IsEmpty())
			{
				Image? loadedFromFile = TryLoadImageFromFile(absolutePath);
				if (loadedFromFile == null || loadedFromFile.IsEmpty())
				{
					return null;
				}

				image = loadedFromFile;
			}

			return ImageTexture.CreateFromImage(image);
		}
		catch
		{
			return null;
		}
	}

	private static Image? TryLoadImageFromFile(string absolutePath)
	{
		try
		{
			return Image.LoadFromFile(absolutePath.Replace('\\', '/'));
		}
		catch
		{
			return null;
		}
	}

	private static void WarnOnce(string message)
	{
		lock (_lock)
		{
			if (!_warnedFailures.Add(message))
			{
				return;
			}
		}

		Log.Warn($"[CardEditor] {message}");
	}
}
