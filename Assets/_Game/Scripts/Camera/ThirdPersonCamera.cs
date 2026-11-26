using Dino.Input;
using UnityEngine;

namespace Dino.CameraRig
{
    /// <summary>
    /// Orbit camera behind the dino. Look input turns it, it pulls in when something is in the way,
    /// and it zooms out for bigger dinos (distance scales with the target's size).
    /// Gently swings behind the dino when the player is moving and not touching the look area.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Target height/size in meters; set by the session from the dino's current size.")]
        public float targetSize = 2f;
        public float distancePerSize = 2.6f;
        public float minDistance = 2.5f;
        public float heightPerSize = 0.9f;
        public float minPitch = -20f, maxPitch = 65f;
        public float autoFollowDelay = 1.5f;
        public float autoFollowSpeed = 90f;
        public float smoothing = 12f;
        public LayerMask collisionMask = ~0;

        float _yaw, _pitch = 15f;
        float _sinceLook = 99f;
        float _currentDistance;

        public float Yaw => _yaw;

        public void SnapBehindTarget()
        {
            if (!target) return;
            _yaw = target.eulerAngles.y;
            _pitch = 15f;
            _currentDistance = DesiredDistance;
            Apply(1f);
        }

        float DesiredDistance => Mathf.Max(minDistance, targetSize * distancePerSize);

        void LateUpdate()
        {
            if (!target) return;
            var input = PlayerInputRouter.Instance ? PlayerInputRouter.Instance.Current : default;

            if (input.Look.sqrMagnitude > 0.0001f)
            {
                _yaw += input.Look.x;
                _pitch = Mathf.Clamp(_pitch - input.Look.y, minPitch, maxPitch);
                _sinceLook = 0f;
            }
            else _sinceLook += Time.deltaTime;

            // Swing behind the dino while it walks and the player isn't steering the camera.
            if (_sinceLook > autoFollowDelay && input.Move.sqrMagnitude > 0.04f)
                _yaw = Mathf.MoveTowardsAngle(_yaw, target.eulerAngles.y, autoFollowSpeed * Time.deltaTime);

            Apply(1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }

        void Apply(float t)
        {
            Vector3 pivot = target.position + Vector3.up * targetSize * heightPerSize;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            float want = DesiredDistance;

            // Pull in when blocked (ignore triggers and the dino itself).
            float radius = 0.25f;
            Vector3 dir = rot * Vector3.back;
            if (Physics.SphereCast(pivot, radius, dir, out var hit, want, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                want = Mathf.Max(0.5f, hit.distance - 0.1f);

            _currentDistance = want < _currentDistance ? want : Mathf.Lerp(_currentDistance, want, t);
            transform.position = pivot + dir * _currentDistance;
            transform.rotation = rot;
        }

        /// <summary>Converts a stick vector into a world direction relative to this camera.</summary>
        public Vector3 CameraRelative(Vector2 move)
        {
            Quaternion yawOnly = Quaternion.Euler(0f, _yaw, 0f);
            return yawOnly * new Vector3(move.x, 0f, move.y);
        }
    }
}
