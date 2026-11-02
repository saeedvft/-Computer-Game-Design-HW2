using UnityEngine;

namespace HW2
{
    /// <summary>
    /// A one-shot particle burst that lives in a pool: it plays, fades out and returns itself.
    /// Used for collision sparks, pickup fades and the sky fireworks.
    /// </summary>
    public sealed class PoolEffect : MonoBehaviour, IPoolable
    {
        [SerializeField] PoolKey poolKey;
        [SerializeField] ParticleSystem[] systems;
        [SerializeField] Color baseColor = Color.white;
        [SerializeField] Light flash;
        [SerializeField] float flashIntensity = 14f;
        [SerializeField] float lifetime = 1.4f;

        float timer;
        bool playing;

        public PoolKey Key => poolKey;

        void Awake()
        {
            if (systems == null || systems.Length == 0)
            {
                systems = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        public void Play(float scale, Color? colorOverride)
        {
            Color color = colorOverride ?? baseColor;

            transform.localScale = Vector3.one * scale;
            timer = lifetime;
            playing = true;

            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                var main = system.main;
                main.startColor = color;
                system.Clear();
                system.Play();
            }

            if (flash != null)
            {
                flash.color = color;
                flash.intensity = flashIntensity;
                flash.enabled = true;
            }
        }

        void Update()
        {
            if (!playing)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                return;
            }

            playing = false;

            if (flash != null)
            {
                flash.enabled = false;
            }

            PoolManager.Instance.Release(poolKey, gameObject);
        }

        public void OnSpawn()
        {
            playing = false;
        }

        public void OnRelease()
        {
            playing = false;

            if (systems == null)
            {
                return;
            }

            foreach (ParticleSystem system in systems)
            {
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (flash != null)
            {
                flash.enabled = false;
            }
        }
    }
}
