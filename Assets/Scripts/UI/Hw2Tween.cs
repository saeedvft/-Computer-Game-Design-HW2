using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HW2
{
    /// <summary>
    /// The whole motion layer of the interface: a handful of tweens advanced outside the time scale.
    ///
    /// Coroutines are not usable here for two reasons. Pause and game over run with
    /// <see cref="Time.timeScale"/> at zero, so every WaitForSeconds would freeze mid transition, and
    /// a screen that finishes fading out is deactivated, which kills any coroutine hosted on it while
    /// the tween still has frames to write. This runner lives on its own object and advances both.
    /// </summary>
    public static class Hw2Tween
    {
        /// <summary>What a tween drives, so starting a second one on the same thing replaces the first.</summary>
        public enum Slot
        {
            Value,
            Alpha,
            Scale,
            Tint,
            Timer
        }

        public enum Ease
        {
            Linear,
            OutQuad,
            OutCubic,
            InOutCubic,
            OutBack,
            OutElastic
        }

        /// <summary>Tweens <paramref name="from"/> to <paramref name="to"/>, reporting the eased value.</summary>
        public static void Float(
            Object owner, Slot slot, float from, float to, float duration, Ease ease,
            Action<float> onValue, Action onDone = null, float delay = 0f)
        {
            MotionRunner.Add(owner, slot, duration, ease, p => onValue(Mathf.LerpUnclamped(from, to, p)), onDone, delay);
        }

        /// <summary>Drives the eased progress itself, for curves a single lerp cannot express.</summary>
        public static void Progress(
            Object owner, Slot slot, float duration, Ease ease,
            Action<float> onProgress, Action onDone = null, float delay = 0f)
        {
            MotionRunner.Add(owner, slot, duration, ease, onProgress, onDone, delay);
        }

        /// <summary>Runs once, seconds later. A timer that keeps working while the time scale is zero.</summary>
        public static void Delay(Object owner, float seconds, Action onDone)
        {
            MotionRunner.Add(owner, Slot.Timer, seconds, Ease.Linear, null, onDone, 0f);
        }

        /// <summary>Lifts a panel from <paramref name="pixels"/> below where it rests, into place.</summary>
        public static void Rise(Object owner, RectTransform target, float pixels, float duration, Ease ease)
        {
            Vector2 rest = target.anchoredPosition;
            Float(owner, Slot.Value, 0f, 1f, duration, ease, p =>
                target.anchoredPosition = rest - Vector2.up * (pixels * (1f - p)));
        }

        /// <summary>
        /// Scales a transform between two multiples of the unit scale. UI transforms rest at one, so
        /// naming the states as numbers keeps a hover and a press from compounding.
        /// </summary>
        public static void Scale(Object owner, Transform target, float from, float to, float duration, Ease ease)
        {
            Float(owner, Slot.Scale, from, to, duration, ease,
                p => target.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, p));
        }

        /// <summary>A short overshoot on <paramref name="target"/>'s own scale: used for pickups and hits.</summary>
        public static void PunchScale(Object owner, Transform target, float amount, float duration)
        {
            Vector3 baseScale = target.localScale;
            Progress(owner, Slot.Scale, duration, Ease.OutElastic, p =>
                target.localScale = baseScale * (1f + amount * (1f - p)));
        }

        /// <summary>Drops every tween owned by an object, before a component resets its own state.</summary>
        public static void Kill(Object owner)
        {
            MotionRunner.Remove(owner);
        }

        internal static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutBack:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                case Ease.OutElastic:
                    const float c4 = 2f * Mathf.PI / 3f;
                    return t <= 0f ? 0f
                        : t >= 1f ? 1f
                        : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                default:
                    return t;
            }
        }
    }

    /// <summary>
    /// Advances the tweens. Created on first use and kept alive across scene loads, because a screen
    /// fading out at the exact moment the scene changes still has frames left to write.
    /// </summary>
    sealed class MotionRunner : MonoBehaviour
    {
        static MotionRunner current;
        static readonly List<Motion> motions = new();

        public static MotionRunner Instance
        {
            get
            {
                // Unity's == treats a destroyed object as null, so this survives a domain reload being
                // disabled as well as a scene unload taking the object away.
                if (current != null)
                {
                    return current;
                }

                current = new GameObject(nameof(MotionRunner)).AddComponent<MotionRunner>();
                DontDestroyOnLoad(current);
                return current;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = motions.Count - 1; i >= 0; i--)
            {
                Motion motion = motions[i];

                if (motion.owner == null)
                {
                    motions.RemoveAt(i);
                    continue;
                }

                motion.elapsed += dt;
                float t = motion.duration > 0.0001f ? Mathf.Clamp01(motion.elapsed / motion.duration) : 1f;

                motion.onProgress?.Invoke(Hw2Tween.Evaluate(motion.ease, t));

                if (t >= 1f)
                {
                    motions.RemoveAt(i);
                    motion.onDone?.Invoke();
                }
            }
        }

        internal static void Add(
            Object owner, Hw2Tween.Slot slot, float duration, Hw2Tween.Ease ease,
            Action<float> onProgress, Action onDone, float delay)
        {
            Replace(owner, slot);

            _ = Instance;
            motions.Add(new Motion
            {
                owner = owner,
                slot = slot,
                elapsed = -delay,
                duration = duration,
                ease = ease,
                onProgress = onProgress,
                onDone = onDone,
            });
        }

        internal static void Remove(Object owner)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                if (motions[i].owner == owner)
                {
                    motions.RemoveAt(i);
                }
            }
        }

        static void Replace(Object owner, Hw2Tween.Slot slot)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                if (motions[i].owner == owner && motions[i].slot == slot)
                {
                    motions.RemoveAt(i);
                }
            }
        }

        class Motion
        {
            public Object owner;
            public Hw2Tween.Slot slot;
            public float elapsed;
            public float duration;
            public Hw2Tween.Ease ease;
            public Action<float> onProgress;
            public Action onDone;
        }
    }
}
