using System.Collections.Generic;
using Dino.Player;
using Dino.Session;
using Dino.Species;
using Dino.Survival;
using Unity.Netcode;
using UnityEngine;

namespace Dino.Net
{
    // The dinos: telling others about this phone's dino, and showing theirs here.
    public partial class NetSession
    {
        class Peer
        {
            public ulong Id;
            public byte NameA, NameB;
            public string SpeciesId = "";
            public string VariantId = "";
            public int Stage;
            public DinoActor Actor;
            public RemoteDino Remote;
            public string DisplayName => DinoNames.Make(NameA, NameB);
        }

        readonly Dictionary<ulong, Peer> _peers = new Dictionary<ulong, Peer>();

        // What we last told everyone about our own dino.
        string _sentSpecies, _sentVariant;
        int _sentStage = -1;
        float _poseTimer, _nextEmoteTime;
        DinoActor _localCached;
        DinoInteractor _localInteractor;

        bool _wantTeleportToHost;
        float _teleportDeadline;

        const float MaxDamagePerBite = 500f;
        const float MaxBiteDistance = 30f;

        ulong LocalId => _nm != null ? _nm.LocalClientId : 0;

        static DinoActor LocalPlayer
        {
            get
            {
                var session = GameSession.Instance;
                var p = session != null ? session.Player : null;
                return p != null && p.Species != null ? p : null;
            }
        }

        void ResetPlayers()
        {
            ClearPlayers();
            _sentSpecies = null; _sentVariant = null; _sentStage = -1;
            _poseTimer = 0f;
            _wantTeleportToHost = false;
        }

        void ClearPlayers()
        {
            foreach (var p in _peers.Values)
                if (p.Actor != null) Destroy(p.Actor.gameObject);
            _peers.Clear();
        }

        Peer GetOrAddPeer(ulong id)
        {
            if (!_peers.TryGetValue(id, out var p))
            {
                p = new Peer { Id = id };
                _peers[id] = p;
                RaiseChanged();
            }
            return p;
        }

        // ------------------------------------------------------------------ sending our own dino

        void TickPlayers()
        {
            var local = LocalPlayer;
            if (local != _localCached)
            {
                _localCached = local;
                _localInteractor = local != null ? local.GetComponent<DinoInteractor>() : null;
            }

            string species = local != null ? local.Species.id : "";
            string variant = local != null ? (local.VariantId ?? "") : "";
            int stage = local != null ? local.StageIndex : 0;
            if (species != _sentSpecies || variant != _sentVariant || stage != _sentStage)
            {
                _sentSpecies = species; _sentVariant = variant; _sentStage = stage;
                SendLocalInfo(ulong.MaxValue);
            }

            if (local == null) return;

            if (_wantTeleportToHost && Time.unscaledTime > _teleportDeadline) _wantTeleportToHost = false;

            // Bite / roar / jump animations for the others to see. This is the whole "chat".
            if (!local.Stats.IsFainted && Time.unscaledTime >= _nextEmoteTime)
            {
                var input = local.Motor.Input;
                byte emote = 0;
                if (input.Attack) emote = NetProtocol.EmoteAttack;
                else if (input.Call) emote = NetProtocol.EmoteCall;
                else if (input.Jump) emote = NetProtocol.EmoteJump;
                if (emote != 0)
                {
                    _nextEmoteTime = Time.unscaledTime + 0.25f;
                    SendEmote(LocalId, emote, ulong.MaxValue);
                }
            }

            _poseTimer += Time.unscaledDeltaTime;
            if (_poseTimer >= NetProtocol.PoseSendInterval)
            {
                _poseTimer = 0f;
                SendLocalPose(local);
            }
        }

        void SendHello()
        {
            DinoNames.GetLocal(out byte a, out byte b);
            var w = NewWriter(NetProtocol.Hello, 8);
            try
            {
                w.WriteValueSafe(a);
                w.WriteValueSafe(b);
                SendToHost(w, true);
            }
            finally { w.Dispose(); }
            // Make sure our dino is announced right after the hello.
            _sentSpecies = null;
        }

        /// <summary>Tell others who our dino is. onlyTo = one client (host only) or ulong.MaxValue for everyone.</summary>
        void SendLocalInfo(ulong onlyTo)
        {
            DinoNames.GetLocal(out byte a, out byte b);
            var local = LocalPlayer;
            string species = local != null ? local.Species.id : "";
            string variant = local != null ? (local.VariantId ?? "") : "";
            int stage = local != null ? local.StageIndex : 0;
            SendInfo(LocalId, a, b, stage, species, variant, onlyTo, ulong.MaxValue);
        }

