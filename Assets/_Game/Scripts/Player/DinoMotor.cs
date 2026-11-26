using Dino.CameraRig;
using Dino.Input;
using Dino.Survival;
using Dino.World;
using UnityEngine;

namespace Dino.Player
{
    /// <summary>
    /// Generic movement for every species, driven by a DinoInputState (player, AI or network).
    /// Walk / run (costs stamina) / sneak / swim / jump, turning toward the move direction.
    /// Every dino can swim (no drowning); semi-aquatic species swim fast.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DinoMotor : MonoBehaviour
    {
        /// <summary>Set every frame by whoever controls this dino.</summary>
        public DinoInputState Input;
        /// <summary>Camera for camera-relative input (player only).</summary>
        public ThirdPersonCamera CameraRig;
        /// <summary>False on network replicas whose position comes from the host.</summary>
        public bool SimulateLocally = true;

        public float gravity = -20f;

        public Vector3 Velocity { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSwimming { get; private set; }
        public bool IsSneaking { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>Current distance at which wild dinos notice this one (stealth).</summary>
        public float NoticeRadius
        {
            get
            {
                var a = Actor;
                if (a == null || a.Species == null) return 10f;
                if (a.Effects && a.Effects.IsHidden) return 0f;
                float r = a.Species.noticeRadius * Mathf.Lerp(0.5f, 1f, a.Scale) * (a.Variant?.noticeRadiusMultiplier ?? 1f);
                if (IsSneaking) r *= a.Species.sneakNoticeMultiplier;
                if (IsRunning) r *= 1.5f;
                return r;
            }
        }

        CharacterController _cc;
        DinoActor _actor;
        SurvivalStats _stats;
        float _verticalSpeed;

        DinoActor Actor => _actor ? _actor : (_actor = GetComponent<DinoActor>());

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<SurvivalStats>();
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = was;
            _verticalSpeed = 0f;
            Velocity = Vector3.zero;
        }

        void Update()
        {
            if (!SimulateLocally || Actor == null || Actor.Species == null) return;
            float dt = Time.deltaTime;
            var sp = Actor.Species;
            var fx = Actor.Effects;
            var input = Input;
            bool fainted = _stats && _stats.IsFainted;
            bool stunned = fx && fx.IsStunned;
            if (fainted || stunned) input = default;

            // ----- desired direction -----
            Vector3 dir;
            if (input.MoveIsWorldSpace || CameraRig == null) dir = new Vector3(input.Move.x, 0f, input.Move.y);
            else dir = CameraRig.CameraRelative(input.Move);
            float amount = Mathf.Clamp01(dir.magnitude);
            if (amount > 0.001f) dir /= Mathf.Max(amount, 1f);

            // ----- water -----
            float h = Actor.Height;
            Vector3 chest = transform.position + Vector3.up * h * 0.6f;
            IsSwimming = WaterVolume.TryGetWaterAt(chest, out var water);

            // ----- speed -----
            float sizeFactor = Mathf.Lerp(0.65f, 1f, Actor.Scale);
            float variantSpeed = Actor.Variant?.speedMultiplier ?? 1f;
            float effectSpeed = fx ? fx.SpeedMultiplier : 1f;
            bool wantsRun = input.Run && amount > 0.1f && !IsSwimming;
            IsRunning = wantsRun && _stats != null && (_stats.IsRunning ? !_stats.Stamina.IsEmpty : _stats.CanRun);
            IsSneaking = input.Sneak && !IsRunning && !IsSwimming;
            if (_stats) _stats.IsRunning = IsRunning;

            float speed;
            if (IsSwimming)
            {
                bool aquatic = sp.moveType == Species.MoveType.SemiAquatic;
                speed = sp.swimSpeed * (Actor.Variant?.swimMultiplier ?? 1f) * (aquatic ? 1f : 0.8f);
            }
            else if (IsRunning) speed = sp.runSpeed * variantSpeed;
            else if (IsSneaking) speed = sp.sneakSpeed * variantSpeed;
            else speed = sp.walkSpeed * variantSpeed;
            speed *= sizeFactor * effectSpeed;

            Vector3 horizontal = dir * speed * amount;
            if (fx && fx.IsDashing) horizontal = fx.DashVelocity;

            // ----- turning -----
            Vector3 face = fx && fx.IsDashing ? fx.DashVelocity : horizontal;
            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
                float turn = sp.turnSpeed * (IsRunning ? 0.8f : 1f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turn * dt);
                // Big dinos can't strafe: move mostly where they face.
                float align = Mathf.Clamp01(Vector3.Dot(transform.forward, face.normalized));
                horizontal *= Mathf.Lerp(0.35f, 1f, align);
            }

            // ----- vertical -----
            if (IsSwimming)
            {
                float targetY = water.SurfaceY - h * 0.55f; // float with the head above water
                _verticalSpeed = Mathf.Clamp((targetY - transform.position.y) * 3f, -3f, 3f);
                if (input.Jump) _verticalSpeed = 4f; // hop out onto a bank
            }
            else
            {
                if (IsGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
                if (input.Jump && IsGrounded && (_stats == null || _stats.TryUseStamina(10f)))
                {
                    float jh = sp.jumpHeight * Mathf.Lerp(0.6f, 1f, Actor.Scale);
                    _verticalSpeed = Mathf.Sqrt(2f * jh * -gravity);
                    Actor.Animator.SafeTrigger("Jump");
                }
                float g = gravity * (fx ? fx.GravityMultiplier : 1f);
                _verticalSpeed += g * dt;
            }

            Vector3 motion = horizontal + Vector3.up * _verticalSpeed;
            if (_cc.enabled) _cc.Move(motion * dt);
            IsGrounded = _cc.isGrounded;
            Velocity = motion;
            HorizontalSpeed = new Vector3(motion.x, 0, motion.z).magnitude;

            // Safety net: fell through the world.
            if (transform.position.y < -200f) Teleport(transform.position + Vector3.up * 400f, transform.rotation);

            UpdateAnimator(sp.runSpeed * sizeFactor, fainted);
        }

        void UpdateAnimator(float maxSpeed, bool fainted)
        {
            var anim = Actor.Animator;
            if (anim == null) return;
            anim.SafeFloat("Speed", maxSpeed > 0 ? HorizontalSpeed / maxSpeed : 0f);
            anim.SafeBool("Grounded", IsGrounded);
            anim.SafeBool("Swimming", IsSwimming);
            anim.SafeBool("Sneaking", IsSneaking);
            anim.SafeBool("Fainted", fainted);
        }
    }

    /// <summary>Animator helpers that silently skip parameters a model's controller doesn't have.</summary>
    public static class AnimatorExtensions
    {
        static bool Has(Animator a, string name, AnimatorControllerParameterType type)
        {
            if (a == null || a.runtimeAnimatorController == null) return false;
            foreach (var p in a.parameters) if (p.name == name && p.type == type) return true;
            return false;
        }
        public static void SafeFloat(this Animator a, string n, float v) { if (Has(a, n, AnimatorControllerParameterType.Float)) a.SetFloat(n, v); }
        public static void SafeBool(this Animator a, string n, bool v) { if (Has(a, n, AnimatorControllerParameterType.Bool)) a.SetBool(n, v); }
        public static void SafeTrigger(this Animator a, string n) { if (Has(a, n, AnimatorControllerParameterType.Trigger)) a.SetTrigger(n); }
    }
}
