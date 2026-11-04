using System;
using UnityEngine;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// The screen's reaction to a hit. One impulse, several surfaces: a wash over the whole frame and a
    /// stroke around its edge, both fading on the same clock so they read as a single event.
    ///
    /// The decay is linear in unscaled time and was previously written inline in <c>UIManager</c>. It
    /// stays linear: a curve would look smoother in a screenshot and be wrong in play, because the
    /// first frames after an impact are the ones the player is reacting to.
    /// </summary>
    public sealed class ScreenFlash : MonoBehaviour
    {
        /// <summary>A surface this component owns the alpha of.</summary>
        [Serializable]
        public class Layer
        {
            public Image image;
            public Color color = Color.white;

            [Tooltip("How much of the impulse this surface shows, 0 to 1.")]
            [Range(0f, 1f)]
            public float strength = 1f;
        }

        [SerializeField] Layer[] layers = Array.Empty<Layer>();
        [SerializeField] float decayPerSecond = 1.6f;

        float amount;
        float written = -1f;

        /// <summary>Names the surfaces this impulse lights up, and how fast it fades.</summary>
        public void Wire(Layer[] layers, float decayPerSecond = -1f)
        {
            this.layers = layers ?? Array.Empty<Layer>();

            if (decayPerSecond > 0f)
            {
                this.decayPerSecond = decayPerSecond;
            }
        }

        /// <summary>Reacts to a hit. A stronger impulse wins, so two hits in one frame do not halve.</summary>
        public void Punch(float impulse)
        {
            amount = Mathf.Max(amount, impulse);
        }

        /// <summary>
        /// Takes a flash away mid fade, for a new run that would otherwise open on the last one's hit.
        /// </summary>
        public void Clear()
        {
            amount = 0f;
            written = 0f;
            Write();
        }

        void Awake()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = layers[i];

                if (layer == null || layer.image == null)
                {
                    Debug.LogError($"{name} has a flash layer with no image on it.", this);
                    continue;
                }

                layer.image.color = Tint(layer);
            }
        }

        void Update()
        {
            if (amount > 0f)
            {
                amount = Mathf.Max(0f, amount - Time.unscaledDeltaTime * decayPerSecond);
            }

            if (amount == written)
            {
                return;
            }

            written = amount;
            Write();
        }

        void Write()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = layers[i];

                if (layer != null && layer.image != null)
                {
                    layer.image.color = Tint(layer);
                }
            }
        }

        Color Tint(Layer layer)
        {
            Color tint = layer.color;
            tint.a = layer.color.a * layer.strength * amount;
            return tint;
        }
    }
}
