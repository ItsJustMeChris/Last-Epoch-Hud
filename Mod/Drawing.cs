using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Color = UnityEngine.Color;
using MelonLoader;

namespace Mod
{
	internal class Drawing
	{
		public static Texture2D? lineTex = new Texture2D(1, 1);
		public static GUIStyle? StringStyle { get; private set; }
		private static int s_hudBackgroundAttempts;
		private static Texture2D? s_hudBackgroundTexture;

		// private static int debugLastFrame = -1;
		// private static int debugLogsThisFrame = 0;
		// private const int DebugMaxLogsPerFrame = 8;

		public static readonly Color BloodOrange = new Color(1.00f, 0.50f, 0.00f, 1f);
		public static readonly Color MagicLightBlue = new Color(0.55f, 0.8f, 1f, 1f);
		private static readonly Vector2 LabelScreenPadding = new Vector2(25f, 25f);
		private const float OffScreenLabelSpacing = 3f;
		private const int OffScreenLabelMaxPlacementAttempts = 24;
		private static readonly Color EmphasizedOutlineColor = new Color(0f, 0f, 0f, 0.95f);

		private static readonly GUIContent s_contentA = new GUIContent();
		private static readonly GUIContent s_contentB = new GUIContent();
		private static readonly List<Rect> s_offScreenLabelRects = new List<Rect>(128);
		private static readonly Dictionary<Vector3, Vector3> s_screenPointCache = new Dictionary<Vector3, Vector3>(512);
		private static Camera? s_passCamera;
		private static int s_passScreenWidth;
		private static int s_passScreenHeight;
		private static bool s_drawingPassActive;

		public static void BeginDrawingPass()
		{
			s_passCamera = Camera.main;
			s_passScreenWidth = Screen.width;
			s_passScreenHeight = Screen.height;
			s_screenPointCache.Clear();
			s_drawingPassActive = true;
			BeginLabelPlacementPass();
		}

		public static void EndDrawingPass()
		{
			s_drawingPassActive = false;
			s_passCamera = null;
			s_screenPointCache.Clear();
		}

		private static Camera? GetCamera()
		{
			return s_drawingPassActive ? s_passCamera : Camera.main;
		}

		private static int CurrentScreenWidth => s_drawingPassActive ? s_passScreenWidth : Screen.width;
		private static int CurrentScreenHeight => s_drawingPassActive ? s_passScreenHeight : Screen.height;

		private static Vector3 GetScreenPoint(Vector3 worldPosition, Camera camera)
		{
			if (s_drawingPassActive && s_screenPointCache.TryGetValue(worldPosition, out var cached))
				return cached;

			var screenPoint = camera.WorldToScreenPoint(worldPosition);
			if (s_drawingPassActive && s_screenPointCache.Count < 2048)
				s_screenPointCache[worldPosition] = screenPoint;

			return screenPoint;
		}

		public static void BeginLabelPlacementPass()
		{
			s_offScreenLabelRects.Clear();
		}

		static Vector2 ClampToScreen(Vector3 vecIn, Vector3 padding)
		{
			if (vecIn.z < 0)
			{
				vecIn *= -1;
			}

			return new Vector2(
				  Mathf.Clamp(vecIn.x, padding.x, CurrentScreenWidth - padding.x),
				  Mathf.Clamp(vecIn.y, padding.y, CurrentScreenHeight - padding.y)
							 );
		}

		// Ensures a rectangle defined by its upper-left corner and size stays within the screen bounds with padding
		static Vector2 ClampRectToScreen(Vector2 upperLeft, Vector2 size, Vector2 padding)
		{
			float clampedX = Mathf.Clamp(upperLeft.x, padding.x, CurrentScreenWidth - size.x - padding.x);
			float clampedY = Mathf.Clamp(upperLeft.y, padding.y, CurrentScreenHeight - size.y - padding.y);
			return new Vector2(clampedX, clampedY);
		}

