using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal enum CardEditorCustomFoilParamType
{
	Float,
	Int,
	Bool,
	Color,
	Vec2,
	Vec3,
	Vec4,
	Texture,
	String
}

internal sealed class CardEditorCustomFoilParam
{
	public string Key { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public CardEditorCustomFoilParamType Type { get; init; } = CardEditorCustomFoilParamType.Float;
	public float Min { get; init; } = 0f;
	public float Max { get; init; } = 1f;
	public float Step { get; init; } = 0.01f;
	public string DefaultValue { get; init; } = string.Empty;
	public bool Hidden { get; init; }
}

internal sealed class CardEditorCustomFoilDefinition
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string DirectoryPath { get; init; } = string.Empty;
	public string ShaderPath { get; init; } = string.Empty;
	public string ShaderCode { get; init; } = string.Empty;
	public Shader Shader { get; init; } = null!;
	public IReadOnlyList<CardEditorCustomFoilParam> Params { get; init; } = Array.Empty<CardEditorCustomFoilParam>();
}

internal static class CardEditorCustomFoilRegistry
{
	private const string FolderName = "custom_foils";
	private static readonly Regex UniformRegex = new(
		@"^\s*uniform\s+(?<type>[A-Za-z0-9_]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*:\s*(?<hint>[^=;]+))?(?:\s*=\s*(?<default>[^;]+))?\s*;",
		RegexOptions.Multiline | RegexOptions.Compiled);
	private static readonly object _lock = new();
	private static readonly Dictionary<string, CardEditorCustomFoilDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _warned = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, ImageTexture?> _textureCache = new(StringComparer.OrdinalIgnoreCase);
	private static IReadOnlyList<CardEditorCustomFoilDefinition> _definitions = Array.Empty<CardEditorCustomFoilDefinition>();
	private static bool _loaded;

	public static string CustomFoilDirectory => GetCustomFoilDirectory();

	public static IReadOnlyList<CardEditorCustomFoilDefinition> GetDefinitions()
	{
		EnsureLoaded();
		return _definitions;
	}

	public static bool TryGet(string? id, out CardEditorCustomFoilDefinition definition)
	{
		EnsureLoaded();
		if (!string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id.Trim(), out definition!))
		{
			return true;
		}

