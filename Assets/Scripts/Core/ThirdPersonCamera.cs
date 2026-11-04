using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Third person chase camera. The world scrolls instead of the car, so the rig only tracks
    /// lateral position and adds speed driven framing (pull back + wider FOV).
    ///
    /// On the title screen it becomes an attract mode: the framing breathes on slow noise while the
    /// car is parked, which is the difference between a screenshot of a road and a world that is
    /// still running behind a menu.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0f, 4.2f, -9.6f);
        [SerializeField] Vector3 lookAhead = new(0f, 1.25f, 5f);
        [SerializeField] float lateralFollow = 0.55f;
        [SerializeField] float smoothTime = 0.13f;
        [SerializeField] float speedPullBack = 1.1f;
        [SerializeField] float speedLift = 0.4f;
        [SerializeField] float minFieldOfView = 58f;
        [SerializeField] float maxFieldOfView = 76f;

        [Header("Attract mode")]
        [Tooltip("How long the title screen takes to lean into the drift, and the run takes to drop it.")]
        [SerializeField] float menuBlendTime = 1.4f;
        [Tooltip("Noise cycles per second. Slow enough that the eye reads it as weather, not as wobble.")]
        [SerializeField] float driftSpeed = 0.055f;
        [SerializeField] Vector3 driftRange = new(2.8f, 0.85f, 1.6f);
        [SerializeField] Vector2 driftLookRange = new(1.7f, 0.45f);
        [SerializeField] float driftFieldOfView = 3.5f;
        [Tooltip("How much of the lateral follow the title screen gives up, so the parked car is not welded to the middle.")]
        [SerializeField] float lateralDrift = 0.7f;

        Camera cam;
        Vector3 chase;
        Vector3 velocity;
        float menuWeight;
        float menuVelocity;

        void Awake()
        {
            cam = GetComponent<Camera>();

            // The rig keeps its own idea of where the chase put it. Reading it back off the transform
            // instead would feed the drift and the shake into the next frame's SmoothDamp as its
            // starting point, and a slow offset added to a damp that is chasing a gap is a resonance:
            // the velocity grows until the camera is swinging ten metres past the road.
            chase = transform.position;
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            bool menu = gm != null && gm.State == GameState.Menu;

            float before = menuWeight;
            menuWeight = Mathf.SmoothDamp(menuWeight, menu ? 1f : 0f, ref menuVelocity,
                menuBlendTime, float.MaxValue, Time.unscaledDeltaTime);

            // The smoothing that carried the drift must not be handed to the run, or the first frame of
            // play yanks the framing toward wherever the noise happened to leave it.
            if (before < 0.5f != menuWeight < 0.5f)
            {
                velocity = Vector3.zero;
            }

            // A run that ended at 180 km/h would otherwise leave the menu inheriting its wide framing.
            float speed01 = gm != null ? Mathf.Lerp(gm.Speed01, 0f, menuWeight) : 0f;
            float follow = lateralFollow * (1f - menuWeight * lateralDrift);

            // Perlin rather than sine: three sines are a rotation, and a rotation around a parked car
            // looks like a turntable. Each channel reads a different slice of the noise, so the three
            // axes never come back into step.
            float t = Time.unscaledTime * driftSpeed;
            Vector3 drift = menuWeight * new Vector3(
                (Mathf.PerlinNoise(t, 0.31f) - 0.5f) * 2f * driftRange.x,
                (Mathf.PerlinNoise(t, 1.77f) - 0.5f) * 2f * driftRange.y,
                (Mathf.PerlinNoise(t, 3.19f) - 0.5f) * 2f * driftRange.z);
            Vector3 lookDrift = menuWeight * new Vector3(
                (Mathf.PerlinNoise(t, 5.53f) - 0.5f) * 2f * driftLookRange.x,
                (Mathf.PerlinNoise(t, 7.91f) - 0.5f) * 2f * driftLookRange.y,
                0f);
            float fovDrift = menuWeight * (Mathf.PerlinNoise(t, 9.13f) - 0.5f) * 2f * driftFieldOfView;

            Vector3 focus = target.position;
            Vector3 desired = new(
                focus.x * follow,
                offset.y + speed01 * speedLift,
                offset.z - speed01 * speedPullBack);

            Vector3 smoothed = Vector3.SmoothDamp(chase, desired, ref velocity, smoothTime);
            chase = smoothed;

            // Drift and shake both sit outside the smoothing: the first is already slow, and the second
            // is meant to be felt as a hit rather than absorbed into the chase.
            Vector3 shake = CameraShake.Instance != null ? CameraShake.Instance.CurrentOffset : Vector3.zero;

            transform.position = smoothed + drift + shake;

            Vector3 lookAt = focus + new Vector3(lookAhead.x * follow, lookAhead.y, lookAhead.z) + lookDrift;
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);

            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(minFieldOfView, maxFieldOfView, speed01) + fovDrift;
            }
        }
    }
}
