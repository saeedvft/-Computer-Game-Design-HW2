using UnityEngine;

namespace HW2
{
    /// <summary>Fuel can pickup: refills the tank, which is what keeps the run alive.</summary>
    public sealed class FuelCan : Collectible
    {
        public override PoolKey Key => PoolKey.FuelCan;

        protected override void Collect(PlayerCar player)
        {
            GameManager gm = GameManager.Instance;
            gm.Refuel(GameSettings.Instance.fuelPerCan);
            gm.AddScore(PointValue);
            AudioManager.Instance.Play(Sfx.FuelPickup);
            PlayPickupFeedback();
            ReturnToPool();
        }
    }
}
