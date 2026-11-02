using UnityEngine;

namespace HW2
{
    /// <summary>Thin façade over the pooled VFX prefabs so gameplay code stays readable.</summary>
    public sealed class EffectPlayer : Singleton<EffectPlayer>
    {
        public void Play(PoolKey key, Vector3 position, float scale = 1f, Color? color = null)
        {
            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            GameObject go = pool.Get(key, position, Quaternion.identity, transform);
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent<PoolEffect>(out PoolEffect effect))
            {
                effect.Play(scale, color);
            }
            else
            {
                pool.Release(key, go);
            }
        }
    }
}
