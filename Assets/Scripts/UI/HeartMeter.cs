using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// The life hearts. All three stay in the hierarchy and animate, instead of switching GameObjects
    /// on and off: a heart that vanishes tells you nothing, while one that pops and empties does.
    /// </summary>
    public sealed class HeartMeter : MonoBehaviour
    {
        const string FillName = "Fill";

        [SerializeField] float fadeDuration = 0.28f;
        [SerializeField] float pop = 0.3f;
        [SerializeField] float popDuration = 0.45f;

        readonly List<Heart> hearts = new();

        void Awake()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                // The heart's own image is the empty shell and its child is the fill, so the two
                // states are one GameObject each and never overlap.
                Image outline = child.GetComponent<Image>();
                Image fill = child.Find(FillName)?.GetComponent<Image>();

                if (outline == null || fill == null)
                {
                    Debug.LogError($"Heart '{child.name}' needs an outline image and a '{FillName}' child.", this);
                    continue;
                }

                hearts.Add(new Heart
                {
                    gameObject = child.gameObject,
                    fill = fill,
                    filled = fill.color.a > 0.5f,
                    restColor = fill.color,
                });
            }
        }

        /// <summary>
        /// Shows exactly this many hearts. An absolute write, so the next run cannot inherit the last
        /// one's dimmed hearts: every difference from the current state is animated, and everything
        /// already correct is left alone.
        /// </summary>
        public void SetLives(int lives)
        {
            for (int i = 0; i < hearts.Count; i++)
            {
                bool filled = i < lives;

                if (hearts[i].filled != filled)
                {
                    hearts[i].filled = filled;
                    Animate(hearts[i], filled);
                }
            }
        }

        void Animate(Heart heart, bool filled)
        {
            Hw2Tween.Kill(heart.gameObject);
            heart.gameObject.transform.localScale = Vector3.one;

            float from = heart.fill.color.a;
            Hw2Tween.Float(heart.gameObject, Hw2Tween.Slot.Alpha, from, filled ? 1f : 0f,
                fadeDuration, Hw2Tween.Ease.OutCubic, a =>
                {
                    Color tint = heart.restColor;
                    tint.a = a;
                    heart.fill.color = tint;
                });

            // The scale of the whole heart, emptied or filled, is what the eye actually follows.
            Hw2Tween.PunchScale(heart.gameObject, heart.gameObject.transform,
                filled ? pop : -pop, popDuration);
        }

        class Heart
        {
            public GameObject gameObject;
            public Image fill;
            public Color restColor;
            public bool filled;
        }
    }
}
