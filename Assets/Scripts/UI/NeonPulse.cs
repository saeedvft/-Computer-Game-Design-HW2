using UnityEngine;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// A slow breathing loop for the things that should never sit perfectly still: the new record chip,
    /// the tip of a full tank. Brightens and grows together, so the element looks lit from inside
    /// rather than scaled by a script.
    ///
    /// The loop is clocked from the moment it was enabled, not from the start of the session, so a chip
    /// that pops into existence always begins at its dimmest and smallest and grows into view.
    /// </summary>
    public sealed class NeonPulse : MonoBehaviour
    {
        [OptionalReference] [SerializeField] Graphic target;
        [OptionalReference] [SerializeField] RectTransform rect;

        [Header("Loop")]
        [SerializeField] float seconds = 1.2f;
        [SerializeField] float alphaDepth = 0.30f;
        [SerializeField] float scaleDepth = 0.05f;

        Color rest;
        float started;

        /// <summary>Names the graphic and the tempo. Negative values leave the defaults alone.</summary>
        public void Wire(Graphic target = null, float seconds = -1f, float alphaDepth = -1f, float scaleDepth = -1f)
        {
            if (target != null)
            {
                this.target = target;
            }

            if (seconds > 0f)
            {
                this.seconds = seconds;
            }

            if (alphaDepth >= 0f)
            {
                this.alphaDepth = alphaDepth;
            }

            if (scaleDepth >= 0f)
            {
                this.scaleDepth = scaleDepth;
            }
        }

        void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Graphic>();
            }

            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            if (target == null)
            {
                Debug.LogError($"{name} has no Graphic to pulse.", this);
                enabled = false;
                return;
            }

            rest = target.color;
        }

        void OnEnable()
        {
            started = Time.unscaledTime;
        }

        void OnDisable()
        {
            if (target != null)
            {
                target.color = rest;
            }

            rect.localScale = Vector3.one;
        }

        void Update()
        {
            float phase = Mathf.Repeat(Time.unscaledTime - started, seconds) / seconds;
            float wave = 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);

            Color tint = rest;
            tint.a *= Mathf.Lerp(1f - alphaDepth, 1f, wave);
            target.color = tint;

            rect.localScale = Vector3.one * Mathf.Lerp(1f, 1f + scaleDepth, wave);
        }
    }
}
