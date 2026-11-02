using System.Collections.Generic;
using UnityEngine;

namespace HW2
{
    public enum Sfx
    {
        Coin,
        FuelPickup,
        Crash,
        LaneChange,
        UiClick,
        FireworkLaunch,
        FireworkBoom,
        GameOver
    }

    /// <summary>
    /// Sound design for the game: a looping night music bed plus synthesised one-shots for
    /// coins, fuel, collisions and fireworks, played through a small rotating voice pool.
    /// </summary>
    public sealed class AudioManager : Singleton<AudioManager>
    {
        const float MusicLength = 8f;

        [SerializeField] int voiceCount = 10;
        [SerializeField, Range(0f, 1f)] float musicLevel = 0.26f;
        [SerializeField, Range(0f, 1f)] float sfxLevel = 0.85f;

        readonly Dictionary<Sfx, AudioClip> clips = new();
        readonly List<AudioSource> voices = new();

        AudioSource musicSource;
        int cursor;

        public bool MusicIsPlaying => musicSource != null && musicSource.isPlaying;

        void Start()
        {
            int sampleRate = AudioSettings.outputSampleRate;

            clips[Sfx.Coin] = OneShot("Coin", 0.3f, sampleRate, FillCoin);
            clips[Sfx.FuelPickup] = OneShot("FuelPickup", 0.55f, sampleRate, FillFuel);
            clips[Sfx.Crash] = OneShot("Crash", 0.75f, sampleRate, FillCrash);
            clips[Sfx.LaneChange] = OneShot("LaneChange", 0.3f, sampleRate, FillSkid);
            clips[Sfx.UiClick] = OneShot("UiClick", 0.12f, sampleRate, FillClick);
            clips[Sfx.FireworkLaunch] = OneShot("FireworkLaunch", 0.5f, sampleRate, FillLaunch);
            clips[Sfx.FireworkBoom] = OneShot("FireworkBoom", 1.2f, sampleRate, FillBoom);
            clips[Sfx.GameOver] = OneShot("GameOver", 1.8f, sampleRate, FillGameOver);

            for (int i = 0; i < voiceCount; i++)
            {
                voices.Add(CreateSource($"SfxVoice{i}", spatialBlend2D: true, looping: false));
            }

            musicSource = CreateSource("Music", spatialBlend2D: true, looping: true);
            musicSource.clip = OneShot("Music", MusicLength, sampleRate, FillMusic, loopSafe: true);
            musicSource.volume = musicLevel;
            musicSource.Play();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateChanged += HandleStateChanged;
                HandleStateChanged(gm.State);
            }
        }

