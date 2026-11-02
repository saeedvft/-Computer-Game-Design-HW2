using UnityEngine;

namespace HW2
{
    /// <summary>Decays a shake impulse that <see cref="ThirdPersonCamera"/> adds to its offset.</summary>
    public sealed class CameraShake : Singleton<CameraShake>
    {
        [SerializeField] float minimumDuration = 0.15f;

        float amplitude;
        float duration;
        float elapsed;

        public Vector3 CurrentOffset
        {
            get
            {
                if (amplitude <= 0.0001f)
                {
                    return Vector3.zero;
                }

                float envelope = 1f - Mathf.Clamp01(elapsed / Mathf.Max(minimumDuration, duration));
                float seed = Time.time * 43f;

                Vector3 noise = new(
                    Mathf.PerlinNoise(seed, 0.31f) - 0.5f,
                    Mathf.PerlinNoise(seed, 1.73f) - 0.5f,
                    Mathf.PerlinNoise(seed, 3.19f) - 0.5f);

                return noise * (2f * amplitude * envelope * envelope);
            }
        }

        public void Shake(float amount, float time)
        {
            amplitude = Mathf.Max(amplitude, amount);
            duration = Mathf.Max(duration, time);
            elapsed = 0f;
        }

        void Update()
        {
            if (amplitude <= 0.0001f)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (elapsed >= duration)
            {
                amplitude = 0f;
                duration = 0f;
                elapsed = 0f;
            }
        }
    }
}
