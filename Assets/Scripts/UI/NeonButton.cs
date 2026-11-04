using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HW2
{
    /// <summary>
    /// Hover, focus and press for one button. Replaces the colour tint transition, which on a screen
    /// space overlay canvas can only clamp to white: a highlight that erases the colour it sits on is
    /// not a highlight.
    ///
    /// Pointer hover and keyboard focus are deliberately different looks. A selector that merely
    /// imitates hover leaves a player on a gamepad unable to tell what Enter would do.
    /// </summary>
    public sealed class NeonButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Parts (built by Hw2SceneBuilder.PillButton)")]
        [SerializeField] Image surface;
        [SerializeField] Image halo;
        [SerializeField] Image focusFrame;

        [Header("Motion")]
        [SerializeField] float hoverScale = 1.022f;
        [SerializeField] float pressScale = 0.965f;
        [SerializeField] float stateDuration = 0.12f;
        [SerializeField] float pressDuration = 0.06f;
        [SerializeField] float haloHoverAlpha = 0.34f;
        [SerializeField] float pressDarkening = 0.72f;
        [SerializeField] float focusSpeed = 4f;
        [SerializeField] float breathSeconds = 1.6f;
        [SerializeField] float breathDepth = 0.18f;

        Color surfaceRest;
        Color haloRest;
        float frameRestAlpha;

        /// <summary>
        /// Names the parts the scene builder just made. The button's own image cannot be guessed from
        /// the hierarchy because it deliberately has several images of the same type around it.
        /// </summary>
        public void Wire(Image surface, Image halo, Image focusFrame)
        {
            this.surface = surface;
            this.halo = halo;
            this.focusFrame = focusFrame;
        }

        bool hover;
        bool focus;
        bool pressed;

        float frameAmount;
        float scaleTarget = 1f;

        void Awake()
        {
            // The look is animated here, so Unity's own tint has to be off rather than merely unused.
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }

            if (surface != null)
            {
                surfaceRest = surface.color;
            }

            if (halo != null)
            {
                haloRest = halo.color;
            }

            if (focusFrame != null)
            {
                frameRestAlpha = focusFrame.color.a > 0f ? focusFrame.color.a : 1f;
            }
        }

        void OnEnable()
        {
            // A tween outlives the object it is writing to, because the runner is not attached to the
            // panel. Drop whatever was in flight before re-deriving the rest state.
            Hw2Tween.Kill(this);

            focus = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            hover = !focus && IsPointerInside();

            scaleTarget = 1f;
            transform.localScale = Vector3.one;
            WriteSurface();
            WriteHalo(haloRest.a);
            frameAmount = focus ? 1f : 0f;

            // The frame is built at full strength so its colour can be inspected in the editor, which
            // means nothing may assume it starts out invisible.
            if (focusFrame != null)
            {
                Color tint = focusFrame.color;
                tint.a = frameAmount * frameRestAlpha;
                focusFrame.color = tint;
            }
        }

        // ---------------------------------------------------------------- events

        public void OnPointerEnter(PointerEventData _)
        {
            hover = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData _)
        {
            hover = false;
            pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData _)
        {
            pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData _)
        {
            pressed = false;
            hover = IsPointerInside();
            Apply();
        }

        public void OnSelect(BaseEventData _)
        {
            focus = true;
            Apply();
        }

        public void OnDeselect(BaseEventData _)
        {
            focus = false;
            Apply();
        }

        // ---------------------------------------------------------------- state

        /// <summary>
        /// Scales toward the size the previous state was heading for rather than wherever the transform
        /// currently is, so a fast hover in and out cannot make the button creep.
        /// </summary>
        void Apply()
        {
            float to = pressed ? pressScale : hover ? hoverScale : 1f;

            if (!Mathf.Approximately(to, scaleTarget))
            {
                Hw2Tween.Scale(this, transform, scaleTarget, to,
                    pressed ? pressDuration : stateDuration,
                    pressed ? Hw2Tween.Ease.OutQuad : Hw2Tween.Ease.OutBack);
                scaleTarget = to;
            }

            WriteSurface();
            WriteHalo(pressed ? Mathf.Max(haloHoverAlpha, haloRest.a)
                : hover ? haloHoverAlpha
                : haloRest.a);
        }

        void WriteSurface()
        {
            if (surface == null)
            {
                return;
            }

            surface.color = pressed ? surfaceRest * pressDarkening : surfaceRest;
        }

        void WriteHalo(float alpha)
        {
            if (halo == null)
            {
                return;
            }

            Color tint = haloRest;
            tint.a = alpha;

            if (Mathf.Approximately(tint.a, halo.color.a))
            {
                return;
            }

            Hw2Tween.Float(this, Hw2Tween.Slot.Alpha, halo.color.a, tint.a, stateDuration,
                Hw2Tween.Ease.OutQuad, a =>
                {
                    Color current = haloRest;
                    current.a = a;
                    halo.color = current;
                });
        }

        /// <summary>
        /// Where the pointer actually is, rather than what the EventSystem last reported. Deselect does
        /// not fire when a parent panel is switched off, so a button that was hovered when the screen
        /// closed would otherwise come back still lit.
        /// </summary>
        bool IsPointerInside()
        {
            Pointer pointer = Pointer.current;
            return pointer != null
                && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)transform, pointer.position.ReadValue(), null);
        }

        // ---------------------------------------------------------------- focus breath
        // The brackets are the only thing that says "this one is selected", so they fade in and then
        // keep a slow pulse. Written directly every frame instead of tweened: it is the sole writer of
        // this alpha, and a looping tween would have to reschedule itself to do the same thing.

        void Update()
        {
            if (focusFrame == null || (!focus && frameAmount <= 0f))
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            frameAmount = Mathf.MoveTowards(frameAmount, focus ? 1f : 0f, dt * focusSpeed);

            float breath = focus
                ? 1f - breathDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / breathSeconds))
                : 1f;

            Color tint = focusFrame.color;
            tint.a = frameAmount * frameRestAlpha * breath;
            focusFrame.color = tint;
        }
    }
}