        void SendInfo(ulong owner, byte nameA, byte nameB, int stage, string species, string variant, ulong onlyTo, ulong except)
        {
            var w = NewWriter(NetProtocol.Info, 96);
            try
            {
                w.WriteValueSafe(owner);
                w.WriteValueSafe(nameA);
                w.WriteValueSafe(nameB);
                w.WriteValueSafe((byte)Mathf.Clamp(stage, 0, 255));
                w.WriteValueSafe(Short(species));
                w.WriteValueSafe(Short(variant));
                if (!IsHost) SendToHost(w, true);
                else if (onlyTo != ulong.MaxValue) SendTo(onlyTo, w, true);
                else Broadcast(w, true, except);
            }
            finally { w.Dispose(); }
        }

        static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 48 ? s.Substring(0, 48) : s);

        void SendLocalPose(DinoActor local)
        {
            var motor = local.Motor;
            var stats = local.Stats;
            byte flags = 0;
            if (motor.IsRunning) flags |= NetProtocol.FlagRun;
            if (motor.IsSneaking) flags |= NetProtocol.FlagSneak;
            if (motor.IsSwimming) flags |= NetProtocol.FlagSwim;
            if (motor.IsGrounded) flags |= NetProtocol.FlagGrounded;
            if (stats.IsFainted) flags |= NetProtocol.FlagFainted;
            if (_localInteractor != null && _localInteractor.IsEating) flags |= NetProtocol.FlagEating;

            float maxSpeed = local.Species.runSpeed * Mathf.Lerp(0.65f, 1f, local.Scale);
            float speed01 = maxSpeed > 0.01f ? motor.HorizontalSpeed / maxSpeed : 0f;

            SendPose(LocalId, local.transform.position, NetProtocol.YawToShort(local.transform.eulerAngles.y), flags,
                NetProtocol.ToByte01(speed01), NetProtocol.ToByte01(stats.Health.Normalized),
                NetProtocol.ToByte01(stats.Food.Normalized), NetProtocol.ToByte01(stats.Water.Normalized), ulong.MaxValue);
        }

        void SendPose(ulong owner, Vector3 pos, ushort yaw, byte flags, byte speed, byte health, byte food, byte water, ulong except)
        {
            var w = NewWriter(NetProtocol.Pose, 40);
            try
            {
                w.WriteValueSafe(owner);
                w.WriteValueSafe(pos.x);
                w.WriteValueSafe(pos.y);
                w.WriteValueSafe(pos.z);
                w.WriteValueSafe(yaw);
                w.WriteValueSafe(flags);
                w.WriteValueSafe(speed);
                w.WriteValueSafe(health);
                w.WriteValueSafe(food);
                w.WriteValueSafe(water);
                if (IsHost) Broadcast(w, false, except);
                else SendToHost(w, false);
            }
            finally { w.Dispose(); }
        }

        void SendEmote(ulong owner, byte emote, ulong except)
        {
            var w = NewWriter(NetProtocol.Emote, 16);
            try
            {
                w.WriteValueSafe(owner);
                w.WriteValueSafe(emote);
                if (IsHost) Broadcast(w, true, except);
                else SendToHost(w, true);
            }
            finally { w.Dispose(); }
        }

        // ------------------------------------------------------------------ receiving

        void OnHello(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte a);
            r.ReadValueSafe(out byte b);
            var peer = GetOrAddPeer(sender);
            peer.NameA = a; peer.NameB = b;

            // Welcome: the rules, then everyone who is already here, then the state of the world.
            var w = NewWriter(NetProtocol.Welcome, 8);
            try
            {
                w.WriteValueSafe((byte)(AllowPlayerDamage ? 1 : 0));
                SendTo(sender, w, true);
            }
            finally { w.Dispose(); }

            SendLocalInfo(sender);
            foreach (var other in _peers.Values)
            {
                if (other.Id == sender) continue;
                SendInfo(other.Id, other.NameA, other.NameB, other.Stage, other.SpeciesId, other.VariantId, sender, ulong.MaxValue);
            }
            SendWorldTo(sender);

