using UnityEngine;

namespace Dino.Player
{
    /// <summary>
    /// Temporary modifiers that abilities (charge, sprint burst, pounce, glide, stun, roar) set on a dino.
    /// DinoMotor reads this if present. Lives in core so the motor compiles without the species code;
    /// the species thread's abilities write to it.
    /// </summary>
    [DisallowMultipleComponent]
    public class DinoStatusEffects : MonoBehaviour
    {
        [Tooltip("Multiplies all move speeds (sprint burst > 1, slowed < 1).")]
        public float SpeedMultiplier = 1f;
        [Tooltip("Multiplies gravity (glide < 1).")]
        public float GravityMultiplier = 1f;
        [Tooltip("Ignore move/jump/attack input while true.")]
        public bool IsStunned;
        [Tooltip("Hidden in tall grass etc. Wild AI should not notice the dino.")]
        public bool IsHidden;

        Vector3 _dashDir; float _dashSpeed, _dashTime;

        /// <summary>Forces movement in a world direction for 'seconds' (head-butt charge, pounce).</summary>
        public void Dash(Vector3 worldDirection, float speed, float seconds)
        {
            worldDirection.y = 0f;
            _dashDir = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : transform.forward;
            _dashSpeed = speed; _dashTime = seconds;
        }

        public bool IsDashing => _dashTime > 0f;
        public Vector3 DashVelocity => IsDashing ? _dashDir * _dashSpeed : Vector3.zero;
        public void CancelDash() => _dashTime = 0f;

        void Update() { if (_dashTime > 0f) _dashTime -= Time.deltaTime; }
    }
}
