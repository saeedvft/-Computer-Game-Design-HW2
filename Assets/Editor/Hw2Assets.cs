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
        const string PanelSpriteName = "S_Rounded";
        const string BarSpriteName = "S_Bar";

        static readonly Dictionary<string, Material> materials = new();

        /// <summary>Rounded panel sprite shared by every UI background and button.</summary>
        public static Sprite Panel { get; private set; }
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

            Panel = RoundedSprite();
            Bar = BarSprite();

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
        /// Anti aliased rounded rectangle, imported as a 9 slice sprite so every panel and button
        /// can stretch to any size without distorting its corners.
        /// </summary>
        static Sprite RoundedSprite()
        {
            const int size = 128;
            const float radius = 24f;
            const float inset = 8f;
            const float border = 32f;

            Color[] pixels = NewGrid(size, size);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - inset) + radius, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - inset) + radius, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist));
                }
            }

            string path = $"{TextureFolder}/{PanelSpriteName}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { name = PanelSpriteName };
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(GetProjectPath(), path), png);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
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

        /// <summary>
        /// Borderless opaque quad for gauge fills. Image.type Filled insets its geometry by the sprite
        /// border, so only a zero-border sprite makes the drawn length equal to fillAmount.
        /// </summary>
        static Sprite BarSprite()
        {
            const int size = 16;

            string path = $"{TextureFolder}/{BarSpriteName}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { name = BarSpriteName };
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(GetProjectPath(), path), png);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.alphaIsTransparency = false;
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
