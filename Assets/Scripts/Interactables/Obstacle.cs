using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Abstract middle of the hierarchy for the two obstacle flavours (parked/broken cars and
    /// cars that drive along the road). Costing a life is common behaviour, the visual reaction
    /// is left to the subclass.
    /// </summary>
    public abstract class Obstacle : Interactable
    {
        protected override void OnPlayerContact(PlayerCar player)
        {
            if (player == null || player.IsInvulnerable)
            {
                return;
            }

            OnImpact(player);
            player.RegisterCollision(this);
        }

        protected virtual void OnImpact(PlayerCar player)
        {
        }

        protected Vector3 LaneWorldPosition(int lane, float z) =>
            new(settings.LaneX(lane), transform.position.y, z);
    }
}
