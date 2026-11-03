using UnityEngine;

namespace HW2
{
    /// <summary>Every repeating object in the game is pooled under one of these keys.</summary>
    public enum PoolKey
    {
        RoadSegment,
        StaticObstacle,
        MovingObstacle,
        Coin,
        FuelCan,
        FireworkRocket,
        FireworkBurst,
        ImpactSparks,
        PickupBurst
    }

    /// <summary>
    /// All tunables plus the prefab references. Stored as a Resources asset so every system
    /// can reach the same data without per-object scene wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "HW2/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        public const string ResourceName = "GameSettings";

        static GameSettings s_instance;

        public static GameSettings Instance
        {
            get
            {
                if (s_instance != null)
                {
                    return s_instance;
                }

                s_instance = Resources.Load<GameSettings>(ResourceName);
                if (s_instance == null)
                {
                    s_instance = CreateInstance<GameSettings>();
                    s_instance.name = ResourceName;
                    Debug.LogWarning("GameSettings asset missing from Resources - running with code defaults.");
                }

                return s_instance;
            }
        }

        [Header("Road and lanes")]
        public int laneCount = 3;
        public float laneWidth = 3.5f;
        public float roadWidth = 11.5f;
        public float segmentLength = 60f;
        public int segmentCount = 9;
        public float recycleBehindPlayer = 20f;
        public float drivePastPlayerZ = -16f;

        [Header("Speed and progressive difficulty")]
        public float baseSpeed = 13f;
        public float maxSpeed = 46f;
        public float speedAcceleration = 5f;
        public float difficultyRampDistance = 2800f;
        public float scorePerMeter = 1f;

        [Header("Player car")]
        public int maxLives = 3;
        public float laneChangeSeconds = 0.24f;
        public float laneRepeatSeconds = 0.2f;
        public float invulnerableSeconds = 1.3f;
        public float laneChangeTiltDegrees = 7f;

        [Header("Fuel system")]
        public float fuelMax = 100f;
        public float fuelBurnPerSecond = 2.9f;
        public float fuelBurnSpeedCoefficient = 0.022f;
        public float fuelPerCan = 42f;
        public float fuelFirstDistance = 150f;
        public float fuelSpacingMeters = 240f;

        [Header("Obstacle spawning")]
        public int rowsPerSegment = 3;
        public float obstacleChanceAtStart = 0.55f;
        public float obstacleChanceAtMax = 0.96f;
        public float doubleBlockChanceAtStart = 0.05f;
        public float doubleBlockChanceAtMax = 0.55f;
        public float movingCarShare = 0.45f;
        public float movingCarCruiseFactor = 0.42f;

        [Header("Collectibles")]
        public float coinRowChance = 0.6f;
        public int coinsPerRow = 4;
        public float coinSpacing = 4.4f;
        public int coinValue = 50;
        public float collectibleY = 1.05f;

        [Header("Night lighting and neon")]
        public Color neonColor = new Color(0.15f, 1f, 0.95f, 1f);
        public float neonMinIntensity = 1.1f;
        public float neonMaxIntensity = 8.5f;
        public float neonMinGlow = 0.9f;
        public float neonMaxGlow = 7f;
        public Color headlightColor = new Color(1f, 0.95f, 0.8f, 1f);
        public float streetlightSpacing = 20f;

        [Header("Sky fireworks")]
        public float fireworksMinInterval = 1.3f;
        public float fireworksMaxInterval = 3.9f;
        public float rocketMinFuse = 1.05f;
        public float rocketMaxFuse = 1.75f;
        public Color headlightBeam = new Color(1f, 0.97f, 0.85f, 1f);

        [Header("Prefabs")]
        public GameObject roadSegmentPrefab;
        public GameObject playerCarPrefab;
        public GameObject staticObstaclePrefab;
        public GameObject movingObstaclePrefab;
        public GameObject coinPrefab;
        public GameObject fuelCanPrefab;
        public GameObject fireworkRocketPrefab;
        public GameObject fireworkBurstPrefab;
        public GameObject impactSparksPrefab;
        public GameObject pickupBurstPrefab;

        public float LaneX(int lane) => (lane - (laneCount - 1) * 0.5f) * laneWidth;

        public float TargetSpeed(float distance) =>
            Mathf.Lerp(baseSpeed, maxSpeed, Mathf.Clamp01(distance / difficultyRampDistance));

        public GameObject PrefabFor(PoolKey key) => key switch
        {
            PoolKey.RoadSegment => roadSegmentPrefab,
            PoolKey.StaticObstacle => staticObstaclePrefab,
            PoolKey.MovingObstacle => movingObstaclePrefab,
            PoolKey.Coin => coinPrefab,
            PoolKey.FuelCan => fuelCanPrefab,
            PoolKey.FireworkRocket => fireworkRocketPrefab,
            PoolKey.FireworkBurst => fireworkBurstPrefab,
            PoolKey.ImpactSparks => impactSparksPrefab,
            PoolKey.PickupBurst => pickupBurstPrefab,
            _ => null
        };

        public int CapacityFor(PoolKey key) => key switch
        {
            PoolKey.RoadSegment => segmentCount,
            PoolKey.StaticObstacle => 14,
            PoolKey.MovingObstacle => 14,
            PoolKey.Coin => 48,
            PoolKey.FuelCan => 6,
            PoolKey.FireworkRocket => 6,
            PoolKey.FireworkBurst => 8,
            PoolKey.ImpactSparks => 6,
            PoolKey.PickupBurst => 12,
            _ => 8
        };
    }
}