        protected override void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateChanged -= HandleStateChanged;
            }

            base.OnDestroy();
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(sfx, out AudioClip clip) || clip == null || voices.Count == 0)
            {
                return;
            }

            cursor = (cursor + 1) % voices.Count;
            AudioSource voice = voices[cursor];
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume * sfxLevel);
            voice.pitch = Mathf.Clamp(pitch, 0.25f, 4f);
            voice.time = 0f;
            voice.Play();
        }

        public void SetMusicLevel(float level)
        {
            musicLevel = Mathf.Clamp01(level);

            if (musicSource != null)
            {
                musicSource.volume = musicLevel;
            }
        }

        void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    ResumeMusic();
                    break;
                case GameState.Paused:
                    if (musicSource != null)
                    {
                        musicSource.Pause();
                    }

                    break;
                case GameState.GameOver:
                    Play(Sfx.GameOver);
                    ResumeMusic();
                    break;
                case GameState.Menu:
                    ResumeMusic();
                    break;
            }
        }

        void ResumeMusic()
        {
            if (musicSource == null)
            {
                return;
            }

            musicSource.UnPause();

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        AudioSource CreateSource(string name, bool spatialBlend2D, bool looping)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = looping;
            source.spatialBlend = spatialBlend2D ? 0f : 1f;
            source.rolloffMode = AudioRolloffMode.Linear;

            if (spatialBlend2D)
            {
                source.priority = 128;
            }

            return source;
        }

        static AudioClip OneShot(string name, float seconds, int sampleRate, System.Action<float[], int> fill, bool loopSafe = false)
        {
            return SoundLab.Build(name, seconds, sampleRate, loopSafe, fill);
        }

        // ---------------------------------------------------------------- voices

        static void FillCoin(float[] data, int sr)
        {
            float phase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                float frequency = t < 0.07f ? 1046f : 1568f;
                phase += frequency / sr;

                float decay = t < 0.07f
                    ? SoundLab.AttackDecay(t, 0.003f, 0.05f)
                    : SoundLab.AttackDecay(t - 0.07f, 0.003f, 0.2f);

                data[i] = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.4f * Mathf.Sin(phase * Mathf.PI * 4f)) * decay;
            }
        }

        static void FillFuel(float[] data, int sr)
        {
            float phase = 0f;
            float smooth = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                float frequency = Mathf.Lerp(320f, 980f, t / 0.5f);
                phase += frequency / sr;

                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                smooth = Mathf.Lerp(smooth, SoundLab.Noise(i), 0.25f);

                data[i] = (tone * 0.85f + smooth * 0.25f) * SoundLab.AttackDecay(t, 0.01f, 0.26f);
            }
        }

        static void FillCrash(float[] data, int sr)
        {
            float body = 0f;
            float rumblePhase = 0f;
            float clankPhase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                body = Mathf.Lerp(body, SoundLab.Noise(i), SoundLab.LowpassAlpha(1400f, sr));

                rumblePhase += 58f / sr;
                clankPhase += 1180f / sr;

                float rumble = Mathf.Sin(rumblePhase * Mathf.PI * 2f) * SoundLab.AttackDecay(t, 0.005f, 0.22f);
                float clank = Mathf.Sin(clankPhase * Mathf.PI * 2f) * SoundLab.AttackDecay(t, 0.002f, 0.09f);

                data[i] = body * SoundLab.AttackDecay(t, 0.002f, 0.3f) * 0.9f + rumble * 0.7f + clank * 0.3f;
            }
        }

        static void FillSkid(float[] data, int sr)
        {
            float band = 0f;
            float previous = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                band = Mathf.Lerp(band, SoundLab.Noise(i), SoundLab.LowpassAlpha(2600f, sr));

                float highpassed = band - previous;
                previous = band;

                float wobble = Mathf.Sin(t * 2f * Mathf.PI * 170f) * 0.25f;
                data[i] = (highpassed * 2f + wobble) * SoundLab.AttackDecay(t, 0.01f, 0.16f);
            }
        }

        static void FillClick(float[] data, int sr)
        {
            float phase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                phase += 880f / sr;
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * SoundLab.AttackDecay(t, 0.001f, 0.05f);
            }
        }

        static void FillLaunch(float[] data, int sr)
        {
            float phase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                float frequency = Mathf.Lerp(520f, 1900f, t / 0.45f);
                phase += frequency / sr;

                float tremolo = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 34f);
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * tremolo * SoundLab.AttackDecay(t, 0.02f, 0.32f);
            }
        }

        static void FillBoom(float[] data, int sr)
        {
            float low = 0f;
            float high = 0f;
            float thumpPhase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                float noise = SoundLab.Noise(i);
                low = Mathf.Lerp(low, noise, SoundLab.LowpassAlpha(320f, sr));
                high = Mathf.Lerp(high, noise, SoundLab.LowpassAlpha(5200f, sr));

                thumpPhase += Mathf.Lerp(70f, 34f, t) / sr;
                float thump = Mathf.Sin(thumpPhase * Mathf.PI * 2f) * SoundLab.AttackDecay(t, 0.004f, 0.3f);

                float sparkle = (high - low) * SoundLab.AttackDecay(t, 0.01f, 0.7f) * 0.5f;
                data[i] = low * SoundLab.AttackDecay(t, 0.003f, 0.55f) * 1.1f + thump * 0.8f + sparkle;
            }
        }

        static void FillGameOver(float[] data, int sr)
        {
            float[] notes = { 440f, 349.23f, 261.63f };

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                float sum = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.22f;
                    if (t < start)
                    {
                        continue;
                    }

                    float local = t - start;
                    sum += Mathf.Sin(local * notes[n] * Mathf.PI * 2f) * SoundLab.AttackDecay(local, 0.01f, 0.9f);
                }

                data[i] = sum / notes.Length;
            }
        }

        static void FillMusic(float[] data, int sr)
        {
            float[][] chords =
            {
                new[] { 110f, 220f, 261.63f, 329.63f },
                new[] { 87.31f, 174.61f, 261.63f, 349.23f },
                new[] { 130.81f, 196f, 261.63f, 392f },
                new[] { 98f, 146.83f, 196f, 293.66f }
            };

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sr;
                int chordIndex = Mathf.Min(3, (int)(t / (MusicLength * 0.25f)));
                float local = t - chordIndex * (MusicLength * 0.25f);
                float[] chord = chords[chordIndex];

                float pad = 0f;
                for (int n = 0; n < chord.Length; n++)
                {
                    float frequency = SoundLab.Quantize(chord[n], MusicLength);
                    float weight = n == 0 ? 0.55f : 0.35f / n;
                    pad += Mathf.Sin(t * frequency * Mathf.PI * 2f) * weight;
                }

                float envelope = SoundLab.AttackDecay(local, 0.45f, 4f);
                float sub = Mathf.Sin(t * SoundLab.Quantize(chord[0] * 0.5f, MusicLength) * Mathf.PI * 2f) * 0.3f;
                float tremolo = 0.85f + 0.15f * Mathf.Sin(t * SoundLab.Quantize(2.75f, MusicLength) * Mathf.PI * 2f);

                int step = (int)(t / 0.25f);
                float stepLocal = t - step * 0.25f;
                float bellFrequency = SoundLab.Quantize(chord[step % chord.Length] * 4f, MusicLength);
                float bell = Mathf.Sin(stepLocal * bellFrequency * Mathf.PI * 2f)
                             * SoundLab.AttackDecay(stepLocal, 0.004f, 0.18f) * 0.18f;

                data[i] = pad * envelope * tremolo + sub * envelope + bell;
            }
        }
    }
}
