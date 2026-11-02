using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Third person chase camera. The world scrolls instead of the car, so the rig only tracks
    /// lateral position and adds speed driven framing (pull back + wider FOV).
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

        Camera cam;
        Vector3 velocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            float speed01 = gm != null ? gm.Speed01 : 0f;

            Vector3 focus = target.position;
            Vector3 desired = new(
                focus.x * lateralFollow,
                offset.y + speed01 * speedLift,
                offset.z - speed01 * speedPullBack);

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            Vector3 shake = CameraShake.Instance != null ? CameraShake.Instance.CurrentOffset : Vector3.zero;

            transform.position = smoothed + shake;

            Vector3 lookAt = focus + new Vector3(lookAhead.x * lateralFollow, lookAhead.y, lookAhead.z);
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);

            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(minFieldOfView, maxFieldOfView, speed01);
            }
        }
    }
}
