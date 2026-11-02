using System.Collections.Generic;
using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Fills a freshly recycled road segment. Difficulty (obstacle chance, share of two-lane
    /// blocks) grows with the distance travelled, while a guaranteed free lane per row and the
    /// periodic fuel can keep the run playable.
    /// </summary>
    public sealed class SpawnDirector : Singleton<SpawnDirector>
    {
        /// No hazards this close to the player, so a new run always starts with breathing room.
        const float StartGraceZ = 45f;

        readonly List<int> laneScratch = new();

        GameSettings settings;
        float lastFuelDistance;

        protected override void Awake()
        {
            base.Awake();
            settings = GameSettings.Instance;
        }

        public void ResetState()
        {
            lastFuelDistance = GameManager.Instance != null ? GameManager.Instance.Distance : 0f;
        }

        public void Populate(RoadSegment segment)
        {
            if (segment == null || !segment.IsArmed)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            float difficulty = gm != null ? gm.Difficulty : 0f;
            float distance = gm != null ? gm.Distance : 0f;

            int laneCount = settings.laneCount;
            int rows = Mathf.Max(1, settings.rowsPerSegment);
            float rowGap = segment.Length / rows;
            int freeLane = Random.Range(0, laneCount);
            bool placeFuel = distance - lastFuelDistance >= settings.fuelSpacingMeters;

            for (int row = 0; row < rows; row++)
            {
                float along = rowGap * row + Random.Range(rowGap * 0.2f, rowGap * 0.55f);
                float z = segment.NearEdgeZ + along;

                if (z < StartGraceZ)
                {
                    continue;
                }

                float obstacleChance = Mathf.Lerp(settings.obstacleChanceAtStart, settings.obstacleChanceAtMax, difficulty);
                float doubleBlockChance = Mathf.Lerp(settings.doubleBlockChanceAtStart, settings.doubleBlockChanceAtMax, difficulty);

                int blocked = 0;
                if (Random.value < obstacleChance)
                {
                    blocked = Random.value < doubleBlockChance ? 2 : 1;
                    blocked = Mathf.Min(blocked, laneCount - 1);
                }

                ChooseBlockedLanes(freeLane, blocked, laneCount);
                foreach (int lane in laneScratch)
                {
                    SpawnObstacle(lane, z);
                }

                bool fuelRow = placeFuel && row == rows / 2;
                if (fuelRow)
                {
                    SpawnFuelCan(freeLane, z);
                    lastFuelDistance = distance;
                }
                else if (Random.value < settings.coinRowChance)
                {
                    SpawnCoinLine(freeLane, z);
                }

                freeLane = NextFreeLane(freeLane, laneCount);
            }
        }

        /// The open lane drifts by at most one lane per row so a lane change is always enough.
        int NextFreeLane(int previous, int laneCount)
        {
            if (laneCount < 2 || Random.value < 0.45f)
            {
                return previous;
            }

            int shift = Random.value < 0.5f ? -1 : 1;
            return Mathf.Clamp(previous + shift, 0, laneCount - 1);
        }

        void ChooseBlockedLanes(int freeLane, int count, int laneCount)
        {
            laneScratch.Clear();
            if (count <= 0)
            {
                return;
            }

            for (int attempt = 0; attempt < 32 && laneScratch.Count < count; attempt++)
            {
                int lane = Random.Range(0, laneCount);
                if (lane == freeLane || laneScratch.Contains(lane))
                {
                    continue;
                }

                laneScratch.Add(lane);
            }
        }

        void SpawnObstacle(int lane, float z)
        {
            PoolKey key = Random.value < settings.movingCarShare
                ? PoolKey.MovingObstacle
                : PoolKey.StaticObstacle;

            SpawnInteractable(key, lane, z, 0f);
        }

        void SpawnCoinLine(int lane, float z)
        {
            for (int i = 0; i < settings.coinsPerRow; i++)
            {
                SpawnInteractable(PoolKey.Coin, lane, z + i * settings.coinSpacing, settings.collectibleY);
            }
        }

        void SpawnFuelCan(int lane, float z)
        {
            SpawnInteractable(PoolKey.FuelCan, lane, z, settings.collectibleY);
        }

        void SpawnInteractable(PoolKey key, int lane, float z, float y)
        {
            Vector3 position = new(settings.LaneX(lane), y, z);
            GameObject go = PoolManager.Instance.Get(key, position, Quaternion.identity, transform);
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent<Interactable>(out Interactable interactable))
            {
                interactable.Spawn(lane);
            }
        }
    }
}
