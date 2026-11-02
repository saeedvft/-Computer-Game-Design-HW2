using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Night sky fireworks: launches pooled rockets from random points around the horizon and
    /// detonates them into colourful HDR bursts, which the Bloom pass turns into sky glows.
    /// </summary>
    public sealed class Fireworks : Singleton<Fireworks>
    {
        static readonly float[] BurstScales = { 1f, 1.35f, 0.8f };

        GameSettings settings;
        float nextLaunch = 1.2f;

        protected override void Awake()
        {
            base.Awake();
            settings = GameSettings.Instance;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                return;
            }

            nextLaunch -= Time.deltaTime;
            if (nextLaunch > 0f)
            {
                return;
            }

            nextLaunch = Random.Range(settings.fireworksMinInterval, settings.fireworksMaxInterval);
            Launch();
        }

        void Launch()
        {
            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector3 origin = new(Random.Range(-42f, 42f), 0.4f, Random.Range(28f, 135f));
            Vector3 velocity = new(Random.Range(-2.5f, 2.5f), Random.Range(21f, 27f), Random.Range(-1.5f, 1.5f));
            float fuse = Random.Range(settings.rocketMinFuse, settings.rocketMaxFuse);
            Color color = Random.ColorHSV(Random.value, 1f, 0.75f, 1f, 0f, 1f) * Random.Range(2.2f, 5.5f);

            GameObject go = pool.Get(PoolKey.FireworkRocket, origin, Quaternion.identity, transform);
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent<FireworkRocket>(out FireworkRocket rocket))
            {
                rocket.Launch(velocity, color, fuse);
                AudioManager.Instance.Play(Sfx.FireworkLaunch, 0.22f, Random.Range(0.9f, 1.15f));
            }
            else
            {
                pool.Release(PoolKey.FireworkRocket, go);
            }
        }

        public void Detonate(Vector3 position, Color color)
        {
            float scale = BurstScales[Random.Range(0, BurstScales.Length)];
            EffectPlayer.Instance.Play(PoolKey.FireworkBurst, position, scale, color);
            AudioManager.Instance.Play(Sfx.FireworkBoom, 0.28f, Random.Range(0.85f, 1.1f));
        }
    }
}
