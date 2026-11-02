using System.Collections.Generic;
using UnityEngine;

namespace HW2
{
    /// <summary>
    /// The player car: lane steering through the Input System, fuel/life reaction, collision
    /// feedback, speed proportional neon underglow, headlights and exhaust particles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerCar : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Hierarchy naming conventions")]
        [SerializeField] string chassisName = "Chassis";
        [SerializeField] string neonPrefix = "Neon";
        [SerializeField] string paintPrefix = "Paint";
        [SerializeField] string headlightPrefix = "Headlight";

        [Header("Feel")]
        [SerializeField] Color damageColor = new Color(1f, 0.28f, 0.24f, 1f);
        [SerializeField] float steerLeanDegrees = 9f;
        [SerializeField] float steerYawDegrees = 6f;
        [SerializeField] float collisionSpeedDip = 6f;

        GameSettings settings;
        Transform chassis;
        readonly List<Light> neonLights = new();
        readonly List<Light> headlights = new();
        readonly List<Renderer> glowRenderers = new();
        readonly List<Renderer> paintRenderers = new();
        readonly List<Color> paintColors = new();
        // Created in Awake: Unity forbids native object creation in a field initializer.
        MaterialPropertyBlock mpb;

        ParticleSystem exhaust;
        WheelSpinner wheels;

        int lane = 1;
        int fromLane = 1;
        int laneDirection;
        float laneProgress = 1f;
        float repeatTimer;
        float invulnerable;
        Vector3 homePosition;
        float baseY;

        public int Lane => lane;
        public bool IsInvulnerable => invulnerable > 0f;
        public Vector3 ContactPoint => transform.position + Vector3.up * 0.65f;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            settings = GameSettings.Instance;
            lane = settings.laneCount / 2;
            fromLane = lane;
            homePosition = transform.position;
            baseY = homePosition.y;

            Rigidbody body = GetComponent<Rigidbody>();

            // Triggers need at least one non-kinematic Rigidbody in the pair, and the movers here
            // are all transform-driven kinematic bodies. So the player keeps a dynamic body whose
            // motion is fully frozen: physics reports the contacts, steering keeps the authority.
            body.isKinematic = false;
            body.useGravity = false;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.detectCollisions = true;

