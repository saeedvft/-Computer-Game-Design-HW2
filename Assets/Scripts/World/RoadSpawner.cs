using System.Collections.Generic;
using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Procedural road: keeps a fixed window of segments scrolling towards the player and
    /// recycles the ones that fall behind, re-arming them in front and refilling their content.
    /// Nothing is instantiated or destroyed after the pools are warmed up.
    /// </summary>
    public sealed class RoadSpawner : Singleton<RoadSpawner>
    {
        static readonly PoolKey[] ClearedOnRebuild =
        {
            PoolKey.StaticObstacle,
            PoolKey.MovingObstacle,
            PoolKey.Coin,
            PoolKey.FuelCan
        };

        readonly List<RoadSegment> segments = new();

        GameSettings settings;

        public int ActiveSegmentCount => segments.Count;

        GameSettings Settings => settings != null ? settings : (settings = GameSettings.Instance);

        protected override void Awake()
        {
            base.Awake();

            settings = GameSettings.Instance;
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RunStarted += BuildRoad;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RunStarted -= BuildRoad;
            }
        }

        void Start()
        {
            BuildRoad();
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || segments.Count == 0)
            {
                return;
            }

            float shift = gm.Speed * Time.deltaTime;
            float recycleZ = -Settings.recycleBehindPlayer;

            for (int i = 0; i < segments.Count; i++)
            {
                RoadSegment segment = segments[i];
                segment.transform.position += Vector3.back * shift;

                if (segment.NearEdgeZ + segment.Length < recycleZ)
                {
                    Recycle(segment);
                }
            }
        }

        /// Full layout pass: used at startup and whenever a new run begins.
        public void BuildRoad()
        {
            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                Debug.LogWarning("PoolManager is not ready yet - road not built.", this);
                return;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                pool.Release(PoolKey.RoadSegment, segments[i].gameObject);
            }

            segments.Clear();

            foreach (PoolKey key in ClearedOnRebuild)
            {
                pool.ReleaseAll(key);
            }

            SpawnDirector.Instance?.ResetState();

            float z = -Settings.recycleBehindPlayer;
            for (int i = 0; i < Settings.segmentCount; i++)
            {
                z = AppendSegment(z);
            }
        }

        float AppendSegment(float z)
        {
            RoadSegment segment = CreateSegment(z);
            if (segment == null)
            {
                return z;
            }

            segments.Add(segment);
            SpawnDirector.Instance?.Populate(segment);
            return z + segment.Length;
        }

        void Recycle(RoadSegment segment)
        {
            segment.Arm(FarthestEdge(), Settings.segmentLength);
            SpawnDirector.Instance?.Populate(segment);
        }

        float FarthestEdge()
        {
            float best = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                best = Mathf.Max(best, segments[i].FarEdgeZ);
            }

            return best;
        }

        RoadSegment CreateSegment(float nearEdgeZ)
        {
            GameObject go = PoolManager.Instance.Get(
                PoolKey.RoadSegment, new Vector3(0f, 0f, nearEdgeZ), Quaternion.identity, transform);

            if (go == null)
            {
                return null;
            }

            if (!go.TryGetComponent<RoadSegment>(out RoadSegment segment))
            {
                Debug.LogError("Road prefab is missing the RoadSegment component.", go);
                return null;
            }

            segment.Arm(nearEdgeZ, Settings.segmentLength);
            return segment;
        }
    }
}
