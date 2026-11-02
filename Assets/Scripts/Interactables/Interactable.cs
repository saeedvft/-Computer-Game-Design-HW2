using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Abstract base of everything the player can meet on the road - obstacle cars and
    /// collectible items. It owns the shared life cycle (lane placement, scrolling with the
    /// road, recycling into the pool, trigger detection); the concrete subclasses only override
    /// the virtual hooks that differ per element type.
    /// </summary>
    public abstract class Interactable : MonoBehaviour, IPoolable
    {
        [SerializeField] protected int pointValue;
        [SerializeField] protected float despawnZ = -16f;

        protected GameSettings settings;

        public abstract PoolKey Key { get; }
        public int Lane { get; protected set; }
        public bool IsSpawned { get; private set; }
        public int PointValue => pointValue;

        protected virtual void Awake()
        {
            settings = GameSettings.Instance;
            despawnZ = settings.drivePastPlayerZ;

            // Movers are transform-driven, so they carry a kinematic body with a trigger collider.
            // Trigger messages need a non-kinematic partner, which the player car provides.
            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.freezeRotation = true;
                body.detectCollisions = true;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
            }
        }

        public virtual void Spawn(int lane)
        {
            Lane = lane;
            IsSpawned = true;
            OnSpawned();
        }

        public void ReturnToPool()
        {
            if (!IsSpawned)
            {
                return;
            }

            IsSpawned = false;
            OnReturned();
            PoolManager.Instance.Release(Key, gameObject);
        }

        public void OnSpawn()
        {
        }

        public void OnRelease()
        {
            IsSpawned = false;
        }

        protected virtual void OnSpawned()
        {
        }

        protected virtual void OnReturned()
        {
        }

        /// <summary>Per-frame motion on top of the plain road scroll (moving cars, spin, bob).</summary>
        protected virtual void ScrollExtra(float deltaTime)
        {
        }

        protected abstract void OnPlayerContact(PlayerCar player);

        protected virtual void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                return;
            }

            float dt = Time.deltaTime;
            transform.position += Vector3.back * gm.Speed * dt;
            ScrollExtra(dt);

            if (transform.position.z < despawnZ)
            {
                ReturnToPool();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned)
            {
                return;
            }

            if (other.TryGetComponent<PlayerCar>(out PlayerCar player))
            {
                OnPlayerContact(player);
            }
        }
    }
}
