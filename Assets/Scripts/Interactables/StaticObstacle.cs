using UnityEngine;

namespace HW2
{
    /// <summary>
    /// A stopped hazard: a broken down car sitting on the lane. It has no motion of its own,
    /// it simply scrolls with the road, which is what makes it a "static" obstacle.
    /// </summary>
    public sealed class StaticObstacle : Obstacle
    {
        [SerializeField] float yawJitterDegrees = 6f;
        [SerializeField] float knockedYawDegrees = 10f;

        public override PoolKey Key => PoolKey.StaticObstacle;

        protected override void OnSpawned()
        {
            transform.rotation = Quaternion.Euler(0f, Random.Range(-yawJitterDegrees, yawJitterDegrees), 0f);
        }

        protected override void OnImpact(PlayerCar player)
        {
            transform.Rotate(0f, Random.Range(-knockedYawDegrees, knockedYawDegrees), 0f);
        }
    }
}
