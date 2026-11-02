using UnityEditor;
using UnityEngine;

namespace HW2.EditorTools
{
    /// <summary>
    /// Assembles every game prefab from Unity primitives plus procedural particle systems, then
    /// saves them as prefab assets. The runtime classes resolve their own children by name, so
    /// the hierarchy naming here (Paint_, Neon_, Headlight_, Wheel_, Chassis) is part of the API.
    /// </summary>
    public static class Hw2Prefabs
    {
        public const string PrefabFolder = "Assets/Prefabs";

        const float LaneWidth = 3.5f;
        const float RoadWidth = 11.5f;
        const float SegmentLength = 60f;

        static Transform source;

        // ---------------------------------------------------------------- shape helpers

        static GameObject Root(string name)
        {
            GameObject go = new(name);
            go.transform.SetParent(source, false);
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Disc(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Ball(Transform parent, string name, Vector3 position, float diameter, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * diameter;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>One road wheel: an axle-aligned container plus a tire, a rim and a spoke.</summary>
        static void Wheel(Transform parent, string name, Vector3 position, bool leftSide)
        {
            GameObject hub = new("Wheel_" + name);
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = position;
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, leftSide ? 90f : -90f);

            Disc(hub.transform, "Tire_" + name, Vector3.zero, new Vector3(0.86f, 0.32f, 0.86f), Hw2Assets.Get("M_Tire"));
            Disc(hub.transform, "Rim_" + name, new Vector3(0f, 0.17f, 0f), new Vector3(0.54f, 0.05f, 0.54f), Hw2Assets.Get("M_Rim"));
            Box(hub.transform, "Spoke_" + name, new Vector3(0f, 0.19f, 0f), new Vector3(0.5f, 0.02f, 0.07f), Hw2Assets.Get("M_Rim"));
        }

        static Light AddLight(
            Transform parent, string name, LightType type, Vector3 position, Vector3 euler,
            Color color, float intensity, float range, float spotAngle)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);

            Light light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.shadows = LightShadows.None;
            return light;
        }

        // ---------------------------------------------------------------- particle helper

        sealed class Burst
        {
            public int count = 200;
            public float minLife = 0.4f;
            public float maxLife = 1.2f;
            public float minSpeed = 4f;
            public float maxSpeed = 12f;
            public float minSize = 0.08f;
            public float maxSize = 0.3f;
            public float gravity = 0.6f;
            public Color color = Color.white;
            public string material = "P_Spark";
            public bool stretch;
            public bool loop;
            public float rate = 60f;
            public float coneAngle = 25f;
            public float radius = 0.12f;
            public bool smokeShape;
        }

        static ParticleSystem Emit(Transform parent, string name, Vector3 position, Vector3 euler, Burst config)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = config.loop;
            main.playOnAwake = false;
            main.duration = 2f;
            main.maxParticles = config.count * 4;
            main.startLifetime = new ParticleSystem.MinMaxCurve(config.minLife, config.maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(config.minSpeed, config.maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(config.minSize, config.maxSize);
            main.startColor = config.color;
            main.gravityModifier = config.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = config.loop ? config.rate : 0f;
            if (!config.loop)
            {
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, config.count) });
            }