            RaiseToast($"{peer.DisplayName} joined!");
            if (Beacon.IsBroadcasting) Beacon.SetInfo(DinoNames.Local, PlayerCount, SceneName);
            RaiseChanged();
        }

        void OnWelcome(ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte allow);
            AllowPlayerDamage = allow != 0;
            _wantTeleportToHost = true;
            _teleportDeadline = Time.unscaledTime + 20f;
            RaiseChanged();
        }

        void OnInfo(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out ulong owner);
            r.ReadValueSafe(out byte nameA);
            r.ReadValueSafe(out byte nameB);
            r.ReadValueSafe(out byte stage);
            r.ReadValueSafe(out string species);
            r.ReadValueSafe(out string variant);

            if (IsHost) owner = sender; // a client may only describe its own dino
            else if (sender != NetworkManager.ServerClientId) return;
            if (owner == LocalId) return;

            var peer = GetOrAddPeer(owner);
            bool firstDino = string.IsNullOrEmpty(peer.SpeciesId) && !string.IsNullOrEmpty(species);
            peer.NameA = nameA; peer.NameB = nameB;
            peer.SpeciesId = Short(species); peer.VariantId = Short(variant); peer.Stage = stage;
            ApplyPeerIdentity(peer);

            if (IsHost) SendInfo(owner, nameA, nameB, stage, peer.SpeciesId, peer.VariantId, ulong.MaxValue, sender);
            else if (firstDino && owner != NetworkManager.ServerClientId) RaiseToast($"{peer.DisplayName} is here!");
        }

        void ApplyPeerIdentity(Peer peer)
        {
            var session = GameSession.Instance;
            var catalog = session != null && session.catalog != null ? session.catalog : SpeciesCatalog.Load();
            SpeciesDefinition species = string.IsNullOrEmpty(peer.SpeciesId) || catalog == null ? null : catalog.Find(peer.SpeciesId);

            if (species == null)
            {
                // Picking a dino, or a species this phone doesn't know: show nothing.
                if (peer.Actor != null) Destroy(peer.Actor.gameObject);
                peer.Actor = null; peer.Remote = null;
                return;
            }

            string variant = string.IsNullOrEmpty(peer.VariantId) ? null : peer.VariantId;
            if (peer.Actor == null)
            {
                peer.Actor = DinoActor.Spawn(species, peer.Stage, variant, Vector3.zero, Quaternion.identity);
                peer.Actor.name = $"Friend_{peer.DisplayName}";
                peer.Remote = peer.Actor.gameObject.AddComponent<RemoteDino>();
                peer.Remote.Init(peer.Actor, peer.Id, peer.DisplayName);
                ulong id = peer.Id;
                peer.Actor.Stats.RemoteDamageRequested += info => OnReplicaBitten(id, info);
                peer.Actor.gameObject.SetActive(false); // appears with its first position
            }
            else if (peer.Actor.Species != species || (peer.Actor.VariantId ?? "") != (variant ?? ""))
            {
                peer.Actor.Setup(species, peer.Stage, variant);
            }
            else if (peer.Actor.StageIndex != peer.Stage)
            {
                peer.Actor.SetStage(peer.Stage);
            }
            peer.Remote.SetName(peer.DisplayName);
            peer.Remote.RefreshTagHeight();
        }

        void OnPose(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out ulong owner);
            r.ReadValueSafe(out float x);
            r.ReadValueSafe(out float y);
            r.ReadValueSafe(out float z);
            r.ReadValueSafe(out ushort yaw);
            r.ReadValueSafe(out byte flags);
            r.ReadValueSafe(out byte speed);
            r.ReadValueSafe(out byte health);
            r.ReadValueSafe(out byte food);
            r.ReadValueSafe(out byte water);

            if (IsHost) owner = sender;
            else if (sender != NetworkManager.ServerClientId) return;
            if (owner == LocalId) return;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) return;
            var pos = new Vector3(x, y, z);

            if (IsHost) SendPose(owner, pos, yaw, flags, speed, health, food, water, sender);

            if (!_peers.TryGetValue(owner, out var peer) || peer.Actor == null) return;
            if (!peer.Actor.gameObject.activeSelf) peer.Actor.gameObject.SetActive(true);
            peer.Remote.ApplyPose(pos, NetProtocol.ShortToYaw(yaw), flags, NetProtocol.FromByte01(speed),
                NetProtocol.FromByte01(health), NetProtocol.FromByte01(food), NetProtocol.FromByte01(water));

            if (_wantTeleportToHost && owner == NetworkManager.ServerClientId) TeleportNear(pos);
        }

        void OnEmote(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out ulong owner);
            r.ReadValueSafe(out byte emote);
            if (IsHost) owner = sender;
            else if (sender != NetworkManager.ServerClientId) return;
            if (owner == LocalId) return;
            if (IsHost) SendEmote(owner, emote, sender);
            if (_peers.TryGetValue(owner, out var peer) && peer.Remote != null) peer.Remote.PlayEmote(emote);
        }

        void OnLeft(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ulong id);
            RemovePeer(id);
        }

        void HostRemovePeer(ulong id)
        {
            if (!_peers.ContainsKey(id)) return;
            RemovePeer(id);
            var w = NewWriter(NetProtocol.Left, 16);
            try
            {
                w.WriteValueSafe(id);
                Broadcast(w, true, id);
            }
            finally { w.Dispose(); }
            if (Beacon.IsBroadcasting) Beacon.SetInfo(DinoNames.Local, PlayerCount, SceneName);
        }

        void RemovePeer(ulong id)
        {
            if (!_peers.TryGetValue(id, out var peer)) return;
            if (peer.Actor != null) Destroy(peer.Actor.gameObject);
            _peers.Remove(id);
            RaiseToast($"{peer.DisplayName} left.");
            RaiseChanged();
        }

        /// <summary>When you join, you pop up next to the host so nobody has to search a whole island.</summary>
        void TeleportNear(Vector3 hostPos)
        {
            var local = LocalPlayer;
            if (local == null) return;
            _wantTeleportToHost = false;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 p = hostPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4f;
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                p = hit.point;
            else p = hostPos;
            Vector3 look = hostPos - p; look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : local.transform.rotation;
            local.Motor.Teleport(p + Vector3.up * 0.5f, rot);
        }

        // ------------------------------------------------------------------ biting other players

        // Our own dino bit a friend's dino on this phone. Their phone decides what happens.
        void OnReplicaBitten(ulong victim, DamageInfo info)
        {
            if (!IsActive || !AllowPlayerDamage) return;
            var local = LocalPlayer;
            if (local == null || info.attacker != local.gameObject) return;
            float amount = Mathf.Clamp(info.amount, 0f, MaxDamagePerBite);
            if (amount <= 0f) return;

            var w = NewWriter(NetProtocol.Damage, 16);
            try
            {
                if (IsHost)
                {
                    w.WriteValueSafe(LocalId);   // host -> victim: who bit you
                    w.WriteValueSafe(amount);
                    SendTo(victim, w, true);
                }
                else
                {
                    w.WriteValueSafe(victim);    // client -> host: who I bit
                    w.WriteValueSafe(amount);
                    SendToHost(w, true);
                }
            }
            finally { w.Dispose(); }
        }

        void OnDamage(ulong sender, ref FastBufferReader r)
        {
            r.ReadValueSafe(out ulong id);
            r.ReadValueSafe(out float amount);
            if (float.IsNaN(amount) || amount <= 0f) return;
            amount = Mathf.Min(amount, MaxDamagePerBite);

            if (!IsHost)
            {
                if (sender == NetworkManager.ServerClientId) ApplyDamageToLocal(id, amount);
                return;
            }

            // Host referees: the rule must be on, and the biter must actually be next to the target.
            if (!AllowPlayerDamage) return;
            ulong attacker = sender, victim = id;
            if (!_peers.TryGetValue(attacker, out var attackerPeer) || attackerPeer.Actor == null) return;
            Vector3 attackerPos = attackerPeer.Actor.transform.position;

            if (victim == LocalId)
            {
                var local = LocalPlayer;
                if (local == null || (local.transform.position - attackerPos).sqrMagnitude > MaxBiteDistance * MaxBiteDistance) return;
                ApplyDamageToLocal(attacker, amount);
                return;
            }

            if (!_peers.TryGetValue(victim, out var victimPeer) || victimPeer.Actor == null) return;
            if ((victimPeer.Actor.transform.position - attackerPos).sqrMagnitude > MaxBiteDistance * MaxBiteDistance) return;
            var w = NewWriter(NetProtocol.Damage, 16);
            try
            {
                w.WriteValueSafe(attacker);
                w.WriteValueSafe(amount);
                SendTo(victim, w, true);
            }
            finally { w.Dispose(); }
        }

        void ApplyDamageToLocal(ulong attackerId, float amount)
        {
            if (!AllowPlayerDamage) return;
            var local = LocalPlayer;
            if (local == null) return;
            GameObject attacker = _peers.TryGetValue(attackerId, out var p) && p.Actor != null ? p.Actor.gameObject : null;
            local.Stats.TakeDamage(new DamageInfo(amount, attacker, local.transform.position));
        }
    }
}