		static bool TryGetLabelPlacement(Vector3 worldPosition, GUIContent content, GUIStyle style, bool centered, out Vector2 upperLeft, out Vector2 size)
		{
			upperLeft = Vector2.zero;
			size = Vector2.zero;

			var cam = GetCamera();
			if (cam == null) return false;

			Vector3 screen = GetScreenPoint(worldPosition, cam);
			bool isBehindCamera = screen.z < 0;
			if (isBehindCamera) screen *= -1; // mirror behind-camera points like ClampToScreen
			bool isOffScreen = isBehindCamera || IsOffScreen(screen);
			bool preferVerticalStacking = ShouldStackVertically(screen);
			screen.y = CurrentScreenHeight - screen.y;

			size = style.CalcSize(content);
			Vector2 desiredUpperLeft = centered
				? new Vector2(screen.x, screen.y) - size / 2f
				: new Vector2(screen.x, screen.y);
			upperLeft = ClampRectToScreen(desiredUpperLeft, size, LabelScreenPadding);
			if (isOffScreen)
			{
				return TryReserveOffScreenLabelPlacement(upperLeft, size, preferVerticalStacking, out upperLeft);
			}

			return true;
		}

		static bool IsOffScreen(Vector3 screen)
		{
			return screen.x < 0f
				|| screen.x > CurrentScreenWidth
				|| screen.y < 0f
				|| screen.y > CurrentScreenHeight;
		}

		static bool ShouldStackVertically(Vector3 screen)
		{
			float overflowX = 0f;
			if (screen.x < 0f) overflowX = -screen.x;
			else if (screen.x > CurrentScreenWidth) overflowX = screen.x - CurrentScreenWidth;

			float overflowY = 0f;
			if (screen.y < 0f) overflowY = -screen.y;
			else if (screen.y > CurrentScreenHeight) overflowY = screen.y - CurrentScreenHeight;

			return overflowX >= overflowY;
		}

		static bool TryReserveOffScreenLabelPlacement(Vector2 preferredUpperLeft, Vector2 size, bool preferVerticalStacking, out Vector2 upperLeft)
		{
			upperLeft = preferredUpperLeft;
			if (TryReserveOffScreenRect(new Rect(preferredUpperLeft, size)))
			{
				return true;
			}

			float step = (preferVerticalStacking ? size.y : size.x) + OffScreenLabelSpacing;
			for (int attempt = 1; attempt <= OffScreenLabelMaxPlacementAttempts; attempt++)
			{
				int slot = (attempt + 1) / 2;
				float offset = slot * step * ((attempt & 1) == 1 ? 1f : -1f);
				Vector2 candidate = preferVerticalStacking
					? new Vector2(preferredUpperLeft.x, preferredUpperLeft.y + offset)
					: new Vector2(preferredUpperLeft.x + offset, preferredUpperLeft.y);

				candidate = ClampRectToScreen(candidate, size, LabelScreenPadding);
				if (TryReserveOffScreenRect(new Rect(candidate, size)))
				{
					upperLeft = candidate;
					return true;
				}
			}

			return TryReserveFirstAvailableOffScreenSlot(preferredUpperLeft, size, preferVerticalStacking, out upperLeft);
		}

		static bool TryReserveFirstAvailableOffScreenSlot(Vector2 preferredUpperLeft, Vector2 size, bool preferVerticalStacking, out Vector2 upperLeft)
		{
			upperLeft = preferredUpperLeft;
			float step = (preferVerticalStacking ? size.y : size.x) + OffScreenLabelSpacing;
			if (step <= 0f)
			{
				return false;
			}

			if (preferVerticalStacking)
			{
				float maxY = CurrentScreenHeight - size.y - LabelScreenPadding.y;
				for (float y = LabelScreenPadding.y; y <= maxY; y += step)
				{
					var candidate = new Vector2(preferredUpperLeft.x, y);
					if (TryReserveOffScreenRect(new Rect(candidate, size)))
					{
						upperLeft = candidate;
						return true;
					}
				}
			}
			else
			{
				float maxX = CurrentScreenWidth - size.x - LabelScreenPadding.x;
				for (float x = LabelScreenPadding.x; x <= maxX; x += step)
				{
					var candidate = new Vector2(x, preferredUpperLeft.y);
					if (TryReserveOffScreenRect(new Rect(candidate, size)))
					{
						upperLeft = candidate;
						return true;
					}
				}
			}

			return false;
		}

