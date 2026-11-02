using UnityEngine;

namespace HW2
{
    /// <summary>Rising phase of a firework: a pooled rocket with a trailing particle jet.</summary>
    public sealed class FireworkRocket : MonoBehaviour, IPoolable
    {
        [SerializeField] ParticleSystem trail;
        [SerializeField] float gravity = 8.5f;

        Vector3 velocity;
        Color burstColor;
        float fuse;
        bool flying;

        void Awake()
        {
            if (trail == null)
            {
                trail = GetComponentInChildren<ParticleSystem>();
            }
        }

        public void Launch(Vector3 initialVelocity, Color color, float fuseSeconds)
        {
            velocity = initialVelocity;
            burstColor = color;
            fuse = fuseSeconds;
            flying = true;

            transform.localScale = Vector3.one;
            trail?.Clear();
            trail?.Play();
        }

        void Update()
        {
            if (!flying)
            {
                return;
            }

            float dt = Time.deltaTime;
            transform.position += velocity * dt;
            velocity.y -= gravity * dt;
            fuse -= dt;

            if (velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }

            if (fuse > 0f)
            {
                return;
            }

            flying = false;
            Fireworks.Instance?.Detonate(transform.position, burstColor);
            PoolManager.Instance.Release(PoolKey.FireworkRocket, gameObject);
        }

        public void OnSpawn()
        {
            flying = false;
        }

        public void OnRelease()
        {
            flying = false;

            if (trail != null)
            {
                trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
