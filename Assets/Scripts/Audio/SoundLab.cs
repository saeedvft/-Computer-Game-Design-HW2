using System;
using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Tiny sample-level synthesiser. The project ships no audio assets, so the engine note,
    /// the night music bed and every effect are generated from samples at start-up.
    /// </summary>
    public static class SoundLab
    {
        public static AudioClip Build(string name, float seconds, int sampleRate, bool loopSafe, Action<float[], int> fill)
        {
            int count = Mathf.Max(4, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[count];
            fill(data, sampleRate);

            if (loopSafe)
            {
                CrossfadeEnds(data, Mathf.Max(1, Mathf.RoundToInt(0.025f * sampleRate)));
            }

            Normalize(data, 0.9f);

            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(data, 0);
            clip.name = name;
            return clip;
        }

        /// Deterministic pseudo random in -1..1, used for noise beds.
        public static float Noise(int index)
        {
            uint x = (uint)(index * 374761393) + 668265263u;
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFFu) / (float)0x800000u - 1f;
        }

        /// One pole low pass coefficient for the given cutoff.
        public static float LowpassAlpha(float cutoffHz, int sampleRate) =>
            1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / sampleRate);

        /// Frequencies of a looped clip must be multiples of 1/seconds to stay seamless.
        public static float Quantize(float frequency, float seconds)
        {
            float grid = 1f / Mathf.Max(0.01f, seconds);
            return Mathf.Round(frequency / grid) * grid;
        }

        public static float AttackDecay(float t, float attack, float decay)
        {
            float a = attack <= 0.0001f ? 1f : Mathf.Clamp01(t / attack);
            float d = decay <= 0.0001f ? 1f : Mathf.Exp(-t / decay);
            return a * d;
        }

        static void CrossfadeEnds(float[] data, int blend)
        {
            int count = data.Length;
            if (blend <= 0 || count < blend * 2)
            {
                return;
            }

            var head = new float[blend];
            Array.Copy(data, head, blend);

            for (int i = 0; i < blend; i++)
            {
                float mix = i / (float)blend;
                data[i] = data[count - blend + i] * (1f - mix) + head[i] * mix;
            }

            for (int i = 0; i < blend; i++)
            {
                float mix = i / (float)blend;
                data[count - blend + i] *= mix;
            }
        }

        static void Normalize(float[] data, float peak)
        {
            float loudest = 0.0001f;
            for (int i = 0; i < data.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(data[i]));
            }

            float gain = peak / loudest;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= gain;
            }
        }
    }
}