		static bool TryReserveOffScreenRect(Rect rect)
		{
			for (int i = 0; i < s_offScreenLabelRects.Count; i++)
			{
				if (RectsOverlap(rect, s_offScreenLabelRects[i], OffScreenLabelSpacing))
				{
					return false;
				}
			}

			s_offScreenLabelRects.Add(rect);
			return true;
		}

		static bool RectsOverlap(Rect a, Rect b, float padding)
		{
			return a.xMin - padding < b.xMax
				&& a.xMax + padding > b.xMin
				&& a.yMin - padding < b.yMax
				&& a.yMax + padding > b.yMin;
		}

		public static void SetupGuiStyle()
		{
			// Must be called from within OnGUI
			if (StringStyle == null)
			{
				StringStyle = new GUIStyle(GUI.skin.label);
				StringStyle.clipping = TextClipping.Overflow;
				StringStyle.wordWrap = false;
				// Ensure default text color is neutral; specific draws will override
				StringStyle.normal.textColor = Color.white;
			}

			EnsureHudBackground();
		}

		public static void DrawHudBackground(Rect rect, float opacity = 0.6f)
		{
			EnsureHudBackground();
			if (s_hudBackgroundTexture == null)
				return;

			Color previousColor = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, opacity);
			GUI.DrawTexture(rect, s_hudBackgroundTexture, ScaleMode.ScaleAndCrop, true);
			GUI.color = previousColor;
		}

		private static void EnsureHudBackground()
		{
			if (s_hudBackgroundTexture != null || s_hudBackgroundAttempts >= 5)
				return;

			s_hudBackgroundAttempts++;
			try
			{
				string userDataDirectory = Path.GetDirectoryName(SettingsConfig.GetStandaloneConfigPath())!;
				string overridePath = Path.Combine(userDataDirectory, "LEHudBackground.png");
				byte[]? imageBytes = File.Exists(overridePath)
					? File.ReadAllBytes(overridePath)
					: ReadEmbeddedHudBackground();
				if (imageBytes == null || imageBytes.Length == 0)
				{
					MelonLogger.Warning("[LEHud] HUD background image not found.");
					return;
				}

				var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (!ImageConversion.LoadImage(source, imageBytes))
				{
					UnityEngine.Object.Destroy(source);
					MelonLogger.Warning("[LEHud] HUD background image failed to decode.");
					return;
				}

				s_hudBackgroundTexture = CreateSoftHudBackground(source);
				UnityEngine.Object.Destroy(source);
				MelonLogger.Msg("[LEHud] HUD background loaded.");
			}
			catch (System.Exception exception)
			{
				MelonLogger.Warning($"[LEHud] HUD background could not be loaded: {exception.Message}");
			}
		}

		private static byte[]? ReadEmbeddedHudBackground()
		{
			using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mod.LEHudBackground.png");
			if (resource == null)
				return null;

			using var memory = new MemoryStream();
			resource.CopyTo(memory);
			return memory.ToArray();
		}

		private static Texture2D CreateSoftHudBackground(Texture2D source)
		{
			int width = Mathf.Max(1, source.width / 2);
			int height = Mathf.Max(1, source.height / 2);
			Color[] sourcePixels = source.GetPixels();
			Color[] reducedPixels = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				int startY = y * source.height / height;
				int endY = Mathf.Max(startY + 1, (y + 1) * source.height / height);
				for (int x = 0; x < width; x++)
				{
					int startX = x * source.width / width;
					int endX = Mathf.Max(startX + 1, (x + 1) * source.width / width);
					Color sum = Color.clear;
					int count = 0;
					for (int sampleY = startY; sampleY < endY; sampleY++)
					{
						for (int sampleX = startX; sampleX < endX; sampleX++)
						{
							sum += sourcePixels[sampleY * source.width + sampleX];
							count++;
						}
					}
					reducedPixels[y * width + x] = sum / count;
				}
			}