		definition = null!;
		return false;
	}

	public static bool IsCustomShader(Shader? shader)
	{
		if (shader == null)
		{
			return false;
		}

		EnsureLoaded();
		foreach (CardEditorCustomFoilDefinition def in _definitions)
		{
			if (def.Shader == shader)
			{
				return true;
			}
		}
		return false;
	}

	public static void ApplyParameters(ShaderMaterial material, CardEditorCustomFoilDefinition definition, bool fullArt, Dictionary<string, string>? values, Vector2 rectSize)
	{
		if (material == null || definition == null)
		{
			return;
		}

		Vector2 size = rectSize.X > 0f && rectSize.Y > 0f ? rectSize : new Vector2(300f, 422f);
		material.SetShaderParameter("rect_size", size);
		material.SetShaderParameter("foil_size", size);
		material.SetShaderParameter("full_art", fullArt);

		foreach (CardEditorCustomFoilParam param in definition.Params)
		{
			if (string.IsNullOrWhiteSpace(param.Key))
			{
				continue;
			}

			string raw = values != null && values.TryGetValue(param.Key, out string? configured) && configured != null
				? configured
				: param.DefaultValue;
			SetUniform(material, definition, param, raw);
		}
	}

	public static Dictionary<string, string> BuildDefaultParamValues(string? id)
	{
		if (!TryGet(id, out CardEditorCustomFoilDefinition def))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		return def.Params
			.Where(p => !p.Hidden)
			.ToDictionary(p => p.Key, p => p.DefaultValue, StringComparer.Ordinal);
	}

	public static Dictionary<string, string>? NormalizeParamValues(string? id, Dictionary<string, string>? values)
	{
		if (!TryGet(id, out CardEditorCustomFoilDefinition def))
		{
			return values != null && values.Count > 0
				? new Dictionary<string, string>(values, StringComparer.Ordinal)
				: null;
		}

		Dictionary<string, string> normalized = new(StringComparer.Ordinal);
		foreach (CardEditorCustomFoilParam param in def.Params)
		{
			if (param.Hidden)
			{
				continue;
			}
			string value = values != null && values.TryGetValue(param.Key, out string? configured) && configured != null
				? configured
				: param.DefaultValue;
			if (!string.Equals(value, param.DefaultValue, StringComparison.Ordinal))
			{
				normalized[param.Key] = value;
			}
		}

		return normalized.Count > 0 ? normalized : null;
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}

			string root = GetCustomFoilDirectory();
			Directory.CreateDirectory(root);

			_byId.Clear();
			List<CardEditorCustomFoilDefinition> definitions = new();
			HashSet<string> manifestShaderPaths = new(StringComparer.OrdinalIgnoreCase);

			foreach (string manifestPath in SafeEnumerateFiles(root, "*.json"))
			{
				if (TryLoadManifest(root, manifestPath, out CardEditorCustomFoilDefinition? def) && def != null)
				{
					AddDefinition(definitions, def);
					manifestShaderPaths.Add(Path.GetFullPath(def.ShaderPath));
				}
			}

			foreach (string shaderPath in SafeEnumerateFiles(root, "*.gdshader"))
			{
				string fullShaderPath = Path.GetFullPath(shaderPath);
				if (manifestShaderPaths.Contains(fullShaderPath))
				{
					continue;
				}
				if (TryLoadShaderOnly(root, shaderPath, out CardEditorCustomFoilDefinition? def) && def != null)
				{
					AddDefinition(definitions, def);
				}
			}

			_definitions = definitions
				.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
				.ToList();
			_loaded = true;
		}
	}

	private static void AddDefinition(List<CardEditorCustomFoilDefinition> definitions, CardEditorCustomFoilDefinition def)
	{
		if (string.IsNullOrWhiteSpace(def.Id))
		{
			return;
		}
		if (_byId.ContainsKey(def.Id))
		{
			WarnOnce($"Duplicate custom foil id '{def.Id}' in '{def.ShaderPath}'. The first definition wins.");
			return;
		}
		_byId[def.Id] = def;
		definitions.Add(def);
	}

	private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
	{
		try
		{
			return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).ToList();
		}
		catch (Exception ex)
		{
			WarnOnce($"Could not scan custom foils folder '{root}': {ex.Message}");
			return Array.Empty<string>();
		}
	}

	private static bool TryLoadManifest(string root, string manifestPath, out CardEditorCustomFoilDefinition? definition)
	{
		definition = null;
		try
		{
			CustomFoilManifestDto? dto = JsonSerializer.Deserialize<CustomFoilManifestDto>(
				File.ReadAllText(manifestPath),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (dto == null || string.IsNullOrWhiteSpace(dto.Shader))
			{
				return false;
			}

			string baseDir = Path.GetDirectoryName(manifestPath) ?? root;
			string shaderPath = ResolveContainedPath(root, baseDir, dto.Shader);
			if (string.IsNullOrWhiteSpace(shaderPath) || !File.Exists(shaderPath))
			{
				WarnOnce($"Custom foil manifest '{manifestPath}' points to missing shader '{dto.Shader}'.");
				return false;
			}

			string shaderCode = File.ReadAllText(shaderPath);
			string fallbackId = Path.GetFileNameWithoutExtension(shaderPath);
			string id = NormalizeId(dto.Id, fallbackId);
			Dictionary<string, string> defaults = ConvertDefaults(dto.Defaults);
			List<CardEditorCustomFoilParam> autoParams = ParseShaderUniforms(shaderCode, defaults);
			List<CardEditorCustomFoilParam> manifestParams = dto.Knobs != null
				? dto.Knobs.Select(k => ToParam(k, autoParams, defaults)).Where(p => p != null).Cast<CardEditorCustomFoilParam>().ToList()
				: new List<CardEditorCustomFoilParam>();
			List<CardEditorCustomFoilParam> merged = MergeParams(manifestParams, autoParams);

			definition = new CardEditorCustomFoilDefinition
			{
				Id = id,
				Name = string.IsNullOrWhiteSpace(dto.Name) ? Titleize(id) : dto.Name.Trim(),
				DirectoryPath = baseDir,
				ShaderPath = shaderPath,
				ShaderCode = shaderCode,
				Shader = new Shader { Code = shaderCode },
				Params = merged
			};
			return true;
		}
		catch (Exception ex)
		{
			WarnOnce($"Could not load custom foil manifest '{manifestPath}': {ex.Message}");
			return false;
		}
	}

	private static bool TryLoadShaderOnly(string root, string shaderPath, out CardEditorCustomFoilDefinition? definition)
	{
		definition = null;
		try
		{
			string shaderCode = File.ReadAllText(shaderPath);
			string id = NormalizeId(null, Path.GetFileNameWithoutExtension(shaderPath));
			definition = new CardEditorCustomFoilDefinition
			{
				Id = id,
				Name = Titleize(id),
				DirectoryPath = Path.GetDirectoryName(shaderPath) ?? root,
				ShaderPath = Path.GetFullPath(shaderPath),
				ShaderCode = shaderCode,
				Shader = new Shader { Code = shaderCode },
				Params = ParseShaderUniforms(shaderCode, new Dictionary<string, string>(StringComparer.Ordinal))
			};
			return true;
		}
		catch (Exception ex)
		{
			WarnOnce($"Could not load custom foil shader '{shaderPath}': {ex.Message}");
			return false;
		}
	}

	private static List<CardEditorCustomFoilParam> MergeParams(List<CardEditorCustomFoilParam> manifestParams, List<CardEditorCustomFoilParam> autoParams)
	{
		if (manifestParams.Count == 0)
		{
			return autoParams;
		}

		HashSet<string> seen = new(manifestParams.Select(p => p.Key), StringComparer.Ordinal);
		foreach (CardEditorCustomFoilParam param in autoParams)
		{
			if (!seen.Contains(param.Key) && param.Hidden)
			{
				manifestParams.Add(param);
			}
		}
		return manifestParams;
	}

	private static CardEditorCustomFoilParam? ToParam(CustomFoilKnobDto dto, List<CardEditorCustomFoilParam> autoParams, Dictionary<string, string> defaults)
	{
		if (string.IsNullOrWhiteSpace(dto.Key))
		{
			return null;
		}

		CardEditorCustomFoilParam? auto = autoParams.FirstOrDefault(p => string.Equals(p.Key, dto.Key, StringComparison.Ordinal));
		CardEditorCustomFoilParamType type = ParseType(dto.Type, auto?.Type ?? CardEditorCustomFoilParamType.Float);
		string defaultValue = ToStringValue(dto.Default);
		if (string.IsNullOrWhiteSpace(defaultValue) && defaults.TryGetValue(dto.Key, out string? configuredDefault))
		{
			defaultValue = configuredDefault;
		}
		if (string.IsNullOrWhiteSpace(defaultValue))
		{
			defaultValue = auto?.DefaultValue ?? DefaultForType(type);
		}

		return new CardEditorCustomFoilParam
		{
			Key = dto.Key.Trim(),
			Label = string.IsNullOrWhiteSpace(dto.Label) ? auto?.Label ?? Titleize(dto.Key) : dto.Label.Trim(),
			Type = type,
			Min = dto.Min ?? auto?.Min ?? 0f,
			Max = dto.Max ?? auto?.Max ?? 1f,
			Step = dto.Step ?? auto?.Step ?? (type == CardEditorCustomFoilParamType.Int ? 1f : 0.01f),
			DefaultValue = defaultValue,
			Hidden = dto.Hidden ?? auto?.Hidden ?? IsAutoUniform(dto.Key)
		};
	}

	private static List<CardEditorCustomFoilParam> ParseShaderUniforms(string shaderCode, Dictionary<string, string> configuredDefaults)
	{
		List<CardEditorCustomFoilParam> result = new();
		foreach (Match match in UniformRegex.Matches(shaderCode ?? string.Empty))
		{
			string typeText = match.Groups["type"].Value;
			string key = match.Groups["name"].Value;
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			CardEditorCustomFoilParamType type = ParseGodotUniformType(typeText, match.Groups["hint"].Value);
			if (!IsSupportedType(type))
			{
				continue;
			}

			(float min, float max, float step) = ParseRange(match.Groups["hint"].Value, type);
			string rawDefault = match.Groups["default"].Success ? match.Groups["default"].Value.Trim() : string.Empty;
			string defaultValue = configuredDefaults.TryGetValue(key, out string? configuredDefault)
				? configuredDefault
				: NormalizeShaderDefault(rawDefault, type);

			result.Add(new CardEditorCustomFoilParam
			{
				Key = key,
				Label = Titleize(key),
				Type = type,
				Min = min,
				Max = max,
				Step = step,
				DefaultValue = string.IsNullOrWhiteSpace(defaultValue) ? DefaultForType(type) : defaultValue,
				Hidden = IsAutoUniform(key)
			});
		}
		return result;
	}

	private static bool IsSupportedType(CardEditorCustomFoilParamType type)
		=> type != CardEditorCustomFoilParamType.String || true;

	private static CardEditorCustomFoilParamType ParseGodotUniformType(string typeText, string hint)
	{
		string normalized = typeText.Trim().ToLowerInvariant();
		if (normalized == "float")
			return CardEditorCustomFoilParamType.Float;
		if (normalized == "int")
			return CardEditorCustomFoilParamType.Int;
		if (normalized == "bool")
			return CardEditorCustomFoilParamType.Bool;
		if (normalized == "vec2")
			return CardEditorCustomFoilParamType.Vec2;
		if (normalized == "vec3")
			return hint.Contains("source_color", StringComparison.OrdinalIgnoreCase)
				? CardEditorCustomFoilParamType.Color
				: CardEditorCustomFoilParamType.Vec3;
		if (normalized == "vec4")
			return hint.Contains("source_color", StringComparison.OrdinalIgnoreCase)
				? CardEditorCustomFoilParamType.Color
				: CardEditorCustomFoilParamType.Vec4;
		if (normalized.StartsWith("sampler", StringComparison.Ordinal))
			return CardEditorCustomFoilParamType.Texture;
		return CardEditorCustomFoilParamType.String;
	}

	private static CardEditorCustomFoilParamType ParseType(string? typeText, CardEditorCustomFoilParamType fallback)
	{
		if (string.IsNullOrWhiteSpace(typeText))
		{
			return fallback;
		}

		return typeText.Trim().ToLowerInvariant() switch
		{
			"float" or "number" => CardEditorCustomFoilParamType.Float,
			"int" or "integer" => CardEditorCustomFoilParamType.Int,
			"bool" or "boolean" => CardEditorCustomFoilParamType.Bool,
			"color" => CardEditorCustomFoilParamType.Color,
			"vec2" => CardEditorCustomFoilParamType.Vec2,
			"vec3" => CardEditorCustomFoilParamType.Vec3,
			"vec4" => CardEditorCustomFoilParamType.Vec4,
			"texture" or "sampler2d" => CardEditorCustomFoilParamType.Texture,
			"string" or "text" => CardEditorCustomFoilParamType.String,
			_ => fallback
		};
	}

	private static (float Min, float Max, float Step) ParseRange(string hint, CardEditorCustomFoilParamType type)
	{
		if (type == CardEditorCustomFoilParamType.Int)
		{
			return (0f, 100f, 1f);
		}
		if (type != CardEditorCustomFoilParamType.Float)
		{
			return (0f, 1f, 0.01f);
		}

		Match match = Regex.Match(hint ?? string.Empty, @"hint_range\s*\((?<args>[^)]*)\)", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return (0f, 5f, 0.01f);
		}

		string[] parts = match.Groups["args"].Value.Split(',').Select(p => p.Trim()).ToArray();
		float min = parts.Length > 0 && TryParseFloat(parts[0], out float parsedMin) ? parsedMin : 0f;
		float max = parts.Length > 1 && TryParseFloat(parts[1], out float parsedMax) ? parsedMax : Math.Max(1f, min + 1f);
		float step = parts.Length > 2 && TryParseFloat(parts[2], out float parsedStep) ? parsedStep : 0.01f;
		return (min, max, step <= 0f ? 0.01f : step);
	}

	private static string NormalizeShaderDefault(string rawDefault, CardEditorCustomFoilParamType type)
	{
		if (string.IsNullOrWhiteSpace(rawDefault))
		{
			return DefaultForType(type);
		}

		rawDefault = rawDefault.Trim();
		if (type == CardEditorCustomFoilParamType.Color)
		{
			if (TryParseColor(rawDefault, out Color color))
			{
				return ColorToText(color);
			}
		}
		return rawDefault.Trim('"');
	}

	private static string DefaultForType(CardEditorCustomFoilParamType type) => type switch
	{
		CardEditorCustomFoilParamType.Int => "0",
		CardEditorCustomFoilParamType.Bool => "false",
		CardEditorCustomFoilParamType.Color => "#ffffffff",
		CardEditorCustomFoilParamType.Vec2 => "0,0",
		CardEditorCustomFoilParamType.Vec3 => "0,0,0",
		CardEditorCustomFoilParamType.Vec4 => "0,0,0,1",
		_ => "0"
	};

	private static void SetUniform(ShaderMaterial material, CardEditorCustomFoilDefinition definition, CardEditorCustomFoilParam param, string raw)
	{
		try
		{
			switch (param.Type)
			{
				case CardEditorCustomFoilParamType.Float:
					material.SetShaderParameter(param.Key, TryParseFloat(raw, out float f) ? f : ParseFallbackFloat(param.DefaultValue));
					break;
				case CardEditorCustomFoilParamType.Int:
					material.SetShaderParameter(param.Key, TryParseInt(raw, out int i) ? i : (int)MathF.Round(ParseFallbackFloat(param.DefaultValue)));
					break;
				case CardEditorCustomFoilParamType.Bool:
					material.SetShaderParameter(param.Key, TryParseBool(raw, out bool b) && b);
					break;
				case CardEditorCustomFoilParamType.Color:
					material.SetShaderParameter(param.Key, TryParseColor(raw, out Color c) ? c : Colors.White);
					break;
				case CardEditorCustomFoilParamType.Vec2:
					material.SetShaderParameter(param.Key, TryParseNumbers(raw, 2, out float[] v2) ? new Vector2(v2[0], v2[1]) : Vector2.Zero);
					break;
				case CardEditorCustomFoilParamType.Vec3:
					material.SetShaderParameter(param.Key, TryParseNumbers(raw, 3, out float[] v3) ? new Vector3(v3[0], v3[1], v3[2]) : Vector3.Zero);
					break;
				case CardEditorCustomFoilParamType.Vec4:
					material.SetShaderParameter(param.Key, TryParseNumbers(raw, 4, out float[] v4) ? new Vector4(v4[0], v4[1], v4[2], v4[3]) : Vector4.Zero);
					break;
				case CardEditorCustomFoilParamType.Texture:
					ImageTexture? tex = ResolveTexture(definition, raw);
					if (tex != null)
					{
						material.SetShaderParameter(param.Key, tex);
					}
					break;
				case CardEditorCustomFoilParamType.String:
					break;
			}
		}
		catch (Exception ex)
		{
			WarnOnce($"Could not apply custom foil uniform '{param.Key}' for '{definition.Id}': {ex.Message}");
		}
	}

	private static ImageTexture? ResolveTexture(CardEditorCustomFoilDefinition definition, string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}

		try
		{
			string path = ResolveContainedPath(CustomFoilDirectory, definition.DirectoryPath, raw.Trim().Trim('"'));
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				return null;
			}
			if (_textureCache.TryGetValue(path, out ImageTexture? cached))
			{
				return cached;
			}

			Image? image = Image.LoadFromFile(path.Replace('\\', '/'));
			ImageTexture? texture = image != null && !image.IsEmpty()
				? ImageTexture.CreateFromImage(image)
				: null;
			_textureCache[path] = texture;
			return texture;
		}
		catch (Exception ex)
		{
			WarnOnce($"Could not load custom foil texture '{raw}' for '{definition.Id}': {ex.Message}");
			return null;
		}
	}

	private static Dictionary<string, string> ConvertDefaults(Dictionary<string, JsonElement>? defaults)
	{
		Dictionary<string, string> result = new(StringComparer.Ordinal);
		if (defaults == null)
		{
			return result;
		}

		foreach ((string key, JsonElement value) in defaults)
		{
			string text = ToStringValue(value);
			if (!string.IsNullOrWhiteSpace(key) && text != null)
			{
				result[key] = text;
			}
		}
		return result;
	}

	private static string ToStringValue(JsonElement? value)
	{
		if (!value.HasValue)
		{
			return string.Empty;
		}

		return ToStringValue(value.Value);
	}

	private static string ToStringValue(JsonElement value)
	{
		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? string.Empty,
			JsonValueKind.Number => value.GetRawText(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			JsonValueKind.Array => string.Join(",", value.EnumerateArray().Select(ToStringValue)),
			_ => value.GetRawText()
		};
	}

	private static string NormalizeId(string? configured, string fallback)
	{
		string source = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
		char[] chars = source
			.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? char.ToLowerInvariant(ch) : '_')
			.ToArray();
		string id = new string(chars).Trim('_');
		return string.IsNullOrWhiteSpace(id) ? "custom_foil" : id;
	}

	private static string Titleize(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return "Custom Foil";
		}
		string[] parts = Regex.Split(key.Replace('-', '_'), "_+")
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.ToArray();
		return parts.Length == 0
			? key
			: string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
	}

	private static bool IsAutoUniform(string key)
		=> string.Equals(key, "rect_size", StringComparison.Ordinal)
			|| string.Equals(key, "foil_size", StringComparison.Ordinal)
			|| string.Equals(key, "full_art", StringComparison.Ordinal);

	private static bool TryParseFloat(string text, out float value)
		=> float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

	private static float ParseFallbackFloat(string text)
		=> TryParseFloat(text, out float value) ? value : 0f;

	private static bool TryParseInt(string text, out int value)
		=> int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

	private static bool TryParseBool(string text, out bool value)
	{
		if (bool.TryParse(text, out value))
		{
			return true;
		}
		if (TryParseFloat(text, out float f))
		{
			value = Math.Abs(f) > 0.0001f;
			return true;
		}
		value = false;
		return false;
	}

	private static bool TryParseNumbers(string text, int count, out float[] values)
	{
		string normalized = text
			.Replace("vec2", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("vec3", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("vec4", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("(", string.Empty)
			.Replace(")", string.Empty)
			.Replace("[", string.Empty)
			.Replace("]", string.Empty);
		string[] parts = normalized.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		values = new float[count];
		if (parts.Length < count)
		{
			return false;
		}
		for (int i = 0; i < count; i++)
		{
			if (!TryParseFloat(parts[i], out values[i]))
			{
				return false;
			}
		}
		return true;
	}

	private static bool TryParseColor(string text, out Color color)
	{
		string value = text.Trim().Trim('"');
		if (value.StartsWith("#", StringComparison.Ordinal))
		{
			string hex = value[1..];
			if (hex.Length == 6 || hex.Length == 8)
			{
				try
				{
					byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					byte a = hex.Length == 8 ? byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)255;
					color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
					return true;
				}
				catch
				{
				}
			}
		}

		if (TryParseNumbers(value, 4, out float[] rgba4))
		{
			color = new Color(rgba4[0], rgba4[1], rgba4[2], rgba4[3]);
			return true;
		}
		if (TryParseNumbers(value, 3, out float[] rgb3))
		{
			color = new Color(rgb3[0], rgb3[1], rgb3[2], 1f);
			return true;
		}

		color = Colors.White;
		return false;
	}

	private static string ColorToText(Color color)
	{
		static int ToByte(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
		return $"#{ToByte(color.R):x2}{ToByte(color.G):x2}{ToByte(color.B):x2}{ToByte(color.A):x2}";
	}

	private static string ResolveContainedPath(string root, string baseDir, string relativeOrAbsolute)
	{
		if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
		{
			return string.Empty;
		}

		string candidate = Path.IsPathRooted(relativeOrAbsolute)
			? relativeOrAbsolute
			: Path.Combine(baseDir, relativeOrAbsolute);
		string full = Path.GetFullPath(candidate);
		string rootFull = Path.GetFullPath(root);
		if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException($"Path '{relativeOrAbsolute}' is outside '{FolderName}'.");
		}
		return full;
	}

	private static string GetCustomFoilDirectory()
	{
		try
		{
			string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (!string.IsNullOrWhiteSpace(assemblyDir))
			{
				return Path.Combine(assemblyDir, FolderName);
			}
		}
		catch
		{
		}

		return Path.Combine(Directory.GetCurrentDirectory(), FolderName);
	}

	private static void WarnOnce(string message)
	{
		lock (_lock)
		{
			if (!_warned.Add(message))
			{
				return;
			}
		}
		Log.Warn($"[CardEditor] {message}");
	}

	private sealed class CustomFoilManifestDto
	{
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? Shader { get; set; }
		public Dictionary<string, JsonElement>? Defaults { get; set; }
		public List<CustomFoilKnobDto>? Knobs { get; set; }
	}

	private sealed class CustomFoilKnobDto
	{
		public string? Key { get; set; }
		public string? Label { get; set; }
		public string? Type { get; set; }
		public float? Min { get; set; }
		public float? Max { get; set; }
		public float? Step { get; set; }
		public JsonElement? Default { get; set; }
		public bool? Hidden { get; set; }
	}
}