            CacheParts();
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RunStarted += ResetForRun;
            }
        }

        void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RunStarted -= ResetForRun;
            }
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            float dt = gm.IsPlaying ? Time.deltaTime : 0f;

            if (gm.IsPlaying)
            {
                ReadSteering(dt);
            }

            float steerNow = InputManager.Instance != null ? InputManager.Instance.Steer : 0f;

            MoveAlongLane(dt, gm, steerNow);
            UpdateDamageFlash(dt);
            UpdateNeon(gm.Speed01);
            UpdateExhaust(gm);
        }

        void CacheParts()
        {
            chassis = transform.Find(chassisName);

            foreach (Light light in GetComponentsInChildren<Light>(true))
            {
                if (light.name.StartsWith(neonPrefix))
                {
                    neonLights.Add(light);
                }
                else if (light.name.StartsWith(headlightPrefix))
                {
                    headlights.Add(light);
                }
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith(neonPrefix))
                {
                    glowRenderers.Add(renderer);
                }
                else if (renderer.name.StartsWith(paintPrefix))
                {
                    paintRenderers.Add(renderer);

                    // The damage flash overwrites _BaseColor, so remember what was authored.
                    Material shared = renderer.sharedMaterial;
                    paintColors.Add(shared != null ? shared.color : Color.gray);
                }
            }

            exhaust = GetComponentInChildren<ParticleSystem>(true);
            wheels = GetComponentInChildren<WheelSpinner>();

            foreach (Light light in headlights)
            {
                light.color = settings.headlightColor;
            }
        }

        void ResetForRun()
        {
            lane = settings.laneCount / 2;
            fromLane = lane;
            laneDirection = 0;
            laneProgress = 1f;
            repeatTimer = 0f;
            invulnerable = 0f;

            transform.position = homePosition;
            if (chassis != null)
            {
                chassis.localRotation = Quaternion.identity;
                chassis.localPosition = Vector3.zero;
            }

            exhaust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void ReadSteering(float dt)
        {
            float steer = InputManager.Instance != null ? InputManager.Instance.Steer : 0f;
            repeatTimer -= dt;

            if (Mathf.Abs(steer) < 0.5f || repeatTimer > 0f)
            {
                return;
            }

            int direction = steer > 0f ? 1 : -1;
            int target = Mathf.Clamp(lane + direction, 0, settings.laneCount - 1);

            if (target != lane)
            {
                fromLane = lane;
                lane = target;
                laneDirection = direction;
                laneProgress = 0f;
                repeatTimer = settings.laneRepeatSeconds;
                AudioManager.Instance.Play(Sfx.LaneChange, 0.35f);
            }
            else
            {
                repeatTimer = 0.3f;
            }
        }

        void MoveAlongLane(float dt, GameManager gm, float steerNow)
        {
            float speed01 = gm.Speed01;
            laneProgress = Mathf.Clamp01(laneProgress + dt / Mathf.Max(0.01f, settings.laneChangeSeconds));
            float blend = Mathf.SmoothStep(0f, 1f, laneProgress);

            float x = Mathf.Lerp(settings.LaneX(fromLane), settings.LaneX(lane), blend);
            float bob = Mathf.Sin(Time.time * 21f) * 0.014f * (0.35f + speed01);
            transform.position = new Vector3(x, baseY + bob, homePosition.z);

            // Bank into the lane change plus a small amount of raw steering for gamepad/mouse.
            float bank = Mathf.Sin(blend * Mathf.PI) * laneDirection;
            float lean = Mathf.Clamp(bank + steerNow * 0.35f, -1.4f, 1.4f);

            wheels?.Spin(gm.Speed, dt);

            if (chassis == null)
            {
                return;
            }

            chassis.localRotation = Quaternion.Euler(0f, -lean * steerYawDegrees, -lean * steerLeanDegrees);
            chassis.localPosition = new Vector3(0f, -0.03f * speed01, 0f);
        }

        void UpdateDamageFlash(float dt)
        {
            if (invulnerable > 0f)
            {
                invulnerable -= Time.unscaledDeltaTime;
            }

            float pulse = invulnerable > 0f ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 17f)) : 0f;

            for (int i = 0; i < paintRenderers.Count; i++)
            {
                Renderer renderer = paintRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, Color.Lerp(paintColors[i], damageColor, pulse));
                renderer.SetPropertyBlock(mpb);
            }
        }

        void UpdateNeon(float speed01)
        {
            Color neon = settings.neonColor;

            foreach (Light light in neonLights)
            {
                if (light == null)
                {
                    continue;
                }

                light.color = neon;
                light.intensity = Mathf.Lerp(settings.neonMinIntensity, settings.neonMaxIntensity, speed01);
            }

            float glow = Mathf.Lerp(settings.neonMinGlow, settings.neonMaxGlow, speed01);
            Color emission = neon * glow;

            foreach (Renderer renderer in glowRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(mpb);
            }
        }

        void UpdateExhaust(GameManager gm)
        {
            if (exhaust == null)
            {
                return;
            }

            if (!gm.IsPlaying)
            {
                if (exhaust.isPlaying)
                {
                    exhaust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                return;
            }

            if (!exhaust.isPlaying)
            {
                exhaust.Play();
            }

            var emission = exhaust.emission;
            emission.rateOverTime = Mathf.Lerp(6f, 46f, gm.Speed01);
        }

        /// <summary>Called by an <see cref="Obstacle"/> when the player runs into it.</summary>
        public void RegisterCollision(Obstacle obstacle)
        {
            if (IsInvulnerable)
            {
                return;
            }

            invulnerable = settings.invulnerableSeconds;

            Vector3 contact = obstacle != null
                ? Vector3.Lerp(transform.position, obstacle.transform.position, 0.5f)
                : ContactPoint;

            EffectPlayer.Instance.Play(PoolKey.ImpactSparks, contact + Vector3.up * 0.5f, 1.25f);
            CameraShake.Instance.Shake(0.85f, 0.42f);
            AudioManager.Instance.Play(Sfx.Crash);

            GameManager.Instance.DipSpeed(collisionSpeedDip);
            GameManager.Instance.RegisterHit(obstacle);
        }
    }
}
