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

        static readonly Color PanelColor = new(0.035f, 0.05f, 0.09f, 0.78f);
        static readonly Color Accent = new(0.2f, 0.95f, 0.9f, 1f);
        static readonly Color DimText = new(0.68f, 0.74f, 0.86f, 1f);
        static readonly Color BrightText = new(0.96f, 0.98f, 1f, 1f);
        static readonly Color ButtonColor = new(0.09f, 0.36f, 0.42f, 0.95f);

        // ---------------------------------------------------------------- menu

        [MenuItem("HW2/Build Game (assets + scene)")]
        public static void BuildGame()
        {
            Hw2Assets.BuildAll();

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
                        || property.objectReferenceValue != null)
                    {
                        continue;
                    }

                    problems++;
                    Debug.LogWarning($"{behaviour.GetType().Name} on '{behaviour.gameObject.name}' has an empty '{property.name}'.", behaviour);
                }
            }

            Debug.Log(problems == 0 ? "HW2: every scene reference is wired." : $"HW2: {problems} empty scene reference(s).");
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

            // Speed, bottom left. The number grows leftwards from the unit label so a
            // three digit reading never touches it.
            RectTransform speedPanel = Panel(hud, "SpeedPanel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(250f, 76f), new Vector2(48f, 40f));
            Text(Node(speedPanel, "SpeedValue"), "0", 58f, BrightText, TextAlignmentOptions.Right,
                new Vector2(0f, 0.22f), new Vector2(0.7f, 1f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), true);
            Text(Node(speedPanel, "Unit"), "KM/H", 20f, Accent, TextAlignmentOptions.Left,
                new Vector2(0.72f, 0.24f), new Vector2(1f, 0.72f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), true);

            // Score, top centre.
            RectTransform scorePanel = Panel(hud, "ScorePanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(460f, 128f), new Vector2(0f, -28f));
            Text(Node(scorePanel, "ScoreValue"), "0", 62f, BrightText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.42f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, true);
            Text(Node(scorePanel, "BestValue"), "Best 0", 26f, DimText, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, true);

            // Fuel bar, top left.
            RectTransform fuelPanel = Panel(hud, "FuelPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(430f, 92f), new Vector2(48f, -34f));
            Text(Node(fuelPanel, "Title"), "FUEL", 24f, DimText, TextAlignmentOptions.Left,
                new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), true);

            RectTransform track = Node(fuelPanel, "Track");
            Stretch(track, new Vector2(16f, 16f), new Vector2(-16f, -40f), PanelColor);
            Image fill = Solid(Node(track, "Fill"), SpriteKind.Rounded, Accent);
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(6f, 6f);
            fillRect.offsetMax = new Vector2(-6f, -6f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            // Lives, top right.
            RectTransform lifePanel = Panel(hud, "LifePanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(330f, 92f), new Vector2(-48f, -34f));
            Text(Node(lifePanel, "Title"), "LIVES", 24f, DimText, TextAlignmentOptions.Right,
                new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), true);

            RectTransform lives = Node(lifePanel, "Lives");
            Stretch(lives, new Vector2(-14f, 10f), new Vector2(14f, -46f), Color.clear);
            for (int i = 0; i < 3; i++)
            {
                RectTransform heart = Node(lives, "Life" + i);
                heart.anchorMin = new Vector2(1f, 0f);
                heart.anchorMax = new Vector2(1f, 1f);
                heart.pivot = new Vector2(1f, 0.5f);
                heart.sizeDelta = new Vector2(26f, 26f);
                heart.anchoredPosition = new Vector2(-i * 34f, 0f);
                Solid(heart, SpriteKind.Rounded, new Color(Accent.r, Accent.g, Accent.b, 0.92f));
            }

            // Full screen damage wash, animated by UIManager on every hit.
            RectTransform flash = Node(hud, "DamageFlash");
            Stretch(flash, Vector2.zero, Vector2.zero, Color.clear);
            Image flashImage = Solid(flash, SpriteKind.None, new Color(1f, 0.2f, 0.16f, 0f));
            flashImage.raycastTarget = false;

            foreach (Image image in hud.GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = false;
            }

            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                text.raycastTarget = false;
            }
        }

        static void BuildMenu(Transform root)
        {
            RectTransform menu = Node(root, "Menu");
            Scrim(menu, new Color(0.004f, 0.01f, 0.03f, 0.72f));

            RectTransform panel = Panel(menu, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(680f, 620f), Vector2.zero);

            Text(Node(panel, "Title"), "NIGHT RUN", 104f, BrightText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.76f), new Vector2(1f, 0.99f), new Vector2(0.5f, 0.5f), Vector2.zero, true)
                .outlineColor = Accent;
            Text(Node(panel, "Subtitle"), "Endless neon highway", 28f, Accent, TextAlignmentOptions.Center,
                new Vector2(0f, 0.68f), new Vector2(1f, 0.76f), new Vector2(0.5f, 0.5f), Vector2.zero, true);

            ButtonRow(Node(panel, "StartButton"), "START", 44f, new Vector2(0.16f, 0.48f), new Vector2(0.84f, 0.62f));
            ButtonRow(Node(panel, "QuitButton"), "QUIT", 34f, new Vector2(0.24f, 0.28f), new Vector2(0.76f, 0.4f));

            Text(Node(panel, "BestValue"), "Best  0", 30f, BrightText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.16f), new Vector2(1f, 0.26f), new Vector2(0.5f, 0.5f), Vector2.zero, true);

            Caption(Node(panel, "ControlsHint"), "A / D or arrow keys steer   -   Esc pauses", 22f,
                new Vector2(0f, 0.03f), new Vector2(1f, 0.12f), DimText);
        }

        static void BuildPause(Transform root)
        {
            RectTransform pause = Node(root, "Pause");
            Scrim(pause, new Color(0.004f, 0.01f, 0.03f, 0.82f));

            RectTransform panel = Panel(pause, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(620f, 560f), Vector2.zero);

            Text(Node(panel, "Title"), "PAUSED", 84f, BrightText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, true);

            ButtonRow(Node(panel, "ResumeButton"), "RESUME", 40f, new Vector2(0.1f, 0.46f), new Vector2(0.9f, 0.62f));
            ButtonRow(Node(panel, "RestartButton"), "RESTART", 40f, new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.42f));
            ButtonRow(Node(panel, "MenuButton"), "MAIN MENU", 40f, new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.22f));
        }

        static void BuildGameOver(Transform root)
        {
            RectTransform over = Node(root, "GameOver");
            Scrim(over, new Color(0.06f, 0.005f, 0.01f, 0.8f));

            RectTransform panel = Panel(over, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(720f, 640f), Vector2.zero);

            Text(Node(panel, "Title"), "GAME OVER", 92f, new Color(1f, 0.4f, 0.34f), TextAlignmentOptions.Center,
                new Vector2(0f, 0.76f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, true);
            Text(Node(panel, "ReasonValue"), "", 30f, DimText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.68f), new Vector2(1f, 0.76f), new Vector2(0.5f, 0.5f), Vector2.zero, true);
            Text(Node(panel, "ScoreValue"), "Score  0", 54f, BrightText, TextAlignmentOptions.Center,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, true);
            Text(Node(panel, "BestValue"), "Best  0", 36f, Accent, TextAlignmentOptions.Center,
                new Vector2(0f, 0.4f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, true);
            Text(Node(panel, "NewRecord"), "NEW RECORD!", 34f, new Color(1f, 0.86f, 0.3f), TextAlignmentOptions.Center,
                new Vector2(0f, 0.35f), new Vector2(1f, 0.43f), new Vector2(0.5f, 0.5f), Vector2.zero, true);

            ButtonRow(Node(panel, "RestartButton"), "RESTART", 42f, new Vector2(0.12f, 0.19f), new Vector2(0.88f, 0.33f));
            ButtonRow(Node(panel, "MenuButton"), "MAIN MENU", 34f, new Vector2(0.22f, 0.05f), new Vector2(0.78f, 0.16f));
        }

        // ---------------------------------------------------------------- UI primitives

        enum SpriteKind
        {
            None,
            Rounded
        }

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

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Color background)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;

            if (background.a > 0f)
            {
                Solid(rect, SpriteKind.Rounded, background);
            }
        }

        static Image Solid(RectTransform rect, SpriteKind kind, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (kind == SpriteKind.Rounded)
            {
                image.sprite = Hw2Assets.Panel;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 anchorMax,
            Vector2 pivot, Vector2 size, Vector2 position)
        {
            RectTransform rect = Node(parent, name);
            rect.anchorMin = anchor;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Solid(rect, SpriteKind.Rounded, PanelColor);
            return rect;
        }

        static void Scrim(RectTransform root, Color color)
        {
            RectTransform rect = Node(root, "Scrim");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Solid(rect, SpriteKind.None, color);
        }

        static TMP_Text Text(RectTransform rect, string value, float size, Color color,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, bool outline)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            if (outline)
            {
                text.outlineWidth = 0.14f;
                text.outlineColor = new Color(0f, 0f, 0f, 0.55f);
            }

            return text;
        }

        static void ButtonRow(RectTransform rect, string caption, float size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(20f, 8f);
            rect.offsetMax = new Vector2(-20f, -8f);

            Image background = Solid(rect, SpriteKind.Rounded, ButtonColor);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.45f, 1.5f, 1f);
            colors.pressedColor = new Color(0.7f, 0.8f, 0.85f, 1f);
            colors.selectedColor = new Color(1.25f, 1.45f, 1.5f, 1f);
            button.colors = colors;

            RectTransform label = Node(rect, "Label");
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            Text(label, caption, size, BrightText, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, false);
        }

        /// <summary>Plain caption row used for the control hints inside the menu.</summary>
        static void Caption(RectTransform rect, string value, float size, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Text(rect, value, size, color, TextAlignmentOptions.Center, anchorMin, anchorMax,
                new Vector2(0.5f, 0.5f), Vector2.zero, false);
        }
    }
}
