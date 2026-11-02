using UnityEngine;

namespace HW2
{
    /// <summary>Spins the four wheel transforms of a car body by surface speed.</summary>
    public sealed class WheelSpinner : MonoBehaviour
    {
        [SerializeField] float wheelRadius = 0.42f;

        Transform[] wheels;

        void Awake()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name.StartsWith("Wheel_"))
                {
                    found.Add(t);
                }
            }

            wheels = found.ToArray();
        }

        public void Spin(float surfaceSpeed, float deltaTime)
        {
            if (wheels == null || Mathf.Abs(surfaceSpeed) < 0.01f || deltaTime <= 0f)
            {
                return;
            }

            float degrees = surfaceSpeed * deltaTime / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg;
            foreach (Transform wheel in wheels)
            {
                if (wheel != null)
                {
                    // The wheel cylinders are oriented so that their own Y axis is the axle.
                    wheel.Rotate(0f, degrees, 0f, Space.Self);
                }
            }
        }
    }
}
