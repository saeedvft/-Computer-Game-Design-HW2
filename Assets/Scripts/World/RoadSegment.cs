using UnityEngine;

namespace HW2
{
    /// <summary>
    /// One recyclable stretch of road. Its geometry starts at the local origin and runs for
    /// <see cref="Length"/> metres along +Z, so the transform position is the near edge.
    /// </summary>
    public sealed class RoadSegment : MonoBehaviour
    {
        public float Length { get; private set; }
        public bool IsArmed { get; private set; }

        public float NearEdgeZ => transform.position.z;
        public float FarEdgeZ => transform.position.z + Length;

        public void Arm(float nearEdgeZ, float length)
        {
            Length = length;
            IsArmed = true;
            transform.position = new Vector3(0f, 0f, nearEdgeZ);
        }

        public void Disarm()
        {
            IsArmed = false;
        }

        public Vector3 LanePosition(int lane, float alongSegment) =>
            new(GameSettings.Instance.LaneX(lane), 0f, NearEdgeZ + alongSegment);
    }
}
