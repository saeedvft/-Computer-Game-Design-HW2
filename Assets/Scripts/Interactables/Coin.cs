using UnityEngine;

namespace HW2
{
    /// <summary>Score pickup. Part of a line of coins laid out in a free lane.</summary>
    public sealed class Coin : Collectible
    {
        public override PoolKey Key => PoolKey.Coin;

        protected override void Collect(PlayerCar player)
        {
            GameManager.Instance.AddScore(PointValue);
            AudioManager.Instance.Play(Sfx.Coin, 0.85f, Random.Range(0.96f, 1.06f));
            PlayPickupFeedback();
            ReturnToPool();
        }
    }
}
