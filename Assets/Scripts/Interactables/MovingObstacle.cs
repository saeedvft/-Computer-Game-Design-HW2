using UnityEngine;

namespace HW2
{
    /// <summary>
    /// A car that drives along the road in the same direction as the player. It keeps a share of
    /// the road speed, so the player closes in on it instead of hitting a wall.
    /// </summary>
    public sealed class MovingObstacle : Obstacle
    {
        [SerializeField] float cruiseFactorOverride = -1f;
        [SerializeField] float weaveWidth = 0.35f;
        [SerializeField] float weaveFrequency = 0.55f;

        WheelSpinner wheels;
        float laneCenterX;
        float weavePhase;
        float elapsed;
        float cruiseFactor;

        public override PoolKey Key => PoolKey.MovingObstacle;

        protected override void Awake()
        {
            base.Awake();
            wheels = GetComponentInChildren<WheelSpinner>();
        }

        protected override void OnSpawned()
        {
            cruiseFactor = cruiseFactorOverride > 0f
                ? cruiseFactorOverride
                : settings.movingCarCruiseFactor;

            laneCenterX = settings.LaneX(Lane);
            weavePhase = Random.value * Mathf.PI * 2f;
            elapsed = 0f;
            transform.rotation = Quaternion.identity;
        }

        protected override void ScrollExtra(float deltaTime)
        {
            GameManager gm = GameManager.Instance;
            float cruiseSpeed = gm.Speed * cruiseFactor;

            // Drive forward along the lane, which reduces the closing speed with the player.
            transform.position += Vector3.forward * cruiseSpeed * deltaTime;
            wheels?.Spin(cruiseSpeed, deltaTime);

            elapsed += deltaTime;
            float offset = Mathf.Sin(elapsed * weaveFrequency * Mathf.PI * 2f + weavePhase) * weaveWidth;
            Vector3 pos = transform.position;
            float dx = laneCenterX + offset - pos.x;
            pos.x += dx * Mathf.Clamp01(deltaTime * 6f);
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, -dx * 4f, 0f);
        }
    }
}
