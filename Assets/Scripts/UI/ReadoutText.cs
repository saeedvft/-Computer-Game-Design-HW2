using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace HW2
{
    /// <summary>
    /// A number that arrives instead of appearing: it counts up to its new value and overshoots as it
    /// lands. Used on the game over score, where the roll gives the run a length, and on the HUD score,
    /// where only the pop is used.
    ///
    /// The wording around the number belongs to whoever built the label, so the prefix and suffix are
    /// serialized and this component is only ever responsible for the digits between them.
    /// </summary>
    public sealed class ReadoutText : MonoBehaviour
    {
        [OptionalReference] [SerializeField] TMP_Text label;

        [Header("Copy")]
        [SerializeField] string prefix = string.Empty;
        [SerializeField] string suffix = string.Empty;
        [SerializeField] bool groupThousands = true;

        [Header("Motion")]
        [SerializeField] float rollDuration = 0.55f;
        [SerializeField] float punchAmount = 0.14f;
        [SerializeField] float punchDuration = 0.4f;

        float shown;
        bool hasLabel;

        /// <summary>
        /// Names the text to write and the copy that surrounds it. The builder owns the wording, so a
        /// score readout can be "SCORE 12,400" while the speed readout is bare digits, with the same
        /// component driving both.
        /// </summary>
        public void Wire(TMP_Text label, string prefix = null, string suffix = null)
        {
            if (label != null)
            {
                this.label = label;
            }

            if (prefix != null)
            {
                this.prefix = prefix;
            }

            if (suffix != null)
            {
                this.suffix = suffix;
            }
        }

        void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }

            hasLabel = label != null;

            if (!hasLabel)
            {
                Debug.LogError($"{name} has no TMP_Text to write to.", this);
            }
        }

        /// <summary>Writes the number immediately, cutting short any roll still in flight.</summary>
        public void SetValue(float value)
        {
            Hw2Tween.Kill(gameObject);
            shown = value;
            Write();
        }

        /// <summary>Counts from whatever is on screen to <paramref name="value"/>, then pops.</summary>
        public void RollTo(float value)
        {
            RollTo(value, rollDuration);
        }

        public void RollTo(float value, float duration)
        {
            if (!hasLabel)
            {
                return;
            }

            Hw2Tween.Float(gameObject, Hw2Tween.Slot.Value, shown, value, duration,
                Hw2Tween.Ease.InOutCubic, p =>
                {
                    shown = p;
                    Write();
                },
                onDone: () =>
                {
                    shown = value;
                    Write();
                    Punch(punchAmount, punchDuration);
                });
        }

        /// <summary>A short scale overshoot, for the frame a score is earned.</summary>
        public void Punch(float amount, float duration)
        {
            RectTransform rect = (RectTransform)transform;
            Hw2Tween.Scale(gameObject, rect, 1f + amount, 1f, duration, Hw2Tween.Ease.OutElastic);
        }

        void Write()
        {
            if (!hasLabel)
            {
                return;
            }

            // Grouping is culture independent on purpose: the reference scene is scored on a machine
            // whose locale may use a space or a dot as the thousands separator.
            string digits = Mathf.RoundToInt(shown).ToString(
                groupThousands ? "N0" : "0", CultureInfo.InvariantCulture);

            label.text = prefix.Length > 0 || suffix.Length > 0
                ? $"{prefix}{digits}{suffix}"
                : digits;
        }
    }
}
