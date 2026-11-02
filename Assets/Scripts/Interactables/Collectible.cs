using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Abstract base for the two pickup flavours. Unlike obstacles, a collectible always reacts
    /// to the player (even while invulnerable), plays its own fade-out burst and vanishes.
    /// </summary>
    public abstract class Collectible : Interactable
    {
        [SerializeField] protected Color burstColor = Color.white;
        [SerializeField] protected float spinDegreesPerSecond = 150f;

        float baseY;

        protected override void OnPlayerContact(PlayerCar player)
        {
            Collect(player);
        }

        protected abstract void Collect(PlayerCar player);

        protected override void OnSpawned()
        {
            baseY = transform.position.y;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        protected override void ScrollExtra(float deltaTime)
        {
            transform.Rotate(0f, spinDegreesPerSecond * deltaTime, 0f);

            Vector3 pos = transform.position;
            pos.y = baseY + Mathf.Sin(Time.time * 2.6f + pos.z * 0.35f) * 0.12f;
            transform.position = pos;
        }

        protected void PlayPickupFeedback()
        {
            EffectPlayer.Instance.Play(PoolKey.PickupBurst, transform.position, 1f, burstColor);
        }
    }
}
