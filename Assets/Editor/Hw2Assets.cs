using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HW2.EditorTools
{
    /// <summary>
    /// Builds every material and procedural texture the game needs and caches them as assets,
    /// so the whole night look is reproducible from code (no hand made art required).
    /// </summary>
    public static class Hw2Assets
    {
        public const string MaterialFolder = "Assets/Materials";
        public const string TextureFolder = "Assets/Materials/Textures";

        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";
        const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        const string SkyShader = "Skybox/Procedural";

        static readonly Dictionary<string, Material> materials = new();
        static readonly Dictionary<UiShape, Sprite> ui = new();

        /// <summary>
        /// Sprite for a UI shape, or null for <see cref="UiShape.Quad"/>, which is drawn by a
        /// borderless Image that simply fills its rect.
        /// </summary>
        public static Sprite Ui(UiShape shape)
        {
            if (shape == UiShape.Quad)
            {
                return null;
            }

            if (!ui.TryGetValue(shape, out Sprite sprite) || sprite == null)
            {
                Debug.LogError($"UI sprite for shape '{shape}' was never generated. Run BuildAll first.");
            }

            return sprite;
        }

        /// <summary>Borderless quad kept as the fill primitive for any gauge that wants fillAmount.</summary>
        public static Sprite Bar { get; private set; }

        // ---------------------------------------------------------------- palette

        static readonly Color Asphalt = new(0.045f, 0.052f, 0.07f);
        static readonly Color Shoulder = new(0.06f, 0.065f, 0.085f);
        static readonly Color Concrete = new(0.085f, 0.09f, 0.11f);
        static readonly Color Steel = new(0.34f, 0.37f, 0.42f);
        static readonly Color Pole = new(0.11f, 0.12f, 0.15f);
        static readonly Color Building = new(0.028f, 0.033f, 0.055f);
        static readonly Color PlayerPaint = new(0.075f, 0.09f, 0.34f);
        static readonly Color StaticPaint = new(0.28f, 0.05f, 0.06f);
        static readonly Color MovingPaint = new(0.05f, 0.19f, 0.24f);
        static readonly Color TruckPaint = new(0.1f, 0.1f, 0.13f);
        static readonly Color Glass = new(0.012f, 0.016f, 0.03f);
        static readonly Color Tire = new(0.03f, 0.03f, 0.036f);
        static readonly Color Rim = new(0.55f, 0.58f, 0.64f);
        static readonly Color Neon = new(0.15f, 1f, 0.95f);
        static readonly Color CoinGold = new(1f, 0.72f, 0.16f);
        static readonly Color FuelGreen = new(0.22f, 1f, 0.48f);
        static readonly Color LampWarm = new(1f, 0.72f, 0.36f);
        static readonly Color Headlight = new(1f, 0.96f, 0.85f);
        static readonly Color Taillight = new(1f, 0.12f, 0.08f);
        static readonly Color LanePaint = new(1f, 0.93f, 0.66f);
        static readonly Color Dirt = new(0.022f, 0.024f, 0.038f);

        // ---------------------------------------------------------------- entry point

        public static void BuildAll()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);

            Texture2D asphalt = AsphaltTexture();
            Texture2D windows = WindowTexture(0);
            Texture2D windowsB = WindowTexture(1);
            Texture2D windowsC = WindowTexture(2);
            Texture2D sky = SkyTexture();
            Texture2D soft = SoftDotTexture();

            // Road furniture.
            Lit("M_Asphalt", Asphalt, 0f, 0.64f, null, asphalt, new Vector2(6f, 22f));
            Lit("M_Shoulder", Shoulder, 0f, 0.35f);
            Lit("M_Concrete", Concrete, 0f, 0.42f);
            Lit("M_Dirt", Dirt, 0f, 0.12f);
            Lit("M_Guardrail", Steel, 0.85f, 0.58f);
            Lit("M_Pole", Pole, 0.6f, 0.45f);
            Lit("M_Tire", Tire, 0f, 0.28f);
            Lit("M_Rim", Rim, 0.9f, 0.62f);
            Lit("M_Glass", Glass, 0.85f, 0.92f);

            Lit("M_BuildingA", Building, 0.1f, 0.3f, windows, windows, new Vector2(2f, 6f), LampWarm * 1.35f);
            Lit("M_BuildingB", Building, 0.1f, 0.3f, windowsB, windowsB, new Vector2(3f, 9f), LampWarm * 1.15f);
            Lit("M_BuildingC", Building, 0.1f, 0.3f, windowsC, windowsC, new Vector2(2f, 11f), new Color(0.6f, 0.8f, 1f) * 1.2f);

            // Car bodies (the player paints are tinted at runtime through a property block,
            // so their authored colour doubles as the resting colour).
            Lit("M_CarPlayer", PlayerPaint, 0.55f, 0.82f);
            Lit("M_CarStatic", StaticPaint, 0.4f, 0.66f);
            Lit("M_CarMoving", MovingPaint, 0.4f, 0.72f);
            Lit("M_Truck", TruckPaint, 0.35f, 0.5f);

            // Emissive details.
            Lit("M_Neon", new Color(0.01f, 0.06f, 0.06f), 0f, 0.4f, null, null, Vector2.one, Neon * 4.2f);
            Lit("M_Lamp", new Color(0.1f, 0.08f, 0.05f), 0f, 0.3f, null, null, Vector2.one, LampWarm * 5.5f);
            Lit("M_Headlight", new Color(0.2f, 0.19f, 0.16f), 0f, 0.7f, null, null, Vector2.one, Headlight * 3.4f);
            Lit("M_Taillight", new Color(0.16f, 0.02f, 0.02f), 0f, 0.5f, null, null, Vector2.one, Taillight * 2.6f);
            Lit("M_Coin", new Color(0.55f, 0.36f, 0.06f), 0.85f, 0.75f, null, null, Vector2.one, CoinGold * 3.6f);
            Lit("M_FuelCan", new Color(0.03f, 0.16f, 0.08f), 0.2f, 0.45f, null, null, Vector2.one, FuelGreen * 2.4f);

            // Unlit marks and sky.
            Unlit("U_LaneDash", LanePaint * 1.25f);
            Unlit("U_EdgeLine", new Color(0.95f, 0.96f, 1f) * 1.1f);
            Unlit("U_Sky", Color.white, sky);
            ProceduralSky();

            // Particle materials.
            Particle("P_Spark", Color.white, true);
            Particle("P_Smoke", new Color(0.5f, 0.52f, 0.56f, 1f), false);
            Particle("P_Glow", Color.white, true, soft);

            BuildUiSprites();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- material helpers

        static Material Load(string name, string shaderName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader.name != shaderName)
            {
                Object.DestroyImmediate(mat);
                AssetDatabase.DeleteAsset(path);
                mat = null;
            }

            if (mat == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"Shader '{shaderName}' is not available.");
                    return null;
                }

                mat = new Material(shader) { name = name, hideFlags = HideFlags.None };
                AssetDatabase.CreateAsset(mat, path);
            }

            materials[name] = mat;
            return mat;
        }

        static Material Lit(
            string name, Color color, float metallic, float smoothness,
            Texture2D emissiveMap = null, Texture2D baseMap = null,
            Vector2? baseTiling = null, Color? emission = null)
        {
            Material mat = Load(name, LitShader);
            if (mat == null)
            {
                return null;
            }

            mat.color = color;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetVector("_BaseMap_ST", ToVector(baseTiling ?? Vector2.one, Vector2.zero));
            }

            if (emissiveMap != null)
            {
                mat.SetTexture("_EmissionMap", emissiveMap);
            }

            Color glow = emission ?? Color.black;
            mat.SetColor("_EmissionColor", glow);
            if (Mathf.Max(glow.r, glow.g, glow.b) > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Unlit(string name, Color color, Texture2D baseMap = null)
        {
            Material mat = Load(name, UnlitShader);
            if (mat == null)
            {
                return null;
            }

            mat.color = color;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cull", 0f);

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetColor("_BaseColor", Color.white);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Particle material; additive ones skip the depth write and the alpha blend.</summary>
        static Material Particle(string name, Color color, bool additive, Texture2D baseMap = null)
        {
            Material mat = Load(name, ParticleShader);
            if (mat == null)
            {
                return null;
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 1f : 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat(
                "_SrcBlend",
                (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            mat.SetFloat(
                "_DstBlend",
                (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
            mat.SetFloat("_QueueOffset", 0f);
            mat.renderQueue = 3000;

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Night sky under the star dome: a very thin atmosphere so the horizon keeps a faint
        /// city glow while the zenith stays close to black. Also drives the box reflections.
        /// </summary>
        static Material ProceduralSky()
        {
            Material mat = Load("M_SkyProcedural", SkyShader);
            if (mat == null)
            {
                return null;
            }

            mat.SetFloat("_SunDisk", 0f);
            mat.SetFloat("_SunSize", 0.04f);
            mat.SetFloat("_SunSizeConvergence", 5f);
            mat.SetFloat("_AtmosphereThickness", 0.08f);
            mat.SetFloat("_Exposure", 1.15f);
            mat.SetColor("_SkyTint", new Color(0.16f, 0.2f, 0.42f));
            mat.SetColor("_GroundColor", new Color(0.014f, 0.016f, 0.03f));

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Get(string name)
        {
            if (materials.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"Material '{name}' has not been built yet.");
            }

            return mat;
        }

        static Vector4 ToVector(Vector2 tiling, Vector2 offset) =>
            new(tiling.x, tiling.y, offset.x, offset.y);

        // ---------------------------------------------------------------- textures

        static Texture2D Make(string name, int width, int height, bool repeat, Color[] pixels, bool mips)
        {
            // The mipmap chain is left to the importer: Apply(true, ...) releases the CPU side of
            // the texture, which would make the PNG encode below fail.
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false, false)
            {
                name = name,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string path = $"{TextureFolder}/{name}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            System.IO.File.WriteAllBytes(System.IO.Path.Combine(GetProjectPath(), path), png);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = mips;
            importer.sRGBTexture = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string GetProjectPath()
        {
            string data = Application.dataPath;
            return data.Substring(0, data.Length - "Assets".Length);
        }

        static Color[] NewGrid(int width, int height) => new Color[width * height];

        static Texture2D AsphaltTexture()
        {
            const int size = 256;
            Color[] pixels = NewGrid(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = Hash(x, y) * 0.1f - 0.05f;
                    float patch = Hash(x / 8, y / 8) * 0.14f - 0.07f;
                    float wet = Hash(x / 40, y / 6) * 0.12f;
                    float v = 0.055f + grain + patch;
                    pixels[y * size + x] = new Color(v * 0.9f, v, v * 1.25f + wet, 1f);
                }
            }

            return Make("T_Asphalt", size, size, true, pixels, true);
        }

        /// <summary>Faded window grid used both as the building albedo and its night emission.</summary>
        static Texture2D WindowTexture(int variant)
        {
            const int width = 64;
            const int height = 128;
            Color[] pixels = NewGrid(width, height);

            Color dark = new(0.02f, 0.025f, 0.04f, 1f);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = dark;
            }

            int columns = 6;
            int rows = 16;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    float lit = Hash(col + variant * 31, row + variant * 17);
                    if (lit < 0.42f)
                    {
                        continue;
                    }

                    Color warm = lit > 0.82f
                        ? new Color(0.75f, 0.86f, 1f, 1f)
                        : new Color(1f, 0.78f, 0.42f, 1f);
                    float strength = 0.55f + Hash(col * 3 + row, row * 7) * 0.45f;
                    Color window = warm * strength;

                    int x0 = col * width / columns + 2;
                    int x1 = (col + 1) * width / columns - 2;
                    int y0 = row * height / rows + 2;
                    int y1 = (row + 1) * height / rows - 3;

                    for (int y = y0; y < y1; y++)
                    {
                        for (int x = x0; x < x1; x++)
                        {
                            pixels[y * width + x] = window;
                        }
                    }
                }
            }

            return Make($"T_Windows_{variant}", width, height, true, pixels, true);
        }

        static Texture2D SkyTexture()
        {
            const int width = 1024;
            const int height = 512;
            Color[] pixels = NewGrid(width, height);

            Color zenith = new(0.006f, 0.01f, 0.03f, 1f);
            Color midSky = new(0.012f, 0.02f, 0.06f, 1f);
            Color horizon = new(0.06f, 0.05f, 0.12f, 1f);
            Color ground = new(0.012f, 0.014f, 0.026f, 1f);

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color sky;
                if (v < 0.5f)
                {
                    sky = Color.Lerp(ground, horizon, v / 0.5f);
                }
                else if (v < 0.72f)
                {
                    sky = Color.Lerp(horizon, midSky, (v - 0.5f) / 0.22f);
                }
                else
                {
                    sky = Color.Lerp(midSky, zenith, (v - 0.72f) / 0.28f);
                }

                // A thin city glow band just above the horizon line.
                float band = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) / 0.06f);
                sky += new Color(0.11f, 0.06f, 0.03f, 0f) * band * band;

                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = sky;
                }
            }

            // Stars: a few thousand dots, brighter towards the zenith.
            for (int i = 0; i < 2600; i++)
            {
                float u = Hash(i, 11);
                float v = 0.5f + Hash(i, 23) * 0.49f;
                int x = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);

                float magnitude = Hash(i, 37);
                float altitude = (v - 0.5f) * 2f;
                float brightness = Mathf.Lerp(0.25f, 1f, magnitude) * Mathf.Lerp(0.45f, 1f, altitude);
                float tone = Mathf.Lerp(0.2f, 1f, Hash(i, 53));
                Color tint = Color.white;
                if (tone < 0.3f)
                {
                    tint = new Color(0.75f, 0.82f, 1f, 1f);
                }
                else if (tone > 0.8f)
                {
                    tint = new Color(1f, 0.86f, 0.7f, 1f);
                }

                int radius = magnitude > 0.965f ? 2 : 1;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = Mathf.Clamp(x + dx, 0, width - 1);
                        int sy = Mathf.Clamp(y + dy, 0, height - 1);
                        float falloff = 1f / (1f + dx * dx + dy * dy);
                        pixels[sy * width + sx] += tint * (brightness * falloff * 0.9f);
                    }
                }
            }

            // Moon with a soft halo.
            int moonX = (int)(width * 0.24f);
            int moonY = (int)(height * 0.79f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = Mathf.Abs(x - moonX);
                    int dy = Mathf.Abs(y - moonY);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy * 1.6f);

                    if (distance < 13f)
                    {
                        float crater = 0.86f + Hash(x / 4, y / 4) * 0.14f;
                        pixels[y * width + x] = new Color(1.15f, 1.12f, 1f, 1f) * crater;
                    }
                    else if (distance < 46f)
                    {
                        float halo = 1f - (distance - 13f) / 33f;
                        pixels[y * width + x] += new Color(0.24f, 0.28f, 0.4f, 1f) * halo * halo * 0.55f;
                    }
                }
            }

            return Make("T_Sky", width, height, false, pixels, true);
        }

        static Texture2D SoftDotTexture()
        {
            const int size = 64;
            Color[] pixels = NewGrid(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - size * 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f) / (size * 0.5f);
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = d * d;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            return Make("T_SoftDot", size, size, false, pixels, false);
        }

        /// <summary>
        /// Builds the interface sprite set. Each shape returns its own 9 slice border, derived from
        /// the geometry that was actually drawn, because a wrong border is invisible in the editor and
        /// only shows up as distorted corners several screens later.
        /// </summary>
        static void BuildUiSprites()
        {
            ui.Clear();

            ui[UiShape.Rounded] = SaveSprite("S_Rounded", Rounded(128, 128, 14f, 2f));
            ui[UiShape.Cut] = SaveSprite("S_Cut", Chamfered(128, 128, 4f, 2f, 26f));
            ui[UiShape.PillL] = SaveSprite("S_PillL", Capsule(128, 96, 2f));
            ui[UiShape.PillM] = SaveSprite("S_PillM", Capsule(128, 64, 2f));
            ui[UiShape.PillS] = SaveSprite("S_PillS", Capsule(96, 40, 2f));
            ui[UiShape.OutlineM] = SaveSprite("S_PillOutlineM", CapsuleOutline(128, 64, 2f, 2f));
            ui[UiShape.OutlineS] = SaveSprite("S_PillOutlineS", CapsuleOutline(96, 40, 2f, 2f));
            ui[UiShape.Frame] = SaveSprite("S_Frame", Brackets(128, 128, 8f, 40f, 3f));
            ui[UiShape.Shadow] = SaveSprite("S_Shadow", Halo(256, 256, 20f, 26f, 8));
            ui[UiShape.Bloom] = SaveSprite("S_Bloom", Bloom(256, 1.7f));
            ui[UiShape.Edge] = SaveSprite("S_Edge", EdgeFrame(256, 256, 26f, 16));
            ui[UiShape.SoftDot] = SaveSprite("S_SoftDot", Radial(64));
            ui[UiShape.Key] = SaveSprite("S_Key", KeyCap(96, 64, 10f, 2f));
            ui[UiShape.Heart] = SaveSprite("S_Heart", Heart(64, 0f));
            ui[UiShape.HeartOutline] = SaveSprite("S_HeartOutline", Heart(64, 0.72f));

            // Still generated although the fuel gauge now sizes its own fill: it is the borderless
            // quad that made Image.type Filled report fuel honestly, and it stays available as the
            // fallback if a future gauge wants fillAmount instead of width.
            Bar = SaveSprite("S_Bar", Quad(16, 16), alphaIsTransparency: false);
        }

        static Sprite SaveSprite(string name, UiArt art, bool alphaIsTransparency = true)
        {
            string path = $"{TextureFolder}/{name}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Texture2D tex = new(art.width, art.height, TextureFormat.RGBA32, false) { name = name };
            tex.SetPixels(art.pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(GetProjectPath(), path), png);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(art.border, art.border, art.border, art.border);
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"Could not import the UI sprite at '{path}'.");
            }

            return sprite;
        }

        /// <summary>Generated pixels plus the 9 slice border that goes with them.</summary>
        readonly struct UiArt
        {
            public readonly int width;
            public readonly int height;
            public readonly int border;
            public readonly Color[] pixels;

            public UiArt(int width, int height, int border, Color[] pixels)
            {
                this.width = width;
                this.height = height;
                this.border = border;
                this.pixels = pixels;
            }
        }

        /// <summary>
        /// Signed distance to a rounded rectangle: zero on the edge, negative inside. The canvas is
        /// the sprite, the shape sits inset from it, and the border has to cover inset plus radius.
        /// </summary>
        static float Sdf(int w, int h, float radius, float inset, int x, int y)
        {
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - halfW) - (halfW - inset) + radius, 0f);
            float dy = Mathf.Max(Mathf.Abs(y + 0.5f - halfH) - (halfH - inset) + radius, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        static UiArt Rounded(int w, int h, float radius, float inset)
        {
            Color[] px = NewGrid(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - Sdf(w, h, radius, inset, x, y)));
                }
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + radius + 1f), px);
        }

        /// <summary>
        /// Rounded rect with the top right and bottom left corners cut at 45 degrees. The chamfer is
        /// the angular signal of the whole look, so it is used on every card the game shows.
        /// </summary>
        static UiArt Chamfered(int w, int h, float radius, float inset, float cut)
        {
            Color[] px = NewGrid(w, h);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float line = halfW + halfH - cut;
            float invSqrt2 = 1f / Mathf.Sqrt(2f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - halfW;
                    float cy = y + 0.5f - halfH;
                    // Intersection keeps the larger of the two distances.
                    float dist = Mathf.Max(
                        Sdf(w, h, radius, inset, x, y),
                        Mathf.Max((cx + cy - line) * invSqrt2, (-cx - cy - line) * invSqrt2));
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist));
                }
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + Mathf.Max(radius, cut) + 1f), px);
        }

        static UiArt Capsule(int w, int h, float inset)
        {
            return Rounded(w, h, h * 0.5f - inset, inset);
        }

        /// <summary>A capsule drawn as a ring of the given thickness, centre fully transparent.</summary>
        static UiArt CapsuleOutline(int w, int h, float inset, float thickness)
        {
            float radius = h * 0.5f - inset;
            Color[] px = NewGrid(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = Sdf(w, h, radius, inset, x, y);
                    px[y * w + x] = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(thickness * 0.5f + 0.5f - Mathf.Abs(dist)));
                }
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + radius + 1f), px);
        }

        /// <summary>Four L shaped corner brackets, used as card decoration and as the focus ring.</summary>
        static UiArt Brackets(int w, int h, float inset, float arm, float thickness)
        {
            Color[] px = NewGrid(w, h);

            static float Band(float v, float lo, float hi) =>
                Mathf.Clamp01(Mathf.Min(v - lo, hi - v) + 0.5f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = x + 0.5f;
                    float r = w - 0.5f - x;
                    float b = y + 0.5f;
                    float t = h - 0.5f - y;

                    float a = 0f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float e0 = (corner & 1) == 0 ? l : r;
                        float e1 = (corner & 2) == 0 ? t : b;
                        a = Mathf.Max(a, Band(e0, inset, inset + arm) * Band(e1, inset, inset + thickness));
                        a = Mathf.Max(a, Band(e1, inset, inset + arm) * Band(e0, inset, inset + thickness));
                    }

                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + arm + 1f), px);
        }

        /// <summary>
        /// Soft rectangular shadow. The overlay canvas is composited after post processing, so depth
        /// under a card has to be drawn rather than lit.
        ///
        /// Two numbers are load bearing. The blur has to die out inside the sprite, so the inset must
        /// cover the three passes of it (3 times the radius); an alpha that is still above zero where
        /// the pixels stop is clipped, and a clipped gradient is a hard edge exactly one rect out from
        /// the card. And the border is inset plus radius: the corner curve has to sit wholly inside the
        /// corner region, or nine slicing stretches it and the corners stop matching the corners.
        /// </summary>
        static UiArt Halo(int w, int h, float radius, float inset, int blur)
        {
            UiArt core = Rounded(w, h, radius, inset);
            Color[] px = core.pixels;
            for (int pass = 0; pass < 3; pass++)
            {
                px = BoxBlur(px, w, h, blur);
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + radius + 1f), px);
        }

        /// <summary>
        /// A stroke that hugs the four edges of whatever it is stretched over and fades into the middle.
        /// Used for the damage tell, which has to read as the frame itself lighting up rather than as a
        /// box drawn inside it.
        /// </summary>
        static UiArt EdgeFrame(int w, int h, float thickness, int blur)
        {
            Color[] px = NewGrid(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Min(x + 0.5f, w - 0.5f - x, y + 0.5f, h - 0.5f - y);
                    float a = 1f - Mathf.SmoothStep(0f, thickness, d);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            // Blur on both sides of the falloff: the corner where two edges meet is the brightest
            // point of the shape and must not be a hard right angle.
            px = BoxBlur(px, w, h, blur);

            return new UiArt(w, h, Mathf.CeilToInt(thickness + blur * 2f + 1f), px);
        }

        static Color[] BoxBlur(Color[] src, int w, int h, int radius)        {
            Color[] tmp = NewGrid(w, h);
            Color[] outPixels = NewGrid(w, h);
            int span = radius * 2 + 1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int i = -radius; i <= radius; i++)
                    {
                        sum += src[y * w + Mathf.Clamp(x + i, 0, w - 1)].a;
                    }

                    tmp[y * w + x] = new Color(1f, 1f, 1f, sum / span);
                }
            }

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float sum = 0f;
                    for (int i = -radius; i <= radius; i++)
                    {
                        sum += tmp[Mathf.Clamp(y + i, 0, h - 1) * w + x].a;
                    }

                    outPixels[y * w + x] = new Color(1f, 1f, 1f, sum / span);
                }
            }

            return outPixels;
        }

        /// <summary>
        /// Light with no shape of its own: a body that fades in every direction and no border, so
        /// UGUI stretches it instead of slicing it. Stretching is the point. A sliced halo reproduces
        /// its own middle at a constant alpha across the whole of the stretched region, which is a
        /// hard edged rectangle, not a glow; a stretched falloff scales with the rect it lights.
        /// The softness exponent trades a fat body for a long tail, and the dither breaks up the
        /// banding an 8 bit alpha channel shows in a gradient this shallow.
        /// </summary>
        static UiArt Bloom(int size, float softness)
        {
            Color[] px = NewGrid(size, size);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(t, softness);

                    // 4x4 ordered dither: deterministic, so a rebuild is byte for byte repeatable.
                    float[] table =
                    {
                        0f / 16f, 8f / 16f, 2f / 16f, 10f / 16f,
                        12f / 16f, 4f / 16f, 14f / 16f, 6f / 16f,
                        3f / 16f, 11f / 16f, 1f / 16f, 9f / 16f,
                        15f / 16f, 7f / 16f, 13f / 16f, 5f / 16f
                    };
                    a = Mathf.Clamp01(a + (table[(y % 4) * 4 + x % 4] - 0.5f) / 128f);

                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            return new UiArt(size, size, 0, px);
        }

        /// <summary>Smooth radial dot, stretched rather than sliced.</summary>
        static UiArt Radial(int size)        {
            Color[] px = NewGrid(size, size);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color(1f, 1f, 1f, t * t * (3f - 2f * t));
                }
            }

            return new UiArt(size, size, 0, px);
        }

        /// <summary>
        /// Key cap with the shading baked into the colour channels: an Image multiplies its colour by
        /// the sprite, so a body below 1 leaves room for a brighter inner rim along the top edge.
        /// </summary>
        static UiArt KeyCap(int w, int h, float radius, float inset)
        {
            Color[] px = NewGrid(w, h);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = Sdf(w, h, radius, inset, x, y);
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    float shade = Mathf.Lerp(0.66f, 0.88f, v);
                    if (dist < -1.5f && dist > -3.5f && v > 0.5f)
                    {
                        shade = 1f;
                    }

                    px[y * w + x] = new Color(shade, shade, shade, alpha);
                }
            }

            return new UiArt(w, h, Mathf.CeilToInt(inset + radius + 1f), px);
        }

        /// <summary>
        /// Heart for the lives pips. The algebraic heart is not a distance field, so its edge is
        /// antialiased by supersampling instead of by an analytic falloff. A shrink below 1 turns the
        /// filled shape into an outline by subtracting the smaller heart from the larger one.
        /// </summary>
        static UiArt Heart(int size, float shrink)
        {
            const int samples = 4;
            Color[] px = NewGrid(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hit = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = ((x * samples + sx + 0.5f) / (size * samples) - 0.5f) * 2.5f;
                            float v = ((y * samples + sy + 0.5f) / (size * samples) - 0.5f) * 2.5f - 0.05f;
                            if (HeartInside(u, v) && (shrink <= 0f || !HeartInside(u / shrink, v / shrink)))
                            {
                                hit += 1f;
                            }
                        }
                    }

                    px[y * size + x] = new Color(1f, 1f, 1f, hit / (samples * samples));
                }
            }

            return new UiArt(size, size, 0, px);
        }

        static bool HeartInside(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        static UiArt Quad(int w, int h)
        {
            Color[] px = NewGrid(w, h);
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Color.white;
            }

            return new UiArt(w, h, 0, px);
        }

        /// <summary>Stable integer hash, so every rebuild produces the same art.</summary>
        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = folder.Substring(0, folder.LastIndexOf('/'));
                string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