            var shape = ps.shape;
            if (config.smokeShape)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = config.coneAngle;
                shape.radius = config.radius;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = config.radius;
            }

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            Gradient gradient = new();
            gradient.colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.75f),
                new GradientColorKey(Color.white, 1f)
            };
            gradient.alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.55f, 0.45f),
                new GradientAlphaKey(0.05f, 1f)
            };
            colorOver.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Hw2Assets.Get(config.material);
            renderer.sortingFudge = 2f;
            renderer.renderMode = config.stretch
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            if (config.stretch)
            {
                renderer.velocityScale = 0.045f;
                renderer.lengthScale = 2.6f;
            }

            return ps;
        }

        // ---------------------------------------------------------------- player car

        static GameObject BuildPlayerCar()
        {
            GameObject go = Root("PlayerCar");
            go.transform.localPosition = Vector3.zero;

            BoxCollider body = go.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.62f, 0f);
            body.size = new Vector3(1.98f, 1.15f, 4.3f);
            body.isTrigger = false;

            go.AddComponent<Rigidbody>();
            go.AddComponent<WheelSpinner>();
            go.AddComponent<PlayerCar>();

            AudioSource engineSource = go.AddComponent<AudioSource>();
            engineSource.playOnAwake = false;
            go.AddComponent<EngineSound>();

            Transform chassis = new GameObject("Chassis").transform;
            chassis.SetParent(go.transform, false);

            Material paint = Hw2Assets.Get("M_CarPlayer");
            Material glass = Hw2Assets.Get("M_Glass");
            Material neon = Hw2Assets.Get("M_Neon");
            Material lamp = Hw2Assets.Get("M_Headlight");
            Material tail = Hw2Assets.Get("M_Taillight");

            Box(chassis, "Paint_Body", new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 0.62f, 4.2f), paint);
            Box(chassis, "Paint_Cabin", new Vector3(0f, 1.06f, -0.22f), new Vector3(1.58f, 0.44f, 1.95f), paint);
            Box(chassis, "Paint_Nose", new Vector3(0f, 0.52f, 1.9f), new Vector3(1.86f, 0.34f, 0.5f), paint);
            Box(chassis, "Paint_Tail", new Vector3(0f, 0.52f, -1.95f), new Vector3(1.86f, 0.36f, 0.4f), paint);

            GameObject wind = Box(chassis, "Glass_Windshield", new Vector3(0f, 1.07f, 0.72f), new Vector3(1.46f, 0.4f, 0.85f), glass);
            wind.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            GameObject rear = Box(chassis, "Glass_Rear", new Vector3(0f, 1.08f, -1.16f), new Vector3(1.46f, 0.36f, 0.7f), glass);
            rear.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            // Underglow: emissive strips plus two point lights, both driven by speed at runtime.
            Box(chassis, "Neon_Strip_L", new Vector3(-0.93f, 0.2f, 0f), new Vector3(0.1f, 0.06f, 3.4f), neon);
            Box(chassis, "Neon_Strip_R", new Vector3(0.93f, 0.2f, 0f), new Vector3(0.1f, 0.06f, 3.4f), neon);
            Box(chassis, "Neon_Front", new Vector3(0f, 0.2f, 2.06f), new Vector3(1.7f, 0.05f, 0.08f), neon);
            Box(chassis, "Neon_Rear", new Vector3(0f, 0.2f, -2.1f), new Vector3(1.7f, 0.05f, 0.08f), neon);
            AddLight(chassis, "Neon_Light_L", LightType.Point, new Vector3(-0.7f, 0.16f, 0.5f), Vector3.zero, Color.white, 1.5f, 6f, 0f);
            AddLight(chassis, "Neon_Light_R", LightType.Point, new Vector3(0.7f, 0.16f, -0.5f), Vector3.zero, Color.white, 1.5f, 6f, 0f);

            Box(chassis, "Headlight_Lens_L", new Vector3(-0.62f, 0.78f, 2.14f), new Vector3(0.36f, 0.16f, 0.08f), lamp);
            Box(chassis, "Headlight_Lens_R", new Vector3(0.62f, 0.78f, 2.14f), new Vector3(0.36f, 0.16f, 0.08f), lamp);
            AddLight(chassis, "Headlight_Beam_L", LightType.Spot, new Vector3(-0.62f, 0.74f, 2.1f), new Vector3(6f, 4f, 0f), Color.white, 3.2f, 34f, 42f);
            AddLight(chassis, "Headlight_Beam_R", LightType.Spot, new Vector3(0.62f, 0.74f, 2.1f), new Vector3(6f, -4f, 0f), Color.white, 3.2f, 34f, 42f);

            Box(chassis, "Taillight_L", new Vector3(-0.68f, 0.74f, -2.14f), new Vector3(0.34f, 0.14f, 0.06f), tail);
            Box(chassis, "Taillight_R", new Vector3(0.68f, 0.74f, -2.14f), new Vector3(0.34f, 0.14f, 0.06f), tail);

            Wheel(chassis, "FL", new Vector3(-0.88f, 0.43f, 1.32f), true);
            Wheel(chassis, "FR", new Vector3(0.88f, 0.43f, 1.32f), false);
            Wheel(chassis, "RL", new Vector3(-0.88f, 0.43f, -1.38f), true);
            Wheel(chassis, "RR", new Vector3(0.88f, 0.43f, -1.38f), false);

            Emit(chassis, "ExhaustFx", new Vector3(-0.42f, 0.34f, -2.25f), new Vector3(0f, 180f, 0f), new Burst
            {
                loop = true,
                rate = 12f,
                count = 400,
                minLife = 0.45f,
                maxLife = 1.15f,
                minSpeed = 1.8f,
                maxSpeed = 4.2f,
                minSize = 0.24f,
                maxSize = 0.75f,
                gravity = -0.08f,
                color = new Color(0.34f, 0.34f, 0.38f, 0.4f),
                material = "P_Smoke",
                smokeShape = true,
                coneAngle = 14f,
                radius = 0.1f
            });

            return go;
        }

        // ---------------------------------------------------------------- obstacles

        static GameObject BuildCarBody(string name, string material, string[] windowTint)
        {
            GameObject go = Root(name);

            BoxCollider body = go.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.68f, 0f);
            body.size = new Vector3(1.96f, 1.4f, 4.25f);

            go.AddComponent<Rigidbody>();

            Material paint = Hw2Assets.Get(material);
            Material glass = Hw2Assets.Get(windowTint[0]);
            Material tail = Hw2Assets.Get(windowTint[1]);

            Box(go.transform, "Body", new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.72f, 4.1f), paint);
            Box(go.transform, "Cabin", new Vector3(0f, 1.2f, -0.3f), new Vector3(1.62f, 0.52f, 2f), paint);
            Box(go.transform, "Windows", new Vector3(0f, 1.2f, -0.3f), new Vector3(1.66f, 0.4f, 1.6f), glass);
            Box(go.transform, "Tail_L", new Vector3(-0.66f, 0.78f, -2.1f), new Vector3(0.4f, 0.16f, 0.08f), tail);
            Box(go.transform, "Tail_R", new Vector3(0.66f, 0.78f, -2.1f), new Vector3(0.4f, 0.16f, 0.08f), tail);

            return go;
        }

        static GameObject BuildStaticObstacle()
        {
            GameObject go = BuildCarBody("StaticObstacle", "M_CarStatic", new[] { "M_Glass", "M_Taillight" });

            // A stopped car sits slightly crooked and has its hazard lights glowing.
            Box(go.transform, "Debris", new Vector3(0.4f, 0.1f, 2.4f), new Vector3(0.9f, 0.16f, 0.5f), Hw2Assets.Get("M_Truck"));
            Disc(go.transform, "Wheel_FL", new Vector3(-0.9f, 0.42f, 1.3f), new Vector3(0.84f, 0.3f, 0.84f), Hw2Assets.Get("M_Tire")).transform
                .localRotation = Quaternion.Euler(0f, 0f, 90f);
            Disc(go.transform, "Wheel_FR", new Vector3(0.9f, 0.42f, 1.3f), new Vector3(0.84f, 0.3f, 0.84f), Hw2Assets.Get("M_Tire")).transform
                .localRotation = Quaternion.Euler(0f, 0f, -90f);
            Disc(go.transform, "Wheel_RL", new Vector3(-0.9f, 0.42f, -1.35f), new Vector3(0.84f, 0.3f, 0.84f), Hw2Assets.Get("M_Tire")).transform
                .localRotation = Quaternion.Euler(0f, 0f, 90f);
            Disc(go.transform, "Wheel_RR", new Vector3(0.9f, 0.42f, -1.35f), new Vector3(0.84f, 0.3f, 0.84f), Hw2Assets.Get("M_Tire")).transform
                .localRotation = Quaternion.Euler(0f, 0f, -90f);

            go.AddComponent<StaticObstacle>();
            return go;
        }

        static GameObject BuildMovingObstacle()
        {
            GameObject go = BuildCarBody("MovingObstacle", "M_CarMoving", new[] { "M_Glass", "M_Taillight" });

            Wheel(go.transform, "FL", new Vector3(-0.88f, 0.43f, 1.3f), true);
            Wheel(go.transform, "FR", new Vector3(0.88f, 0.43f, 1.3f), false);
            Wheel(go.transform, "RL", new Vector3(-0.88f, 0.43f, -1.35f), true);
            Wheel(go.transform, "RR", new Vector3(0.88f, 0.43f, -1.35f), false);

            Material lamp = Hw2Assets.Get("M_Headlight");
            Box(go.transform, "Headlight_L", new Vector3(-0.62f, 0.8f, 2.1f), new Vector3(0.36f, 0.16f, 0.08f), lamp);
            Box(go.transform, "Headlight_R", new Vector3(0.62f, 0.8f, 2.1f), new Vector3(0.36f, 0.16f, 0.08f), lamp);

            go.AddComponent<WheelSpinner>();
            go.AddComponent<MovingObstacle>();
            return go;
        }

        // ---------------------------------------------------------------- collectibles

        static GameObject BuildCoin()
        {
            GameObject go = Root("Coin");
            go.AddComponent<SphereCollider>().radius = 0.62f;
            go.AddComponent<Rigidbody>();

            GameObject spin = new("Spin");
            spin.transform.SetParent(go.transform, false);
            spin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Disc(spin.transform, "Disc", Vector3.zero, new Vector3(0.9f, 0.12f, 0.9f), Hw2Assets.Get("M_Coin"));
            Disc(spin.transform, "Ring", new Vector3(0f, 0.07f, 0f), new Vector3(0.62f, 0.02f, 0.62f), Hw2Assets.Get("M_Taillight"));

            Coin coin = go.AddComponent<Coin>();
            SerializedObject so = new(coin);
            so.FindProperty("pointValue").intValue = 50;
            so.FindProperty("burstColor").colorValue = new Color(1f, 0.8f, 0.3f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        static GameObject BuildFuelCan()
        {
            GameObject go = Root("FuelCan");
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.85f, 1.05f, 0.55f);
            go.AddComponent<Rigidbody>();

            Material can = Hw2Assets.Get("M_FuelCan");
            Box(go.transform, "Body", Vector3.zero, new Vector3(0.72f, 0.92f, 0.42f), Hw2Assets.Get("M_Truck"));
            Box(go.transform, "Cap", new Vector3(0.2f, 0.55f, 0f), new Vector3(0.2f, 0.18f, 0.2f), Hw2Assets.Get("M_Pole"));
            Box(go.transform, "Spout", new Vector3(-0.3f, 0.5f, 0f), new Vector3(0.16f, 0.3f, 0.16f), Hw2Assets.Get("M_Pole"));
            Box(go.transform, "Glow", new Vector3(0f, -0.05f, 0.23f), new Vector3(0.5f, 0.42f, 0.05f), can);
            Box(go.transform, "GlowBack", new Vector3(0f, -0.05f, -0.23f), new Vector3(0.5f, 0.42f, 0.05f), can);

            FuelCan fuel = go.AddComponent<FuelCan>();
            SerializedObject so = new(fuel);
            so.FindProperty("pointValue").intValue = 150;
            so.FindProperty("burstColor").colorValue = new Color(0.3f, 1f, 0.5f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ---------------------------------------------------------------- road segment

        static GameObject BuildRoadSegment()
        {
            GameObject go = Root("RoadSegment");
            go.AddComponent<RoadSegment>();

            float mid = SegmentLength * 0.5f;

            Box(go.transform, "Asphalt", new Vector3(0f, -0.05f, mid), new Vector3(RoadWidth, 0.1f, SegmentLength), Hw2Assets.Get("M_Asphalt"));
            Box(go.transform, "Shoulder_L", new Vector3(-10f, -0.06f, mid), new Vector3(8.5f, 0.12f, SegmentLength), Hw2Assets.Get("M_Shoulder"));
            Box(go.transform, "Shoulder_R", new Vector3(10f, -0.06f, mid), new Vector3(8.5f, 0.12f, SegmentLength), Hw2Assets.Get("M_Shoulder"));

            // Solid edge lines and dashed lane dividers.
            Box(go.transform, "Edge_L", new Vector3(-5.5f, 0.012f, mid), new Vector3(0.14f, 0.02f, SegmentLength), Hw2Assets.Get("U_EdgeLine"));
            Box(go.transform, "Edge_R", new Vector3(5.5f, 0.012f, mid), new Vector3(0.14f, 0.02f, SegmentLength), Hw2Assets.Get("U_EdgeLine"));

            for (int laneBoundary = -1; laneBoundary <= 1; laneBoundary += 2)
            {
                float x = laneBoundary * LaneWidth * 0.5f;
                for (int i = 0; i < 7; i++)
                {
                    float z = 4f + i * 8f;
                    Box(go.transform, $"Dash_{(laneBoundary > 0 ? "R" : "L")}{i}",
                        new Vector3(x, 0.012f, z), new Vector3(0.16f, 0.02f, 3.2f), Hw2Assets.Get("U_LaneDash"));
                }
            }

            // Barrier wall plus a steel guardrail on each side.
            Box(go.transform, "Barrier_L", new Vector3(-6.1f, 0.3f, mid), new Vector3(0.5f, 0.6f, SegmentLength), Hw2Assets.Get("M_Concrete"));
            Box(go.transform, "Barrier_R", new Vector3(6.1f, 0.3f, mid), new Vector3(0.5f, 0.6f, SegmentLength), Hw2Assets.Get("M_Concrete"));
            Box(go.transform, "Rail_L", new Vector3(-5.86f, 0.8f, mid), new Vector3(0.22f, 0.18f, SegmentLength), Hw2Assets.Get("M_Guardrail"));
            Box(go.transform, "Rail_R", new Vector3(5.86f, 0.8f, mid), new Vector3(0.22f, 0.18f, SegmentLength), Hw2Assets.Get("M_Guardrail"));

            // Streetlights alternate sides every 20 m; the lamp head glows into the Bloom pass.
            for (int i = 0; i < 3; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = 10f + i * 20f;
                Transform lamp = new GameObject($"Streetlight_{i}").transform;
                lamp.SetParent(go.transform, false);
                lamp.localPosition = new Vector3(side * 6.9f, 0f, z);

                Box(lamp, "Pole", new Vector3(0f, 3.7f, 0f), new Vector3(0.22f, 7.4f, 0.22f), Hw2Assets.Get("M_Pole"));
                Box(lamp, "Arm", new Vector3(-side * 0.75f, 7.2f, 0f), new Vector3(1.5f, 0.16f, 0.16f), Hw2Assets.Get("M_Pole"));
                Box(lamp, "Head", new Vector3(-side * 1.45f, 7.08f, 0f), new Vector3(0.6f, 0.16f, 0.36f), Hw2Assets.Get("M_Lamp"));
            }

            // Skyline blocks with lit windows, further out on both sides.
            float[] offsets = { 15f, 21.5f, 28f };
            float[] heights = { 11f, 22f, 16f };
            float[] widths = { 7f, 9f, 6.5f };
            string[] buildingMaterials = { "M_BuildingA", "M_BuildingB", "M_BuildingC" };

            for (int i = 0; i < offsets.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = 8f + i * 20f + (side > 0 ? 9f : 0f);
                    Box(go.transform, $"Building_{(side > 0 ? "R" : "L")}{i}",
                        new Vector3(side * offsets[i], heights[i] * 0.5f - 0.1f, z),
                        new Vector3(widths[i], heights[i], 12f + i * 3f),
                        Hw2Assets.Get(buildingMaterials[i]));
                }
            }

            return go;
        }

        // ---------------------------------------------------------------- VFX prefabs

        static GameObject BuildFireworkRocket()
        {
            GameObject go = Root("FireworkRocket");
            go.AddComponent<FireworkRocket>();

            Ball(go.transform, "Core", Vector3.zero, 0.22f, Hw2Assets.Get("M_Lamp"));

            Emit(go.transform, "Trail", Vector3.zero, Vector3.zero, new Burst
            {
                loop = true,
                rate = 110f,
                count = 500,
                minLife = 0.3f,
                maxLife = 0.8f,
                minSpeed = 0.4f,
                maxSpeed = 1.6f,
                minSize = 0.06f,
                maxSize = 0.2f,
                gravity = -0.1f,
                material = "P_Glow"
            });

            return go;
        }

        static void ConfigurePoolEffect(GameObject go, PoolKey key, Color baseColor, float lifetime, Light flash, float flashIntensity)
        {
            PoolEffect effect = go.AddComponent<PoolEffect>();
            SerializedObject so = new(effect);
            so.FindProperty("poolKey").enumValueIndex = (int)key;
            so.FindProperty("baseColor").colorValue = baseColor;
            so.FindProperty("lifetime").floatValue = lifetime;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("flashIntensity").floatValue = flashIntensity;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject BuildFireworkBurst()
        {
            GameObject go = Root("FireworkBurst");

            ParticleSystem stars = Emit(go.transform, "Stars", Vector3.zero, Vector3.zero, new Burst
            {
                count = 340,
                minLife = 0.7f,
                maxLife = 1.9f,
                minSpeed = 7f,
                maxSpeed = 17f,
                minSize = 0.09f,
                maxSize = 0.3f,
                gravity = 0.45f,
                radius = 0.12f,
                stretch = true,
                material = "P_Spark"
            });

            ParticleSystem glow = Emit(go.transform, "Glow", Vector3.zero, Vector3.zero, new Burst
            {
                count = 90,
                minLife = 1.1f,
                maxLife = 2.4f,
                minSpeed = 1.6f,
                maxSpeed = 5f,
                minSize = 0.6f,
                maxSize = 1.8f,
                gravity = 0.05f,
                radius = 0.1f,
                material = "P_Glow"
            });

            Light flash = AddLight(go.transform, "Flash", LightType.Point, Vector3.zero, Vector3.zero, Color.white, 0f, 160f, 0f);
            ConfigurePoolEffect(go, PoolKey.FireworkBurst, Color.white, 2.1f, flash, 26f);

            SerializedObject systems = new(go.GetComponent<PoolEffect>());
            SerializedProperty array = systems.FindProperty("systems");
            array.arraySize = 2;
            array.GetArrayElementAtIndex(0).objectReferenceValue = stars;
            array.GetArrayElementAtIndex(1).objectReferenceValue = glow;
            systems.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        static GameObject BuildImpactSparks()
        {
            GameObject go = Root("ImpactSparks");

            Emit(go.transform, "Shards", Vector3.zero, Vector3.zero, new Burst
            {
                count = 160,
                minLife = 0.2f,
                maxLife = 0.6f,
                minSpeed = 5f,
                maxSpeed = 16f,
                minSize = 0.04f,
                maxSize = 0.14f,
                gravity = 2.4f,
                radius = 0.2f,
                stretch = true,
                material = "P_Spark"
            });

            Emit(go.transform, "Flare", Vector3.zero, Vector3.zero, new Burst
            {
                count = 26,
                minLife = 0.18f,
                maxLife = 0.45f,
                minSpeed = 0.5f,
                maxSpeed = 3f,
                minSize = 0.5f,
                maxSize = 1.4f,
                gravity = 0f,
                radius = 0.1f,
                material = "P_Glow"
            });

            Light flash = AddLight(go.transform, "Flash", LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, 0.55f, 0.25f), 0f, 14f, 0f);
            ConfigurePoolEffect(go, PoolKey.ImpactSparks, new Color(1f, 0.55f, 0.25f), 0.7f, flash, 18f);
            return go;
        }

        static GameObject BuildPickupBurst()
        {
            GameObject go = Root("PickupBurst");

            Emit(go.transform, "Motes", Vector3.zero, Vector3.zero, new Burst
            {
                count = 60,
                minLife = 0.3f,
                maxLife = 0.7f,
                minSpeed = 2f,
                maxSpeed = 6f,
                minSize = 0.1f,
                maxSize = 0.32f,
                gravity = -0.3f,
                radius = 0.12f,
                material = "P_Glow"
            });

            Light flash = AddLight(go.transform, "Flash", LightType.Point, Vector3.zero, Vector3.zero, Color.white, 0f, 7f, 0f);
            ConfigurePoolEffect(go, PoolKey.PickupBurst, Color.white, 0.55f, flash, 5f);
            return go;
        }

        // ---------------------------------------------------------------- public API

        public static void BuildAll(GameSettings settings)
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            GameObject holder = new("PrefabSource");
            source = holder.transform;

            try
            {
                settings.roadSegmentPrefab = Save(BuildRoadSegment());
                settings.playerCarPrefab = Save(BuildPlayerCar());
                settings.staticObstaclePrefab = Save(BuildStaticObstacle());
                settings.movingObstaclePrefab = Save(BuildMovingObstacle());
                settings.coinPrefab = Save(BuildCoin());
                settings.fuelCanPrefab = Save(BuildFuelCan());
                settings.fireworkRocketPrefab = Save(BuildFireworkRocket());
                settings.fireworkBurstPrefab = Save(BuildFireworkBurst());
                settings.impactSparksPrefab = Save(BuildImpactSparks());
                settings.pickupBurstPrefab = Save(BuildPickupBurst());
            }
            finally
            {
                Object.DestroyImmediate(holder);
                source = null;
            }
        }

        static GameObject Save(GameObject go)
        {
            string path = $"{PrefabFolder}/{go.name}.prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }
    }
}
