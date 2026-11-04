using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// A meter whose bar is a width rather than a fill amount. <c>Image.Type.Filled</c> cannot be used
    /// on a nine sliced sprite, because the border insets the fill and the gauge quietly starts lying;
    /// writing the width directly makes the visible length the number, rounded at every level, and
    /// proportionality stops being something that has to be defended.
    ///
    /// Drives both instruments on the HUD: fuel, with its tip glow, percent readout and low tank pulse,
    /// and speed, which is the same component with the optional parts left empty.
    /// </summary>
    public sealed class MeterBar : MonoBehaviour
    {
        [Serializable]
        public struct ColorStop
        {
            public float at;
            public Color color;
        }

        [Header("Parts")]
        [SerializeField] RectTransform track;
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [OptionalReference] [SerializeField] RectTransform tip;
        [OptionalReference] [SerializeField] TMP_Text valueLabel;

        [Header("Geometry")]
        [SerializeField] float inset = 2f;
        [SerializeField] float smoothing = 9f;

        [Header("Colour")]
        [SerializeField] ColorStop[] ramp = Array.Empty<ColorStop>();
        [SerializeField] Color warnColor = new(1f, 0.69f, 0.13f, 1f);
        [SerializeField] float warnThreshold = 0.28f;
        [SerializeField] float pulseDepth = 0.22f;
        [SerializeField] float pulseSeconds = 0.9f;

        float target;
        float value;
        Color rest;
        Color labelRest;
        bool warning;

        /// <summary>
        /// Names the parts the scene builder just made. Anything left null is simply not driven, which
        /// is how the speed strip gets a bar without a percent readout, and how a gauge that should
        /// never flash gets its warning turned off.
        /// </summary>
        public void Wire(
            RectTransform track, RectTransform fill, Image fillImage, RectTransform tip = null,
            TMP_Text valueLabel = null, ColorStop[] ramp = null,
            float inset = 2f, float smoothing = 9f, float warnThreshold = 0.28f)
        {
            this.track = track;
            this.fill = fill;
            this.fillImage = fillImage;
            this.tip = tip;
            this.valueLabel = valueLabel;
            this.ramp = ramp ?? Array.Empty<ColorStop>();
            this.inset = inset;
            this.smoothing = smoothing;
            this.warnThreshold = warnThreshold;
        }

        void Awake()
        {
            if (track == null || fill == null || fillImage == null)
            {
                Debug.LogError($"{name} is a meter with no track or fill to write to.", this);
                enabled = false;
                return;
            }

            rest = fillImage.color;

            if (valueLabel != null)
            {
                labelRest = valueLabel.color;
            }
        }

        /// <summary>The fraction to show, 0 at empty and 1 at full.</summary>
        public void SetTarget(float normalized)
        {
            target = Mathf.Clamp01(normalized);
        }

        void Update()
        {
            // Pause holds the time scale at zero, so the meter eases on unscaled time: it keeps moving
            // for the frame after a run starts, and the low tank pulse keeps beating behind a menu.
            float dt = Time.unscaledDeltaTime;
            value += (target - value) * (1f - Mathf.Exp(-smoothing * dt));

            float width = Mathf.Max(0f, track.rect.width - inset * 2f) * value;
            fill.sizeDelta = new Vector2(width, fill.sizeDelta.y);

            if (tip != null)
            {
                tip.anchoredPosition = new Vector2(inset + width, tip.anchoredPosition.y);
            }

            if (valueLabel != null)
            {
                valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
            }

            bool nowWarning = value < warnThreshold && value > 0f;

            if (nowWarning != warning)
            {
                warning = nowWarning;

                if (valueLabel != null)
                {
                    valueLabel.color = nowWarning ? warnColor : labelRest;
                }
            }

            Color tinted = Evaluate(value);

            if (warning)
            {
                float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / pulseSeconds));
                tinted *= pulse;
            }

            fillImage.color = tinted;
        }

        Color Evaluate(float t)
        {
            if (ramp == null || ramp.Length == 0)
            {
                // No ramp means the colour the designer put on the fill is the colour that stays.
                return rest;
            }

            if (t <= ramp[0].at)
            {
                return ramp[0].color;
            }

            for (int i = 1; i < ramp.Length; i++)
            {
                if (t > ramp[i].at)
                {
                    continue;
                }

                ColorStop lower = ramp[i - 1];
                ColorStop upper = ramp[i];
                float span = upper.at - lower.at;

                return span <= 0.0001f
                    ? upper.color
                    : Color.Lerp(lower.color, upper.color, (t - lower.at) / span);
            }

            return ramp[ramp.Length - 1].color;
        }
    }
}
