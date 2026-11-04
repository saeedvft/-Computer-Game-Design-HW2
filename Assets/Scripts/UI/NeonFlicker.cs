using UnityEngine;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// Gas discharge lamp: mostly steady, faintly buzzing, and cutting out now and then. Drives the
    /// alpha of one graphic, leaving its colour to whoever set it, so a halo can be dimmed as much as
    /// a title.
    ///
    /// Deliberately not a tween. A flicker has no start and no end, and easing curves interpolate
    /// between two known states, so sampling noise against the clock is both shorter and honest.
    /// </summary>
    public sealed class NeonFlicker : MonoBehaviour
    {
        [OptionalReference] [SerializeField] Graphic target;

        [Header("Character")]
        [SerializeField] float humDepth = 0.10f;
        [SerializeField] float humSpeed = 11f;
        [SerializeField] float dropoutsPerSecond = 2f;
        [SerializeField] float dropoutFloor = 0.45f;

        [Tooltip("Separates this tube from the others on screen. Two elements sharing a seed flicker in lockstep.")]
        [SerializeField] int seed = 0;

        Color rest;
        float offset;

        /// <summary>
        /// Chooses which graphic this tube is, and how it fails. A negative number leaves the default
        /// alone; a seed shared by two components makes them flicker together, which is what a title
        /// and the halo behind it need.
        /// </summary>
        public void Wire(Graphic target = null, int seed = 0, float humDepth = -1f, float dropoutFloor = -1f)
        {
            if (target != null)
            {
                this.target = target;
            }

            this.seed = seed;

            if (humDepth >= 0f)
            {
                this.humDepth = humDepth;
            }

            if (dropoutFloor >= 0f)
            {
                this.dropoutFloor = dropoutFloor;
            }
        }

        void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Graphic>();
            }

            if (target == null)
            {
                Debug.LogError($"{name} has no Graphic to flicker.", this);
                enabled = false;
                return;
            }

            rest = target.color;

            // Perlin noise is a surface, so moving off along the second axis picks a different
            // sequence without needing a random number that would change every run.
            offset = seed * 13.37f;
        }

        void OnDisable()
        {
            if (target != null)
            {
                target.color = rest;
            }
        }

        void Update()
        {
            // Alpha only: the colour is the design, and scaling the channels with it would dim the
            // hue as the tube sagged.
            Color level = rest;
            level.a *= Level(Time.unscaledTime);
            target.color = level;
        }

        float Level(float time)
        {
            float hum = Mathf.PerlinNoise(time * humSpeed, offset);
            float level = 1f - humDepth * hum;

            // A drop out is a crossing of a high threshold, and Perlin passes it smoothly, so the tube
            // sags out and back rather than switching hard off.
            float cut = Mathf.PerlinNoise(time * dropoutsPerSecond, offset + 7.13f);
            level *= Mathf.Lerp(1f, dropoutFloor, Mathf.InverseLerp(0.72f, 0.95f, cut));

            return level;
        }
    }
}
