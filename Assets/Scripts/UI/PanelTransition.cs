using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HW2
{
    /// <summary>
    /// Fades a screen in with its widgets arriving one after another, and fades it out before handing
    /// the GameObject back to <c>SetActive</c>. Attached to a screen root by the scene builder, which
    /// also puts a CanvasGroup on every direct child so the stagger has something to drive.
    /// </summary>
    public sealed class PanelTransition : MonoBehaviour
    {
        [SerializeField] float inDuration = 0.30f;
        [SerializeField] float outDuration = 0.18f;
        [SerializeField] float stagger = 0.045f;
        [SerializeField] float rise = 26f;
        [SerializeField] float entryScale = 0.955f;

        /// <summary>
        /// The node whose direct children arrive one after another. Usually the screen root, but a
        /// dialog fades its card's contents, not the card. Assigned by the scene builder.
        /// </summary>
        [OptionalReference] [SerializeField] RectTransform staggerRoot;

        CanvasGroup root;
        RectTransform rect;
        Vector2 restPosition;
        CanvasGroup[] children = Array.Empty<CanvasGroup>();

        // What the game asked for, rather than what is active: a screen stays in the hierarchy for the
        // length of its out-fade, and a second "hide me" during that fade must not restart it.
        bool shown;

        public void Wire(RectTransform steps)
        {
            staggerRoot = steps;
        }

        void Awake()
        {
            root = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;

            if (root == null)
            {
                Debug.LogError($"{name} has no CanvasGroup, so there is nothing for it to fade.", this);
                enabled = false;
                return;
            }

            restPosition = rect.anchoredPosition;

            List<CanvasGroup> found = new();
            Transform source = staggerRoot != null ? staggerRoot : rect;

            for (int i = 0; i < source.childCount; i++)
            {
                CanvasGroup child = source.GetChild(i).GetComponent<CanvasGroup>();
                if (child != null)
                {
                    found.Add(child);
                }
            }

            children = found.ToArray();
        }

        /// <summary>
        /// The one call a screen makes: show it, or take it away. Showing plays the entrance, hiding
        /// plays the exit and only then deactivates, so a panel never disappears mid sentence.
        /// </summary>
        public void SetVisible(bool value)
        {
            if (value == shown)
            {
                return;
            }

            shown = value;

            if (value)
            {
                gameObject.SetActive(true);
                PlayIn();
            }
            else
            {
                // A selection outlives a deactivated panel otherwise, because deselect is not sent when
                // a parent goes off, and the next screen would inherit a button it cannot see.
                EventSystem system = EventSystem.current;
                if (system != null && system.currentSelectedGameObject != null
                    && system.currentSelectedGameObject.transform.IsChildOf(transform))
                {
                    system.SetSelectedGameObject(null);
                }

                PlayOut();
            }
        }

        /// <summary>
        /// Starts a screen out with no fade. The builder leaves every screen active in the saved scene,
        /// so this is what keeps the first frame from showing all four at once.
        /// </summary>
        public void StartHidden()
        {
            shown = false;
            Restore();
            gameObject.SetActive(false);
        }

        public void PlayIn()
        {
            Restore();

            root.alpha = 1f;
            root.blocksRaycasts = false;
            root.interactable = false;

            rect.localScale = Vector3.one * entryScale;
            Hw2Tween.Scale(gameObject, rect, entryScale, 1f, inDuration, Hw2Tween.Ease.OutCubic);
            Hw2Tween.Rise(gameObject, rect, rise, inDuration, Hw2Tween.Ease.OutCubic);

            for (int i = 0; i < children.Length; i++)
            {
                CanvasGroup child = children[i];
                child.alpha = 0f;
                Hw2Tween.Float(child, Hw2Tween.Slot.Alpha, 0f, 1f, inDuration, Hw2Tween.Ease.OutQuad,
                    a => child.alpha = a, delay: i * stagger);
            }

            float settle = inDuration + (children.Length - 1) * stagger;

            // Nothing is clickable while it is still arriving, but a pointer already over a button
            // must find it live the moment the screen is up.
            Hw2Tween.Delay(gameObject, settle, () =>
            {
                root.blocksRaycasts = true;
                root.interactable = true;
            });
        }

        public void PlayOut(Action onDone = null)
        {
            if (!gameObject.activeSelf)
            {
                onDone?.Invoke();
                return;
            }

            Hw2Tween.Kill(gameObject);
            foreach (CanvasGroup child in children)
            {
                Hw2Tween.Kill(child);
            }

            root.blocksRaycasts = false;
            root.interactable = false;

            float from = root.alpha;
            Hw2Tween.Float(gameObject, Hw2Tween.Slot.Alpha, from, 0f, outDuration, Hw2Tween.Ease.Linear,
                a => root.alpha = a,
                onDone: () =>
                {
                    gameObject.SetActive(false);
                    onDone?.Invoke();
                });
        }

        /// <summary>
        /// Puts the screen back exactly where it rests, so an interrupted transition cannot leave a
        /// panel half faded or off centre for the next time it appears.
        /// </summary>
        void Restore()
        {
            Hw2Tween.Kill(gameObject);
            foreach (CanvasGroup child in children)
            {
                Hw2Tween.Kill(child);
            }

            root.alpha = 1f;
            rect.anchoredPosition = restPosition;
            rect.localScale = Vector3.one;

            foreach (CanvasGroup child in children)
            {
                child.alpha = 1f;
            }
        }
    }
}
