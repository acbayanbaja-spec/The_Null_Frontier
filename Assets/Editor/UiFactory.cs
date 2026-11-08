using System.IO;
using System.Reflection;
using NullFrontier.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Script-built UI: generated sprites, widgets and the menu screens.
    /// The project ships no UI art and TextMeshPro essentials are not imported, so
    /// every pixel here is either a procedurally written PNG or a built-in font.
    /// Sprites are saved as assets rather than built in memory, otherwise the scene
    /// references would not survive a rebuild or a player build.
    /// </summary>
    public static class UiFactory
    {
        public const string SpriteDir = "Assets/Art/UI";

        // ------------------------------------------------------------------ palette
        public static readonly Color Ink = new Color(0.035f, 0.045f, 0.075f, 0.94f);
        public static readonly Color Panel = new Color(0.09f, 0.11f, 0.16f, 0.95f);
        public static readonly Color PanelSoft = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color Track = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color Accent = new Color(0.10f, 0.78f, 0.92f);
        public static readonly Color Accent2 = new Color(0.95f, 0.28f, 0.62f);
        public static readonly Color Body = new Color(1f, 1f, 1f, 0.88f);
        public static readonly Color Subtle = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Health = new Color(0.22f, 0.95f, 0.55f);
        public static readonly Color Shield = new Color(0.28f, 0.72f, 1f);
        public static readonly Color Stamina = new Color(1f, 0.78f, 0.22f);
        public static readonly Color Danger = new Color(0.88f, 0.18f, 0.24f);

        private static Sprite _rect, _circle, _ring, _vignette, _glow, _streak;
        private static Sprite _atmosphere;
        private static Sprite _plate, _rimCut, _pill, _rimRound, _hex;
        private static Sprite _tab, _rimTab, _tip, _rimTip, _ticks;
        private static readonly Sprite[] _icons = new Sprite[20];
        private static bool _spritesReady;
        private static Font _font;

        // Chrome geometry is authored in source pixels and carried on the sprite's
        // nine-slice border, so a chamfer stays the same size on screen whether the
        // plate is a 240 px back key or a 470 px menu row.
        private const int Cut = 22;
        private const int Round = 26;
        private const float Band = 3.5f;

        /// <summary>Procedural icon glyphs. Every one is a real shape, not a font glyph,
        /// because the built-in legacy font has no dependable symbol coverage.</summary>
        public enum Icon
        {
            Play, Plus, Operative, Duo, Gear, Cloud, Power, Bolt,
            Aim, Run, Jump, Sword, Interact, Reload, Med, Chevron, Down,
            Gift, Trophy, Help
        }

        public static Font TypeFont =>
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ----------------------------------------------------------------- sprites

        public static void EnsureSprites()
        {
            if (_spritesReady) return;
            _spritesReady = true;

            _rect = LoadOrBuild("Rect", 8, 8, (u, v, w, h) => 1f);
            _circle = LoadOrBuild("Circle", 96, 96, Disc);
            _ring = LoadOrBuild("Ring", 96, 96, Ring);
            _vignette = LoadOrBuild("Vignette", 256, 256, VignetteAlpha);
            // World-space lights (menu neon pools, rain streaks) are sprites too: the
            // built-in sprite material is the only shader guaranteed to be in the build.
            _glow = LoadOrBuild("Glow", 128, 128, GlowAlpha);
            _streak = LoadOrBuild("Streak", 6, 32, StreakAlpha);
            _atmosphere = LoadOrBuild("Atmosphere", 256, 256, AtmosphereAlpha);

            _plate = LoadOrBuild("Plate", 200, 88, PlateAlpha, Cut);
            _rimCut = LoadOrBuild("RimPlate", 200, 88, CutRimAlpha, Cut);
            _pill = LoadOrBuild("Pill", 200, 88, (u, v, w, h) => Rounded(u * w, v * h, w, h, Round), Round);
            _rimRound = LoadOrBuild("RimPill", 200, 88, RoundRimAlpha, Round);
            _hex = LoadOrBuild("Hex", 128, 128, HexAlpha);

            // Menu rows are banner tabs: both left corners chamfered, right edge square so
            // an arrow tip can be bolted onto it without a step in the silhouette.
            _tab = LoadOrBuild("TabPlate", 200, 88, TabAlpha, Cut);
            _rimTab = LoadOrBuild("TabRim", 200, 88, TabRimAlpha, Cut);
            _tip = LoadOrBuild("TabTip", 44, 88, TipAlpha);
            _rimTip = LoadOrBuild("TabTipRim", 44, 88, TipRimAlpha);
            _ticks = LoadOrBuild("Ticks", 96, 40, TicksAlpha);

            const int glyph = 56;
            _icons[(int)Icon.Play] = LoadOrBuild("IconPlay", glyph, glyph,
                (u, v, w, h) => Play(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Plus] = LoadOrBuild("IconPlus", glyph, glyph, (u, v, w, h) =>
                Stroke(u * w - w * 0.5f, v * h - h * 0.5f, 3.4f,
                    -14f, 0f, 14f, 0f, 0f, -14f, 0f, 14f));
            _icons[(int)Icon.Operative] = LoadOrBuild("IconOperative", glyph, glyph, (u, v, w, h) =>
                Silhouette(u * w - w * 0.5f, v * h - h * 0.5f, 0f, 0f, 1f));
            _icons[(int)Icon.Duo] = LoadOrBuild("IconDuo", glyph, glyph, (u, v, w, h) =>
                Mathf.Max(Silhouette(u * w - w * 0.5f, v * h - h * 0.5f, -8f, -1f, 0.78f),
                    Silhouette(u * w - w * 0.5f, v * h - h * 0.5f, 9f, 1f, 0.62f)));
            _icons[(int)Icon.Gear] = LoadOrBuild("IconGear", glyph, glyph, Gear);
            _icons[(int)Icon.Cloud] = LoadOrBuild("IconCloud", glyph, glyph, Cloud);
            _icons[(int)Icon.Power] = LoadOrBuild("IconPower", glyph, glyph, Power);
            _icons[(int)Icon.Bolt] = LoadOrBuild("IconBolt", glyph, glyph, (u, v, w, h) =>
                Stroke(u * w - w * 0.5f, v * h - h * 0.5f, 3f,
                    5f, 17f, -5f, 1f, -5f, 1f, 5f, -1f, 5f, -1f, -5f, -17f));

            // Action keys: the verb has to be readable before the caption is, because a
            // thumb is already on its way to the disc while the eye is still finding it.
            _icons[(int)Icon.Aim] = LoadOrBuild("IconAim", glyph, glyph, Aim);
            _icons[(int)Icon.Run] = LoadOrBuild("IconRun", glyph, glyph, (u, v, w, h) =>
                Runner(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Jump] = LoadOrBuild("IconJump", glyph, glyph, (u, v, w, h) =>
                Jumper(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Sword] = LoadOrBuild("IconSword", glyph, glyph, (u, v, w, h) =>
                Sword(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Interact] = LoadOrBuild("IconInteract", glyph, glyph, (u, v, w, h) =>
                Interact(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Reload] = LoadOrBuild("IconReload", glyph, glyph, (u, v, w, h) =>
                Reload(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Med] = LoadOrBuild("IconMed", glyph, glyph, (u, v, w, h) =>
                Med(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Chevron] = LoadOrBuild("IconChevron", glyph, glyph, (u, v, w, h) =>
                Stroke(u * w - w * 0.5f, v * h - h * 0.5f, 3.2f,
                    -8f, 13f, 4f, 0f, 4f, 0f, -8f, -13f,
                    2f, 13f, 14f, 0f, 14f, 0f, 2f, -13f));
            _icons[(int)Icon.Down] = LoadOrBuild("IconDown", glyph, glyph, (u, v, w, h) =>
                Stroke(u * w - w * 0.5f, v * h - h * 0.5f, 3.2f,
                    0f, 15f, 0f, -6f, -10f, 1f, 0f, -9f, 0f, -9f, 10f, 1f));

            // Title-screen shortcuts: the hex row is read at a glance, so each mark has to
            // be one object, not a scene.
            _icons[(int)Icon.Gift] = LoadOrBuild("IconGift", glyph, glyph, (u, v, w, h) =>
                Gift(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Trophy] = LoadOrBuild("IconTrophy", glyph, glyph, (u, v, w, h) =>
                Trophy(u * w - w * 0.5f, v * h - h * 0.5f));
            _icons[(int)Icon.Help] = LoadOrBuild("IconHelp", glyph, glyph, (u, v, w, h) =>
                Help(u * w - w * 0.5f, v * h - h * 0.5f));
        }

        public static Sprite RectSprite { get { EnsureSprites(); return _rect; } }
        public static Sprite CircleSprite { get { EnsureSprites(); return _circle; } }
        public static Sprite RingSprite { get { EnsureSprites(); return _ring; } }
        public static Sprite VignetteSprite { get { EnsureSprites(); return _vignette; } }
        public static Sprite GlowSprite { get { EnsureSprites(); return _glow; } }
        public static Sprite StreakSprite { get { EnsureSprites(); return _streak; } }

        /// <summary>
        /// A ring, not a disc: the planet's silhouette hides the middle of any glow laid
        /// behind it, and what survives is this band. Drawn at 0.93 of the quad's radius,
        /// so the billboard only has to clear the sphere by a few percent.
        /// </summary>
        public static Sprite AtmosphereSprite { get { EnsureSprites(); return _atmosphere; } }

        public static Sprite PlateSprite { get { EnsureSprites(); return _plate; } }
        public static Sprite RimCutSprite { get { EnsureSprites(); return _rimCut; } }
        public static Sprite PillSprite { get { EnsureSprites(); return _pill; } }
        public static Sprite RimRoundSprite { get { EnsureSprites(); return _rimRound; } }
        public static Sprite HexSprite { get { EnsureSprites(); return _hex; } }
        public static Sprite TabSprite { get { EnsureSprites(); return _tab; } }
        public static Sprite TabRimSprite { get { EnsureSprites(); return _rimTab; } }
        public static Sprite TipSprite { get { EnsureSprites(); return _tip; } }
        public static Sprite TipRimSprite { get { EnsureSprites(); return _rimTip; } }
        public static Sprite TicksSprite { get { EnsureSprites(); return _ticks; } }

        private static Sprite _starField;

        /// <summary>
        /// Distant sky for the menu stage. Dots are splatted straight into the texture
        /// instead of being evaluated per pixel - a signed-distance pass over a million
        /// pixels cost more than the rest of the menu build combined. It keeps its own
        /// import size because the UI sheets cap at 256 px, which would turn these stars
        /// into blobs, and it is drawn as one world sprite: a single draw call that
        /// ignores fog, so the sky survives the menu's haze at 150 m.
        /// </summary>
        public static Sprite StarFieldSprite
        {
            get
            {
                if (_starField != null) return _starField;

                ArtAssets.EnsureFolder(SpriteDir);
                const int width = 1024, height = 512;
                string path = $"{SpriteDir}/Starfield.png";

                var onDisk = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (onDisk != null)
                {
                    _starField = onDisk;
                    return _starField;
                }

                var alpha = new float[width * height];
                var rng = new System.Random(80417);
                for (int i = 0; i < 320; i++)
                {
                    float cx = (float)rng.NextDouble() * width;
                    float cy = (float)rng.NextDouble() * height;
                    float radius = 0.7f + (float)rng.NextDouble() * (float)rng.NextDouble() * 2.1f;
                    float brightness = 0.22f + (float)rng.NextDouble() * 0.78f;

                    int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
                    int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
                    int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius));

                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float dx = x - cx, dy = y - cy;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d > radius) continue;
                            int index = y * width + x;
                            alpha[index] = Mathf.Max(alpha[index], brightness * (1f - d / radius));
                        }
                }

                var pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(1f, 1f, 1f, alpha[i]);

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                if (AssetImporter.GetAtPath(path) is TextureImporter skyImporter)
                {
                    skyImporter.textureType = TextureImporterType.Sprite;
                    skyImporter.spriteImportMode = SpriteImportMode.Single;
                    skyImporter.sRGBTexture = false;
                    skyImporter.mipmapEnabled = false;
                    skyImporter.filterMode = FilterMode.Bilinear;
                    skyImporter.wrapMode = TextureWrapMode.Clamp;
                    skyImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    skyImporter.alphaIsTransparency = true;
                    skyImporter.maxTextureSize = width;
                    skyImporter.SaveAndReimport();
                }

                _starField = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (_starField == null)
                    Debug.LogError($"[NullFrontier] {path} imported without a sprite sub-asset.");
                return _starField;
            }
        }

        public static Sprite IconSprite(Icon icon)
        {
            EnsureSprites();
            return _icons[(int)icon];
        }

        private delegate float Pixel(float u, float v, int width, int height);

        /// <summary>Writes a UI PNG when missing, imports it as a sprite and returns it.
        /// A non-zero border makes the asset nine-slice so its corners survive scaling.</summary>
        private static Sprite LoadOrBuild(string name, int width, int height, Pixel shade,
            int border = 0)
        {
            ArtAssets.EnsureFolder(SpriteDir);
            string path = $"{SpriteDir}/{name}.png";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                // A plate written before borders existed would stretch its chamfer to the
                // whole button instead of keeping it a fixed corner, so re-assert it.
                if (border > 0 && existing.border.x < border) SetBorder(path, border);
                return existing;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Clamp01(shade(x / (float)width, y / (float)height, width, height));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }

            if (border > 0) SetBorder(path, border);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"[NullFrontier] {path} imported without a sprite sub-asset.");
            return sprite;
        }

        private static void SetBorder(string path, int border)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            ApplyBorder(importer, border);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Nine-slice border plus full-rect mesh. Unity 6 keeps the mesh type on the
        /// settings blob rather than the importer; a tight mesh would crop the transparent
        /// chamfer corners and shift the slice grid inward.
        /// </summary>
        private static void ApplyBorder(TextureImporter importer, int border)
        {
            importer.spriteBorder = new Vector4(border, border, border, border);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        // ---------------------------------------------------------------- chrome SDFs
        // Every shape below returns a signed distance in source pixels plus a half-pixel
        // feather; LoadOrBuild clamps that to coverage, which is what keeps the edges smooth.

        /// <summary>A rect with the top-left and bottom-right corners chamfered.</summary>
        private static float PlateAlpha(float u, float v, int w, int h)
            => Chamfer(u * w, v * h, w, h, Cut);

        private static float CutRimAlpha(float u, float v, int w, int h)
        {
            float d = Chamfer(u * w, v * h, w, h, Cut) - 0.5f;
            return Mathf.Min(d, Band - d) + 0.5f;
        }

        private static float RoundRimAlpha(float u, float v, int w, int h)
        {
            float d = Rounded(u * w, v * h, w, h, Round) - 0.5f;
            return Mathf.Min(d, Band - d) + 0.5f;
        }

        private static float Chamfer(float x, float y, float w, float h, float cut)
        {
            float edge = Mathf.Min(x, w - x, y, h - y);
            // Both cut lines run at 45 degrees, so the distance needs the unit normal.
            float topLeft = (x + h - y - cut) * 0.7071f;
            float bottomRight = (w - x + y - cut) * 0.7071f;
            return Mathf.Min(edge, Mathf.Min(topLeft, bottomRight)) + 0.5f;
        }

        private static float Rounded(float x, float y, float w, float h, float r)
        {
            float dx = Mathf.Max(r - x, x - (w - r), 0f);
            float dy = Mathf.Max(r - y, y - (h - r), 0f);
            return r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f;
        }

        /// <summary>Banner tab: chamfers on both left corners, square right edge so the
        /// arrow head can be bolted on without a step in the silhouette.</summary>
        private static float TabAlpha(float u, float v, int w, int h)
            => Tab(u * w, v * h, w, h, Cut);

        private static float TabRimAlpha(float u, float v, int w, int h)
        {
            float d = Tab(u * w, v * h, w, h, Cut) - 0.5f;
            return Mathf.Min(d, Band - d) + 0.5f;
        }

        private static float Tab(float x, float y, float w, float h, float cut)
        {
            float edge = Mathf.Min(x, w - x, y, h - y);
            float topLeft = (x + h - y - cut) * 0.7071f;
            float bottomLeft = (x + y - cut) * 0.7071f;
            return Mathf.Min(edge, Mathf.Min(topLeft, bottomLeft)) + 0.5f;
        }

        /// <summary>Arrow head for a row's right edge: full-height base on the left, apex
        /// at the right middle.</summary>
        private static float TipAlpha(float u, float v, int w, int h)
        {
            float x = u * w, y = v * h;
            float slope = w / (h * 0.5f);
            float hyp = (w - x - slope * Mathf.Abs(y - h * 0.5f)) / Mathf.Sqrt(1f + slope * slope);
            return Mathf.Min(x, hyp) + 0.5f;
        }

        private static float TipRimAlpha(float u, float v, int w, int h)
        {
            // Only the two sloping edges get a rim - the base is buried in the plate, and a
            // hairline there would read as a seam across the button.
            float x = u * w, y = v * h;
            float slope = w / (h * 0.5f);
            float hyp = (w - x - slope * Mathf.Abs(y - h * 0.5f)) / Mathf.Sqrt(1f + slope * slope);
            return Mathf.Min(hyp, Band - hyp) + 0.5f;
        }

        /// <summary>Three leaning bars - the corner furniture in front of the tagline and
        /// behind the version number.</summary>
        private static float TicksAlpha(float u, float v, int w, int h)
        {
            float x = u * w, y = v * h;
            float best = 0f;
            for (int i = 0; i < 3; i++)
            {
                float ox = 6f + i * 22f;
                float skew = (h - y) * 0.5f;
                float bar = 5f - Mathf.Abs(x - ox - skew);
                best = Mathf.Max(best, Mathf.Min(bar, Mathf.Min(y, h - y)) * 0.8944f);
            }
            return best + 0.5f;
        }

        /// <summary>Crate with a carved ribbon cross and a two-loop bow.</summary>
        private static float Gift(float x, float y)
        {
            float body = Rounded(x + 16f, y + 7f, 32f, 22f, 3f);
            float lid = Rounded(x + 18f, y + 15f, 36f, 8f, 2f);
            float box = Mathf.Max(body, lid);

            float ribbon = Mathf.Max(2.4f - Mathf.Abs(x), 2f - Mathf.Abs(y + 4f));
            float carved = Mathf.Min(box, -ribbon);

            float bow = Mathf.Max(
                3.4f - Mathf.Sqrt((x + 6f) * (x + 6f) + (y - 18f) * (y - 18f)),
                3.4f - Mathf.Sqrt((x - 6f) * (x - 6f) + (y - 18f) * (y - 18f)));
            return Mathf.Max(carved, bow) + 0.5f;
        }

        private static float Trophy(float x, float y)
        {
            float cup = Rounded(x + 13f, y + 20f, 26f, 20f, 5f);

            float hx = Mathf.Abs(x) - 14f;
            float dr = Mathf.Sqrt(hx * hx + (y - 11f) * (y - 11f));
            float handle = Mathf.Min(7f - dr, dr - 4f);

            float stem = Mathf.Min(3f - Mathf.Abs(x), Mathf.Min(y + 6f, -y));
            float foot = Mathf.Min(11f - Mathf.Abs(x), Mathf.Min(y + 12f, -8f - y));

            return Mathf.Max(cup, Mathf.Max(handle, Mathf.Max(stem, foot))) + 0.5f;
        }

        /// <summary>Open hook, stem and dot. The hex plate is already the frame, so the
        /// mark itself stays unframed and reads at key size.</summary>
        private static float Help(float x, float y)
        {
            float hy = y - 8f;
            float r = Mathf.Sqrt(x * x + hy * hy);
            float hook = Mathf.Min(9f - r, r - 5.5f);
            if (x < 0f && hy < 0f) hook = -99f;

            float stem = Mathf.Min(2.6f - Mathf.Abs(x), Mathf.Min(y + 1f, 6f - y));
            float dot = 2.8f - Mathf.Sqrt(x * x + (y + 12f) * (y + 12f));
            return Mathf.Max(hook, Mathf.Max(stem, dot)) + 0.5f;
        }

        /// <summary>Flat-sided hexagon: the chip shape ability icons sit in.</summary>
        private static float HexAlpha(float u, float v, int w, int h)
        {
            float x = (u - 0.5f) * w;
            float y = (v - 0.5f) * h;
            float apothem = Mathf.Min(w, h) * 0.5f - 1f;
            float side = apothem * 0.866f;
            return Mathf.Min(side - Mathf.Abs(x),
                       Mathf.Min(side - Mathf.Abs(0.5f * x + 0.866f * y),
                                 side - Mathf.Abs(-0.5f * x + 0.866f * y))) + 0.5f;
        }

        // ------------------------------------------------------------------ glyph SDFs
        // Coordinates are centred on the 44 px cell, so the shapes below read at any size.

        private static float Play(float x, float y)
        {
            return Mathf.Min(x + 11f,
                       Mathf.Min((169f - 13f * x + 24f * y) * 0.0369f,
                                 (169f - 13f * x - 24f * y) * 0.0369f)) + 0.5f;
        }

        /// <summary>Stroked polyline: four numbers per segment.</summary>
        private static float Stroke(float x, float y, float half, params float[] segment)
        {
            float best = -999f;
            for (int i = 0; i + 3 < segment.Length; i += 4)
            {
                float vx = segment[i + 2] - segment[i];
                float vy = segment[i + 3] - segment[i + 1];
                float wx = x - segment[i];
                float wy = y - segment[i + 1];
                float length = vx * vx + vy * vy;
                float t = length > 0f ? Mathf.Clamp01((wx * vx + wy * vy) / length) : 0f;
                float dx = wx - vx * t;
                float dy = wy - vy * t;
                best = Mathf.Max(best, half - Mathf.Sqrt(dx * dx + dy * dy));
            }
            return best + 0.5f;
        }

        /// <summary>Head over shoulders - the operative silhouette.</summary>
        private static float Silhouette(float x, float y, float ox, float oy, float scale)
        {
            float px = (x - ox) / scale;
            float py = (y - oy) / scale;
            float head = 6.4f - Mathf.Sqrt(px * px + (py - 7f) * (py - 7f));
            float shoulders = 13f - Mathf.Sqrt(px * px + (py + 13f) * (py + 13f));
            return Mathf.Max(head, shoulders) * scale + 0.5f;
        }

        private static float Gear(float u, float v, int w, int h)
        {
            float x = u * w - w * 0.5f;
            float y = v * h - h * 0.5f;
            float distance = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float teeth = Mathf.Cos(angle * 8f) > 0.15f ? 14.6f : 11.4f;
            return Mathf.Min(teeth - distance, distance - 5.4f) + 0.5f;
        }

        private static float Cloud(float u, float v, int w, int h)
        {
            float x = u * w - w * 0.5f;
            float y = v * h - h * 0.5f;
            float left = 6f - Mathf.Sqrt((x + 6.5f) * (x + 6.5f) + y * y);
            float middle = 8.4f - Mathf.Sqrt(x * x + (y - 3.4f) * (y - 3.4f));
            float right = 6f - Mathf.Sqrt((x - 6.5f) * (x - 6.5f) + y * y);
            return Mathf.Min(Mathf.Max(left, Mathf.Max(middle, right)), y + 6.4f) + 0.5f;
        }

        private static float Power(float u, float v, int w, int h)
        {
            float x = u * w - w * 0.5f;
            float y = v * h - h * 0.5f;
            float distance = Mathf.Sqrt(x * x + y * y);
            float band = Mathf.Min(13f - distance, distance - 9.4f);
            // The ring opens at the top so the stem can read as one continuous stroke.
            float channel = -(Mathf.Min(4.6f - Mathf.Abs(x), y));
            float stem = Mathf.Min(2.2f - Mathf.Abs(x), Mathf.Min(14f - y, y + 13f));
            return Mathf.Max(Mathf.Min(band, channel), stem) + 0.5f;
        }

        // ---------------------------------------------------------------- action glyphs
        // Drawn as strokes at 56 px source: the key discs are 120-250 px on screen, so a
        // limb only has to be legible as a direction, not as anatomy.

        /// <summary>Lens ring, four ticks and a centre pip.</summary>
        private static float Aim(float u, float v, int w, int h)
        {
            float x = u * w - w * 0.5f;
            float y = v * h - h * 0.5f;
            float distance = Mathf.Sqrt(x * x + y * y);
            float ring = Mathf.Min(distance - 13.4f, 16.6f - distance);
            float pip = 2.8f - distance;
            float ticks = Stroke(x, y, 1.9f,
                18f, 0f, 25f, 0f, -18f, 0f, -25f, 0f,
                0f, 18f, 0f, 25f, 0f, -18f, 0f, -25f);
            return Mathf.Max(Mathf.Max(ring, pip), ticks) + 0.5f;
        }

        /// <summary>Head, leaning torso, one arm each way, legs split.</summary>
        private static float Runner(float x, float y)
        {
            float head = 5f - Mathf.Sqrt((x - 4f) * (x - 4f) + (y - 13f) * (y - 13f));
            float limbs = Stroke(x, y, 3.2f,
                4f, 8f, -3f, -2f,
                -3f, 5f, 10f, 2f,
                -3f, 5f, -12f, 10f,
                -3f, -2f, 6f, -11f,
                -3f, -2f, -12f, -7f);
            return Mathf.Max(head, limbs) + 0.5f;
        }

        /// <summary>Arms up, feet apart - the leave-the-ground verb.</summary>
        private static float Jumper(float x, float y)
        {
            float head = 4.8f - Mathf.Sqrt(x * x + (y - 12f) * (y - 12f));
            float body = Stroke(x, y, 3.2f,
                0f, 8f, 0f, -4f,
                0f, 6f, -10f, 14f,
                0f, 6f, 10f, 14f,
                0f, -4f, -8f, -14f,
                0f, -4f, 8f, -14f);
            return Mathf.Max(head, body) + 0.5f;
        }

        /// <summary>Blade, crossguard, grip and pommel.</summary>
        private static float Sword(float x, float y)
        {
            float blade = Stroke(x, y, 2.8f, -6f, -6f, 12f, 12f);
            float tip = Mathf.Max(3.4f - Mathf.Sqrt((x - 12f) * (x - 12f) + (y - 12f) * (y - 12f)), blade);
            float furniture = Stroke(x, y, 2.6f,
                -11f, 1f, 1f, 13f,
                2f, -14f, 1f, -6f);
            float pommel = 3.4f - Mathf.Sqrt((x - 2f) * (x - 2f) + (y + 14f) * (y + 14f));
            return Mathf.Max(Mathf.Max(tip, furniture), pommel) + 0.5f;
        }

        /// <summary>Chip frame with an exclamation: something here answers a tap.</summary>
        private static float Interact(float x, float y)
        {
            // Rounded() is measured from a corner, so the centred glyph is offset first.
            float inside = Rounded(x + 24f, y + 24f, 48f, 48f, 9f);
            float frame = Mathf.Min(inside, 3.2f - inside);
            float mark = Stroke(x, y, 2.4f, 0f, 9f, 0f, -1f);
            float dot = 2.8f - Mathf.Sqrt(x * x + (y + 7f) * (y + 7f));
            return Mathf.Max(frame, Mathf.Max(mark, dot)) + 0.5f;
        }

        /// <summary>Consumable: a cross inside a soft square.</summary>
        private static float Med(float x, float y)
        {
            float inside = Rounded(x + 22f, y + 22f, 44f, 44f, 8f);
            float frame = Mathf.Min(inside, 3f - inside);
            float cross = Stroke(x, y, 3.4f, -10f, 0f, 10f, 0f, 0f, -10f, 0f, 10f);
            return Mathf.Max(frame, cross) + 0.5f;
        }

        /// <summary>Ring opened on the right with an arrowhead in the gap.</summary>
        private static float Reload(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float ring = Mathf.Min(distance - 13.6f, 16.8f - distance);
            if (Mathf.Abs(angle) < 0.55f && distance > 10f) ring = -9f;
            float head = Stroke(x, y, 2.6f, 15f, 8f, 22f, 0f, 22f, 0f, 15f, -8f);
            return Mathf.Max(ring, head) + 0.5f;
        }

        // Signed-distance coverage with a one-pixel feather keeps the edge smooth.
        private static float Disc(float u, float v, int w, int h)
        {
            float radius = w * 0.5f;
            float dx = u * w - radius;
            float dy = v * h - radius;
            return radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f;
        }

        private static float Ring(float u, float v, int w, int h)
        {
            float radius = w * 0.5f;
            float dx = u * w - radius;
            float dy = v * h - radius;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Min(radius - distance + 0.5f, distance - radius * 0.76f + 0.5f);
        }

        private static float VignetteAlpha(float u, float v, int w, int h)
        {
            float dx = (u - 0.5f) * 2f;
            float dy = (v - 0.5f) * 2f;
            return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - 0.5f) * 1.4f;
        }

        // Squared falloff: bright at the centre, gone by the rim, with no hard edge to
        // give the neon pool away as a decal.
        private static float GlowAlpha(float u, float v, int w, int h)
        {
            float dx = u * 2f - 1f;
            float dy = v * 2f - 1f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float inside = Mathf.Clamp01(1f - radius);
            return inside * inside;
        }

        // Bright band at 0.93 of the quad's radius, feathered on both sides and taken to
        // zero at the edge so the quad itself never shows as a seam.
        private static float AtmosphereAlpha(float u, float v, int w, int h)
        {
            float dx = u * 2f - 1f;
            float dy = v * 2f - 1f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float band = 1f - Mathf.Abs(radius - 0.93f) / 0.13f;
            float edge = Mathf.Clamp01((1f - radius) / 0.045f);
            return Mathf.Clamp01(band) * edge;
        }

        private static float StreakAlpha(float u, float v, int w, int h)
        {
            float across = 1f - Mathf.Abs(u * 2f - 1f);
            float along = 1f - Mathf.Abs(v * 2f - 1f);
            return across * across * Mathf.Pow(along, 0.45f);
        }

        // ---------------------------------------------------------------- plumbing

        /// <summary>Sets a private [SerializeField] field, walking base classes too.</summary>
        public static void SetField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                Debug.LogError($"[NullFrontier] No target for field '{fieldName}'.");
                return;
            }
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Debug.LogError($"[NullFrontier] {target.GetType().Name}.{fieldName} not found.");
        }

        /// <summary>
        /// Scene-persistent click binding. A runtime lambda is not serialised into the
        /// .unity file, so anything that has to survive the save goes through here.
        /// </summary>
        public static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        public static RectTransform Root(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Center(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static RectTransform BottomLeft(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static RectTransform TopLeft(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static RectTransform RightCenter(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static RectTransform TopRight(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static RectTransform BottomRight(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static Image Solid(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Root(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : RectSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Sheet(string name, Transform parent, Color color)
        {
            var image = Solid(name, parent, color);
            Stretch(image.rectTransform, 0f);
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Root(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = TypeFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Shadow AddShadow(Text label, Color color, Vector2 distance)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            return shadow;
        }

        /// <summary>Four hairline bars hugging a rect - the panel's only decoration.</summary>
        public static void Frame(RectTransform target, Color color, float thickness)
        {
            var frame = Root("Frame", target.parent);
            frame.anchorMin = target.anchorMin;
            frame.anchorMax = target.anchorMax;
            frame.pivot = target.pivot;
            frame.anchoredPosition = target.anchoredPosition;
            frame.sizeDelta = target.sizeDelta + Vector2.one * (thickness * 2f);

            Edge(frame, color, thickness, new Vector2(0.5f, 1f), true);
            Edge(frame, color, thickness, new Vector2(0.5f, 0f), true);
            Edge(frame, color, thickness, new Vector2(0f, 0.5f), false);
            Edge(frame, color, thickness, new Vector2(1f, 0.5f), false);
        }

        /// <summary>
        /// Bevel the plates with instead of stacking hairlines: an inner lip that fakes the
        /// lit top face of a moulded panel, and a neon rim 3 px outside the silhouette. Both
        /// are children, so a Button's colour tint only ever touches the plate itself.
        /// </summary>
        private static void Chrome(Image plate, Color accent, float rimAlpha)
        {
            Sprite rim = plate.sprite == PillSprite ? RimRoundSprite
                : plate.sprite == TabSprite ? TabRimSprite : RimCutSprite;

            var lip = Solid("Lip", plate.transform, new Color(1f, 1f, 1f, 0.055f), plate.sprite);
            lip.type = Image.Type.Sliced;
            Stretch(lip.rectTransform, 5f);

            var image = Solid("Rim", plate.transform,
                new Color(accent.r, accent.g, accent.b, rimAlpha), rim);
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform, -3f);
        }

        /// <summary>Icon inside a plate, inset from the left edge so the chamfer stays clear.</summary>
        /// <summary>Lit icon chip: dark lens, accent rim, glyph. Rows and keys use this so
        /// the verb is recognisable before the caption is read.</summary>
        private static void IconChip(Image plate, Icon icon, Color accent, float size, float inset)
        {
            var halo = Solid("ChipRim", plate.transform,
                new Color(accent.r, accent.g, accent.b, 0.75f), RingSprite);
            LeftOf(halo.rectTransform, size, inset);

            var lens = Solid("Chip", plate.transform, new Color(0.02f, 0.03f, 0.055f, 0.62f),
                CircleSprite);
            LeftOf(lens.rectTransform, size - 5f, inset + 2.5f);

            var glyph = Solid("Icon", plate.transform, accent, IconSprite(icon));
            float inner = size * 0.5f;
            LeftOf(glyph.rectTransform, inner, inset + (size - inner) * 0.5f);
        }

        /// <summary>Places a square child at a fixed inset from the left edge, vertically
        /// centred.</summary>
        private static void LeftOf(RectTransform rt, float size, float inset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(inset + size * 0.5f, 0f);
        }

        /// <summary>
        /// A read-only panel: rounded plate plus lit edge. Everything that only displays
        /// state uses this, so the HUD stops looking like a spreadsheet of rectangles.
        /// </summary>
        private static Image Plated(string name, Transform parent, Color fill, Color accent)
        {
            var image = Solid(name, parent, fill, PillSprite);
            image.type = Image.Type.Sliced;
            Chrome(image, accent, 0.55f);
            return image;
        }

        private static void Edge(RectTransform parent, Color color, float thickness,
            Vector2 anchor, bool horizontal)
        {
            var edge = Solid("Edge", parent, color);
            edge.rectTransform.anchorMin = edge.rectTransform.anchorMax = anchor;
            edge.rectTransform.pivot = anchor;
            var rect = parent.rect;
            edge.rectTransform.sizeDelta = horizontal
                ? new Vector2(rect.width, thickness)
                : new Vector2(thickness, rect.height);
            // Decorative borders are siblings of the panel they outline, so a raycast
            // target here sits on top of the button underneath and swallows the tap.
            edge.raycastTarget = false;
        }

        /// <summary>
        /// Turns a whole panel tree decorative. HUD plates, frames and labels must not
        /// answer pointer events, or they eat the touches meant for the control layer.
        /// </summary>
        public static void DisableRaycasts(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                image.raycastTarget = false;
            foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                text.raycastTarget = false;
        }

        /// <summary>
        /// Round action key in the MOBA idiom: a tinted bezel, a dark face carrying the
        /// verb, and an optional radial veil that unwinds over real state - an ability
        /// cooling down, a spent stamina bar, an empty magazine. Returns the TouchButton
        /// the input provider polls.
        /// </summary>
        public static TouchButton SkillKey(string name, Transform parent, string label,
            string hint, Vector2 anchoredPos, Vector2 size, Color tint,
            KeyOverlay.Source? overlay = null, Icon? glyph = null)
        {
            var rt = BottomRight(Root(name, parent), anchoredPos);
            rt.sizeDelta = size;

            var bezel = Solid("Bezel", rt, new Color(tint.r, tint.g, tint.b, 0.8f), RingSprite);
            Stretch(bezel.rectTransform, 0f);
            bezel.raycastTarget = false;

            var face = Solid("Face", rt, Ink, CircleSprite);
            Stretch(face.rectTransform, 7f);
            // The face answers the tap; the handler sits on the key itself and UGUI
            // bubbles the event up to it.
            face.raycastTarget = true;

            // Glyph above, verb below. At thumb distance the shape is what the eye catches
            // first; the caption only has to confirm it.
            var caption = Label("Label", rt, label,
                glyph.HasValue ? (size.x > 200f ? 24 : 16) : (size.x > 200f ? 30 : 20),
                Color.white);
            Center(caption.rectTransform, new Vector2(0f, glyph.HasValue ? -size.y * 0.16f
                : (string.IsNullOrEmpty(hint) ? 0f : 12f)));
            caption.rectTransform.sizeDelta = new Vector2(size.x - 30f, 34f);
            caption.raycastTarget = false;
            AddShadow(caption, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            if (glyph.HasValue)
            {
                float mark = size.x * 0.34f;
                var glyphImage = Solid("Glyph", rt,
                    new Color(tint.r, tint.g, tint.b, 0.95f), IconSprite(glyph.Value));
                Center(glyphImage.rectTransform, new Vector2(0f, size.y * 0.13f));
                glyphImage.rectTransform.sizeDelta = new Vector2(mark, mark);
                glyphImage.raycastTarget = false;
            }

            // A glyph key already spends its lower band on the live readout (ammo,
            // cooldown), so the static hint would sit on top of it.
            if (!string.IsNullOrEmpty(hint) && !glyph.HasValue)
            {
                var sub = Label("Hint", rt, hint, 15, new Color(tint.r, tint.g, tint.b, 0.95f));
                Center(sub.rectTransform, new Vector2(0f,
                    -size.y * (glyph.HasValue ? 0.3f : 0.22f)));
                sub.rectTransform.sizeDelta = new Vector2(size.x - 34f, 20f);
                sub.raycastTarget = false;
            }

            var readout = Label("Readout", rt, string.Empty, size.x > 200f ? 21 : 15, Color.white);
            Center(readout.rectTransform, new Vector2(0f, -size.y * 0.36f));
            readout.rectTransform.sizeDelta = new Vector2(size.x - 22f, 24f);
            readout.raycastTarget = false;
            readout.enabled = false;
            AddShadow(readout, new Color(0f, 0f, 0f, 0.85f), new Vector2(1f, -1f));

            var veil = Solid("Veil", rt, new Color(0.01f, 0.02f, 0.045f, 0.74f), CircleSprite);
            Stretch(veil.rectTransform, 7f);
            veil.raycastTarget = false;
            veil.type = Image.Type.Filled;
            veil.fillMethod = Image.FillMethod.Radial360;
            veil.fillOrigin = (int)Image.Origin360.Top;
            veil.fillClockwise = true;
            veil.fillAmount = 0f;
            veil.enabled = false;

            if (overlay.HasValue)
            {
                var hud = rt.gameObject.AddComponent<KeyOverlay>();
                SetField(hud, "source", overlay.Value);
                SetField(hud, "veil", veil);
                SetField(hud, "readout", readout);
            }

            return rt.gameObject.AddComponent<TouchButton>();
        }

        /// <summary>
        /// Field kit tray: the three consumables SupplyPanel offers, counted from the live
        /// inventory. An empty kit reads as 0 rather than a hole in the layout, and there is
        /// no fourth "locked" slot because nothing locks - the mockup's greyed key had no
        /// system behind it.
        /// </summary>
        private static Text[] BuildKitTray(Transform root)
        {
            var plate = Plated("Kit", root, Ink, Stamina);
            // Between the stick's ring and the weapon plate: the only bottom band a thumb
            // is not already living in.
            Center(plate.rectTransform, new Vector2(-320f, -404f));
            plate.rectTransform.sizeDelta = new Vector2(260f, 118f);

            string[] captions = { "GEL", "CELL", "STIM" };
            Color[] tints = { Health, Shield, Stamina };
            var counts = new Text[captions.Length];

            for (int i = 0; i < counts.Length; i++)
            {
                float x = -80f + i * 80f;

                var lens = Solid($"Kit{i}", plate.transform,
                    new Color(tints[i].r, tints[i].g, tints[i].b, 0.13f), CircleSprite);
                Center(lens.rectTransform, new Vector2(x, 24f));
                lens.rectTransform.sizeDelta = new Vector2(56f, 56f);

                var rim = Solid($"KitRim{i}", lens.transform,
                    new Color(tints[i].r, tints[i].g, tints[i].b, 0.85f), RingSprite);
                Stretch(rim.rectTransform, -3f);

                var mark = Solid($"KitGlyph{i}", lens.transform, tints[i], IconSprite(Icon.Med));
                Stretch(mark.rectTransform, 15f);

                counts[i] = Label($"KitCount{i}", plate.transform, "0", 21, Color.white);
                Center(counts[i].rectTransform, new Vector2(x, -22f));
                counts[i].rectTransform.sizeDelta = new Vector2(76f, 24f);
                AddShadow(counts[i], new Color(0f, 0f, 0f, 0.8f), new Vector2(1f, -1f));

                var cap = Label($"KitName{i}", plate.transform, captions[i], 12, Subtle);
                Center(cap.rectTransform, new Vector2(x, -46f));
                cap.rectTransform.sizeDelta = new Vector2(76f, 16f);
            }

            return counts;
        }

        // ------------------------------------------------------------------ widgets

        public static Button Button(string name, Transform parent, string text, Vector2 pos,
            Vector2 size, Color background, int fontSize = 30)
        {
            var rt = Center(Root(name, parent), pos);
            rt.sizeDelta = size;

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = PlateSprite;
            image.type = Image.Type.Sliced;
            image.color = background;

            // A dark plate needs the cyan rim to read as lit chrome; a saturated one is
            // its own accent and only wants the edge to match.
            Chrome(image, background.maxColorComponent < 0.35f ? Accent : background, 0.8f);

            var label = Label("Label", rt, text, fontSize, Color.white);
            Stretch(label.rectTransform, 4f);
            // The chamfer eats the corners, so the caption gets more side room than a
            // plain rect would need.
            label.rectTransform.offsetMin = new Vector2(22f, 4f);
            label.rectTransform.offsetMax = new Vector2(-22f, -4f);
            AddShadow(label, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            return button;
        }

        /// <summary>
        /// Bar. Slider drives the fill's anchors itself, so the fill only needs a parent
        /// that defines the track; interactive bars also answer clicks anywhere on it.
        /// </summary>
        public static Slider Bar(string name, Transform parent, Vector2 pos, Vector2 size,
            Color fill, bool interactive, float value)
        {
            var rt = Center(Root(name, parent), pos);
            rt.sizeDelta = size;

            var backdrop = rt.gameObject.AddComponent<Image>();
            backdrop.sprite = RectSprite;
            backdrop.color = Track;
            backdrop.raycastTarget = interactive;

            var track = Root("Fill Area", rt);
            Stretch(track, 2f);

            var fillRt = Root("Fill", track);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImage = fillRt.gameObject.AddComponent<Image>();
            fillImage.sprite = RectSprite;
            fillImage.color = fill;
            fillImage.raycastTarget = interactive;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = interactive;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = value;
            return slider;
        }

        public static InputField Input(string name, Transform parent, Vector2 pos, Vector2 size,
            string placeholder, bool password)
        {
            var rt = Center(Root(name, parent), pos);
            rt.sizeDelta = size;

            var backdrop = rt.gameObject.AddComponent<Image>();
            backdrop.sprite = RectSprite;
            backdrop.color = Panel;

            var text = Label("Text", rt, string.Empty, 26, Color.white, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 14f);

            var hint = Label("Placeholder", rt, placeholder, 26, Subtle, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 14f);

            var field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.characterLimit = password ? 48 : 64;
            return field;
        }

        /// <summary>Back/close button that swaps to another named screen on click.</summary>
        public static Button BackButton(string name, Transform parent, string text, Vector2 pos,
            Vector2 size, string screenName)
        {
            var button = Button(name, parent, text, pos, size, Panel);
            var link = button.gameObject.AddComponent<ScreenLink>();
            SetField(link, "screenName", screenName);
            Bind(button, new UnityAction(link.Show));
            return button;
        }

        // ---------------------------------------------------------------------- HUD

        /// <summary>Builds the in-game HUD and wires the controller's private fields.</summary>
        public static void BuildHud(Canvas canvas, HUDController hud)
        {
            EnsureSprites();
            var root = canvas.transform;

            // Full-rect vignette that only ever shows on damage; never blocks input.
            var flashRt = Stretch(Root("DamageFlash", root), 0f);
            var group = flashRt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var flash = flashRt.gameObject.AddComponent<Image>();
            flash.sprite = VignetteSprite;
            flash.color = Danger;

            var plate = Plated("Vitals", root, Ink, Accent);
            // Left column, directly under the operative card. The movement pad owns the
            // bottom-left corner and a plate on top of the stick reads as a broken control.
            TopLeft(plate.rectTransform, new Vector2(24f, -164f));
            plate.rectTransform.sizeDelta = new Vector2(430f, 172f);

            var caption = Label("PlateTitle", plate.transform, "VITALS", 17, Subtle,
                TextAnchor.MiddleLeft);
            TopLeft(caption.rectTransform, new Vector2(18f, -10f));
            caption.rectTransform.sizeDelta = new Vector2(200f, 24f);

            var health = Gauge(plate.transform, "Health", "HP", Health, 0);
            var shield = Gauge(plate.transform, "Shield", "SHIELD", Shield, 1);
            var stamina = Gauge(plate.transform, "Stamina", "STAMINA", Stamina, 2);

            var mission = Plated("Mission", root, Ink, Accent2);
            // The right column below the scope belongs to the reload and drop keys, so the
            // live contract finishes the left column under the vitals instead.
            TopLeft(mission.rectTransform, new Vector2(24f, -348f));
            mission.rectTransform.sizeDelta = new Vector2(430f, 170f);

            var heading = Label("MissionHeading", mission.transform, "MISSION", 17, Subtle,
                TextAnchor.MiddleLeft);
            TopLeft(heading.rectTransform, new Vector2(18f, -10f));
            heading.rectTransform.sizeDelta = new Vector2(394f, 24f);

            var title = Label("MissionTitle", mission.transform, string.Empty, 26, Color.white,
                TextAnchor.UpperLeft);
            TopLeft(title.rectTransform, new Vector2(18f, -36f));
            title.rectTransform.sizeDelta = new Vector2(394f, 38f);

            var objectives = Label("Objectives", mission.transform, string.Empty, 19, Body,
                TextAnchor.UpperLeft);
            TopLeft(objectives.rectTransform, new Vector2(18f, -74f));
            objectives.rectTransform.sizeDelta = new Vector2(394f, 88f);

            var reticle = Solid("Reticle", root, Accent, RingSprite);
            Center(reticle.rectTransform, Vector2.zero);
            reticle.rectTransform.sizeDelta = new Vector2(44f, 44f);
            var pip = Solid("Pip", root, Accent2, CircleSprite);
            Center(pip.rectTransform, Vector2.zero);
            pip.rectTransform.sizeDelta = new Vector2(6f, 6f);

            var prompt = Label("InteractPrompt", root, string.Empty, 27, Color.white);
            Center(prompt.rectTransform, new Vector2(0f, -190f));
            prompt.rectTransform.sizeDelta = new Vector2(760f, 44f);
            prompt.enabled = false;
            AddShadow(prompt, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

            // Objective feedback sits top-centre: vitals own the bottom-left, the tracker the
            // top-right, and the hands own the bottom corners on a phone.
            var toast = Label("ObjectiveToast", root, string.Empty, 30, Accent2);
            Center(toast.rectTransform, new Vector2(0f, 258f));
            toast.rectTransform.sizeDelta = new Vector2(860f, 52f);
            toast.enabled = false;
            AddShadow(toast, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -3f));

            // Bottom-centre: the pad owns the left, the thumb cluster the right, and this
            // strip is the only place the screen stays free.
            var weaponPlate = Plated("WeaponPlate", root, Ink, Accent2);
            Center(weaponPlate.rectTransform, new Vector2(0f, -404f));
            weaponPlate.rectTransform.sizeDelta = new Vector2(360f, 104f);

            var weaponName = Label("WeaponName", weaponPlate.transform, string.Empty, 19, Subtle);
            Center(weaponName.rectTransform, new Vector2(0f, 28f));
            weaponName.rectTransform.sizeDelta = new Vector2(330f, 26f);

            var ammo = Label("Ammo", weaponPlate.transform, string.Empty, 44, Color.white);
            Center(ammo.rectTransform, new Vector2(0f, -14f));
            ammo.rectTransform.sizeDelta = new Vector2(330f, 54f);
            AddShadow(ammo, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

            BuildRadar(root);
            BuildStatusStrip(root, out var clockText, out var hostileText);
            var kitCounts = BuildKitTray(root);
            // The operative's own face, rank and progress to the next one - the same
            // card the menu leads with, in the corner the eye checks first. HUDController
            // drives its level label and XP bar from the live save file.
            PlayerCard(root, new Vector2(24f, -24f), out var levelText, out var xpBar);

            SetField(hud, "healthBar", health);            SetField(hud, "shieldBar", shield);
            SetField(hud, "staminaBar", stamina);
            SetField(hud, "missionTitle", title);
            SetField(hud, "missionObjectives", objectives);
            SetField(hud, "damageFlash", group);
            SetField(hud, "interactPrompt", prompt);
            SetField(hud, "toastLabel", toast);
            SetField(hud, "weaponName", weaponName);
            SetField(hud, "ammoText", ammo);
            SetField(hud, "clockText", clockText);
            SetField(hud, "hostileText", hostileText);
            SetField(hud, "levelText", levelText);
            SetField(hud, "xpBar", xpBar);
            SetField(hud, "kitCounts", kitCounts);

            // Nothing on this canvas is clickable and it draws above the control layer,
            // so any raycast target here (Text defaults to one) sits over the movement
            // pad and silently eats the finger.
            DisableRaycasts(root);
        }

        /// <summary>
        /// Tactical scope: a fixed bed, two grid rings, a north marker that turns with the
        /// operative's facing, and pooled dots the radar positions on its own refresh.
        /// </summary>
        private static RadarController BuildRadar(Transform root)
        {
            var panel = Solid("Radar", root, Ink, CircleSprite);
            // Top-right, one band under the pause key. A scope in the corner the thumb
            // works from would be tapped constantly.
            TopRight(panel.rectTransform, new Vector2(-24f, -164f));
            panel.rectTransform.sizeDelta = new Vector2(300f, 300f);

            // Round housing, drawn as a ring rather than a rectangle plus hairlines: a
            // scope is a lens, and the square frame made it read as a placeholder.
            var housing = Solid("Housing", panel.transform,
                new Color(Accent.r, Accent.g, Accent.b, 0.8f), RingSprite);
            Stretch(housing.rectTransform, 0f);

            var dish = Root("Dish", panel.rectTransform);
            Center(dish, Vector2.zero);
            dish.sizeDelta = new Vector2(268f, 268f);

            var bed = Solid("Bed", dish, new Color(0.045f, 0.075f, 0.12f, 0.92f), CircleSprite);
            Stretch(bed.rectTransform, 0f);

            for (int i = 1; i <= 2; i++)
            {
                var grid = Solid("Grid" + i, dish,
                    new Color(Accent.r, Accent.g, Accent.b, 0.18f), RingSprite);
                Stretch(grid.rectTransform, 30f * i);
            }

            var compass = Root("Compass", dish);
            Stretch(compass, 0f);
            var north = Label("North", compass, "N", 19, Subtle);
            Center(north.rectTransform, new Vector2(0f, 122f));
            north.rectTransform.sizeDelta = new Vector2(40f, 26f);

            var hostiles = Root("Hostiles", dish);
            Stretch(hostiles, 0f);
            for (int i = 0; i < 14; i++) Dot(hostiles, "Hostile", 15f, new Color(1f, 0.34f, 0.3f));

            var objectives = Root("Objectives", dish);
            Stretch(objectives, 0f);
            for (int i = 0; i < 8; i++) Dot(objectives, "Objective", 17f, new Color(1f, 0.78f, 0.28f));

            var self = Solid("Self", dish, Accent, CircleSprite);
            self.rectTransform.sizeDelta = new Vector2(18f, 18f);
            Center(self.rectTransform, Vector2.zero);

            var facing = Solid("Facing", dish, Accent, RectSprite);
            facing.rectTransform.sizeDelta = new Vector2(6f, 30f);
            Center(facing.rectTransform, new Vector2(0f, 24f));

            var radar = panel.gameObject.AddComponent<RadarController>();
            SetField(radar, "playerMarker", self.rectTransform);
            SetField(radar, "hostileLayer", hostiles);
            SetField(radar, "objectiveLayer", objectives);
            SetField(radar, "compass", compass);
            return radar;
        }

        /// <summary>One parked radar dot. The controller switches these on and moves them.</summary>
        private static void Dot(RectTransform parent, string name, float size, Color color)
        {
            var dot = Solid(name, parent, color, CircleSprite);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            Center(dot.rectTransform, Vector2.zero);
            dot.gameObject.SetActive(false);
        }

        /// <summary>Match clock and live hostile count, top-centre where the eyes already rest.</summary>
        private static void BuildStatusStrip(Transform root, out Text clock, out Text hostiles)
        {
            var plate = Plated("Match", root, Ink, Accent2);
            // Top-centre, but 53 px below the very edge: on a 21:9 phone the canvas only
            // shows ~483 px either side of centre, so anything measured against the 1080
            // reference bleeds off screen.
            Center(plate.rectTransform, new Vector2(0f, 430f));
            plate.rectTransform.sizeDelta = new Vector2(460f, 76f);

            clock = Label("Clock", plate.transform, "00:00", 36, Color.white);
            Center(clock.rectTransform, new Vector2(-110f, 0f));
            clock.rectTransform.sizeDelta = new Vector2(200f, 48f);
            AddShadow(clock, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            var divider = Solid("Divider", plate.transform, new Color(1f, 1f, 1f, 0.14f));
            Center(divider.rectTransform, new Vector2(8f, 0f));
            divider.rectTransform.sizeDelta = new Vector2(2f, 48f);

            var caption = Label("HostileCaption", plate.transform, "HOSTILES", 15, Subtle,
                TextAnchor.MiddleRight);
            Center(caption.rectTransform, new Vector2(112f, 14f));
            caption.rectTransform.sizeDelta = new Vector2(150f, 20f);

            hostiles = Label("Hostiles", plate.transform, "0", 30, Danger);
            Center(hostiles.rectTransform, new Vector2(150f, -16f));
            hostiles.rectTransform.sizeDelta = new Vector2(90f, 36f);
        }

        private static Slider Gauge(Transform plate, string name, string caption, Color color, int row)        {
            var label = Label(caption, plate, caption, 16, Subtle, TextAnchor.MiddleLeft);
            TopLeft(label.rectTransform, new Vector2(18f, -42f - row * 40f));
            label.rectTransform.sizeDelta = new Vector2(92f, 22f);

            var bar = Bar(name, plate, Vector2.zero, Vector2.zero, color, false, 1f);
            var rt = bar.transform as RectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(114f, -44f - row * 40f);
            rt.sizeDelta = new Vector2(300f, 22f);
            return bar;
        }

        /// <summary>Dossier stat line: caption and numeric readout on one row, meter
        /// underneath. The runtime sets value + text; the geometry lives only here.</summary>
        private static void DossierBar(Transform plate, string caption, int row, Color color,
            out Slider bar, out Text value)
        {
            var label = Label(caption, plate, caption, 15, Subtle, TextAnchor.MiddleLeft);
            TopLeft(label.rectTransform, new Vector2(20f, -162f - row * 36f));
            label.rectTransform.sizeDelta = new Vector2(180f, 20f);

            value = Label(caption + "Value", plate, string.Empty, 15, Body, TextAnchor.MiddleRight);
            TopRight(value.rectTransform, new Vector2(-20f, -162f - row * 36f));
            value.rectTransform.sizeDelta = new Vector2(220f, 20f);

            bar = Bar("Bar", plate, Vector2.zero, Vector2.zero, color, false, 0f);
            var rt = bar.transform as RectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -182f - row * 36f);
            rt.sizeDelta = new Vector2(420f, 11f);
        }

        // ------------------------------------------------------------------ screens

        /// <summary>
        /// Account chip: the rendered portrait of the operative the player actually
        /// chose, their rank, and the real XP bar toward the next one.
        /// </summary>
        public static PlayerCardController PlayerCard(Transform parent, Vector2 anchoredPos,
            out Text level, out Slider xp)
            => PlayerCard(parent, anchoredPos, out level, out xp, false);

        /// <param name="floating">Drops the plate so the card sits straight on the live
        /// stage - the title screen's corner, where a panel would cover the scene.</param>
        public static PlayerCardController PlayerCard(Transform parent, Vector2 anchoredPos,
            out Text level, out Slider xp, bool floating)
        {
            Transform body;
            float height = floating ? 88f : 128f;

            if (floating)
            {
                var root = Root("PlayerCard", parent);
                TopLeft(root, anchoredPos);
                root.sizeDelta = new Vector2(430f, height);
                body = root;
            }
            else
            {
                var plate = Plated("PlayerCard", parent, Ink, Accent);
                TopLeft(plate.rectTransform, anchoredPos);
                plate.rectTransform.sizeDelta = new Vector2(430f, 128f);
                body = plate.transform;
            }

            // The portrait renders are square crops, so the lens is a mask: the card reads
            // as a porthole in the chrome instead of a thumbnail pasted onto a plate.
            if (floating)
            {
                var bloom = Solid("Bloom", body, new Color(Accent.r, Accent.g, Accent.b, 0.16f),
                    GlowSprite);
                Center(bloom.rectTransform, new Vector2(-150f, 0f));
                bloom.rectTransform.sizeDelta = new Vector2(220f, 220f);
            }

            var ring = Solid("Bezel", body,
                new Color(Accent.r, Accent.g, Accent.b, 0.9f), RingSprite);
            Center(ring.rectTransform, new Vector2(-150f, 0f));
            ring.rectTransform.sizeDelta = new Vector2(floating ? 92f : 114f, floating ? 92f : 114f);

            var window = Solid("Window", body, new Color(0.12f, 0.16f, 0.22f, 1f), CircleSprite);
            Center(window.rectTransform, new Vector2(-150f, 0f));
            window.rectTransform.sizeDelta = new Vector2(floating ? 78f : 98f, floating ? 78f : 98f);
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var face = Solid("Portrait", window.transform, Color.white);
            Stretch(face.rectTransform, 0f);

            var name = Label("Name", body, "OPERATIVE", floating ? 28 : 26, Color.white,
                TextAnchor.MiddleLeft);
            TopLeft(name.rectTransform, new Vector2(180f, floating ? -8f : -22f));
            name.rectTransform.sizeDelta = new Vector2(234f, floating ? 34f : 38f);
            if (floating) AddShadow(name, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

            level = Label("Level", body, "Lv. 1", 20, Stamina, TextAnchor.MiddleLeft);
            TopLeft(level.rectTransform, new Vector2(180f, floating ? -42f : -58f));
            level.rectTransform.sizeDelta = new Vector2(234f, 26f);
            if (floating) AddShadow(level, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

            xp = Bar("Xp", body, Vector2.zero, Vector2.zero, Accent, false, 0f);
            var xpRt = xp.transform as RectTransform;
            xpRt.anchorMin = xpRt.anchorMax = xpRt.pivot = new Vector2(0f, 1f);
            xpRt.anchoredPosition = new Vector2(180f, floating ? -66f : -90f);
            xpRt.sizeDelta = new Vector2(234f, floating ? 9f : 12f);

            var card = body.gameObject.AddComponent<PlayerCardController>();
            SetField(card, "portrait", face);
            SetField(card, "nameLabel", name);
            SetField(card, "levelLabel", level);
            SetField(card, "xpBar", xp);
            return card;
        }

        /// <summary>
        /// Menu row: a chamfered plate, an accent rail on the reading side and an icon
        /// glyph. Only the one action the screen exists for gets a filled face, so the eye
        /// lands there; the rest stay dark with a lit edge.
        /// </summary>
        public static Button MenuButton(string name, Transform parent, string text, Vector2 size,
            Color accent, bool primary, Icon? glyph = null)
        {
            var rt = Root(name, parent);
            rt.sizeDelta = size;

            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = TabSprite;
            plate.type = Image.Type.Sliced;
            plate.color = primary
                ? accent
                : new Color(0.055f, 0.07f, 0.105f, 0.94f);

            Chrome(plate, accent, primary ? 0.95f : 0.42f);

            // The head is a separate sprite rather than part of the nine-slice: a point
            // cannot survive a stretch, so it is authored at one size and pinned to the
            // plate's square right edge.
            var tip = Solid("Tip", rt, plate.color, TipSprite);
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            tip.rectTransform.pivot = new Vector2(0f, 0.5f);
            tip.rectTransform.anchoredPosition = Vector2.zero;
            tip.rectTransform.sizeDelta = new Vector2(size.y * 0.42f, size.y);

            var tipRim = Solid("TipRim", rt,
                new Color(accent.r, accent.g, accent.b, primary ? 0.95f : 0.42f), TipRimSprite);
            tipRim.rectTransform.anchorMin = tipRim.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            tipRim.rectTransform.pivot = new Vector2(0f, 0.5f);
            tipRim.rectTransform.anchoredPosition = new Vector2(-3f, 0f);
            tipRim.rectTransform.sizeDelta = new Vector2(size.y * 0.42f - 3f, size.y - 6f);

            var stripe = Solid("Stripe", rt, accent, RectSprite);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.anchoredPosition = new Vector2(11f, 0f);
            stripe.rectTransform.sizeDelta = new Vector2(5f, size.y - 30f);

            var label = Label("Label", rt, text, primary ? 30 : 26,
                primary ? new Color(0.04f, 0.05f, 0.08f, 1f) : Body, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(glyph.HasValue ? 92f : 34f, 0f);
            label.rectTransform.offsetMax = new Vector2(-(size.y * 0.42f + 18f), 0f);
            AddShadow(label, new Color(0f, 0f, 0f, primary ? 0.35f : 0.7f), new Vector2(2f, -2f));

            if (glyph.HasValue)
                IconChip(plate, glyph.Value,
                    primary ? new Color(0.04f, 0.05f, 0.08f, 0.9f) : accent, 58f, 24f);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
            colors.pressedColor = new Color(0.66f, 0.66f, 0.66f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            return button;
        }

        /// <summary>
        /// Title page. New Game / Settings / Quit are persistent binds because they point
        /// at controller methods; Continue and Multiplayer are only assigned - 
        /// MainMenuController adds its own listener in Start, and binding here would
        /// fire the action twice.
        /// </summary>
        public static void BuildTitleScreen(Transform page, MainMenuController controller)
        {
            EnsureSprites();

            // The stage carries the light now, so the sheet behind it is barely a wash:
            // just enough contrast for the wordmark without flattening the gate.
            var backdrop = Solid("Backdrop", page, new Color(0.018f, 0.026f, 0.05f, 0.42f),
                VignetteSprite);
            Stretch(backdrop.rectTransform, 0f);

            BuildWordmark(backdrop.transform);
            BuildHexDock(backdrop.transform, controller);

            // Left-aligned rows: the reading edge stays a single line no matter how wide
            // each caption is, which is what keeps a column of eight from turning into a
            // stack of bricks.
            var column = Root("Column", backdrop.transform);
            // Card and menu share one left margin (44 px, the same as the wordmark's
            // right margin) and one vertical flow: CONTINUE can no longer land under
            // the operative name at any aspect ratio.
            Center(column, new Vector2(-681f, 25f));
            column.sizeDelta = new Vector2(470f, 854f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // The operative is the column's header, not an absolute overlay: the layout
            // group reserves its 88 px and places CONTINUE underneath.
            var operative = PlayerCard(column, Vector2.zero, out _, out _, true);
            ((RectTransform)operative.transform).anchoredPosition = Vector2.zero;

            // The primary action is the loudest thing on the page: same plate as the rest,
            // with a bloom behind it so the eye starts where the game wants it to.
            var continueGlow = Solid("ContinueGlow", column,
                new Color(Accent.r, Accent.g, Accent.b, 0.22f), GlowSprite);
            continueGlow.rectTransform.anchorMin = new Vector2(0f, 1f);
            continueGlow.rectTransform.anchorMax = new Vector2(0f, 1f);
            continueGlow.rectTransform.pivot = new Vector2(0.5f, 1f);
            continueGlow.rectTransform.anchoredPosition = new Vector2(225f, 26f);
            continueGlow.rectTransform.sizeDelta = new Vector2(620f, 260f);
            continueGlow.transform.SetAsFirstSibling();
            // A layout group re-positions every child except an ignored one, otherwise
            // the bloom becomes a 620 px row and pushes the menu off the page.
            continueGlow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // The bloom breathes, so the page the player lands on is lit rather than printed.
            continueGlow.gameObject.AddComponent<MenuGlowPulse>();

            var continueButton = MenuButton("Continue", column, "CONTINUE", new Vector2(470f, 88f),
                Accent, true, Icon.Play);
            SetField(controller, "continueButton", continueButton);

            // One accent down the column. Each row used to take a different hue - amber,
            // violet, green, red - and seven coloured stripes read as seven unrelated
            // systems rather than one menu. Cyan is the brand; red is reserved for QUIT.
            Bind(MenuButton("NewGame", column, "NEW GAME", new Vector2(470f, 84f), Accent, false,
                Icon.Plus), new UnityAction(controller.StartNewGame));
            Bind(MenuButton("Character", column, "OPERATIVES", new Vector2(470f, 84f), Accent,
                false, Icon.Operative), new UnityAction(controller.OpenCharacterSelect));
            // MainMenuController wires this one itself in Start (it is the only row whose
            // interactable state tracks whether the backend is reachable) - binding here
            // too would fire the transition twice.
            SetField(controller, "multiplayerButton",
                MenuButton("Multiplayer", column, "CO-OP", new Vector2(470f, 84f), Accent, false,
                    Icon.Duo));
            Bind(MenuButton("Settings", column, "SETTINGS", new Vector2(470f, 84f), Accent, false,
                Icon.Gear), new UnityAction(controller.OpenSettings));
            Bind(MenuButton("Account", column, "ACCOUNT", new Vector2(470f, 84f), Accent,
                false, Icon.Cloud), new UnityAction(controller.OpenAccount));
            Bind(MenuButton("Help", column, "HELP", new Vector2(470f, 84f), Accent, false,
                Icon.Help), new UnityAction(controller.OpenHelp));
            Bind(MenuButton("Quit", column, "QUIT", new Vector2(470f, 78f), Danger, false,
                Icon.Power), new UnityAction(controller.QuitGame));

            // Nothing may sit closer than 88 px to the top or bottom of the 1080 reference:
            // with a half-way match on a 21:9 phone the canvas is 1080 tall inside a 926 px
            // viewport, so the outer 77 px of it is simply off the screen. At 28 px the
            // level card and the hex dock were cut in half on any modern wide phone.
            var ticks = Solid("Ticks", backdrop.transform,
                new Color(Accent.r, Accent.g, Accent.b, 0.85f), TicksSprite);
            BottomLeft(ticks.rectTransform, new Vector2(44f, 88f));
            ticks.rectTransform.sizeDelta = new Vector2(72f, 26f);

            var tagline = Label("Tagline", backdrop.transform, "STEP INTO A NEW REALITY", 24,
                Body, TextAnchor.MiddleLeft);
            BottomLeft(tagline.rectTransform, new Vector2(128f, 88f));
            tagline.rectTransform.sizeDelta = new Vector2(620f, 30f);
            AddShadow(tagline, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            var stripes = Solid("Stripes", backdrop.transform,
                new Color(Accent.r, Accent.g, Accent.b, 0.6f), TicksSprite);
            BottomRight(stripes.rectTransform, new Vector2(-156f, 88f));
            stripes.rectTransform.sizeDelta = new Vector2(64f, 24f);

            var version = Label("Version", backdrop.transform,
                "v" + Application.version, 22, Subtle, TextAnchor.MiddleRight);
            BottomRight(version.rectTransform, new Vector2(-44f, 88f));
            version.rectTransform.sizeDelta = new Vector2(104f, 30f);
        }

        /// <summary>Stacked wordmark on the right third of the page. The built-in font has
        /// no metal face, so the chrome is faked the way print does it: a dark body, a lit
        /// offset copy and a cyan drop.
        ///
        /// Anchored to the right edge, not to the canvas centre. Offsetting 690 px from the
        /// centre put the right edge of the 620 px block 40 px outside a 16:9 canvas, and
        /// the built menu cut the end off "FRONTIER" - the wordmark is the brand, so it is
        /// pinned to a 44 px margin that every aspect ratio honours.</summary>
        private static void BuildWordmark(Transform parent)
        {
            var halo = Solid("Halo", parent, new Color(Accent2.r, Accent2.g, Accent2.b, 0.10f),
                GlowSprite);
            RightCenter(halo.rectTransform, new Vector2(-40f, 4f));
            halo.rectTransform.sizeDelta = new Vector2(760f, 470f);

            var top = Label("Null", parent, "NULL", 96, new Color(0.78f, 0.84f, 0.92f, 1f),
                TextAnchor.MiddleRight);
            RightCenter(top.rectTransform, new Vector2(-44f, 118f));
            top.rectTransform.sizeDelta = new Vector2(620f, 116f);
            AddShadow(top, new Color(0f, 0f, 0f, 0.9f), new Vector2(6f, -6f));

            var bottom = Label("Frontier", parent, "FRONTIER", 118, Color.white,
                TextAnchor.MiddleRight);
            RightCenter(bottom.rectTransform, new Vector2(-44f, 4f));
            bottom.rectTransform.sizeDelta = new Vector2(620f, 140f);
            AddShadow(bottom, new Color(0.05f, 0.62f, 0.85f, 0.9f), new Vector2(6f, -6f));

            var rule = Solid("Rule", parent, new Color(Accent.r, Accent.g, Accent.b, 0.8f),
                RectSprite);
            RightCenter(rule.rectTransform, new Vector2(-44f, -74f));
            rule.rectTransform.sizeDelta = new Vector2(560f, 3f);

            var sub = Label("Motto", parent, "SURVIVE   ·   EXPLORE   ·   DOMINATE", 26, Subtle,
                TextAnchor.MiddleRight);
            RightCenter(sub.rectTransform, new Vector2(-44f, -114f));
            sub.rectTransform.sizeDelta = new Vector2(620f, 40f);
        }

        /// <summary>Top-right shortcuts. Rewards and badges open the two panels the save
        /// actually backs; Settings and Exit duplicate rows that already exist because the
        /// reference puts them here too.</summary>
        private static void BuildHexDock(Transform parent, MainMenuController controller)
        {
            var dock = Root("HexDock", parent);
            TopRight(dock, new Vector2(-44f, -88f));
            dock.sizeDelta = new Vector2(462f, 118f);
            var layout = dock.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // One accent across all four: the reference keeps the dock neutral and saves
            // its red for QUIT, while green/blue/red hexes read as four different systems.
            var rewards = HexAction("Rewards", dock, "REWARDS", Icon.Gift, Accent, out var dot);
            SetField(controller, "rewardsDot", dot);
            Bind(rewards, new UnityAction(controller.OpenRewards));

            Bind(HexAction("Badges", dock, "ACHIEVEMENTS", Icon.Trophy, Accent, out _),
                new UnityAction(controller.OpenBadges));
            Bind(HexAction("Settings", dock, "SETTINGS", Icon.Gear, Accent, out _),
                new UnityAction(controller.OpenSettings));
            Bind(HexAction("Exit", dock, "EXIT", Icon.Power, Accent, out _),
                new UnityAction(controller.QuitGame));
        }

        /// <summary>Hex shortcut: lit halo, dark lens, glyph and a caption underneath. The
        /// dot is the caller's to drive - it only ever marks something real and pending.</summary>
        private static Button HexAction(string name, Transform parent, string caption, Icon glyph,
            Color accent, out Image dot)
        {
            var rt = Root(name, parent);
            rt.sizeDelta = new Vector2(108f, 118f);

            var halo = Solid("Halo", rt, new Color(accent.r, accent.g, accent.b, 0.5f), HexSprite);
            Center(halo.rectTransform, new Vector2(0f, 9f));
            halo.rectTransform.sizeDelta = new Vector2(104f, 104f);

            var plate = Solid("Plate", rt, new Color(0.06f, 0.075f, 0.115f, 0.95f), HexSprite);
            Center(plate.rectTransform, new Vector2(0f, 9f));
            plate.rectTransform.sizeDelta = new Vector2(96f, 96f);

            var mark = Solid("Glyph", rt, accent, IconSprite(glyph));
            Center(mark.rectTransform, new Vector2(0f, 11f));
            mark.rectTransform.sizeDelta = new Vector2(44f, 44f);

            // "ACHIEVEMENTS" is twelve characters in a cell 108 px wide, so long captions
            // take the smaller face rather than wrapping down onto the hex.
            var label = Label("Caption", rt, caption, caption.Length > 10 ? 12 : 15, Subtle);
            BottomLeft(label.rectTransform, new Vector2(2f, 0f));
            label.rectTransform.sizeDelta = new Vector2(104f, 22f);

            dot = Solid("Dot", rt, new Color(0.95f, 0.3f, 0.35f, 1f), CircleSprite);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1f, 1f);
            dot.rectTransform.pivot = new Vector2(1f, 1f);
            dot.rectTransform.anchoredPosition = new Vector2(-6f, -6f);
            dot.rectTransform.sizeDelta = new Vector2(22f, 22f);
            dot.gameObject.SetActive(false);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            return button;
        }

        /// <summary>Pause overlay. Resume and Save &amp; Quit call straight through.</summary>
        public static void BuildPauseScreen(Transform page, PauseMenuController controller,
            string settingsScreen)
        {
            EnsureSprites();
            var backdrop = Sheet("Backdrop", page, new Color(0f, 0f, 0f, 0.78f));

            var heading = Label("Heading", backdrop.transform, "PAUSED", 76, Color.white);
            Center(heading.rectTransform, new Vector2(0f, 210f));
            heading.rectTransform.sizeDelta = new Vector2(1000f, 100f);

            var state = Label("State", backdrop.transform, "the city holds its breath", 22, Subtle);
            Center(state.rectTransform, new Vector2(0f, 150f));
            state.rectTransform.sizeDelta = new Vector2(1000f, 40f);

            Bind(Button("Resume", backdrop.transform, "RESUME", new Vector2(0f, 60f),
                new Vector2(460f, 92f), Accent), new UnityAction(controller.Resume));
            BackButton("Settings", backdrop.transform, "SETTINGS", new Vector2(0f, -50f),
                new Vector2(460f, 84f), settingsScreen);
            Bind(Button("Quit", backdrop.transform, "SAVE & QUIT TO MENU", new Vector2(0f, -160f),
                new Vector2(520f, 84f), Danger), new UnityAction(controller.SaveAndQuitToMenu));
        }

        /// <summary>Settings page. Every control is bound; see SettingsMenuController.</summary>
        public static void BuildSettingsScreen(Transform page, SettingsMenuController controller,
            string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent, 2f);

            var heading = Label("Heading", page.transform, "SETTINGS", 60, Color.white);
            Center(heading.rectTransform, new Vector2(0f, 330f));
            heading.rectTransform.sizeDelta = new Vector2(900f, 90f);
            AddShadow(heading, new Color(0f, 0f, 0f, 0.8f), new Vector2(3f, -3f));

            var hint = Label("Hint", page.transform,
                "changes apply immediately and are stored with your progress", 20, Subtle);
            Center(hint.rectTransform, new Vector2(0f, 268f));
            hint.rectTransform.sizeDelta = new Vector2(1200f, 34f);

            float y = 190f;
            SetField(controller, "qualityCycle", CycleRow(page.transform, "QUALITY", "HIGH", ref y));
            SetField(controller, "frameRateCycle", CycleRow(page.transform, "FRAME RATE", "60 FPS", ref y));
            SetField(controller, "resolutionScale", SliderRow(page.transform, "RENDER SCALE", "Resolution", ref y));
            SetField(controller, "lookSensitivity", SliderRow(page.transform, "SENSITIVITY", "Sensitivity", ref y));
            SetField(controller, "masterVolume", SliderRow(page.transform, "MASTER", "Master", ref y));
            SetField(controller, "ambientVolume", SliderRow(page.transform, "AMBIENCE", "Ambient", ref y));
            SetField(controller, "effectsVolume", SliderRow(page.transform, "EFFECTS", "Effects", ref y));

            SetField(controller, "resetButton", Button("Reset", page.transform, "RESET DEFAULTS",
                new Vector2(140f, -330f), new Vector2(340f, 84f), Panel));
            BackButton("Back", page.transform, "BACK", new Vector2(-260f, -330f),
                new Vector2(340f, 84f), backScreen);
        }

        private static Button CycleRow(Transform page, string label, string value, ref float y)
        {
            y -= 64f;
            var caption = Label("L_" + label, page, label, 26, Body, TextAnchor.MiddleLeft);
            Center(caption.rectTransform, new Vector2(-300f, y));
            caption.rectTransform.sizeDelta = new Vector2(420f, 40f);
            return Button(label, page, value, new Vector2(320f, y), new Vector2(320f, 60f), Panel, 26);
        }

        private static Slider SliderRow(Transform page, string label, string objectName, ref float y)
        {
            y -= 64f;
            var caption = Label("L_" + label, page, label, 26, Body, TextAnchor.MiddleLeft);
            Center(caption.rectTransform, new Vector2(-300f, y));
            caption.rectTransform.sizeDelta = new Vector2(420f, 40f);

            // The root name is the key SettingsMenuController.ResetDefaults looks up.
            var bar = Bar(objectName, page, new Vector2(140f, y), new Vector2(320f, 28f), Accent, true, 0.5f);

            // SettingsMenuController reads its readout as a child Text of the slider.
            var value = Label("Value", bar.transform, string.Empty, 24, Subtle, TextAnchor.MiddleRight);
            Center(value.rectTransform, new Vector2(480f, 0f));
            value.rectTransform.sizeDelta = new Vector2(140f, 32f);
            return bar;
        }

        /// <summary>Sign-in page. AuthUI wires its own button callbacks in Start.</summary>
        public static void BuildAuthScreen(Transform page, AuthUI auth)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent, 2f);

            var heading = Label("Heading", page.transform, "CLOUD ACCOUNT", 54, Color.white);
            Center(heading.rectTransform, new Vector2(0f, 300f));
            heading.rectTransform.sizeDelta = new Vector2(900f, 80f);

            var hint = Label("Hint", page.transform,
                "sign in to switch on cloud saves and leaderboards", 20, Subtle);
            Center(hint.rectTransform, new Vector2(0f, 244f));
            hint.rectTransform.sizeDelta = new Vector2(1000f, 34f);

            SetField(auth, "emailInput", Input("Email", page.transform,
                new Vector2(0f, 150f), new Vector2(560f, 66f), "email", false));
            SetField(auth, "passwordInput", Input("Password", page.transform,
                new Vector2(0f, 66f), new Vector2(560f, 66f), "password", true));
            SetField(auth, "usernameInput", Input("Username", page.transform,
                new Vector2(0f, -18f), new Vector2(560f, 66f), "username (optional)", false));

            var status = Label("Status", page.transform, string.Empty, 24, Accent);
            Center(status.rectTransform, new Vector2(0f, -104f));
            status.rectTransform.sizeDelta = new Vector2(900f, 40f);
            SetField(auth, "statusLabel", status);

            SetField(auth, "signInButton", Button("SignIn", page.transform, "SIGN IN",
                new Vector2(-160f, -184f), new Vector2(300f, 76f), Accent));
            SetField(auth, "signUpButton", Button("SignUp", page.transform, "SIGN UP",
                new Vector2(160f, -184f), new Vector2(300f, 76f), Accent2));

            // ScreenManager owns visibility, so leaving is a screen swap rather than a
            // SetActive(false) that would leave nothing on screen.
            BackButton("Back", page.transform, "BACK", new Vector2(0f, -292f),
                new Vector2(300f, 68f), "Title");
        }

        /// <summary>
        /// Scrollable roster on the left, dossier on the right, and the middle kept empty
        /// so the live 3D operative on the turntable is the centre of the screen. The card
        /// template keeps activeSelf true while its root is deactivated, so Instantiate
        /// produces visible clones.
        /// </summary>
        public static void BuildCharacterSelect(Transform page, CharacterSelectUI select,
            string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent, 2f);

            var templates = Root("Templates", page);
            Center(templates, Vector2.zero);
            templates.sizeDelta = Vector2.zero;
            var card = RosterCard(templates);
            templates.gameObject.SetActive(false);

            var heading = Label("Heading", page, "CHOOSE YOUR OPERATIVE", 34, Color.white,
                TextAnchor.MiddleCenter);
            Center(heading.rectTransform, new Vector2(0f, 430f));
            heading.rectTransform.sizeDelta = new Vector2(900f, 46f);
            AddShadow(heading, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            var headingRule = Solid("HeadingRule", page, Accent, RectSprite);
            Center(headingRule.rectTransform, new Vector2(0f, 396f));
            headingRule.rectTransform.sizeDelta = new Vector2(320f, 2f);

            // Six operatives, so the roster is a fixed grid: a scroll view here only
            // steals drags from the turntable behind it.
            var content = RosterGrid(page);
            Center(content, new Vector2(-654f, -12f));

            // The page backdrop is a vignette, so this caption is the only text that ever
            // sits directly over the character.
            var caption = Label("Caption", page, "NO OPERATIVE SELECTED", 30, Subtle);
            Center(caption.rectTransform, new Vector2(0f, -444f));
            caption.rectTransform.sizeDelta = new Vector2(620f, 46f);
            AddShadow(caption, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -3f));

            // The dossier is 460 tall: name, role, brief, three meters, the operative's
            // real protocol and the deploy button. Every row is data the save actually
            // carries - nothing here is decoration standing in for a system.
            var detail = Plated("Detail", page, Panel, Accent);
            TopRight(detail.rectTransform, new Vector2(-36f, -96f));
            detail.rectTransform.sizeDelta = new Vector2(460f, 460f);

            // The operative's name is the heading; a separate "DOSSIER" caption over it
            // only pushed every row down into the button.
            SetField(select, "detailName", BoxedLabel(detail.transform, "Name", 32, Color.white,
                TextAnchor.UpperLeft, new Vector2(20f, -14f), new Vector2(420f, 40f)));
            SetField(select, "detailRole", BoxedLabel(detail.transform, "Role", 20, Accent,
                TextAnchor.UpperLeft, new Vector2(20f, -56f), new Vector2(420f, 26f)));
            SetField(select, "detailDescription", BoxedLabel(detail.transform, "Description", 19,
                Body, TextAnchor.UpperLeft, new Vector2(20f, -86f), new Vector2(420f, 64f)));

            // Stats as bars, not as columns of monospace-ish text: the legacy font is not
            // fixed-width, so space-aligned numbers never line up. The lengths are the
            // real numbers, normalised across the roster, and each keeps its own readout.
            DossierBar(detail.transform, "VITALITY", 0, Health, out var vitality, out var vitalityValue);
            DossierBar(detail.transform, "SPEED", 1, Accent, out var mobility, out var mobilityValue);
            DossierBar(detail.transform, "POWER", 2, Stamina, out var firepower, out var firepowerValue);
            SetField(select, "vitalityBar", vitality);
            SetField(select, "mobilityBar", mobility);
            SetField(select, "firepowerBar", firepower);
            SetField(select, "vitalityValue", vitalityValue);
            SetField(select, "mobilityValue", mobilityValue);
            SetField(select, "firepowerValue", firepowerValue);

            // One hex chip per operative: CharacterDefinition carries exactly one
            // AbilityId, so the panel shows that one rather than inventing a loadout.
            var abilities = Label("AbilityHeading", detail.transform, "SPECIAL ABILITIES", 15,
                Subtle, TextAnchor.MiddleLeft);
            TopLeft(abilities.rectTransform, new Vector2(20f, -282f));
            abilities.rectTransform.sizeDelta = new Vector2(420f, 20f);

            var chipRim = Solid("AbilityRim", detail.transform,
                new Color(Accent.r, Accent.g, Accent.b, 0.8f), HexSprite);
            TopLeft(chipRim.rectTransform, new Vector2(20f, -306f));
            chipRim.rectTransform.sizeDelta = new Vector2(64f, 64f);

            var chip = Solid("AbilityChip", detail.transform,
                new Color(0.03f, 0.05f, 0.085f, 0.9f), HexSprite);
            TopLeft(chip.rectTransform, new Vector2(26f, -312f));
            chip.rectTransform.sizeDelta = new Vector2(52f, 52f);

            var chipMark = Solid("AbilityGlyph", chip.transform, Accent, IconSprite(Icon.Bolt));
            Stretch(chipMark.rectTransform, 14f);

            SetField(select, "detailStats", BoxedLabel(detail.transform, "AbilityName", 21,
                Color.white, TextAnchor.UpperLeft, new Vector2(100f, -316f), new Vector2(340f, 44f)));
            SetField(select, "caption", caption);

            SetField(select, "cardContainer", content);
            SetField(select, "cardPrefab", card);

            // CONFIRM lives inside the dossier, pinned to its bottom edge, so the panel and
            // its button can never drift apart or overlap on another aspect ratio.
            var confirm = Button("Deploy", detail.transform, "CONFIRM", new Vector2(0f, -188f),
                new Vector2(420f, 64f), Stamina, 26);
            var confirmLabel = confirm.GetComponentInChildren<Text>();
            confirmLabel.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            confirmLabel.alignment = TextAnchor.MiddleCenter;
            var go = Solid("Chevron", confirm.transform,
                new Color(0.05f, 0.06f, 0.09f, 0.85f), IconSprite(Icon.Chevron));
            go.rectTransform.anchorMin = go.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            go.rectTransform.pivot = new Vector2(1f, 0.5f);
            go.rectTransform.sizeDelta = new Vector2(38f, 38f);
            go.rectTransform.anchoredPosition = new Vector2(-26f, 0f);
            SetField(select, "confirmButton", confirm);

            BackButton("Back", page, "BACK", new Vector2(-794f, 406f),
                new Vector2(240f, 76f), backScreen);
        }

        /// <summary>Portrait-first roster tile. Child names are the runtime contract.</summary>
        private static Button RosterCard(Transform templates)
        {
            var rt = Root("Card", templates);
            Center(rt, Vector2.zero);
            rt.sizeDelta = new Vector2(176f, 216f);

            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = PlateSprite;
            plate.type = Image.Type.Sliced;
            plate.color = Panel;
            // The runtime paints its own selection state onto this graphic, so the
            // built-in tint transition is switched off to keep the two from fighting.
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;

            var scrim = Solid("Scrim", rt, PanelSoft, PlateSprite);
            scrim.type = Image.Type.Sliced;
            Stretch(scrim.rectTransform, 4f);

            var portrait = Solid("Portrait", rt, new Color(0.13f, 0.15f, 0.2f, 1f));
            portrait.sprite = null;
            TopLeft(portrait.rectTransform, new Vector2(12f, -12f));
            portrait.rectTransform.sizeDelta = new Vector2(152f, 138f);

            // The name band gets its own darker plate so caption text never sits on the
            // portrait pixels behind it.
            var band = Solid("Band", rt, new Color(0.02f, 0.03f, 0.05f, 0.82f), PlateSprite);
            band.type = Image.Type.Sliced;
            BottomLeft(band.rectTransform, new Vector2(8f, 8f));
            band.rectTransform.sizeDelta = new Vector2(160f, 62f);
            band.raycastTarget = false;

            var name = Label("Label", rt, string.Empty, 21, Color.white);
            TopLeft(name.rectTransform, new Vector2(0f, -154f));
            name.rectTransform.sizeDelta = new Vector2(176f, 26f);

            var role = Label("Role", rt, string.Empty, 15, Accent);
            TopLeft(role.rectTransform, new Vector2(0f, -180f));
            role.rectTransform.sizeDelta = new Vector2(176f, 20f);

            var status = Label("Status", rt, string.Empty, 13, Subtle);
            TopLeft(status.rectTransform, new Vector2(0f, -198f));
            status.rectTransform.sizeDelta = new Vector2(176f, 16f);

            var lockBadge = Label("Lock", rt, "LOCKED", 18, Danger);
            Center(lockBadge.rectTransform, new Vector2(0f, 46f));
            lockBadge.rectTransform.sizeDelta = new Vector2(150f, 26f);
            lockBadge.enabled = false;

            // Selection state: a lit rim plus a corner tab, both switched on only for the
            // operative the player actually picked. Named children are the contract.
            var edge = Solid("Rim", rt, Color.white, RimCutSprite);
            edge.type = Image.Type.Sliced;
            Stretch(edge.rectTransform, -1f);

            var halo = Solid("Halo", rt, new Color(Stamina.r, Stamina.g, Stamina.b, 0.95f),
                RimCutSprite);
            halo.type = Image.Type.Sliced;
            Stretch(halo.rectTransform, -3f);
            halo.gameObject.SetActive(false);

            // A flat tag, not a chamfered plate: the nine-slice border is 22 px and this tag
            // is 30 px tall, so a sliced corner would collapse onto itself and smear.
            var tab = Solid("Tab", rt, Stamina, RectSprite);
            tab.type = Image.Type.Sliced;
            tab.rectTransform.anchorMin = tab.rectTransform.anchorMax = tab.rectTransform.pivot
                = new Vector2(1f, 1f);
            tab.rectTransform.anchoredPosition = new Vector2(-6f, 6f);
            tab.rectTransform.sizeDelta = new Vector2(86f, 30f);
            var tabLabel = Label("TabText", tab.transform, "SELECTED", 13,
                new Color(0.05f, 0.06f, 0.09f, 1f));
            Stretch(tabLabel.rectTransform, 2f);
            tab.gameObject.SetActive(false);

            return button;
        }

        /// <summary>Fixed 3 x 2 grid that the roster is instantiated into.</summary>
        private static RectTransform RosterGrid(Transform page)
        {
            var grid = Root("Roster", page);
            grid.sizeDelta = new Vector2(564f, 456f);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(176f, 216f);
            layout.spacing = new Vector2(16f, 16f);
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperCenter;
            return grid;
        }

        /// <summary>
        /// Contract board: wide tappable rows on the left, live objective progress and the
        /// accept/abandon controls on the right. Same hidden-template trick as the roster so
        /// the screen stays fully script-built.
        /// </summary>
        /// <summary>
        /// Badge wall and supply outbox. One page builder, one row template, one runtime
        /// component: the wall shows every badge with its live count, the outbox shows only
        /// what has been earned and not yet taken, and taking it moves a real item into the
        /// inventory the HUD's kit tray already reads.
        /// </summary>
        public static void BuildAchievementBoard(Transform page, AchievementBoardUI board,
            string backScreen, bool rewards)
        {
            EnsureSprites();
            Color tint = rewards ? Stamina : Accent;
            Frame(page.GetComponent<RectTransform>(), tint, 2f);

            var heading = Label("Heading", page, rewards ? "SUPPLY OUTBOX" : "BADGE WALL", 54,
                Color.white, TextAnchor.MiddleLeft);
            Center(heading.rectTransform, new Vector2(-420f, 402f));
            heading.rectTransform.sizeDelta = new Vector2(900f, 64f);

            var summary = Label("Summary", page, string.Empty, 22, Subtle, TextAnchor.MiddleLeft);
            Center(summary.rectTransform, new Vector2(-420f, 348f));
            summary.rectTransform.sizeDelta = new Vector2(900f, 32f);

            var templates = Root("Templates", page);
            Center(templates, Vector2.zero);
            templates.sizeDelta = Vector2.zero;
            var row = BadgeRow(templates);
            templates.gameObject.SetActive(false);

            var list = Root("List", page);
            Center(list, new Vector2(-420f, -46f));
            list.sizeDelta = new Vector2(880f, 720f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var empty = Label("Empty", page, string.Empty, 24, Subtle);
            Center(empty.rectTransform, new Vector2(-420f, -60f));
            empty.rectTransform.sizeDelta = new Vector2(820f, 60f);
            empty.gameObject.SetActive(false);

            SetField(board, "rowContainer", list);
            SetField(board, "rowPrefab", row);
            SetField(board, "summary", summary);
            SetField(board, "emptyLabel", empty);

            BackButton("Back", page, "BACK", new Vector2(-760f, -424f),
                new Vector2(240f, 92f), backScreen);
        }

        /// <summary>One badge line: title and blurb on the reading side, the count meter
        /// and the payout on the right, and a claim key that only the outbox switches on.</summary>
        private static Button BadgeRow(Transform templates)
        {
            var rt = Root("Row", templates);
            rt.sizeDelta = new Vector2(880f, 76f);

            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = PlateSprite;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(0.055f, 0.07f, 0.105f, 0.94f);
            Chrome(plate, Accent, 0.3f);

            var title = Label("Title", rt, "BADGE", 24, Color.white, TextAnchor.MiddleLeft);
            TopLeft(title.rectTransform, new Vector2(30f, -12f));
            title.rectTransform.sizeDelta = new Vector2(400f, 30f);

            var blurb = Label("Blurb", rt, string.Empty, 18, Subtle, TextAnchor.MiddleLeft);
            TopLeft(blurb.rectTransform, new Vector2(30f, -44f));
            blurb.rectTransform.sizeDelta = new Vector2(430f, 24f);

            var bar = Bar("Bar", rt, Vector2.zero, Vector2.zero, Accent, false, 0f);
            var barRt = bar.transform as RectTransform;
            barRt.anchorMin = barRt.anchorMax = barRt.pivot = new Vector2(0f, 1f);
            barRt.anchoredPosition = new Vector2(470f, -48f);
            barRt.sizeDelta = new Vector2(190f, 14f);

            var state = Label("State", rt, string.Empty, 20, Body, TextAnchor.MiddleRight);
            TopLeft(state.rectTransform, new Vector2(660f, -14f));
            state.rectTransform.sizeDelta = new Vector2(190f, 28f);

            var reward = Label("Reward", rt, string.Empty, 18, Stamina, TextAnchor.MiddleRight);
            TopLeft(reward.rectTransform, new Vector2(660f, -44f));
            reward.rectTransform.sizeDelta = new Vector2(190f, 24f);

            var claim = Button("Claim", rt, "CLAIM", new Vector2(770f, 0f),
                new Vector2(150f, 52f), Stamina, 20);
            claim.gameObject.SetActive(false);
            return rt.gameObject.AddComponent<Button>();
        }

        /// <summary>
        /// Control sheet. Every line here names an action the shipped input map actually
        /// has, because a help page that describes a control the build does not implement is
        /// worse than no page at all.
        /// </summary>
        public static void BuildHelpScreen(Transform page, string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Shield, 2f);

            var heading = Label("Heading", page, "FIELD MANUAL", 54, Color.white);
            Center(heading.rectTransform, new Vector2(0f, 404f));
            heading.rectTransform.sizeDelta = new Vector2(1200f, 64f);

            var note = Label("Note", page,
                "touch layout on a phone - the same actions on a keyboard or controller in brackets",
                20, Subtle);
            Center(note.rectTransform, new Vector2(0f, 350f));
            note.rectTransform.sizeDelta = new Vector2(1400f, 30f);

            HelpColumn(page, -620f, "MOVE AND LOOK");
            HelpLine(page, -620f, 0, Icon.Plus, "Left stick or W A S D walks; the operative turns into the stick");
            HelpLine(page, -620f, 1, Icon.Chevron, "Drag the right side of the screen, or the mouse, to orbit");
            HelpLine(page, -620f, 2, Icon.Jump, "JUMP lifts off the deck [Space]");
            HelpLine(page, -620f, 3, Icon.Run, "RUN sprints and drains stamina [Shift]");
            HelpLine(page, -620f, 4, Icon.Down, "DROP crouches under cover [Ctrl]");

            HelpColumn(page, 0f, "COMBAT");
            HelpLine(page, 0f, 0, Icon.Sword, "FIRE, held - the key counts the magazine down [Left mouse]");
            HelpLine(page, 0f, 1, Icon.Aim, "AIM narrows the field of view [Right mouse]");
            HelpLine(page, 0f, 2, Icon.Reload, "RELOAD refills it [R]");
            HelpLine(page, 0f, 3, Icon.Bolt, "PROTOCOL fires this operative's ability [Q]");
            HelpLine(page, 0f, 4, Icon.Chevron, "DODGE rolls with brief invulnerability, costs stamina [Alt]");

            HelpColumn(page, 620f, "SURVIVE");
            HelpLine(page, 620f, 0, Icon.Interact, "USE opens doors, caches and terminals [E]");
            HelpLine(page, 620f, 1, Icon.Med, "The kit tray counts MED GEL, SHIELD CELL and STIM you carry");
            HelpLine(page, 620f, 2, Icon.Gear, "PAUSE opens CONTRACTS and SUPPLY [Escape]");
            HelpLine(page, 620f, 3, Icon.Trophy, "Badges on the title screen pay out supplies to claim");
            HelpLine(page, 620f, 4, Icon.Cloud, "CO-OP needs the backend reachable and you signed in");

            BackButton("Back", page, "BACK", new Vector2(-760f, -424f),
                new Vector2(240f, 92f), backScreen);
        }

        private static void HelpColumn(Transform page, float x, string title)
        {
            var caption = Label("H_" + title, page, title, 26, Accent, TextAnchor.MiddleLeft);
            TopLeft(caption.rectTransform, new Vector2(x - 280f, -400f));
            caption.rectTransform.sizeDelta = new Vector2(560f, 34f);

            var rule = Solid("Rule_" + title, page, new Color(Accent.r, Accent.g, Accent.b, 0.4f),
                RectSprite);
            TopLeft(rule.rectTransform, new Vector2(x - 280f, -436f));
            rule.rectTransform.sizeDelta = new Vector2(560f, 2f);
        }

        private static void HelpLine(Transform page, float x, int index, Icon glyph, string text)
        {
            float y = -452f - index * 88f;

            var mark = Solid("Glyph", page, Subtle, IconSprite(glyph));
            TopLeft(mark.rectTransform, new Vector2(x - 278f, y));
            mark.rectTransform.sizeDelta = new Vector2(40f, 40f);

            var body = Label("Body", page, text, 19, Body, TextAnchor.UpperLeft);
            TopLeft(body.rectTransform, new Vector2(x - 226f, y));
            body.rectTransform.sizeDelta = new Vector2(500f, 84f);
        }

        public static void BuildMissionBoardScreen(Transform page, MissionBoardUI board,
            string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent2, 2f);

            var heading = Label("Heading", page, "FIELD CONTRACTS", 46, Color.white);
            Center(heading.rectTransform, new Vector2(-430f, 400f));
            heading.rectTransform.sizeDelta = new Vector2(900f, 60f);

            var templates = Root("Templates", page);
            Center(templates, Vector2.zero);
            templates.sizeDelta = Vector2.zero;
            var card = Button("Card", templates, "CONTRACT", Vector2.zero,
                new Vector2(880f, 104f), Panel, 26);
            templates.gameObject.SetActive(false);

            var list = Root("List", page);
            Center(list, new Vector2(-430f, -40f));
            list.sizeDelta = new Vector2(880f, 660f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var detail = Solid("Detail", page, PanelSoft);
            TopRight(detail.rectTransform, new Vector2(-30f, -120f));
            detail.rectTransform.sizeDelta = new Vector2(470f, 620f);

            SetField(board, "detailName", BoxedLabel(detail.transform, "Name", 32, Color.white,
                TextAnchor.UpperLeft, new Vector2(18f, -16f), new Vector2(434f, 44f)));
            SetField(board, "detailDescription", BoxedLabel(detail.transform, "Description", 22,
                Body, TextAnchor.UpperLeft, new Vector2(18f, -70f), new Vector2(434f, 120f)));
            SetField(board, "detailObjectives", BoxedLabel(detail.transform, "Objectives", 22,
                Subtle, TextAnchor.UpperLeft, new Vector2(18f, -200f), new Vector2(434f, 330f)));

            SetField(board, "cardContainer", list);
            SetField(board, "cardPrefab", card);
            SetField(board, "acceptButton", Button("Accept", page, "ACCEPT",
                new Vector2(660f, -340f), new Vector2(340f, 92f), Accent));
            SetField(board, "abandonButton", Button("Abandon", page, "ABANDON",
                new Vector2(660f, -430f), new Vector2(340f, 76f), Danger, 24));

            BackButton("Back", page, "BACK", new Vector2(-760f, -424f),
                new Vector2(240f, 92f), backScreen);
        }

        /// <summary>
        /// Consumables drawer plus skill-point spending. Both halves bind SupplyPanel:
        /// the count labels double as the item caption, and every button is disabled
        /// until there is something to spend.
        /// </summary>
        public static void BuildSupplyScreen(Transform page, SupplyPanel panel, string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent2, 2f);

            var supplyHeading = Label("SupplyHeading", page, "SUPPLY", 40, Color.white,
                TextAnchor.MiddleLeft);
            Center(supplyHeading.rectTransform, new Vector2(-490f, 340f));
            supplyHeading.rectTransform.sizeDelta = new Vector2(780f, 52f);
            AddShadow(supplyHeading, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            var counts = new Text[3];
            var uses = new Button[3];
            float rowY = 230f;
            for (int i = 0; i < counts.Length; i++)
            {
                SupplyRow(page, new Vector2(-490f, rowY), out counts[i], out uses[i]);
                rowY -= 104f;
            }

            var trainingHeading = Label("TrainingHeading", page, "TRAINING", 40, Color.white,
                TextAnchor.MiddleLeft);
            Center(trainingHeading.rectTransform, new Vector2(490f, 340f));
            trainingHeading.rectTransform.sizeDelta = new Vector2(780f, 52f);
            AddShadow(trainingHeading, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));

            var points = Label("SkillPoints", page, "SKILL POINTS", 24, Accent2, TextAnchor.MiddleLeft);
            Center(points.rectTransform, new Vector2(490f, 282f));
            points.rectTransform.sizeDelta = new Vector2(780f, 40f);

            string[] captions =
            {
                "VITALITY   +5% MAX HEALTH",
                "ENDURANCE   +5% MAX STAMINA",
                "FIREPOWER   +5% WEAPON DAMAGE",
                "MOBILITY   +5% MOVE SPEED",
                "SHIELDING   +5% DAMAGE ABSORBED"
            };
            var skills = new Button[captions.Length];
            float skillY = 200f;
            for (int i = 0; i < skills.Length; i++)
            {
                skills[i] = Button($"Skill{i}", page, captions[i], new Vector2(490f, skillY),
                    new Vector2(780f, 76f), Panel, 24);
                skillY -= 88f;
            }

            SetField(panel, "consumableCounts", counts);
            SetField(panel, "consumableUses", uses);
            SetField(panel, "skillPointsLabel", points);
            SetField(panel, "skillButtons", skills);

            BackButton("Back", page, "BACK", new Vector2(-760f, -424f),
                new Vector2(240f, 92f), backScreen);
        }

        private static void SupplyRow(Transform page, Vector2 anchored, out Text value, out Button use)
        {
            var row = Solid("Row", page, Panel);
            Center(row.rectTransform, anchored);
            row.rectTransform.sizeDelta = new Vector2(780f, 88f);

            value = Label("Value", row.transform, string.Empty, 26, Body, TextAnchor.MiddleLeft);
            TopLeft(value.rectTransform, new Vector2(24f, -24f));
            value.rectTransform.sizeDelta = new Vector2(480f, 40f);

            use = Button("Use", row.transform, "USE", new Vector2(250f, 0f),
                new Vector2(240f, 64f), Accent, 24);
        }

        private static Text BoxedLabel(Transform parent, string name, int size, Color color,
            TextAnchor anchor, Vector2 topLeft, Vector2 area)
        {
            var label = Label(name, parent, string.Empty, size, color, anchor);
            TopLeft(label.rectTransform, topLeft);
            label.rectTransform.sizeDelta = area;
            return label;
        }

        /// <summary>Co-op page: exists so the menu button leads somewhere honest.</summary>
        public static void BuildMultiplayerScreen(Transform page, string backScreen)
        {
            EnsureSprites();
            Frame(page.GetComponent<RectTransform>(), Accent2, 2f);

            var heading = Label("Heading", page, "COOPERATIVE", 54, Color.white);
            Center(heading.rectTransform, new Vector2(0f, 180f));
            heading.rectTransform.sizeDelta = new Vector2(1000f, 80f);

            var body = Label("Body", page,
                "Co-op runs through the game backend: host it, sign in from the account " +
                "screen, then invite from here.", 26, Body);
            Center(body.rectTransform, new Vector2(0f, 20f));
            body.rectTransform.sizeDelta = new Vector2(1100f, 140f);

            BackButton("Back", page, "BACK", new Vector2(0f, -200f),
                new Vector2(320f, 84f), backScreen);
        }
    }
}
