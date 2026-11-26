using Dino.Player;
using UnityEngine;

namespace Dino.Net
{
    /// <summary>
    /// Sits on another player's dino on this phone. It doesn't simulate anything: it glides toward the
    /// last position the owner sent, mirrors their meters and animations, and shows their name tag.
    /// </summary>
    [DisallowMultipleComponent]
    public class RemoteDino : MonoBehaviour
    {
        public ulong OwnerId;

        DinoActor _actor;
        Vector3 _targetPos;
        float _targetYaw;
        byte _flags;
        float _speed01;
        bool _hasPose;
        TextMesh _tag;
        Transform _tagRoot;
        Camera _cam;

        const float Smoothing = 12f;
        const float SnapDistance = 8f;

        public bool HasPose => _hasPose;
        public Vector3 TargetPosition => _targetPos;

        public void Init(DinoActor actor, ulong ownerId, string displayName)
        {
            _actor = actor;
            OwnerId = ownerId;
            actor.IsLocalPlayer = false;
            actor.Motor.SimulateLocally = false;
            actor.Stats.SimulateLocally = false;
            BuildTag(displayName);
            RefreshTagHeight();
        }

        public void SetName(string displayName) { if (_tag) _tag.text = displayName; }

        /// <summary>Call after the dino grows or changes species so the tag sits above its head.</summary>
        public void RefreshTagHeight()
        {
            if (!_tagRoot || _actor == null || _actor.Species == null) return;
            float h = _actor.Height;
            _tagRoot.localPosition = new Vector3(0f, h + 0.35f + h * 0.15f, 0f);
            _tagRoot.localScale = Vector3.one * Mathf.Clamp(h * 0.12f, 0.08f, 0.6f);
        }

        public void ApplyPose(Vector3 pos, float yaw, byte flags, float speed01, float health01, float food01, float water01)
        {
            _targetPos = pos;
            _targetYaw = yaw;
            _flags = flags;
            _speed01 = speed01;
            if (!_hasPose)
            {
                _hasPose = true;
                transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }

            var s = _actor.Stats;
            float health = (flags & NetProtocol.FlagFainted) != 0 ? 0f : Mathf.Max(health01 * s.Health.Max, 0.01f);
            s.SetValues(health, s.Stamina.Current, food01 * s.Food.Max, water01 * s.Water.Max);
        }

        public void PlayEmote(byte kind)
        {
            var anim = _actor ? _actor.Animator : null;
            if (kind == NetProtocol.EmoteAttack) anim.SafeTrigger("Attack");
            else if (kind == NetProtocol.EmoteCall) anim.SafeTrigger("Call");
            else if (kind == NetProtocol.EmoteJump) anim.SafeTrigger("Jump");
        }

        void Update()
        {
            if (!_hasPose || _actor == null) return;

            Vector3 pos = transform.position;
            if ((pos - _targetPos).sqrMagnitude > SnapDistance * SnapDistance) pos = _targetPos;
            else pos = Vector3.Lerp(pos, _targetPos, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, _targetYaw, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var anim = _actor.Animator;
            if (anim != null)
            {
                anim.SafeFloat("Speed", _speed01);
                anim.SafeBool("Grounded", (_flags & NetProtocol.FlagGrounded) != 0);
                anim.SafeBool("Swimming", (_flags & NetProtocol.FlagSwim) != 0);
                anim.SafeBool("Sneaking", (_flags & NetProtocol.FlagSneak) != 0);
                anim.SafeBool("Fainted", (_flags & NetProtocol.FlagFainted) != 0);
                anim.SafeBool("Eating", (_flags & NetProtocol.FlagEating) != 0);
            }
        }

        void LateUpdate()
        {
            if (!_tagRoot) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            // Face the camera so the name is always readable.
            _tagRoot.rotation = Quaternion.LookRotation(_tagRoot.position - _cam.transform.position, Vector3.up);
        }

        void BuildTag(string displayName)
        {
            var go = new GameObject("NameTag");
            _tagRoot = go.transform;
            _tagRoot.SetParent(transform, false);
            _tag = go.AddComponent<TextMesh>();
            var font = UI.UiFactory.DefaultFont;
            _tag.font = font;
            _tag.text = displayName;
            _tag.fontSize = 64;
            _tag.characterSize = 0.1f;
            _tag.anchor = TextAnchor.LowerCenter;
            _tag.alignment = TextAlignment.Center;
            _tag.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && font != null) mr.sharedMaterial = font.material;
        }
    }
}
