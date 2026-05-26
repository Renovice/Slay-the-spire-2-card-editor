using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class CardEditorCustomPortraitLoader
{
	private static readonly object _lock = new();
	private static readonly Dictionary<string, Texture2D> _cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, CardEditorGifAnimation> _gifAnimations = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _warnedFailures = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _loggedLoads = new(StringComparer.OrdinalIgnoreCase);
	private const string CardArtFolderName = "card art";

	[ThreadStatic]
	private static HashSet<ModelId>? _resolvingSourcePortraits;

	[ThreadStatic]
	private static HashSet<ModelId>? _resolvingSourcePortraitPaths;

	internal readonly struct PortraitTransform
	{
		public PortraitTransform(float offsetX, float offsetY, float zoom)
		{
			OffsetX = offsetX;
			OffsetY = offsetY;
			Zoom = zoom <= 0f ? 1f : zoom;
		}

		public float OffsetX { get; }
		public float OffsetY { get; }
		public float Zoom { get; }

		public bool IsDefault =>
			Math.Abs(OffsetX) < 0.01f
			&& Math.Abs(OffsetY) < 0.01f
			&& Math.Abs(Zoom - 1f) < 0.001f;
	}

	public static bool TryGetCustomPortrait(ModelId cardId, out Texture2D texture)
	{
		texture = null!;

		if (cardId == null)
		{
			return false;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(cardId))
		{
			if (CardEditorCreatedCardsStore.TryGetCustomPortraitAbsolutePath(cardId, out string createdAbsolutePath))
			{
				return TryGetOrLoadTexture(createdAbsolutePath, cardId, out texture);
			}

			ModelId? sourceCardId = CardEditorCreatedCardsStore.GetPortraitSourceCardId(cardId);
			if (sourceCardId != null && sourceCardId != ModelId.none)
			{
				return TryGetPortraitFromSourceCard(sourceCardId, out texture);
			}

			return false;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(cardId, out CardOverride overrideData))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile))
		{
			if (!TryGetCustomPortraitAbsolutePath(overrideData.CustomPortraitFile!, out string absolutePath))
			{
				return false;
			}
			return TryGetOrLoadTexture(absolutePath, cardId, out texture);
		}

		if (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none)
		{
			return TryGetPortraitFromSourceCard(overrideData.PortraitSourceCardId, out texture);
		}

		return false;
	}

	public static bool TryGetPortrait(CardModel? card, out Texture2D texture)
	{
		texture = null!;

		if (card?.Id == null)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			if (TryGetPortrait(identitySource, out texture))
			{
				return true;
			}

			string portraitPath = identitySource.PortraitPath;
			if (!string.IsNullOrWhiteSpace(portraitPath) && ResourceLoader.Exists(portraitPath))
			{
				texture = ResourceLoader.Load<Texture2D>(portraitPath, null, ResourceLoader.CacheMode.Reuse);
				return texture != null;
			}
		}

		if (TryGetCustomPortrait(card.Id, out texture))
		{
			return true;
		}

		return TryGetPoolOverridePortraitFallback(card, out texture);
	}

	public static bool TryGetPortraitAnimation(CardModel? card, out CardEditorGifAnimation animation)
	{
		animation = null!;

		if (card?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			return TryGetPortraitAnimation(identitySource, out animation);
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(card.Id))
		{
			if (CardEditorCreatedCardsStore.TryGetCustomPortraitAbsolutePath(card.Id, out string createdAbsolutePath))
			{
				return TryGetGifAnimation(createdAbsolutePath, card.Id, out animation);
			}

			ModelId? sourceCardId = CardEditorCreatedCardsStore.GetPortraitSourceCardId(card.Id);
			if (sourceCardId != null && sourceCardId != ModelId.none)
			{
				return TryGetPortraitAnimationFromSourceCard(sourceCardId, out animation);
			}

			return false;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile))
		{
			return TryGetCustomPortraitAbsolutePath(overrideData.CustomPortraitFile!, out string absolutePath)
				&& TryGetGifAnimation(absolutePath, card.Id, out animation);
		}

		if (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none)
		{
			return TryGetPortraitAnimationFromSourceCard(overrideData.PortraitSourceCardId, out animation);
		}

		return false;
	}

	private static bool TryGetPortraitAnimationFromSourceCard(ModelId sourceCardId, out CardEditorGifAnimation animation)
	{
		animation = null!;
		try
		{
			_resolvingSourcePortraits ??= new HashSet<ModelId>();
			if (!_resolvingSourcePortraits.Add(sourceCardId))
			{
				return false;
			}

			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			return source != null && TryGetPortraitAnimation(source, out animation);
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				_resolvingSourcePortraits?.Remove(sourceCardId);
			}
			catch
			{
			}
		}
	}

	public static bool TryGetPortraitTransform(CardModel? card, out PortraitTransform transform)
	{
		transform = default;

		if (card?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			card = identitySource;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData))
		{
			return false;
		}

		float x = overrideData.PortraitOffsetX ?? 0f;
		float y = overrideData.PortraitOffsetY ?? 0f;
		float zoom = overrideData.PortraitZoom ?? 1f;
		transform = new PortraitTransform(x, y, Math.Clamp(zoom, 0.25f, 4f));
		return !transform.IsDefault;
	}

	public static bool TryGetPortraitPath(CardModel? card, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;

		if (card?.Id == null
			|| CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			portraitPath = beta ? identitySource.BetaPortraitPath : identitySource.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(card.Id)
			|| !CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData))
		{
			return false;
		}

		if (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none)
		{
			return TryGetPortraitPathFromSourceCard(overrideData.PortraitSourceCardId, beta, out portraitPath);
		}

		if (string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile)
			&& string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			return false;
		}

		return TryGetOriginalPortraitPath(card.Id, beta, out portraitPath);
	}

	private static bool TryGetOrLoadTexture(string absolutePath, ModelId cardId, out Texture2D texture)
	{
		texture = null!;

		Texture2D? cached;
		lock (_lock)
		{
			_cache.TryGetValue(absolutePath, out cached);
		}

		if (cached == null)
		{
			cached = TryLoadTextureFromFile(absolutePath);
			if (cached == null)
			{
				WarnOnce($"Failed loading custom art for {cardId} from '{absolutePath}'.");
				return false;
			}

			lock (_lock)
			{
				_cache[absolutePath] = cached;
			}

			InfoOnce($"Loaded custom art for {cardId} from '{absolutePath}' ({cached.GetWidth()}x{cached.GetHeight()}).");
		}

		texture = cached;
		return true;
	}

	private static bool TryGetGifAnimation(string absolutePath, ModelId cardId, out CardEditorGifAnimation animation)
	{
		animation = null!;
		if (!string.Equals(Path.GetExtension(absolutePath), ".gif", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		lock (_lock)
		{
			if (_gifAnimations.TryGetValue(absolutePath, out animation!))
			{
				return animation.FrameCount > 1;
			}
		}

		if (!TryGetOrLoadTexture(absolutePath, cardId, out _))
		{
			return false;
		}

		lock (_lock)
		{
			return _gifAnimations.TryGetValue(absolutePath, out animation!) && animation.FrameCount > 1;
		}
	}

	private static bool TryGetPortraitFromSourceCard(ModelId sourceCardId, out Texture2D texture)
	{
		texture = null!;
		try
		{
			_resolvingSourcePortraits ??= new HashSet<ModelId>();
			if (!_resolvingSourcePortraits.Add(sourceCardId))
			{
				return false;
			}

			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			if (source == null)
			{
				return false;
			}

			texture = source.Portrait;
			return texture != null;
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				_resolvingSourcePortraits?.Remove(sourceCardId);
			}
			catch
			{
			}
		}
	}

	private static bool TryGetPortraitPathFromSourceCard(ModelId sourceCardId, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;
		try
		{
			_resolvingSourcePortraitPaths ??= new HashSet<ModelId>();
			if (!_resolvingSourcePortraitPaths.Add(sourceCardId))
			{
				return false;
			}

			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			if (source == null)
			{
				return false;
			}

			portraitPath = beta ? source.BetaPortraitPath : source.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				_resolvingSourcePortraitPaths?.Remove(sourceCardId);
			}
			catch
			{
			}
		}
	}

	private static bool TryGetPoolOverridePortraitFallback(CardModel card, out Texture2D texture)
	{
		texture = null!;

		if (CardEditorOverrides.SuppressAllOverrides
			|| CardEditorCreatedCardsStore.IsCreatedCardId(card.Id)
			|| !CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
			|| string.IsNullOrWhiteSpace(overrideData.PoolTitle)
			|| !string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile)
			|| (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none))
		{
			return false;
		}

		try
		{
			string currentPortraitPath = card.PortraitPath;
			if (!string.IsNullOrWhiteSpace(currentPortraitPath) && ResourceLoader.Exists(currentPortraitPath))
			{
				return false;
			}
		}
		catch
		{
		}

		return TryGetOriginalPortrait(card, out texture);
	}

	private static bool TryGetOriginalPortrait(CardModel card, out Texture2D texture)
	{
		texture = null!;
		try
		{
			if (!TryGetOriginalPortraitPath(card.Id, beta: false, out string originalPortraitPath))
			{
				return false;
			}

			Texture2D? originalTexture = ResourceLoader.Load<Texture2D>(originalPortraitPath, null, ResourceLoader.CacheMode.Reuse);
			if (originalTexture == null)
			{
				return false;
			}

			texture = originalTexture;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetOriginalPortraitPath(ModelId cardId, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;
		bool previousSuppressAllOverrides = CardEditorOverrides.SuppressAllOverrides;
		try
		{
			CardEditorOverrides.SuppressAllOverrides = true;
			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(cardId);
			if (canonical == null)
			{
				return false;
			}

			portraitPath = beta ? canonical.BetaPortraitPath : canonical.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}
		catch
		{
			return false;
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = previousSuppressAllOverrides;
		}
	}

	private static bool TryGetCustomPortraitAbsolutePath(string fileName, out string absolutePath)
	{
		absolutePath = string.Empty;
		try
		{
			string dir = GetCustomArtDirectory();
			if (string.IsNullOrWhiteSpace(dir))
			{
				return false;
			}

			string candidate = Path.Combine(dir, fileName.Trim());
			if (!File.Exists(candidate))
			{
				return false;
			}

			absolutePath = candidate;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string GetCustomArtDirectory()
	{
		try
		{
			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (string.IsNullOrWhiteSpace(modDir))
			{
				return string.Empty;
			}

			string dir = Path.Combine(modDir, CardArtFolderName);
			if (Directory.Exists(dir))
			{
				return dir;
			}

			string alt = Path.Combine(modDir, "card_art");
			return Directory.Exists(alt) ? alt : dir;
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

			string extension = Path.GetExtension(absolutePath) ?? string.Empty;
			string extLower = extension.ToLowerInvariant();

			byte[] bytes = File.ReadAllBytes(absolutePath);
			if (bytes.Length == 0)
			{
				return null;
			}

			if (extLower == ".gif")
			{
				return TryLoadGifTexture(bytes, absolutePath);
			}

			Image image = new Image();
			Error err = Error.Failed;

			if (extLower == ".png")
			{
				err = image.LoadPngFromBuffer(bytes);
			}
			else if (extLower == ".jpg" || extLower == ".jpeg")
			{
				err = image.LoadJpgFromBuffer(bytes);
			}
			else if (extLower == ".webp")
			{
				err = image.LoadWebpFromBuffer(bytes);
			}

			// Some exported PNGs can fail buffer-decoding on certain Godot builds; fall back to file loader.
			if (err != Error.Ok || image.IsEmpty())
			{
				Image? loadedFromFile = TryLoadImageFromFile(absolutePath);
				if (loadedFromFile == null || loadedFromFile.IsEmpty())
				{
					WarnOnce($"Failed decoding custom art '{absolutePath}' (ext='{extension}', bytes={bytes.Length}, err={err}).");
					return null;
				}
				image = loadedFromFile;
			}

			return ImageTexture.CreateFromImage(image);
		}
		catch (Exception ex)
		{
			WarnOnce($"Exception loading custom art '{absolutePath}': {ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	private static Texture2D? TryLoadGifTexture(byte[] bytes, string absolutePath)
	{
		try
		{
			GifDecodeResult? gif = CardEditorGifDecoder.Decode(bytes, maxFrames: 256);
			if (gif == null || gif.Frames.Count == 0)
			{
				WarnOnce($"Failed decoding custom GIF art '{absolutePath}'.");
				return null;
			}

			int frameCount = Math.Min(gif.Frames.Count, 256);
			List<Texture2D> frameTextures = new List<Texture2D>(frameCount);
			List<float> frameDurations = new List<float>(frameCount);
			for (int i = 0; i < frameCount; i++)
			{
				GifFrame frame = gif.Frames[i];
				ImageTexture? frameTexture = CreateTextureFromRgba(gif.Width, gif.Height, frame.Rgba);
				if (frameTexture == null)
				{
					continue;
				}

				frameTextures.Add(frameTexture);
				frameDurations.Add(Math.Max(0.02f, frame.DelaySeconds));
			}

			if (frameTextures.Count == 0)
			{
				return null;
			}

			if (frameTextures.Count > 1)
			{
				lock (_lock)
				{
					_gifAnimations[absolutePath] = new CardEditorGifAnimation(frameTextures, frameDurations);
				}
			}

			if (gif.Truncated)
			{
				WarnOnce($"Custom GIF art '{absolutePath}' has more than 256 frames; only the first 256 are used.");
			}

			return frameTextures[0];
		}
		catch (Exception ex)
		{
			WarnOnce($"Exception loading custom GIF art '{absolutePath}': {ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	private static ImageTexture? CreateTextureFromRgba(int width, int height, byte[] rgba)
	{
		if (width <= 0 || height <= 0 || rgba.Length < width * height * 4)
		{
			return null;
		}

		Image image = Image.CreateFromData(width, height, useMipmaps: false, Image.Format.Rgba8, rgba);
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	private static Image? TryLoadImageFromFile(string absolutePath)
	{
		try
		{
			string normalizedPath = absolutePath.Replace('\\', '/');
			return Image.LoadFromFile(normalizedPath);
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

	private static void InfoOnce(string message)
	{
		lock (_lock)
		{
			if (!_loggedLoads.Add(message))
			{
				return;
			}
		}
		CardEditorMod.VerboseLog($"[CardEditor] {message}");
	}
}

internal sealed class CardEditorGifAnimation
{
	private readonly List<Texture2D> _frames;
	private readonly List<float> _durations;

	public CardEditorGifAnimation(List<Texture2D> frames, List<float> durations)
	{
		_frames = frames;
		_durations = durations;
	}

	public int FrameCount => _frames.Count;
	public Texture2D FirstFrame => _frames[0];

	public Texture2D GetFrame(int index)
	{
		if (_frames.Count == 0)
		{
			return null!;
		}
		return _frames[Math.Clamp(index, 0, _frames.Count - 1)];
	}

	public float GetDuration(int index)
	{
		if (_durations.Count == 0)
		{
			return 0.1f;
		}
		return _durations[Math.Clamp(index, 0, _durations.Count - 1)];
	}
}

internal partial class CardEditorGifPortraitAnimator : Node
{
	private const string AnimatorName = "__card_editor_gif_portrait_animator";
	private TextureRect? _target;
	private CardEditorGifAnimation? _animation;
	private int _frameIndex;
	private double _elapsed;

	public static void Sync(TextureRect? target, CardEditorGifAnimation? animation)
	{
		if (target == null || !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		CardEditorGifPortraitAnimator? animator = target.GetNodeOrNull<CardEditorGifPortraitAnimator>(AnimatorName);
		if (animation == null || animation.FrameCount <= 1)
		{
			if (animator != null && GodotObject.IsInstanceValid(animator))
			{
				animator.QueueFree();
			}
			return;
		}

		if (animator == null || !GodotObject.IsInstanceValid(animator))
		{
			animator = new CardEditorGifPortraitAnimator
			{
				Name = AnimatorName
			};
			target.AddChild(animator);
		}

		animator.Configure(target, animation);
	}

	private void Configure(TextureRect target, CardEditorGifAnimation animation)
	{
		if (!ReferenceEquals(_target, target) || !ReferenceEquals(_animation, animation))
		{
			_frameIndex = 0;
			_elapsed = 0;
		}

		_target = target;
		_animation = animation;
		_target.Texture = animation.FirstFrame;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_target == null
			|| _animation == null
			|| _animation.FrameCount <= 1
			|| !GodotObject.IsInstanceValid(_target))
		{
			QueueFree();
			return;
		}

		_elapsed += Math.Max(0, delta);
		float duration = _animation.GetDuration(_frameIndex);
		if (_elapsed < duration)
		{
			return;
		}

		_elapsed %= Math.Max(0.02f, duration);
		_frameIndex = (_frameIndex + 1) % _animation.FrameCount;
		_target.Texture = _animation.GetFrame(_frameIndex);
	}
}

internal sealed class GifFrame
{
	public GifFrame(byte[] rgba, float delaySeconds)
	{
		Rgba = rgba;
		DelaySeconds = delaySeconds;
	}

	public byte[] Rgba { get; }
	public float DelaySeconds { get; }
}

internal sealed class GifDecodeResult
{
	public GifDecodeResult(int width, int height, List<GifFrame> frames, bool truncated)
	{
		Width = width;
		Height = height;
		Frames = frames;
		Truncated = truncated;
	}

	public int Width { get; }
	public int Height { get; }
	public List<GifFrame> Frames { get; }
	public bool Truncated { get; }
}

internal static class CardEditorGifDecoder
{
	private readonly struct GifColor
	{
		public GifColor(byte r, byte g, byte b)
		{
			R = r;
			G = g;
			B = b;
		}

		public byte R { get; }
		public byte G { get; }
		public byte B { get; }
	}

	private sealed class GraphicControl
	{
		public int DisposalMethod { get; set; }
		public int DelayHundredths { get; set; }
		public int? TransparentIndex { get; set; }
	}

	public static GifDecodeResult? Decode(byte[] bytes, int maxFrames)
	{
		if (bytes.Length < 13 || maxFrames <= 0)
		{
			return null;
		}

		GifReader reader = new GifReader(bytes);
		string signature = reader.ReadAscii(6);
		if (!signature.StartsWith("GIF", StringComparison.Ordinal))
		{
			return null;
		}

		int canvasWidth = reader.ReadUInt16();
		int canvasHeight = reader.ReadUInt16();
		if (canvasWidth <= 0 || canvasHeight <= 0 || canvasWidth > 4096 || canvasHeight > 4096)
		{
			return null;
		}

		int packed = reader.ReadByte();
		int backgroundIndex = reader.ReadByte();
		reader.ReadByte(); // Pixel aspect ratio.

		GifColor[]? globalColorTable = null;
		if ((packed & 0x80) != 0)
		{
			globalColorTable = reader.ReadColorTable(1 << ((packed & 0x07) + 1));
		}

		byte[] background = ResolveBackgroundColor(globalColorTable, backgroundIndex);
		byte[] canvas = new byte[checked(canvasWidth * canvasHeight * 4)];
		FillRect(canvas, canvasWidth, canvasHeight, 0, 0, canvasWidth, canvasHeight, background);

		List<GifFrame> frames = new List<GifFrame>();
		GraphicControl control = new GraphicControl();
		bool truncated = false;

		while (!reader.EndOfData)
		{
			int introducer = reader.ReadByte();
			if (introducer < 0 || introducer == 0x3B)
			{
				break;
			}

			if (introducer == 0x21)
			{
				ReadExtension(reader, control);
				continue;
			}

			if (introducer != 0x2C)
			{
				break;
			}

			int left = reader.ReadUInt16();
			int top = reader.ReadUInt16();
			int width = reader.ReadUInt16();
			int height = reader.ReadUInt16();
			int imagePacked = reader.ReadByte();
			if (width <= 0 || height <= 0)
			{
				control = new GraphicControl();
				SkipImageData(reader);
				continue;
			}

			bool interlaced = (imagePacked & 0x40) != 0;
			GifColor[]? colorTable = (imagePacked & 0x80) != 0
				? reader.ReadColorTable(1 << ((imagePacked & 0x07) + 1))
				: globalColorTable;
			if (colorTable == null || colorTable.Length == 0)
			{
				control = new GraphicControl();
				SkipImageData(reader);
				continue;
			}

			int lzwMinimumCodeSize = reader.ReadByte();
			byte[] imageData = reader.ReadSubBlocks();
			List<int> indices = DecodeLzw(imageData, lzwMinimumCodeSize, width * height);
			if (indices.Count == 0)
			{
				control = new GraphicControl();
				continue;
			}

			byte[]? restoreRegion = control.DisposalMethod == 3
				? CopyRegion(canvas, canvasWidth, canvasHeight, left, top, width, height)
				: null;

			DrawFrame(canvas, canvasWidth, canvasHeight, left, top, width, height, interlaced, colorTable, control.TransparentIndex, indices);
			frames.Add(new GifFrame((byte[])canvas.Clone(), ResolveDelaySeconds(control.DelayHundredths)));
			if (frames.Count >= maxFrames)
			{
				truncated = !reader.EndOfData;
				break;
			}

			ApplyDisposal(canvas, canvasWidth, canvasHeight, left, top, width, height, control.DisposalMethod, background, restoreRegion);
			control = new GraphicControl();
		}

		return frames.Count == 0 ? null : new GifDecodeResult(canvasWidth, canvasHeight, frames, truncated);
	}

	private static void ReadExtension(GifReader reader, GraphicControl control)
	{
		int label = reader.ReadByte();
		if (label == 0xF9)
		{
			int blockSize = reader.ReadByte();
			if (blockSize == 4)
			{
				int packed = reader.ReadByte();
				control.DisposalMethod = (packed >> 2) & 0x07;
				control.DelayHundredths = reader.ReadUInt16();
				int transparentIndex = reader.ReadByte();
				control.TransparentIndex = (packed & 0x01) != 0 ? transparentIndex : null;
				reader.ReadByte(); // Terminator.
				return;
			}

			reader.SkipBytes(blockSize);
			reader.SkipSubBlocks();
			return;
		}

		reader.SkipSubBlocks();
	}

	private static void SkipImageData(GifReader reader)
	{
		reader.ReadByte();
		reader.SkipSubBlocks();
	}

	private static byte[] ResolveBackgroundColor(GifColor[]? colorTable, int backgroundIndex)
	{
		if (colorTable == null || backgroundIndex < 0 || backgroundIndex >= colorTable.Length)
		{
			return new byte[] { 0, 0, 0, 0 };
		}

		GifColor color = colorTable[backgroundIndex];
		return new byte[] { color.R, color.G, color.B, 255 };
	}

	private static float ResolveDelaySeconds(int delayHundredths)
	{
		return delayHundredths <= 1 ? 0.1f : Math.Clamp(delayHundredths / 100f, 0.02f, 10f);
	}

	private static void DrawFrame(
		byte[] canvas,
		int canvasWidth,
		int canvasHeight,
		int left,
		int top,
		int width,
		int height,
		bool interlaced,
		GifColor[] colorTable,
		int? transparentIndex,
		List<int> indices)
	{
		int[] rowMap = BuildRowMap(height, interlaced);
		int count = Math.Min(indices.Count, width * height);
		for (int i = 0; i < count; i++)
		{
			int colorIndex = indices[i];
			if (transparentIndex.HasValue && colorIndex == transparentIndex.Value)
			{
				continue;
			}
			if (colorIndex < 0 || colorIndex >= colorTable.Length)
			{
				continue;
			}

			int localX = i % width;
			int encodedY = i / width;
			int localY = encodedY >= 0 && encodedY < rowMap.Length ? rowMap[encodedY] : encodedY;
			int targetX = left + localX;
			int targetY = top + localY;
			if (targetX < 0 || targetY < 0 || targetX >= canvasWidth || targetY >= canvasHeight)
			{
				continue;
			}

			GifColor color = colorTable[colorIndex];
			int offset = ((targetY * canvasWidth) + targetX) * 4;
			canvas[offset] = color.R;
			canvas[offset + 1] = color.G;
			canvas[offset + 2] = color.B;
			canvas[offset + 3] = 255;
		}
	}

	private static int[] BuildRowMap(int height, bool interlaced)
	{
		int[] rows = new int[height];
		if (!interlaced)
		{
			for (int i = 0; i < height; i++)
			{
				rows[i] = i;
			}
			return rows;
		}

		int cursor = 0;
		int[] starts = { 0, 4, 2, 1 };
		int[] steps = { 8, 8, 4, 2 };
		for (int pass = 0; pass < starts.Length; pass++)
		{
			for (int y = starts[pass]; y < height && cursor < rows.Length; y += steps[pass])
			{
				rows[cursor++] = y;
			}
		}

		while (cursor < rows.Length)
		{
			rows[cursor] = cursor;
			cursor++;
		}
		return rows;
	}

	private static void ApplyDisposal(
		byte[] canvas,
		int canvasWidth,
		int canvasHeight,
		int left,
		int top,
		int width,
		int height,
		int disposalMethod,
		byte[] background,
		byte[]? restoreRegion)
	{
		if (disposalMethod == 2)
		{
			FillRect(canvas, canvasWidth, canvasHeight, left, top, width, height, background);
			return;
		}

		if (disposalMethod == 3 && restoreRegion != null)
		{
			RestoreRegion(canvas, canvasWidth, canvasHeight, left, top, width, height, restoreRegion);
		}
	}

	private static void FillRect(byte[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height, byte[] color)
	{
		int minX = Math.Clamp(left, 0, canvasWidth);
		int minY = Math.Clamp(top, 0, canvasHeight);
		int maxX = Math.Clamp(left + width, 0, canvasWidth);
		int maxY = Math.Clamp(top + height, 0, canvasHeight);

		for (int y = minY; y < maxY; y++)
		{
			int rowOffset = y * canvasWidth * 4;
			for (int x = minX; x < maxX; x++)
			{
				int offset = rowOffset + (x * 4);
				canvas[offset] = color[0];
				canvas[offset + 1] = color[1];
				canvas[offset + 2] = color[2];
				canvas[offset + 3] = color[3];
			}
		}
	}

	private static byte[]? CopyRegion(byte[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height)
	{
		int minX = Math.Clamp(left, 0, canvasWidth);
		int minY = Math.Clamp(top, 0, canvasHeight);
		int maxX = Math.Clamp(left + width, 0, canvasWidth);
		int maxY = Math.Clamp(top + height, 0, canvasHeight);
		int copyWidth = maxX - minX;
		int copyHeight = maxY - minY;
		if (copyWidth <= 0 || copyHeight <= 0)
		{
			return null;
		}

		byte[] copy = new byte[copyWidth * copyHeight * 4];
		for (int y = 0; y < copyHeight; y++)
		{
			Buffer.BlockCopy(canvas, ((minY + y) * canvasWidth + minX) * 4, copy, y * copyWidth * 4, copyWidth * 4);
		}
		return copy;
	}

	private static void RestoreRegion(byte[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height, byte[] restoreRegion)
	{
		int minX = Math.Clamp(left, 0, canvasWidth);
		int minY = Math.Clamp(top, 0, canvasHeight);
		int maxX = Math.Clamp(left + width, 0, canvasWidth);
		int maxY = Math.Clamp(top + height, 0, canvasHeight);
		int copyWidth = maxX - minX;
		int copyHeight = maxY - minY;
		if (copyWidth <= 0 || copyHeight <= 0 || restoreRegion.Length < copyWidth * copyHeight * 4)
		{
			return;
		}

		for (int y = 0; y < copyHeight; y++)
		{
			Buffer.BlockCopy(restoreRegion, y * copyWidth * 4, canvas, ((minY + y) * canvasWidth + minX) * 4, copyWidth * 4);
		}
	}

	private static List<int> DecodeLzw(byte[] data, int minimumCodeSize, int expectedPixels)
	{
		List<int> output = new List<int>(Math.Max(0, expectedPixels));
		if (data.Length == 0 || minimumCodeSize < 2 || minimumCodeSize > 8 || expectedPixels <= 0)
		{
			return output;
		}

		int clearCode = 1 << minimumCodeSize;
		int endCode = clearCode + 1;
		List<int[]> dictionary = new List<int[]>(4096);
		int codeSize = minimumCodeSize + 1;
		int nextCode = endCode + 1;
		ResetDictionary(dictionary, clearCode, endCode, minimumCodeSize, out codeSize, out nextCode);

		BitReader bits = new BitReader(data);
		int previousCode = -1;
		while (true)
		{
			int code = bits.ReadBits(codeSize);
			if (code < 0)
			{
				break;
			}

			if (code == clearCode)
			{
				ResetDictionary(dictionary, clearCode, endCode, minimumCodeSize, out codeSize, out nextCode);
				previousCode = -1;
				continue;
			}

			if (code == endCode)
			{
				break;
			}

			int[]? entry = null;
			if (code >= 0 && code < dictionary.Count)
			{
				entry = dictionary[code];
			}
			else if (code == nextCode && previousCode >= 0 && previousCode < dictionary.Count)
			{
				int[] previous = dictionary[previousCode];
				entry = AppendCode(previous, previous.Length > 0 ? previous[0] : 0);
			}

			if (entry == null || entry.Length == 0)
			{
				break;
			}

			output.AddRange(entry);
			if (output.Count >= expectedPixels)
			{
				break;
			}

			if (previousCode >= 0 && previousCode < dictionary.Count && nextCode < 4096)
			{
				int[] previous = dictionary[previousCode];
				dictionary.Add(AppendCode(previous, entry[0]));
				nextCode++;
				if (nextCode == (1 << codeSize) && codeSize < 12)
				{
					codeSize++;
				}
			}

			previousCode = code;
		}

		return output;
	}

	private static void ResetDictionary(List<int[]> dictionary, int clearCode, int endCode, int minimumCodeSize, out int codeSize, out int nextCode)
	{
		dictionary.Clear();
		for (int i = 0; i < clearCode; i++)
		{
			dictionary.Add(new[] { i });
		}
		dictionary.Add(Array.Empty<int>());
		dictionary.Add(Array.Empty<int>());
		codeSize = minimumCodeSize + 1;
		nextCode = endCode + 1;
	}

	private static int[] AppendCode(int[] source, int value)
	{
		int[] result = new int[source.Length + 1];
		Array.Copy(source, result, source.Length);
		result[^1] = value;
		return result;
	}

	private sealed class GifReader
	{
		private readonly byte[] _data;
		private int _offset;

		public GifReader(byte[] data)
		{
			_data = data;
		}

		public bool EndOfData => _offset >= _data.Length;

		public string ReadAscii(int count)
		{
			if (_offset + count > _data.Length)
			{
				count = Math.Max(0, _data.Length - _offset);
			}

			string value = System.Text.Encoding.ASCII.GetString(_data, _offset, count);
			_offset += count;
			return value;
		}

		public int ReadByte()
		{
			if (_offset >= _data.Length)
			{
				return -1;
			}

			return _data[_offset++];
		}

		public int ReadUInt16()
		{
			int low = ReadByte();
			int high = ReadByte();
			if (low < 0 || high < 0)
			{
				return 0;
			}
			return low | (high << 8);
		}

		public GifColor[] ReadColorTable(int count)
		{
			GifColor[] colors = new GifColor[Math.Max(0, count)];
			for (int i = 0; i < colors.Length; i++)
			{
				int r = ReadByte();
				int g = ReadByte();
				int b = ReadByte();
				if (r < 0 || g < 0 || b < 0)
				{
					return colors;
				}
				colors[i] = new GifColor((byte)r, (byte)g, (byte)b);
			}
			return colors;
		}

		public byte[] ReadSubBlocks()
		{
			List<byte> bytes = new List<byte>();
			while (!EndOfData)
			{
				int size = ReadByte();
				if (size <= 0)
				{
					break;
				}

				int copy = Math.Min(size, _data.Length - _offset);
				for (int i = 0; i < copy; i++)
				{
					bytes.Add(_data[_offset + i]);
				}
				_offset += copy;
				if (copy < size)
				{
					break;
				}
			}
			return bytes.ToArray();
		}

		public void SkipSubBlocks()
		{
			while (!EndOfData)
			{
				int size = ReadByte();
				if (size <= 0)
				{
					break;
				}
				_offset = Math.Min(_data.Length, _offset + size);
			}
		}

		public void SkipBytes(int count)
		{
			if (count <= 0)
			{
				return;
			}
			_offset = Math.Min(_data.Length, _offset + count);
		}
	}

	private sealed class BitReader
	{
		private readonly byte[] _data;
		private int _bitOffset;

		public BitReader(byte[] data)
		{
			_data = data;
		}

		public int ReadBits(int count)
		{
			if (count <= 0 || count > 12 || _bitOffset + count > _data.Length * 8)
			{
				return -1;
			}

			int value = 0;
			for (int i = 0; i < count; i++)
			{
				int byteIndex = (_bitOffset + i) / 8;
				int bitIndex = (_bitOffset + i) % 8;
				if ((_data[byteIndex] & (1 << bitIndex)) != 0)
				{
					value |= 1 << i;
				}
			}
			_bitOffset += count;
			return value;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
internal static class CardModel_get_PortraitPath_CustomArt_Patch
{
	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (CardEditorCustomPortraitLoader.TryGetPortraitPath(__instance, beta: false, out string portraitPath))
		{
			__result = portraitPath;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_BetaPortraitPath")]
internal static class CardModel_get_BetaPortraitPath_CustomArt_Patch
{
	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (CardEditorCustomPortraitLoader.TryGetPortraitPath(__instance, beta: true, out string portraitPath))
		{
			__result = portraitPath;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Portrait")]
internal static class CardModel_get_Portrait_CustomArt_Patch
{
	public static bool Prefix(CardModel __instance, ref Texture2D __result)
	{
		if (__instance?.Id == null)
		{
			return true;
		}

		// This can be inlined in some call-sites, so we also patch NCard.Reload. This patch remains as a best-effort.
		if (!CardEditorCustomPortraitLoader.TryGetPortrait(__instance, out Texture2D texture))
		{
			return true;
		}

		__result = texture;
		return false;
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_CustomPortrait_Patch
{
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private const string OriginalPositionMeta = "card_editor_original_portrait_position";
	private const string OriginalScaleMeta = "card_editor_original_portrait_scale";
	private const string OriginalPivotMeta = "card_editor_original_portrait_pivot";
	private const string TransformAppliedMeta = "card_editor_portrait_transform_applied";

	public static void Postfix(NCard __instance)
	{
		CardModel? model = __instance?.Model;
		if (model?.Id == null)
		{
			return;
		}

		bool hasCustomPortrait = CardEditorCustomPortraitLoader.TryGetPortrait(model, out Texture2D texture);
		bool hasTransform = CardEditorCustomPortraitLoader.TryGetPortraitTransform(model, out CardEditorCustomPortraitLoader.PortraitTransform transform);
		bool hasAnimation = CardEditorCustomPortraitLoader.TryGetPortraitAnimation(model, out CardEditorGifAnimation animation);

		try
		{
			TextureRect portrait = PortraitRef(__instance);
			if (portrait != null)
			{
				if (hasCustomPortrait)
				{
					portrait.Texture = hasAnimation ? animation.FirstFrame : texture;
				}
				CardEditorGifPortraitAnimator.Sync(portrait, hasAnimation ? animation : null);
				SyncPortraitTransform(portrait, hasTransform && portrait.Visible, transform);
			}

			TextureRect ancientPortrait = AncientPortraitRef(__instance);
			if (ancientPortrait != null)
			{
				if (hasCustomPortrait)
				{
					ancientPortrait.Texture = hasAnimation ? animation.FirstFrame : texture;
				}
				CardEditorGifPortraitAnimator.Sync(ancientPortrait, hasAnimation ? animation : null);
				SyncPortraitTransform(ancientPortrait, hasTransform && ancientPortrait.Visible, transform);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed applying custom portrait to NCard: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void SyncPortraitTransform(TextureRect portrait, bool hasTransform, CardEditorCustomPortraitLoader.PortraitTransform transform)
	{
		if (hasTransform)
		{
			ApplyPortraitTransform(portrait, transform);
			return;
		}

		ResetPortraitTransformIfNeeded(portrait);
	}

	private static void ApplyPortraitTransform(TextureRect portrait, CardEditorCustomPortraitLoader.PortraitTransform transform)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		CacheOriginalPortraitLayout(portrait);
		Vector2 originalPosition = portrait.GetMeta(OriginalPositionMeta).AsVector2();
		Vector2 originalScale = portrait.GetMeta(OriginalScaleMeta).AsVector2();
		Vector2 originalPivot = portrait.GetMeta(OriginalPivotMeta).AsVector2();

		portrait.Position = originalPosition;
		portrait.Scale = originalScale;
		portrait.PivotOffset = originalPivot;

		if (transform.IsDefault)
		{
			return;
		}

		portrait.Position = originalPosition + new Vector2(transform.OffsetX, transform.OffsetY);
		portrait.Scale = originalScale * transform.Zoom;
		portrait.SetMeta(TransformAppliedMeta, true);
	}

	private static void ResetPortraitTransformIfNeeded(TextureRect portrait)
	{
		if (portrait == null
			|| !GodotObject.IsInstanceValid(portrait)
			|| !portrait.HasMeta(TransformAppliedMeta)
			|| !portrait.GetMeta(TransformAppliedMeta).AsBool())
		{
			return;
		}

		if (portrait.HasMeta(OriginalPositionMeta))
		{
			portrait.Position = portrait.GetMeta(OriginalPositionMeta).AsVector2();
		}
		if (portrait.HasMeta(OriginalScaleMeta))
		{
			portrait.Scale = portrait.GetMeta(OriginalScaleMeta).AsVector2();
		}
		if (portrait.HasMeta(OriginalPivotMeta))
		{
			portrait.PivotOffset = portrait.GetMeta(OriginalPivotMeta).AsVector2();
		}
		portrait.SetMeta(TransformAppliedMeta, false);
	}

	private static void CacheOriginalPortraitLayout(TextureRect portrait)
	{
		if (!portrait.HasMeta(OriginalPositionMeta))
		{
			portrait.SetMeta(OriginalPositionMeta, portrait.Position);
		}
		if (!portrait.HasMeta(OriginalScaleMeta))
		{
			portrait.SetMeta(OriginalScaleMeta, portrait.Scale);
		}
		if (!portrait.HasMeta(OriginalPivotMeta))
		{
			portrait.SetMeta(OriginalPivotMeta, portrait.PivotOffset);
		}
	}
}