			var softened = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave
			};
			BoxBlur(reducedPixels, width, height, 3);
			softened.SetPixels(reducedPixels);
			softened.Apply();
			return softened;
		}

		private static void BoxBlur(Color[] pixels, int width, int height, int radius)
		{
			Color[] temp = new Color[pixels.Length];
			float norm = 1f / (radius * 2 + 1);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Color sum = Color.clear;
					for (int k = -radius; k <= radius; k++)
						sum += pixels[y * width + Mathf.Clamp(x + k, 0, width - 1)];
					temp[y * width + x] = sum * norm;
				}
			}
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Color sum = Color.clear;
					for (int k = -radius; k <= radius; k++)
						sum += temp[Mathf.Clamp(y + k, 0, height - 1) * width + x];
					pixels[y * width + x] = sum * norm;
				}
			}
		}

		private static readonly Dictionary<GUIStyle, GUIStyle> s_outlineStyles = new Dictionary<GUIStyle, GUIStyle>();
		private static GUIStyle? s_layoutLabelStyle;

		// Black outline for light text, white outline for dark text.
		public static Color OutlineColorFor(Color textColor)
		{
			float luminance = textColor.r * 0.299f + textColor.g * 0.587f + textColor.b * 0.114f;
			return luminance > 0.5f ? Color.black : Color.white;
		}

		public static void OutlinedLabel(string text, params GUILayoutOption[] options)
		{
			if (s_layoutLabelStyle == null)
			{
				s_layoutLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
			}
			var content = new GUIContent(text);
			Rect rect = GUILayoutUtility.GetRect(content, s_layoutLabelStyle, options);
			OutlinedLabel(rect, content, s_layoutLabelStyle);
		}

		public static void OutlinedLabel(Rect rect, string text, GUIStyle style)
		{
			OutlinedLabel(rect, new GUIContent(text), style);
		}

		public static void OutlinedLabel(Rect rect, GUIContent content, GUIStyle style)
		{
			DrawOutlinedText(rect, content, style, style.normal.textColor);
		}

		private static void DrawOutlinedText(Rect rect, GUIContent content, GUIStyle style, Color styleTextColor)
		{
			if (Event.current.type != EventType.Repaint)
				return;

			if (!s_outlineStyles.TryGetValue(style, out GUIStyle? text) || text == null)
			{
				text = new GUIStyle(style);
				s_outlineStyles[style] = text;
			}
			text.fontSize = style.fontSize;
			text.fontStyle = style.fontStyle;
			text.alignment = style.alignment;
			text.wordWrap = style.wordWrap;
			text.clipping = style.clipping;
			text.font = style.font;
			text.padding = style.padding;
			text.normal.background = null;

			Color previous = GUI.color;
			Color effective = new Color(styleTextColor.r * previous.r, styleTextColor.g * previous.g, styleTextColor.b * previous.b, 1f);
			Color outline = OutlineColorFor(effective);
			float offset = text.fontSize >= 16 ? 2f : 1f;

			text.normal.textColor = outline;
			GUI.color = new Color(1f, 1f, 1f, previous.a);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0)
						continue;
					if (offset < 2f && (dx == 0 || dy == 0))
						continue;
					GUI.Label(new Rect(rect.x + dx * offset, rect.y + dy * offset, rect.width, rect.height), content, text);
				}
			}

			text.normal.textColor = styleTextColor;
			GUI.color = previous;
			GUI.Label(rect, content, text);
		}

		// Button/toggle whose caption is drawn separately so it can be outlined; the caption shrinks to fit.
		public static bool OutlinedButton(Rect rect, string caption, GUIStyle style, FontStyle? fontStyle = null)
		{
			bool clicked = GUI.Button(rect, GUIContent.none, style);
			DrawButtonCaption(rect, caption, style, fontStyle);
			return clicked;
		}

		public static void DrawButtonCaption(Rect rect, string caption, GUIStyle style, FontStyle? fontStyle = null)
		{
			if (Event.current.type != EventType.Repaint)
				return;

			bool hover = rect.Contains(Event.current.mousePosition);
			Color color = hover ? style.hover.textColor : style.normal.textColor;
			GUIStyle captionStyle = GetCaptionStyle(style);
			captionStyle.fontStyle = fontStyle ?? style.fontStyle;
			captionStyle.fontSize = style.fontSize;
			captionStyle.alignment = style.alignment == TextAnchor.UpperLeft ? TextAnchor.MiddleCenter : style.alignment;
			captionStyle.padding = new RectOffset(2, 2, 0, 0);
			captionStyle.wordWrap = false;
			captionStyle.clipping = TextClipping.Overflow;

			var content = new GUIContent(caption);
			while (captionStyle.fontSize > 8 && captionStyle.CalcSize(content).x > rect.width - 6f)
				captionStyle.fontSize--;

			DrawOutlinedText(rect, content, captionStyle, color);
		}

		private static readonly Dictionary<GUIStyle, GUIStyle> s_captionStyles = new Dictionary<GUIStyle, GUIStyle>();

		private static GUIStyle GetCaptionStyle(GUIStyle source)
		{
			if (!s_captionStyles.TryGetValue(source, out GUIStyle? caption) || caption == null)
			{
				caption = new GUIStyle(GUI.skin.label);
				s_captionStyles[source] = caption;
			}
			caption.font = source.font;
			return caption;
		}
		private static Texture2D? s_moveIconTexture;

		private static Texture2D GetMoveIconTexture()
		{
			if (s_moveIconTexture != null)
				return s_moveIconTexture;

			const int size = 64;
			const int samples = 3;
			var pixels = new Color[size * size];
			// Four arrows (up, down, left, right) from the centre: shaft + two head strokes each.
			var segments = new List<(Vector2 a, Vector2 b)>();
			Vector2 c = new Vector2(32f, 32f);
			Vector2[] dirs = { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };
			foreach (Vector2 d in dirs)
			{
				Vector2 perp = new Vector2(-d.y, d.x);
				Vector2 tip = c + d * 26f;
				segments.Add((c + d * 8f, tip));
				segments.Add((tip, tip - d * 9f + perp * 9f));
				segments.Add((tip, tip - d * 9f - perp * 9f));
			}
			const float halfWidth = 2.6f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float coverage = 0f;
					for (int sy = 0; sy < samples; sy++)
					{
						for (int sx = 0; sx < samples; sx++)
						{
							Vector2 pt = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
							foreach (var (a, b) in segments)
							{
								Vector2 ab = b - a;
								float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / ab.sqrMagnitude);
								if ((pt - (a + ab * t)).magnitude <= halfWidth)
								{
									coverage += 1f;
									break;
								}
							}
						}
					}
					pixels[y * size + x] = new Color(1f, 1f, 1f, coverage / (samples * samples));
				}
			}

			s_moveIconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave
			};
			s_moveIconTexture.SetPixels(pixels);
			s_moveIconTexture.Apply();
			return s_moveIconTexture;
		}

		// Four-arrow move handle with a dark outline so it reads on any background.
		public static void DrawMoveIcon(Rect rect)
		{
			if (Event.current.type != EventType.Repaint)
				return;

			Texture2D icon = GetMoveIconTexture();
			Color previous = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, previous.a);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx != 0 || dy != 0)
						GUI.DrawTexture(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), icon);
				}
			}
			GUI.color = new Color(0.93f, 0.9f, 0.83f, previous.a);
			GUI.DrawTexture(rect, icon);
			GUI.color = previous;
		}

		public static void DrawHeading(string text)
		{
			GUIStyle style = global::Mod.Cheats.CooldownTracker.Theme.Button(12, selected: false);
			Rect rect = GUILayoutUtility.GetRect(GUIContent.none, style, GUILayout.Height(26f), GUILayout.ExpandWidth(true));
			if (Event.current.type == EventType.Repaint)
				style.Draw(rect, GUIContent.none, false, false, false, false);
			DrawButtonCaption(rect, text, style, FontStyle.Bold);
		}
		public static void DrawString(Vector3 worldPosition, string label, bool centered = true)
		{
			if (!Settings.showESPLabels)
				return;

			s_contentA.text = label;
			var style = StringStyle ?? new GUIStyle();
			if (!TryGetLabelPlacement(worldPosition, s_contentA, style, centered, out var clampedUpperLeft, out var size))
			{
				return;
			}

			GUI.Label(new Rect(clampedUpperLeft, size), s_contentA, style);
		}

		public static void DrawString(Vector3 worldPosition, string label, Color color, bool centered = true)
		{
			// Respect label visibility toggle
			if (!Settings.showESPLabels)
				return;

			var style = StringStyle ?? new GUIStyle();
			// Backup colors to avoid leaking state across GUI calls
			var backupTextColor = style.normal.textColor;
			var prevContentColor = GUI.contentColor;
			var prevGuiColor = GUI.color;

			// Neutralize any global GUI tint; rely solely on style text color
			GUI.contentColor = Color.white;
			GUI.color = Color.white;

			// Apply the requested color for this draw only via style
			style.normal.textColor = color;

			DrawString(worldPosition, label, centered);

			// Restore previous colors
			style.normal.textColor = backupTextColor;
			GUI.contentColor = prevContentColor;
			GUI.color = prevGuiColor;
		}

		public static void DrawStringEmphasized(Vector3 worldPosition, string label, Color color, bool centered = true)
		{
			if (!Settings.showESPLabels)
				return;

			var style = StringStyle ?? new GUIStyle();
			s_contentA.text = label;
			if (!TryGetLabelPlacement(worldPosition, s_contentA, style, centered, out var upperLeft, out var size))
			{
				return;
			}

			var backupTextColor = style.normal.textColor;
			var backupFontStyle = style.fontStyle;
			var prevContentColor = GUI.contentColor;
			var prevGuiColor = GUI.color;

			GUI.contentColor = Color.white;
			GUI.color = Color.white;
			style.fontStyle = FontStyle.Bold;

			style.normal.textColor = EmphasizedOutlineColor;
			GUI.Label(new Rect(upperLeft.x - 1f, upperLeft.y - 1f, size.x, size.y), s_contentA, style);
			GUI.Label(new Rect(upperLeft.x + 1f, upperLeft.y - 1f, size.x, size.y), s_contentA, style);
			GUI.Label(new Rect(upperLeft.x - 1f, upperLeft.y + 1f, size.x, size.y), s_contentA, style);
			GUI.Label(new Rect(upperLeft.x + 1f, upperLeft.y + 1f, size.x, size.y), s_contentA, style);

			style.normal.textColor = color;
			GUI.Label(new Rect(upperLeft, size), s_contentA, style);

			style.normal.textColor = backupTextColor;
			style.fontStyle = backupFontStyle;
			GUI.contentColor = prevContentColor;
			GUI.color = prevGuiColor;
		}

		public static void DrawCustomString(Vector3 worldPosition, string label, Color color, bool centered = true)
		{
			if (!Settings.showESPLabels)
				return;

			var cam = GetCamera();
			if (cam == null) return;
			Vector3 screen = GetScreenPoint(worldPosition, cam);
			if (screen.z < 0) screen *= -1;
			screen.y = CurrentScreenHeight - screen.y;

			string firstPart = label.Length > 3 ? label.Substring(0, 3) : label;
			string secondPart = label.Length > 3 ? label.Substring(3) : string.Empty;

			s_contentA.text = firstPart;
			s_contentB.text = secondPart;

			var style = StringStyle ?? new GUIStyle();
			var firstSize = style.CalcSize(s_contentA);
			var secondSize = style.CalcSize(s_contentB);

			var totalSize = new Vector2(firstSize.x + secondSize.x, Mathf.Max(firstSize.y, secondSize.y));

			Vector2 desiredUpperLeft = centered
				? new Vector2(screen.x, screen.y) - totalSize / 2f
				: new Vector2(screen.x, screen.y);
			Vector2 upperLeft = ClampRectToScreen(desiredUpperLeft, totalSize, LabelScreenPadding);

			Color prevColor = GUI.color;

			GUI.color = color;
			GUI.Label(new Rect(upperLeft, firstSize), s_contentA, style);

			GUI.color = prevColor;
			GUI.Label(new Rect(new Vector2(upperLeft.x + firstSize.x, upperLeft.y), secondSize), s_contentB, style);

			GUI.color = prevColor;
		}

		public static void DrawLine(Vector3 worldA, Vector3 worldB, Color color, float width)
		{
			// Respect line visibility toggle
			if (!Settings.showESPLines)
				return;

			if (lineTex == null)
			{
				lineTex = new Texture2D(1, 1);
				lineTex.SetPixel(0, 0, Color.white);
				lineTex.Apply();
			}

			Color prevColor = GUI.color;
			Matrix4x4 prevMatrix = GUI.matrix;

			var cam = GetCamera();
			if (cam == null) return;
			Vector3 screenA = GetScreenPoint(worldA, cam);
			Vector3 screenB = GetScreenPoint(worldB, cam);

			screenA.y = CurrentScreenHeight - screenA.y;
			screenB.y = CurrentScreenHeight - screenB.y;


			// Clamp points to screen with padding
			Vector2 pointA = ClampToScreen(screenA, new Vector2(25, 25));
			Vector2 pointB = ClampToScreen(screenB, new Vector2(25, 25));

			// Calculate angle and magnitude
			float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * 180f / Mathf.PI;
			float magnitude = (pointB - pointA).magnitude;

			// Apply color
			GUI.color = color;

			// Create matrix for rotation
			Matrix4x4 matrix = Matrix4x4.TRS(pointA, Quaternion.Euler(0, 0, angle), Vector3.one);

			// Apply the matrix
			GUI.matrix = matrix;

			// Draw the line
			GUI.DrawTexture(new Rect(0, -width / 2, magnitude, width), lineTex);

			// Revert GUI color and matrix to previous state
			GUI.color = prevColor;
			GUI.matrix = prevMatrix;
		}

		public static Color ItemRarityToColor(string rarity)
		{
			var color = Color.white;

			if (rarity.Contains("Magic"))
			{
				color = Color.blue;
			}
			else if (rarity.Contains("Common"))
			{
				color = Color.white;
			}
			else if (rarity.Contains("Unique"))
			{
				color = Color.red;
			}
			else if (rarity.Contains("Legendary"))
			{
				color = new Color(1.0f, 0.5f, 0.0f);
			}
			else if (rarity.Contains("Rare"))
			{
				color = Color.yellow;
			}
			else if (rarity.Contains("Set"))
			{
				color = Color.green;
			}
			else if (rarity.Contains("Exalted"))
			{
				color = new Color(0.5f, 0, 0.5f);
			}

			return color;
		}

		public static Color AlignmentToColor(string alignment)
		{
			var color = Color.white;
			switch (alignment)
			{
				case "Good":
					color = Color.green;
					break;
				case "Evil":
					color = Color.red;
					break;
				case "Barrel":
					color = Color.yellow;
					break;
				case "HostileNeutral":
					color = Color.blue;
					break;
				case "FriendlyNeutral":
					color = Color.cyan;
					break;
				case "SummonedCorpse":
					color = Color.magenta;
					break;
			}

			return color;
		}

		public static void Initialize()
		{
			if (lineTex != null)
			{
				lineTex.SetPixel(0, 0, Color.white);
				lineTex.Apply();
			}
		}

		public static void Cleanup()
		{
			if (lineTex != null)
			{
				UnityEngine.Object.Destroy(lineTex);
				lineTex = null;
			}
			if (s_hudBackgroundTexture != null)
			{
				UnityEngine.Object.Destroy(s_hudBackgroundTexture);
				s_hudBackgroundTexture = null;
			}
			if (s_moveIconTexture != null)
			{
				UnityEngine.Object.Destroy(s_moveIconTexture);
				s_moveIconTexture = null;
			}
			s_hudBackgroundAttempts = 0;
			EndDrawingPass();
			// Do not touch GUI.skin here; class can now be safely touched outside OnGUI
		}
	}
}
