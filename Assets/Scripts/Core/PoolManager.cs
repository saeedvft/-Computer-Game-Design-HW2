using System.Collections.Generic;
using UnityEngine;

namespace HW2
{
    /// <summary>Implemented by objects that are recycled through <see cref="PoolManager"/>.</summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnRelease();
    }

    /// <summary>
    /// Object pooling for every repeating game object. Pools are warmed up once at startup,
    /// so gameplay only ever moves objects between the idle stack and the scene.
    /// </summary>
    public sealed class PoolManager : Singleton<PoolManager>
    {
        sealed class Pool
        {
            public PoolKey key;
            public GameObject prefab;
            public Transform root;
            public readonly Stack<GameObject> idle = new();
            public readonly HashSet<GameObject> active = new();
            public int created;
        }

        readonly Dictionary<PoolKey, Pool> pools = new();

        protected override void Awake()
        {
            base.Awake();
            Build();
        }

        public void Build()
        {
            pools.Clear();
            var settings = GameSettings.Instance;

            foreach (PoolKey key in (PoolKey[])System.Enum.GetValues(typeof(PoolKey)))
            {
                GameObject prefab = settings.PrefabFor(key);
                if (prefab == null)
                {
                    Debug.LogWarning($"No prefab assigned for pool '{key}'.", this);
                    continue;
                }

                var pool = new Pool { key = key, prefab = prefab, root = CreateRoot(key) };
                for (int i = 0; i < settings.CapacityFor(key); i++)
                {
                    pool.idle.Push(CreateInstance(pool));
                }

                pools[key] = pool;
            }
        }

        public GameObject Get(PoolKey key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!pools.TryGetValue(key, out var pool))
            {
                Debug.LogError($"Pool '{key}' is not available.", this);
                return null;
            }

            GameObject go = pool.idle.Count > 0 ? pool.idle.Pop() : CreateInstance(pool);
            pool.active.Add(go);

            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            if (go.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnSpawn();
            }

            return go;
        }

        public void Release(PoolKey key, GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnRelease();
            }

            go.SetActive(false);

            if (pools.TryGetValue(key, out var pool))
            {
                pool.active.Remove(go);
                go.transform.SetParent(pool.root, true);
                pool.idle.Push(go);
            }
        }

        /// Sends every live object of a pool back to the idle stack (used when a run restarts).
        public void ReleaseAll(PoolKey key)
        {
            if (!pools.TryGetValue(key, out var pool))
            {
                return;
            }

            var snapshot = new List<GameObject>(pool.active);
            foreach (GameObject go in snapshot)
            {
                Release(key, go);
            }
        }

        public int CountIdle(PoolKey key) => pools.TryGetValue(key, out var pool) ? pool.idle.Count : 0;

        GameObject CreateInstance(Pool pool)
        {
            GameObject go = Object.Instantiate(pool.prefab, pool.root);
            go.name = $"{pool.key}_{pool.created++}";
            go.SetActive(false);
            return go;
        }

        Transform CreateRoot(PoolKey key)
        {
            Transform existing = transform.Find(key.ToString());
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(key.ToString());
            root.transform.SetParent(transform, false);
            return root.transform;
        }
    }
}
