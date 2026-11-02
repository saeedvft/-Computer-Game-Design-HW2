using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Looping engine note built at runtime. Pitch and level track the car speed so the engine
    /// grows louder and higher as the difficulty ramp pushes the car faster.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineSound : MonoBehaviour
    {
        static readonly int[] Harmonics = { 1, 2, 3, 4, 6, 8 };
        static readonly float[] Weights = { 0.85f, 0.62f, 0.44f, 0.3f, 0.18f, 0.1f };

        [SerializeField] float loopSeconds = 1f;
        [SerializeField] float firingHz = 42f;
        [SerializeField] float pulseHz = 8f;
        [SerializeField] float minPitch = 0.74f;
        [SerializeField] float maxPitch = 1.9f;
        [SerializeField] float maxVolume = 0.5f;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = SoundLab.Build("EngineLoop", loopSeconds, AudioSettings.outputSampleRate, true, FillEngine);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.7f;
            source.minDistance = 2f;
            source.maxDistance = 60f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || source == null)
            {
                return;
            }

            float speed01 = gm.Speed01;
            source.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);
            source.volume = gm.IsPlaying ? Mathf.Lerp(maxVolume * 0.35f, maxVolume, speed01) : 0f;

            if (gm.IsPlaying && !source.isPlaying)
            {
                source.UnPause();
            }
            else if (!gm.IsPlaying && source.isPlaying)
            {
                source.Pause();
            }
        }

        void FillEngine(float[] data, int sampleRate)
        {
            float pulse = SoundLab.Quantize(pulseHz, loopSeconds);
            float noiseState = 0f;
            float alpha = SoundLab.LowpassAlpha(900f, sampleRate);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sampleRate;
                float tone = 0f;

                for (int h = 0; h < Harmonics.Length; h++)
                {
                    float frequency = SoundLab.Quantize(firingHz * Harmonics[h], loopSeconds);
                    tone += Mathf.Sin(t * frequency * Mathf.PI * 2f) * Weights[h];
                }

                float amplitude = 0.75f + 0.25f * Mathf.Sin(t * pulse * Mathf.PI * 2f);
                noiseState = Mathf.Lerp(noiseState, SoundLab.Noise(i), alpha);

                data[i] = tone * amplitude * 0.34f + noiseState * 0.2f;
            }
        }
    }
}
