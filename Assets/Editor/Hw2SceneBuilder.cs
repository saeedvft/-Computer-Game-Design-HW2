using System;
using System.Reflection;
using Object = UnityEngine.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HW2.EditorTools
{
    using static Hw2UiStyle;

    /// <summary>
    /// Builds the whole playable scene from code: materials, textures, prefabs, the settings
    /// asset, lighting, post-processing and the complete UI. Everything is deterministic, so the
    /// project can be regenerated from source alone.
    /// </summary>
    public static class Hw2SceneBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string ScenePath = SceneFolder + "/Game.unity";
        const string SettingsPath = "Assets/Resources/GameSettings.asset";
        const string ProfilePath = "Assets/Settings/Hw2PostFX.asset";
        const string InputActionsPath = "Assets/Resources/GameInput.inputactions";

        // ---------------------------------------------------------------- UI

        [MenuItem("HW2/Build Game (assets + scene)")]
        public static void BuildGame()
        {
            Hw2Assets.BuildAll();
            Hw2FontImporter.EnsureFonts();

            GameSettings settings = LoadOrCreateSettings();
            Hw2Prefabs.BuildAll(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            BuildScene(settings);
            Debug.Log("HW2: assets and scene rebuilt.");
        }

        [MenuItem("HW2/Verify Scene Setup")]
        public static void Verify()
        {
            int problems = 0;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>())
            {
                // Only the game's own components are worth auditing: TMP and URP keep a long list
                // of optional references empty by design.
                if (behaviour == null
                    || behaviour.GetType().Namespace != "HW2"
                    || !behaviour.gameObject.scene.IsValid())
                {
                    continue;
                }

                SerializedObject so = new(behaviour);
                SerializedProperty property = so.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.name == "m_Script"
                        || property.objectReferenceValue != null
                        || IsOptional(behaviour.GetType(), property.name))
                    {
                        continue;
                    }

                    problems++;
                    Debug.LogWarning($"{behaviour.GetType().Name} on '{behaviour.gameObject.name}' has an empty '{property.name}'.", behaviour);
                }
            }

            Debug.Log(problems == 0 ? "HW2: every scene reference is wired." : $"HW2: {problems} empty scene reference(s).");
        }

        /// <summary>
        /// <c>UIManager</c> reaches every widget through a path written as a string, and the builder names
        /// the nodes it looks for. Nothing in the project joins the two, so a rename on one side is a blank
        /// readout at runtime that only shows up once somebody plays the game. This joins them.
        /// </summary>
        [MenuItem("HW2/Verify UI Paths")]
        public static void VerifyUiPaths()
        {
            UIManager ui = Object.FindAnyObjectByType<UIManager>();
            if (ui == null)
            {
                Debug.LogError("HW2: no UIManager in the open scene - build the game first.");
                return;
            }

            int missing = 0;

            foreach (FieldInfo field in typeof(UIManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(string) || !field.Name.EndsWith("Path"))
                {
                    continue;
                }

                string path = (string)field.GetValue(ui);

                if (string.IsNullOrEmpty(path) || ui.transform.Find(path) == null)
                {
                    missing++;
                    Debug.LogError($"HW2: UIManager.{field.Name} = '{path}' resolves to nothing under the canvas.", ui);
                }
            }

            Debug.Log(missing == 0
                ? "HW2: every UI path resolves."
                : $"HW2: {missing} UI path(s) do not resolve.");
        }

        /// <summary>
        /// A field the component itself declares optional: the part it points at is not always built, and
        /// an audit that shouted about every one of them would stop being read.
        /// </summary>
        static bool IsOptional(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null && field.IsDefined(typeof(OptionalReferenceAttribute), false);
        }

        // ---------------------------------------------------------------- settings asset

        static GameSettings LoadOrCreateSettings()
        {
            GameSettings settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            return settings;
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene(GameSettings settings)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupEnvironment();
            GameObject player = PlacePlayer(settings);
            Camera camera = PlaceCamera(player);
            PlaceMoonlight();
            PlaceSystems();
            PlacePostProcessing(camera);
            PlaceUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static void SetupEnvironment()
        {
            Material sky = Hw2Assets.Get("M_SkyProcedural");
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.055f, 0.075f, 0.13f);
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0.045f, 0.075f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.03f);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.35f;

            // Squared exponential haze: the road sinks into the dark a few hundred metres out,
            // which hides the segment seam entirely.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.022f, 0.028f, 0.05f);
            RenderSettings.fogDensity = 0.0058f;

            RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.03f, 0.06f);

            QualitySettings.shadowDistance = 0f;
            QualitySettings.lodBias = 1.4f;

            // Edges are cleaned up by SMAA on the camera, so no MSAA is needed on top of it.
            QualitySettings.antiAliasing = 0;
        }

        static GameObject PlacePlayer(GameSettings settings)
        {
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(settings.playerCarPrefab);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.name = "PlayerCar";
            return player;
        }

        static Camera PlaceCamera(GameObject player)
        {
            GameObject go = new("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(new Vector3(0f, 4.2f, -9.6f), Quaternion.Euler(12f, 0f, 0f));

            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 600f;
            camera.fieldOfView = 58f;
            camera.allowHDR = true;

            UniversalAdditionalCameraData data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                data = go.AddComponent<UniversalAdditionalCameraData>();
            }

            data.renderPostProcessing = true;
            data.volumeLayerMask = ~0;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            go.AddComponent<CameraShake>();
            ThirdPersonCamera rig = go.AddComponent<ThirdPersonCamera>();
            SerializedObject so = new(rig);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<AudioListener>();

            // Star dome rides with the camera so the sky never clips, and the procedural
            // skybox underneath still provides the ambient and reflections.
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "StarDome";
            Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.transform.SetParent(go.transform, false);
            dome.transform.localPosition = Vector3.zero;
            dome.transform.localScale = Vector3.one * 420f;
            dome.GetComponent<MeshRenderer>().sharedMaterial = Hw2Assets.Get("U_Sky");
            dome.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dome.GetComponent<MeshRenderer>().receiveShadows = false;

            return camera;
        }

        static void PlaceMoonlight()
        {
            GameObject go = new("Moon Light");
            go.transform.rotation = Quaternion.Euler(-42f, 152f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.48f, 0.58f, 0.85f);
            light.intensity = 0.32f;
            light.shadows = LightShadows.None;

            RenderSettings.sun = light;
        }

        static void PlaceSystems()
        {
            GameObject go = new("[Game]");

            go.AddComponent<GameManager>();
            go.AddComponent<InputManager>();
            go.AddComponent<PoolManager>();
            go.AddComponent<AudioManager>();
            go.AddComponent<EffectPlayer>();
            go.AddComponent<Fireworks>();
            go.AddComponent<RoadSpawner>();
            go.AddComponent<SpawnDirector>();
        }

        static void PlacePostProcessing(Camera camera)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.components.Clear();

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 1.15f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.92f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.72f;
            bloom.clamp.overrideState = true;
            bloom.clamp.value = 400f;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = true;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(0.88f, 0.93f, 1f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.38f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.42f;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.02f, 0.02f, 0.05f);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.55f;
            color.contrast.overrideState = true;
            color.contrast.value = 12f;
            color.saturation.overrideState = true;
            color.saturation.value = 8f;
            color.colorFilter.overrideState = true;
            color.colorFilter.value = new Color(0.86f, 0.9f, 1f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            GameObject go = new("Global Volume");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            volume.profile = profile;
        }

        // ---------------------------------------------------------------- UI

        static void PlaceUI()
        {
            GameObject canvasGo = new("UI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasGo.AddComponent<GraphicRaycaster>();

            BuildHud(canvasGo.transform);
            BuildMenu(canvasGo.transform);
            BuildPause(canvasGo.transform);
            BuildGameOver(canvasGo.transform);

            canvasGo.AddComponent<UIManager>();

            GameObject eventGo = new("EventSystem");
            eventGo.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventGo.AddComponent<InputSystemUIInputModule>();
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                Debug.LogWarning("GameInput asset not found - the UI module fell back to its default actions.");
            }
            else
            {
                // Going through the property (not the serialized field) is what re-points every
                // individual UI action reference at our asset instead of the module defaults.
                module.actionsAsset = asset;
                EditorUtility.SetDirty(module);
            }
        }

        static void BuildHud(Transform root)
        {
            RectTransform hud = Node(root, "HUD");
            PanelFx(hud, hud);

            // Speed, bottom left. The digits come from a monospaced face so a changing reading never
            // shifts the unit it is measured against. No brackets: those mean "selected", and nothing
            // in the HUD can be selected.
            RectTransform speed = Card(hud, "SpeedPanel", LowLeft, new Vector2(300f, 124f), new Vector2(Gut, Gut),
                UiShape.Cut, Base1);

            Label(Inset(speed, "SpeedValue", new Vector2(Pad_M, 50f), new Vector2(-Pad_M, 0f)), "0", UiFont.MonoBold,
                T_Num_M, Ink, TextAlignmentOptions.Right);
            Label(Inset(speed, "Unit", new Vector2(Pad_M, 18f), new Vector2(-Pad_M, -106f)), "KM/H", UiFont.UiBold,
                T_Micro, CyanDim, TextAlignmentOptions.Right, K_Label);

            Meter(speed, "SpeedBar", LowLeft, new Vector2(264f, 6f), new Vector2(Pad_M, 8f),
                Ramp((0f, Cyan), (1f, Magenta)), groove: Base2, smoothing: 7f, warnThreshold: 0f, inset: 0f);

            // Score, top centre. Bare type over the road with a halo behind it: the one HUD reading that
            // is worth more than a card around it. Three stacked bands, so the caption cannot land on
            // the number it labels. Inset takes the bottom edge as an offset from the parent's bottom and
            // the top edge as an offset from the parent's top, so a band [a,b] of the 168 tall panel is
            // min.y = a and max.y = b - 168.
            RectTransform score = Step(hud, "ScorePanel", HighMid, new Vector2(760f, 168f), new Vector2(0f, -Gut));

            // The halo is sized to the digits, not to the panel, and centred on them: a bloom stretched
            // across the full 760 is a flat slab with steep edges, which reads as a banner behind the
            // score rather than as light coming off it.
            Surface(Inset(score, "ScoreGlow", new Vector2(210f, 12f), new Vector2(-210f, 4f)),
                UiShape.Bloom, Alpha(Magenta, 0.26f)).gameObject.AddComponent<NeonFlicker>().Wire(seed: 5, humDepth: 0.35f);

            Label(Inset(score, "Caption", new Vector2(0f, 140f), Vector2.zero), "SCORE", UiFont.UiBold,
                T_Micro, CyanDim, TextAlignmentOptions.Center, K_Label);

            RectTransform scoreValue = Inset(score, "ScoreValue", new Vector2(0f, 44f), new Vector2(0f, -28f));
            Label(scoreValue, "0", UiFont.MonoBold, T_Num_L, Ink, TextAlignmentOptions.Center);
            scoreValue.gameObject.AddComponent<ReadoutText>();

            Label(Inset(score, "BestValue", new Vector2(0f, 8f), new Vector2(0f, -124f)), "Best 0", UiFont.Mono,
                T_Micro, InkFaint, TextAlignmentOptions.Center);

            // Fuel, top left.
            RectTransform fuel = Card(hud, "FuelPanel", HighLeft, new Vector2(470f, 108f), new Vector2(Gut, -Gut),
                UiShape.Cut, Base1);
            Label(Inset(fuel, "Caption", new Vector2(Pad_L, 74f), new Vector2(-120f, 0f)), "FUEL", UiFont.UiBold,
                T_Micro, CyanDim, TextAlignmentOptions.Left, K_Label);

            // The percentage is written by the meter, which is the only thing that knows the smoothed
            // number it is actually drawing.
            TMP_Text percent = Label(Inset(fuel, "PercentValue", new Vector2(0f, 70f), new Vector2(-Pad_L, 0f)),
                "100%", UiFont.MonoBold, T_Btn_M, Cyan, TextAlignmentOptions.Right);

            // Amber sits on a short segment on purpose: mixing Warning into Cyan gives a pale green at
            // the halfway point, and a gauge that rests in a colour nobody chose is off palette for
            // most of a run. Full is cyan, empty is magenta, and the amber is only ever passed through.
            Meter(fuel, "Track", LowLeft, new Vector2(414f, 26f), new Vector2(Pad_L, 30f),
                Ramp((0f, Magenta), (0.22f, Warning), (0.55f, Cyan), (1f, Cyan)), Base2, percent,
                warnThreshold: 0.28f);

            // Lives, top right. All three hearts stay in the tree; losing one empties it with a pop.
            // The card is sized to what it holds: three 32 px hearts at 40 px spacing is 112, and 112 plus
            // two pads of 28 is the width. A wider card made the readout look like an empty drawer.
            RectTransform lives = Card(hud, "LifePanel", HighRight, new Vector2(168f, 92f), new Vector2(-Gut, -Gut),
                UiShape.Cut, Base1);
            Label(Inset(lives, "Caption", new Vector2(Pad_L, 62f), new Vector2(-Pad_L, 0f)), "LIVES", UiFont.UiBold,
                T_Micro, CyanDim, TextAlignmentOptions.Right, K_Label);

            // A point anchored box, not an inset. Inset's offsets only describe a band while the anchors
            // stretch; re-anchored to a corner they become the size itself, and -34 minus 14 is a negative
            // height that puts the hearts under the card instead of in it.
            RectTransform hearts = Box(lives, "Lives", LowRight, new Vector2(112f, 44f), new Vector2(-Pad_L, 10f));

            for (int i = 0; i < 3; i++)
            {
                RectTransform heart = Box(hearts, "Life" + i, HighRight, new Vector2(32f, 32f), new Vector2(-i * 40f, 0f));
                heart.anchorMin = new Vector2(1f, 0.5f);
                heart.anchorMax = new Vector2(1f, 0.5f);
                heart.pivot = new Vector2(1f, 0.5f);

                Surface(heart, UiShape.HeartOutline, Alpha(Cyan, 0.30f));
                Surface(Inset(heart, "Fill", Vector2.zero, Vector2.zero), UiShape.Heart, Alpha(Cyan, 0.92f));
            }

            hearts.gameObject.AddComponent<HeartMeter>();

            // The hit: a wash over the frame and a stroke around its edge, on one clock.
            RectTransform flash = Node(hud, "DamageFlash");
            Image wash = Surface(flash, UiShape.Quad, Alpha(Danger, 0f));
            Image edge = Surface(Node(flash, "EdgeFlash"), UiShape.Edge, Alpha(Magenta, 0f));

            flash.gameObject.AddComponent<ScreenFlash>().Wire(new[]
            {
                new ScreenFlash.Layer { image = wash, color = Danger, strength = 0.55f },
                new ScreenFlash.Layer { image = edge, color = Magenta, strength = 0.95f },
            });
        }

        static void BuildMenu(Transform root)
        {
            RectTransform menu = Node(root, "Menu");
            Scrim(menu, ScrimMenu);
            PanelFx(menu, menu);

            const float column = Gut;

            // The old menu was a box in the middle of the screen, which is the most generic layout
            // available and hides the road it is standing on. This is a poster column instead, set down
            // the left edge and lit by the same two colours as everything else.
            StepText(menu, "Kicker", "SECTOR 07 · ENDLESS HIGHWAY", UiFont.UiBold, T_Label, CyanDim,
                TextAlignmentOptions.Left, MidLeft, new Vector2(720f, 34f), new Vector2(column, 372f), K_Label);

            RectTransform title = Step(menu, "Title", MidLeft, new Vector2(940f, 160f), new Vector2(column - 8f, 234f));

            // A radial stretched to the aspect of a title is a band, and a band has edges the eye reads
            // as a box. The halo is therefore given an explicit size close to the shape of the light it
            // is faking, not an inset of the text box: two and a half screens wide against the glyph
            // height, and dim, so the falloff runs over a couple of hundred pixels on both axes.
            Surface(Step(title, "TitleGlow", MidMid, new Vector2(1040f, 460f), new Vector2(140f, 0f)), UiShape.Bloom,
                Alpha(Magenta, 0.18f)).gameObject.AddComponent<NeonFlicker>().Wire(seed: 1, humDepth: 0.20f, dropoutFloor: 0.30f);
            Label(title, "NIGHT RUN", UiFont.Display, T_Display, Ink, TextAlignmentOptions.Left, K_Display)
                .gameObject.AddComponent<NeonFlicker>().Wire(seed: 1, humDepth: 0.04f, dropoutFloor: 0.90f);

            Surface(Step(menu, "Rule", MidLeft, new Vector2(420f, Hair), new Vector2(column, 152f)), UiShape.Quad, Cyan);
            StepText(menu, "Subtitle", "Endless neon highway. Don't run dry.", UiFont.Ui, T_Body, InkSoft,
                TextAlignmentOptions.Left, MidLeft, new Vector2(760f, 44f), new Vector2(column, 96f));

            PillButton(menu, "StartButton", "START RUN", MidLeft, new Vector2(520f, BtnL), new Vector2(column, 4f),
                Magenta, InkOnMag, keyHint: "ENTER");
            GhostButton(menu, "QuitButton", "QUIT", MidLeft, new Vector2(320f, BtnM), new Vector2(column, -106f));

            RectTransform best = Card(menu, "BestChip", HighRight, new Vector2(300f, 112f), new Vector2(-Gut, -Gut),
                UiShape.Cut, Base1);
            Labelled(best, "BestValue", "BEST", "0", MidMid, new Vector2(260f, 96f), Vector2.zero,
                UiFont.MonoBold, T_Num_M, Ink, TextAlignmentOptions.Center, captionColor: CyanDim);

            // One cap for both steering keys: two caps, only one of which has a note, reads as if A does
            // something on its own.
            KeyRow(menu, "Keys", LowMid, new Vector2(0f, 40f),
                ("A D", "STEER"), ("ESC", "PAUSE"), ("R", "RESTART"));
        }

        static void BuildPause(Transform root)
        {
            RectTransform pause = Node(root, "Pause");
            Scrim(pause, ScrimPause);

            RectTransform card = Card(pause, "Panel", MidMid, new Vector2(780f, 680f), Vector2.zero,
                UiShape.Cut, Base1, brackets: true);
            PanelFx(pause, card);

            Surface(Box(card, "TopRule", HighMid, new Vector2(724f, Hair), new Vector2(0f, -28f)), UiShape.Quad, Cyan);

            StepText(card, "Kicker", "TIME STOPPED", UiFont.UiBold, T_Label, CyanDim, TextAlignmentOptions.Center,
                HighMid, new Vector2(520f, 28f), new Vector2(0f, -62f), K_Label);

            RectTransform title = Step(card, "Title", HighMid, new Vector2(720f, 112f), new Vector2(0f, -118f));
            // Sized to the word, not to the text box: the box is 720 wide and the glyphs 409, and a halo
            // that fills the box is twice as wide as the light it is supposed to be coming off.
            Surface(Halo(title, "Glow", new Vector2(409f, 95f)), UiShape.Bloom,
                Alpha(Cyan, 0.22f)).gameObject.AddComponent<NeonFlicker>().Wire(seed: 2, humDepth: 0.16f, dropoutFloor: 0.42f);
            Label(title, "PAUSED", UiFont.Display, T_Title, Ink, TextAlignmentOptions.Center, K_Title);

            RectTransform stakes = Step(card, "Stakes", HighMid, new Vector2(560f, 92f), new Vector2(0f, -254f));
            Labelled(stakes, "ScoreValue", "SCORE", "0", MidLeft, new Vector2(240f, 84f), new Vector2(10f, 0f),
                UiFont.MonoBold, T_Num_M, Ink, TextAlignmentOptions.Left, captionColor: CyanDim);
            Labelled(stakes, "BestValue", "BEST", "0", MidRight, new Vector2(240f, 84f), new Vector2(-10f, 0f),
                UiFont.MonoBold, T_Num_M, Cyan, TextAlignmentOptions.Right, captionColor: CyanDim);
            Surface(Box(stakes, "Divider", LowMid, new Vector2(2f, 84f), new Vector2(0f, 4f)), UiShape.Quad,
                Alpha(Cyan, 0.30f));

            PillButton(card, "ResumeButton", "RESUME", HighMid, new Vector2(600f, BtnL), new Vector2(0f, -372f),
                Magenta, InkOnMag, keyHint: "ESC");
            GhostButton(card, "RestartButton", "RESTART", HighMid, new Vector2(600f, BtnM), new Vector2(0f, -474f),
                keyHint: "R");
            GhostButton(card, "MenuButton", "MAIN MENU", HighMid, new Vector2(420f, BtnS), new Vector2(0f, -552f),
                captionSize: T_Btn_S);

            KeyRow(card, "Keys", LowMid, new Vector2(0f, 34f), ("ESC", "RESUME"), ("R", "RESTART"));
        }

        static void BuildGameOver(Transform root)
        {
            RectTransform over = Node(root, "GameOver");
            Scrim(over, ScrimOver);

            RectTransform card = Card(over, "Panel", MidMid, new Vector2(820f, 660f), Vector2.zero,
                UiShape.Cut, Base1, brackets: true);
            PanelFx(over, card);

            Surface(Box(card, "TopRule", HighMid, new Vector2(764f, Hair), new Vector2(0f, -28f)), UiShape.Quad, Danger);

            RectTransform title = Step(card, "Title", HighMid, new Vector2(780f, 150f), new Vector2(0f, -70f));
            // Dropped 60 below the title so the taller halo stays inside the card instead of bleeding
            // over its top rule.
            Surface(Halo(title, "Glow", new Vector2(601f, 95f), new Vector2(0f, -60f)), UiShape.Bloom,
                Alpha(Danger, 0.24f)).gameObject.AddComponent<NeonFlicker>().Wire(seed: 3, humDepth: 0.22f, dropoutFloor: 0.28f);
            Label(title, "GAME OVER", UiFont.Display, T_Title, Danger, TextAlignmentOptions.Center, K_Title);

            // Lower case, a full sentence: the only part of this screen that speaks rather than shouts.
            StepText(card, "ReasonValue", string.Empty, UiFont.Ui, T_Body, InkSoft, TextAlignmentOptions.Center,
                HighMid, new Vector2(720f, 40f), new Vector2(0f, -236f));

            RectTransform result = Step(card, "Result", HighMid, new Vector2(600f, 130f), new Vector2(0f, -290f));
            TMP_Text overScore = Labelled(result, "ScoreValue", "SCORE", "0", MidLeft, new Vector2(250f, 120f),
                new Vector2(10f, 0f), UiFont.MonoBold, T_Num_L, Ink, TextAlignmentOptions.Left,
                captionColor: CyanDim);

            // The score of a run that just ended is worth counting up: the roll gives the number a
            // length, which is what the run felt like from the inside.
            overScore.gameObject.AddComponent<ReadoutText>();

            Labelled(result, "BestValue", "BEST", "0", MidRight, new Vector2(250f, 120f), new Vector2(-10f, 0f),
                UiFont.MonoBold, T_Num_M, Cyan, TextAlignmentOptions.Right, captionColor: CyanDim);
            Surface(Box(result, "Divider", LowMid, new Vector2(2f, 120f), new Vector2(0f, 5f)), UiShape.Quad,
                Alpha(Cyan, 0.30f));

            RectTransform record = RecordChip(card, "RecordChip", "NEW RECORD", HighMid, new Vector2(300f, 44f),
                new Vector2(0f, -420f));

            PillButton(card, "RestartButton", "RESTART", HighMid, new Vector2(600f, BtnL), new Vector2(0f, -486f),
                Magenta, InkOnMag, keyHint: "R");
            GhostButton(card, "MenuButton", "MAIN MENU", HighMid, new Vector2(420f, BtnS), new Vector2(0f, -590f),
                captionSize: T_Btn_S);
        }

        // ---------------------------------------------------------------- UI primitives

        // Anchor corners. A box is placed by its anchor, so these name the nine places a widget can
        // sit instead of writing the same Vector2 pair at every call site.
        static readonly Vector2 LowLeft = new(0f, 0f);
        static readonly Vector2 LowMid = new(0.5f, 0f);
        static readonly Vector2 LowRight = new(1f, 0f);
        static readonly Vector2 MidLeft = new(0f, 0.5f);
        static readonly Vector2 MidMid = new(0.5f, 0.5f);
        static readonly Vector2 MidRight = new(1f, 0.5f);
        static readonly Vector2 HighLeft = new(0f, 1f);
        static readonly Vector2 HighMid = new(0.5f, 1f);
        static readonly Vector2 HighRight = new(1f, 1f);

        static RectTransform Node(Transform parent, string name)
        {
            GameObject go = new(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// A fixed size box at an anchor. The pivot follows the anchor, so the position reads as a
        /// margin in from that edge and the layout survives a resolution change: 64 in from the left
        /// is 64 in from the left at 1920 and at 1280.
        /// </summary>
        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 position)
        {
            RectTransform rect = Node(parent, name);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = SnappedPivot(anchor);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static Vector2 SnappedPivot(Vector2 anchor) => new(
            anchor.x < 0.5f ? 0f : anchor.x > 0.5f ? 1f : 0.5f,
            anchor.y < 0.5f ? 0f : anchor.y > 0.5f ? 1f : 0.5f);

        /// <summary>A child stretched over its block, inset by the two corners.</summary>
        static RectTransform Inset(Transform parent, string name, Vector2 min, Vector2 max)
        {
            RectTransform rect = Node(parent, name);
            rect.offsetMin = min;
            rect.offsetMax = max;
            return rect;
        }

        /// <summary>
        /// A block that arrives with its screen: a box with the CanvasGroup the transition fades.
        /// Only direct children of a screen's stagger root are collected, so a block's own decoration
        /// comes in with it rather than on a beat of its own.
        /// </summary>
        static RectTransform Step(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 position)
        {
            RectTransform rect = Box(parent, name, anchor, size, position);
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        /// <summary>
        /// Draws a shape into a rect. The slice mode comes from the sprite's own border, because a
        /// bordered sprite stretched is a smeared corner and a borderless one sliced is a no-op, and
        /// neither is worth trusting a caller to remember.
        /// </summary>
        static Image Surface(RectTransform rect, UiShape shape, Color color, bool raycast = false)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;

            Sprite sprite = Hw2Assets.Ui(shape);

            if (sprite == null)
            {
                image.type = Image.Type.Simple;
                return image;
            }

            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        /// <summary>A card: a shadow standing in for depth, a surface, and corner brackets.</summary>
        static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 position,
            UiShape shape, Color fill, bool brackets = false)
        {
            RectTransform rect = Step(parent, name, anchor, size, position);

            // A sliced shadow needs at least its own border of room on every side. Give it less and the
            // gradient is cut off mid falloff at the rect edge, which draws a hard line exactly where the
            // card used to end. The drop therefore goes downward, into the extra margin, not sideways.
            float spread = Hw2Assets.Ui(UiShape.Shadow).border.x;
            Surface(Inset(rect, "Shadow", new Vector2(-spread, -spread - 12f), new Vector2(spread, spread)),
                UiShape.Shadow, Alpha(Base0, 0.62f));
            Surface(Inset(rect, "Back", Vector2.zero, Vector2.zero), shape, fill);

            if (brackets)
            {
                Surface(Inset(rect, "Brackets", new Vector2(-7f, -7f), new Vector2(7f, 7f)), UiShape.Frame,
                    Alpha(Cyan, 0.22f));
            }

            return rect;
        }

        static TMP_Text Label(RectTransform rect, string value, UiFont font, float pointSize, Color color,
            TextAlignmentOptions alignment, float tracking = K_None, float outline = 0f)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Hw2FontImporter.Get(font);

            // The weight is in the font asset, so synthesis stays off: TMP faking bold on a face that
            // already has a bold cut gives a heavier, blurrier stroke than the typeface intended.
            text.fontStyle = FontStyles.Normal;
            text.text = value;
            text.fontSize = pointSize;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = tracking;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            if (outline > 0f)
            {
                text.outlineWidth = outline;
                text.outlineColor = new Color(0f, 0f, 0f, 0.35f);
            }

            return text;
        }

        /// <summary>A line of type that is also a block of its screen's stagger.</summary>
        static TMP_Text StepText(Transform parent, string name, string value, UiFont font, float pointSize,
            Color color, TextAlignmentOptions alignment, Vector2 anchor, Vector2 size, Vector2 position,
            float tracking = K_None)
        {
            return Label(Step(parent, name, anchor, size, position), value, font, pointSize, color, alignment, tracking);
        }

        /// <summary>
        /// A micro caption welded above the value it describes. This replaces the boxed panel label:
        /// the number is the object, and the word over it says what kind of number it is.
        /// </summary>
        /// <summary>
        /// A micro caption welded above a value, which is the only label idiom used here: the number is
        /// the thing, the word over it says what it counts. The block has to be at least the caption
        /// strip plus one and a quarter of the point size tall, or a large number climbs into its own
        /// caption, which is how "SCORE" ended up printed across "1,080".
        /// </summary>
        static TMP_Text Labelled(Transform parent, string name, string caption, string value, Vector2 anchor,
            Vector2 size, Vector2 position, UiFont font, float pointSize, Color color,
            TextAlignmentOptions alignment, float tracking = K_None, Color? captionColor = null)
        {
            RectTransform block = Box(parent, name + "Block", anchor, size, position);
            float strip = T_Micro + 10f;

            Label(Inset(block, "Caption", new Vector2(0f, size.y - strip), Vector2.zero), caption, UiFont.UiBold,
                T_Micro, captionColor ?? InkFaint, alignment, K_Label);

            // offsetMax is measured from the parent's top edge, so the band under the caption is
            // [0, size.y - strip] and its max is -strip. A positive strip here grows the box upwards
            // over the caption instead of downwards, and the number lands on top of its own label.
            return Label(Inset(block, name, Vector2.zero, new Vector2(0f, -strip)), value, font, pointSize, color,
                alignment, tracking);
        }

        /// <summary>
        /// A radial stretched to the aspect of a wide pill is a band with two steep edges, and the eye
        /// reads those edges as a box sitting behind the button rather than as light coming off it. The
        /// halo therefore keeps its own proportions: it grows sideways with the widget, but its height is
        /// derived from its width, so the falloff runs over a comparable distance on both axes.
        /// </summary>
        static RectTransform Halo(RectTransform owner, string name, Vector2 size, Vector2 offset = default)
        {
            float width = size.x + 100f;
            float height = Mathf.Max(width / 2.4f, size.y + 130f);

            return Step(owner, name, MidMid, new Vector2(width, height), offset);
        }

        /// <summary>A filled pill: the one thing on a screen the player is meant to press.</summary>
        static Button PillButton(Transform parent, string name, string caption, Vector2 anchor, Vector2 size,
            Vector2 position, Color fill, Color textColor, float captionSize = T_Btn_L, string keyHint = null)
        {
            RectTransform rect = Step(parent, name, anchor, size, position);

            Image halo = Surface(Halo(rect, "Glow", size), UiShape.Bloom, Alpha(Magenta, 0.16f));
            Image surface = Surface(Inset(rect, "Back", Vector2.zero, Vector2.zero), PillFor(size.y), fill,
                raycast: true);
            Label(Inset(rect, "Label", Vector2.zero, Vector2.zero), caption, UiFont.UiBold, captionSize, textColor,
                TextAlignmentOptions.Center, K_Label);

            return WireButton(rect, surface, halo, keyHint);
        }

        /// <summary>An outlined pill: a thing the player can press, but is not being told to press.</summary>
        static Button GhostButton(Transform parent, string name, string caption, Vector2 anchor, Vector2 size,
            Vector2 position, float captionSize = T_Btn_M, string keyHint = null)
        {
            RectTransform rect = Step(parent, name, anchor, size, position);

            Image halo = Surface(Halo(rect, "Glow", size), UiShape.Bloom, Alpha(Cyan, 0.10f));
            Surface(Inset(rect, "Fill", Vector2.zero, Vector2.zero), PillFor(size.y), Alpha(Base1, 0.72f));
            Image surface = Surface(Inset(rect, "Edge", Vector2.zero, Vector2.zero),
                size.y >= 56f ? UiShape.OutlineM : UiShape.OutlineS, Alpha(InkSoft, 0.50f), raycast: true);
            Label(Inset(rect, "Label", Vector2.zero, Vector2.zero), caption, UiFont.UiBold, captionSize, InkSoft,
                TextAlignmentOptions.Center, K_Label);

            return WireButton(rect, surface, halo, keyHint);
        }

        /// <summary>
        /// The three surfaces a button is made of, handed to the component that animates them, plus the
        /// brackets that say "this one is selected" without changing what the button looks like.
        /// </summary>
        static Button WireButton(RectTransform rect, Image surface, Image halo, string keyHint)
        {
            // Cyan, always, rather than the button's own accent: magenta brackets around a magenta pill
            // are invisible, and the brackets are the only thing that says "this one is selected".
            Image frame = Surface(Inset(rect, "Focus", new Vector2(-9f, -9f), new Vector2(9f, 9f)), UiShape.Frame,
                Alpha(Cyan, 0.95f));

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;

            rect.gameObject.AddComponent<NeonButton>().Wire(surface, halo, frame);

            if (!string.IsNullOrEmpty(keyHint))
            {
                // A cap, not a glyph: the arrows and Escape keys have no characters in any of the three
                // faces, and a tofu box in a menu is worse than no hint at all.
                float width = 26f + keyHint.Length * 15f;

                // Inside the pill, and dark against it. A chip floating off the edge reads as a second
                // button, and a light cap on a light pill reads as nothing at all. The same two colours
                // work on a filled and an outlined button, so the hint is not a parameter.
                KeyChip(rect, "Hint", keyHint, MidRight, new Vector2(width, 40f), new Vector2(-18f, 0f),
                    Alpha(Base0, 0.55f), Ink);
            }

            return button;
        }

        /// <summary>A key cap and the word for what it does.</summary>
        static RectTransform KeyChip(Transform parent, string name, string key, Vector2 anchor, Vector2 size,
            Vector2 position, Color cap, Color text)
        {
            RectTransform rect = Box(parent, name, anchor, size, position);
            Surface(rect, UiShape.Key, cap);
            Label(Inset(rect, "Label", Vector2.zero, Vector2.zero), key, UiFont.UiBold, T_Btn_S, text,
                TextAlignmentOptions.Center, K_Label);
            return rect;
        }

        /// <summary>
        /// A row of key caps, laid out by hand from measured widths. A layout group would mark the
        /// canvas dirty on every frame of the stagger that brings the screen in, and the transition is
        /// the one thing here that must not stutter.
        /// </summary>
        static RectTransform KeyRow(Transform parent, string name, Vector2 anchor, Vector2 position,
            params (string Key, string Note)[] keys)
        {
            const float capHeight = 40f;
            const float capPad = 26f;
            const float keyWidth = 15f;
            const float noteWidth = 10.5f;
            const float join = 10f;
            const float space = 26f;

            float[] cap = new float[keys.Length];
            float[] note = new float[keys.Length];
            float total = 0f;

            for (int i = 0; i < keys.Length; i++)
            {
                cap[i] = capPad + keys[i].Key.Length * keyWidth;
                note[i] = keys[i].Note.Length > 0 ? keys[i].Note.Length * noteWidth : 0f;
                total += cap[i] + (note[i] > 0f ? join + note[i] : 0f) + space;
            }

            total -= space;

            RectTransform row = Step(parent, name, anchor, new Vector2(total, capHeight), position);
            float x = 0f;

            for (int i = 0; i < keys.Length; i++)
            {
                KeyChip(row, "Key" + i, keys[i].Key, LowLeft, new Vector2(cap[i], capHeight), new Vector2(x, 0f),
                    Alpha(Base2, 0.95f), InkSoft);
                x += cap[i];

                if (note[i] > 0f)
                {
                    x += join;
                    Label(Box(row, "Note" + i, LowLeft, new Vector2(note[i] + 8f, capHeight), new Vector2(x, 0f)),
                        keys[i].Note, UiFont.UiBold, T_Micro, InkFaint, TextAlignmentOptions.Left, K_Label);
                    x += note[i];
                }

                x += space;
            }

            return row;
        }

        /// <summary>
        /// A track, its fill and the light at the end of it. The fill is a left pinned box whose width
        /// is written every frame, so the gauge cannot report a number it is not showing.
        /// </summary>
        static MeterBar Meter(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 position,
            MeterBar.ColorStop[] ramp, Color groove, TMP_Text readout = null, float inset = 3f,
            float smoothing = 9f, float warnThreshold = 0.28f, bool tipGlow = true)
        {
            RectTransform track = Box(parent, name, anchor, size, position);
            Surface(Inset(track, "Groove", Vector2.zero, Vector2.zero), UiShape.PillS, groove);
            Surface(Inset(track, "Edge", Vector2.zero, Vector2.zero), UiShape.OutlineS, Alpha(Cyan, 0.26f));

            RectTransform fill = Box(track, "Fill", MidLeft, new Vector2(inset, size.y - inset * 2f),
                new Vector2(inset, 0f));
            Image fillImage = Surface(fill, UiShape.PillS, ramp[ramp.Length - 1].color);

            // The leading edge of the tank, not a glow. A soft dot floating off the end of a gauge reads
            // as a light source rather than as a quantity, and it is the only element on the HUD that
            // reports nothing. Pivot (1, 0.5) hangs it backwards from the end, so it sits inside the
            // pill's own curve instead of cutting it square.
            RectTransform tip = null;

            if (tipGlow)
            {
                float capHeight = Mathf.Max(4f, size.y - inset * 2f - 10f);
                tip = Box(track, "Tip", MidLeft, new Vector2(6f, capHeight), new Vector2(inset, 0f));
                tip.pivot = new Vector2(1f, 0.5f);
                Surface(tip, UiShape.Quad, Alpha(Lift(ramp[ramp.Length - 1].color), 0.9f));
            }

            if (warnThreshold > 0f)
            {
                Surface(Box(track, "WarnTick", MidLeft, new Vector2(2f, size.y - inset * 2f),
                    new Vector2(inset + (size.x - inset * 2f) * warnThreshold, 0f)), UiShape.Quad,
                    Alpha(Ink, 0.22f));
            }

            MeterBar meter = fill.gameObject.AddComponent<MeterBar>();
            meter.Wire(track, fill, fillImage, tip, readout, ramp, inset, smoothing, warnThreshold);
            return meter;
        }

        /// <summary>The chip that says a score has just become the best. It breathes; nothing else does.</summary>
        static RectTransform RecordChip(Transform parent, string name, string caption, Vector2 anchor, Vector2 size,
            Vector2 position)
        {
            RectTransform rect = Step(parent, name, anchor, size, position);
            Image halo = Surface(Halo(rect, "Glow", size), UiShape.Bloom, Alpha(Magenta, 0.24f));
            Surface(Inset(rect, "Back", Vector2.zero, Vector2.zero), PillFor(size.y), Magenta);
            TMP_Text label = Label(Inset(rect, "Label", Vector2.zero, Vector2.zero), caption, UiFont.UiBold,
                T_Btn_S, InkOnMag, TextAlignmentOptions.Center, K_Label);

            halo.gameObject.AddComponent<NeonPulse>().Wire(seconds: 1.2f, alphaDepth: 0.45f, scaleDepth: 0.08f);
            label.gameObject.AddComponent<NeonPulse>().Wire(seconds: 1.2f, alphaDepth: 0.12f, scaleDepth: 0f);

            return rect;
        }

        /// <summary>
        /// Gives a screen its motion. Without this a screen still works, it just appears, which is what
        /// made the old menus feel like a still image rather than part of the game.
        /// </summary>
        static void PanelFx(RectTransform root, RectTransform steps)
        {
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<PanelTransition>().Wire(steps);
        }

        static void Scrim(Transform root, Color color)
        {
            Surface(Node(root, "Scrim"), UiShape.Quad, color);
        }

        /// <summary>The capsule family is chosen by the height it has to round.</summary>
        static UiShape PillFor(float height) =>
            height >= 80f ? UiShape.PillL : height >= 56f ? UiShape.PillM : UiShape.PillS;

        /// <summary>A palette entry at a different alpha; nearly every surface here is one.</summary>
        static Color Alpha(Color color, float a)
        {
            Color copy = color;
            copy.a = a;
            return copy;
        }

        /// <summary>The same hue, cleared of its own dark cast, for a tinted white sprite.</summary>
        static Color Lift(Color color) => new(Mathf.Max(color.r, 0.45f), Mathf.Max(color.g, 0.45f),
            Mathf.Max(color.b, 0.45f), color.a);

        static MeterBar.ColorStop[] Ramp(params (float At, Color Color)[] stops)
        {
            MeterBar.ColorStop[] ramp = new MeterBar.ColorStop[stops.Length];

            for (int i = 0; i < stops.Length; i++)
            {
                ramp[i] = new MeterBar.ColorStop { at = stops[i].At, color = stops[i].Color };
            }

            return ramp;
        }
    }
}
