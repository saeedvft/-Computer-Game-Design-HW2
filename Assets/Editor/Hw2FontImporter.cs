using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HW2.EditorTools
{
    /// <summary>
    /// Turns the TTFs committed in Assets/Fonts into TMP font assets, bakes the characters the
    /// interface can actually print into their atlases, and makes the UI face TMP's default.
    ///
    /// Deliberately offline: with no TTFs on disk it warns and leaves TextMesh Pro's own default in
    /// place, so HW2/Build Game still produces a working scene on a machine without network access.
    /// </summary>
    public static class Hw2FontImporter
    {
        const string FontFolder = "Assets/Fonts";
        const string SettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string FallbackAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string FallbackField = "m_fallbackFontAssets";
        const string DefaultField = "m_defaultFontAsset";

        /// <summary>
        /// Every character the UI is able to print, so the atlases already hold it before a build
        /// clears dynamic font data. Lower case included: the game over reason is a real sentence.
        /// No chevrons: Orbitron, Rajdhani and JetBrains Mono all lack U+25B8/25C2, so keyboard focus
        /// is drawn with the S_Frame brackets instead of a glyph.
        /// </summary>
        public const string Charset =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            + "abcdefghijklmnopqrstuvwxyz"
            + "0123456789 .,:!?'-/+%()"
            + "·";

        /// <summary>A type role, the file it comes from and the atlas it is baked into.</summary>
        readonly struct Role
        {
            public readonly UiFont Font;
            public readonly string SourceFile;
            public readonly int SamplingPointSize;
            public readonly int AtlasSize;

            /// <summary>
            /// Characters this face is allowed to lack. The charset is shared, so without it a face
            /// would be faulted for a glyph no label in that role can ever print.
            /// </summary>
            public readonly string ToleratedAbsence;

            public Role(UiFont font, string sourceFile, int samplingPointSize, int atlasSize,
                string toleratedAbsence = "")
            {
                Font = font;
                SourceFile = sourceFile;
                SamplingPointSize = samplingPointSize;
                AtlasSize = atlasSize;
                ToleratedAbsence = toleratedAbsence;
            }

            public string SourcePath => $"{FontFolder}/{SourceFile}";
            public string AssetPath => $"{FontFolder}/{Path.GetFileNameWithoutExtension(SourceFile)} SDF.asset";
        }

        /// <summary>
        /// Display sizes are sampled at a larger point size so the SDF gradient stays smooth when a
        /// 112pt title is rasterised; the faces only ever print the shared charset.
        ///
        /// Orbitron carries no U+00B7. That costs nothing, because a title is never a separated
        /// pair: the only middot on screen is the menu kicker, which sets in the UI face.
        /// </summary>
        static readonly Role[] Roles =
        {
            new Role(UiFont.Display, "Orbitron-Black.ttf", 110, 1024, "·"),
            new Role(UiFont.Ui, "Rajdhani-SemiBold.ttf", 70, 512),
            new Role(UiFont.UiBold, "Rajdhani-Bold.ttf", 70, 512),
            new Role(UiFont.Mono, "JetBrainsMono-Bold.ttf", 70, 512),
            new Role(UiFont.MonoBold, "JetBrainsMono-ExtraBold.ttf", 70, 512),
        };

        static readonly Dictionary<UiFont, TMP_FontAsset> resolved = new();

        /// <summary>
        /// The font asset for a role. Falls back to TMP's default when the fonts were never imported,
        /// which is the offline case: the build stays valid, only the typography degrades.
        /// </summary>
        public static TMP_FontAsset Get(UiFont role)
        {
            if (resolved.TryGetValue(role, out TMP_FontAsset cached) && cached != null)
            {
                return cached;
            }

            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PathOf(role));
            if (asset == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            resolved[role] = asset;
            return asset;
        }

        static string PathOf(UiFont role)
        {
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].Font == role)
                {
                    return Roles[i].AssetPath;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- entry points

        [MenuItem("HW2/Import Neon Arcade Fonts")]
        public static void EnsureFonts()
        {
            resolved.Clear();

            if (!Directory.Exists(FontFolder) || Directory.GetFiles(FontFolder, "*.ttf").Length == 0)
            {
                Debug.LogWarning(
                    $"No TTF files in {FontFolder}; the interface keeps TextMesh Pro's default face. " +
                    "Fetch them with:\n" +
                    "  curl -s -A \"Mozilla/4.0\" \"https://fonts.googleapis.com/css2?family=Orbitron:wght@900" +
                    "&family=Rajdhani:wght@500;600;700&family=JetBrains+Mono:wght@700;800&display=swap\"\n" +
                    "  and save the .ttf links it returns into Assets/Fonts/.");
                return;
            }

            FontEngine.InitializeFontEngine();

            StringBuilder report = new();
            int ready = 0;
            for (int i = 0; i < Roles.Length; i++)
            {
                TMP_FontAsset asset = BuildRole(Roles[i]);
                if (asset == null)
                {
                    continue;
                }

                ready++;
                report.Append($"\n  {Roles[i].Font,-9} {asset.name}  {asset.characterTable.Count} glyphs, atlas {asset.atlasTexture.width}x{asset.atlasTexture.height}");
            }

            if (ready == 0)
            {
                return;
            }

            ApplyDefaults();
            AssetDatabase.SaveAssets();
            Debug.Log($"HW2: {ready}/{Roles.Length} Neon Arcade font assets ready (default '{TMP_Settings.defaultFontAsset?.name}').{report}");
        }

        /// <summary>
        /// Throws the font assets away and rebuilds them, which is what a changed charset needs: an
        /// existing asset only ever gains the characters it is missing at the time it was created.
        /// </summary>
        [MenuItem("HW2/Rebuild Neon Arcade Fonts")]
        public static void RebuildFonts()
        {
            for (int i = 0; i < Roles.Length; i++)
            {
                if (File.Exists(Roles[i].AssetPath))
                {
                    AssetDatabase.DeleteAsset(Roles[i].AssetPath);
                }
            }

            EnsureFonts();
        }

        // ---------------------------------------------------------------- per role

        static TMP_FontAsset BuildRole(in Role role)
        {
            if (!File.Exists(role.SourcePath))
            {
                Debug.LogWarning($"{role.SourceFile} is missing from {FontFolder}; the '{role.Font}' role keeps the default face.");
                return null;
            }

            TrueTypeFontImporter importer = AssetImporter.GetAtPath(role.SourcePath) as TrueTypeFontImporter;
            if (importer != null && !importer.includeFontData)
            {
                // Glyphs outside the baked set are re-rasterised at runtime, which needs the outlines
                // inside the player.
                importer.includeFontData = true;
                importer.characterPadding = 4;
                importer.SaveAndReimport();
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(role.SourcePath);
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(role.AssetPath);
            bool created = false;

            if (asset == null || asset.sourceFontFile != source)
            {
                if (asset != null)
                {
                    AssetDatabase.DeleteAsset(role.AssetPath);
                }

                asset = Create(asset: role.AssetPath, source: source, role: role);
                if (asset == null)
                {
                    return null;
                }

                created = true;
            }

            // TMP seeds this from TMP Settings, where it is on: the pre-baked charset would be dropped
            // from the player and the editor would still look perfect.
            SerializedObject so = new(asset);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (created)
            {
                BakeCharset(asset);
            }

            string absent = AbsentCharacters(asset, role.ToleratedAbsence);
            if (absent.Length > 0)
            {
                Debug.LogWarning(
                    $"'{asset.name}' cannot render \"{absent}\" ({Codepoints(absent)}). Either the face has no "
                    + "such glyph, so the copy must change, or the asset predates the charset, so run "
                    + "HW2/Rebuild Neon Arcade Fonts.", asset);
            }

            resolved[role.Font] = asset;
            return asset;
        }

        static TMP_FontAsset Create(string asset, Font source, in Role role)
        {
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source, role.SamplingPointSize, 8, GlyphRenderMode.SDFAA,
                role.AtlasSize, role.AtlasSize, AtlasPopulationMode.Dynamic, true);

            if (fontAsset == null)
            {
                Debug.LogError($"Could not rasterise '{source.name}' for the '{role.Font}' role.");
                return null;
            }

            AssetDatabase.CreateAsset(fontAsset, asset);

            // The atlas and its material come back as loose objects: TMP only keeps them when they are
            // sub-assets of the font asset, and a font asset without its atlas renders nothing.
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            return fontAsset;
        }

        static void BakeCharset(TMP_FontAsset asset)
        {
            asset.TryAddCharacters(Charset, out _);
            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// Charset characters the asset cannot print. Read from the table rather than from
        /// TryAddCharacters, which reports the whole request as missing when nothing needed adding.
        /// </summary>
        static string AbsentCharacters(TMP_FontAsset asset, string tolerated)
        {
            StringBuilder absent = new();
            for (int i = 0; i < Charset.Length; i++)
            {
                char c = Charset[i];
                if (tolerated.IndexOf(c) >= 0 || absent.ToString().IndexOf(c) >= 0)
                {
                    continue;
                }

                if (!asset.HasCharacter(c))
                {
                    absent.Append(c);
                }
            }

            return absent.ToString();
        }

        static string Codepoints(string characters)
        {
            StringBuilder sb = new();
            for (int i = 0; i < characters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(((int)characters[i]).ToString("X4"));
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- project settings

        /// <summary>
        /// Points TMP's default at the UI face and registers Liberation Sans as the fallback, so a
        /// stray glyph degrades to another typeface instead of turning into an empty box.
        /// </summary>
        static void ApplyDefaults()
        {
            TMP_FontAsset ui = Get(UiFont.Ui);
            if (ui == null)
            {
                return;
            }

            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);

            TMP_Settings.defaultFontAsset = ui;
            List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
            if (fallback != null && !fallbacks.Contains(fallback))
            {
                fallbacks.Add(fallback);
            }

            TMP_Settings.fallbackFontAssets = fallbacks;

            // Written to the asset as well: the static setter and the serialized field can disagree
            // until the settings object is reloaded.
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsAssetPath);
            if (settings == null)
            {
                return;
            }

            SerializedObject so = new(settings);
            so.FindProperty(DefaultField).objectReferenceValue = ui;

            SerializedProperty table = so.FindProperty(FallbackField);
            table.ClearArray();
            if (fallback != null)
            {
                table.InsertArrayElementAtIndex(0);
                table.GetArrayElementAtIndex(0).objectReferenceValue = fallback;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
