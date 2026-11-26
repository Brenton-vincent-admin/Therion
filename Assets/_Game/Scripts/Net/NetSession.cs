using System;
using Dino.Session;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dino.Net
{
    public enum NetState { Offline, Starting, Hosting, Joining, Joined }

    /// <summary>
    /// Play together: one phone hosts, up to four friends join with a code. Nothing runs when nobody plays.
    ///
    /// How it works: every phone keeps running its own dino exactly as in single-player and tells the
    /// host where it is about 15 times a second; the host passes that on to everyone else. The host owns
    /// the shared world (how much food is left in each plant, carcasses, the rules). Netcode for
    /// GameObjects carries the messages; there is no server and no account.
    ///
    /// Kid safety: joining is by code or home Wi-Fi only, names come from a fixed word list, and the only
    /// things players can "say" are the roar/bite/jump animations. There is no text or voice anywhere.
    /// </summary>
    public partial class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }

        public NetState State { get; private set; } = NetState.Offline;
        public bool IsHost => State == NetState.Hosting;
        public bool IsActive => State == NetState.Hosting || State == NetState.Joined;
        /// <summary>6-character code for friends in other homes (online hosting only).</summary>
        public string OnlineCode { get; private set; }
        /// <summary>7-letter code for friends on the same Wi-Fi (Wi-Fi hosting only).</summary>
        public string WifiCode { get; private set; }
        /// <summary>A short, kid-readable line about what is going on. Never a raw error.</summary>
        public string Status { get; private set; } = "";
        /// <summary>Dinos in the game, including this phone's.</summary>
        public int PlayerCount => IsActive ? _peers.Count + 1 : 1;
        /// <summary>Host rule: may players bite each other? Off by default.</summary>
        public bool AllowPlayerDamage { get; private set; }

        /// <summary>State, codes, status or player count changed.</summary>
        public event Action Changed;
        /// <summary>A short message worth showing on screen ("Swift Raptor joined!").</summary>
        public event Action<string> Toast;

        public readonly LanBeacon Beacon = new LanBeacon();

        GameObject _netObject;
        NetworkManager _nm;
        UnityTransport _transport;
        int _attempt;          // bumped on every start/stop so stale async results are ignored
        float _joinDeadline;
        float _blockedUntil;   // short pause after stopping, while the old connection shuts down
        bool _handlerRegistered;

        const float JoinTimeoutSeconds = 12f;
        const string PrefDamage = "net_allow_player_damage";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            AllowPlayerDamage = PlayerPrefs.GetInt(PrefDamage, 0) == 1;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Stop(null);
            Beacon.Stop();
            Instance = null;
        }

        static string SceneName => SceneManager.GetActiveScene().name;
        bool CanStart => State == NetState.Offline && Time.unscaledTime >= _blockedUntil;

        // ------------------------------------------------------------------ starting

        /// <summary>Host for people on the same Wi-Fi. No internet needed.</summary>
        public void HostWifi()
        {
            if (!CanStart) return;
            string ip = LanBeacon.GetLocalIPv4();
            if (ip == null) { SetStatus("Connect to your home Wi-Fi first."); return; }

            CreateManager();
            _transport.SetConnectionData("127.0.0.1", NetProtocol.GamePort, "0.0.0.0");
            if (!StartHostNow()) return;
            WifiCode = LanCode.Encode(ip);
            Beacon.StopListening();
            Beacon.StartBroadcast(DinoNames.Local, PlayerCount, SceneName);
            SetStatus("Your game is open on this Wi-Fi!");
        }

        /// <summary>Host for friends in other homes. The menu puts this behind the grown-up hold button.</summary>
        public async void HostOnline()
        {
            if (!CanStart) return;
            int attempt = ++_attempt;
            CreateManager();
            SetState(NetState.Starting, "Getting a friend code...");
            string code;
            try { code = await RelayLink.HostAsync(_transport, NetProtocol.MaxPlayers - 1); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Online hosting failed: {e}");
                if (attempt == _attempt) Stop("Online play isn't ready. Check the internet, or ask a grown-up.");
                return;
            }
            if (attempt != _attempt || this == null) return;
            if (!StartHostNow()) return;
            OnlineCode = code;
            SetStatus("Your game is open! Share the friend code.");
        }

        /// <summary>Join with whatever code the player typed: 7 letters = same Wi-Fi, 6 = friend code.</summary>
        public void Join(string code)
        {
            if (!CanStart) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == LanCode.Length)
            {
                string ip = LanCode.Decode(code);
                if (ip == null) { SetStatus("That code doesn't look right. Try again!"); return; }
                JoinWifi(ip);
            }
            else if (code.Length == 6) JoinOnline(code);
            else SetStatus("Codes have 6 or 7 letters. Try again!");
        }

        /// <summary>Join a game found on the same Wi-Fi.</summary>
        public void JoinWifi(string address)
        {
            if (!CanStart || !LanCode.IsHomeNetwork(address)) return;
            ++_attempt;
            CreateManager();
            _transport.SetConnectionData(address, NetProtocol.GamePort);
            StartClientNow();
        }

        async void JoinOnline(string code)
        {
            int attempt = ++_attempt;
            CreateManager();
            SetState(NetState.Starting, "Looking for the game...");
            try { await RelayLink.JoinAsync(_transport, code); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Online join failed: {e}");
                if (attempt == _attempt) Stop("Couldn't find that game. Check the code and the internet.");
                return;
            }
            if (attempt != _attempt || this == null) return;
            StartClientNow();
        }

        /// <summary>Stop hosting or leave the game. Everyone keeps their own dino and progress.</summary>
        public void Leave() => Stop(IsHost ? "You closed the game." : "You left the game.");

        void CreateManager()
        {
            DestroyManager();
            _netObject = new GameObject("TherionNetwork");
            _transport = _netObject.AddComponent<UnityTransport>();
            _nm = _netObject.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = _transport,
                ConnectionApproval = true,
                EnableSceneManagement = false, // every phone already has the same map loaded
                ConnectionData = NetProtocol.BuildJoinPayload(NetProtocol.AppVersion, SceneName),
            };
            _nm.ConnectionApprovalCallback = Approve;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        void DestroyManager()
        {
            _handlerRegistered = false;
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback -= OnClientConnected;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
                try { if (_nm.IsListening) _nm.Shutdown(); } catch (Exception e) { Debug.LogWarning($"[Net] Shutdown: {e.Message}"); }
            }
            if (_netObject != null) Destroy(_netObject);
            _netObject = null; _nm = null; _transport = null;
        }

        bool StartHostNow()
        {
            bool ok;
            try { ok = _nm.StartHost(); }
            catch (Exception e) { Debug.LogWarning($"[Net] StartHost: {e}"); ok = false; }
            if (!ok) { Stop("Couldn't open the game. Try again in a moment."); return false; }
            RegisterHandler();
            ResetPlayers();
            ResetWorld();
            SetState(NetState.Hosting, Status);
            return true;
        }

        void StartClientNow()
        {
            bool ok;
            try { ok = _nm.StartClient(); }
            catch (Exception e) { Debug.LogWarning($"[Net] StartClient: {e}"); ok = false; }
            if (!ok) { Stop("Couldn't join. Try again in a moment."); return; }
            RegisterHandler();
            ResetPlayers();
            ResetWorld();
            _joinDeadline = Time.unscaledTime + JoinTimeoutSeconds;
            SetState(NetState.Joining, "Joining...");
        }

        void Stop(string status)
        {
            ++_attempt;
            bool wasActive = State != NetState.Offline;
            ClearPlayers();
            ResetWorld();
            Beacon.StopBroadcast();
            DestroyManager();
            OnlineCode = null;
            WifiCode = null;
            if (wasActive) _blockedUntil = Time.unscaledTime + 0.75f;
            State = NetState.Offline;
            if (status != null) Status = status;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ connection events

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }

            string reason = NetProtocol.CheckJoinPayload(request.Payload, NetProtocol.AppVersion, SceneName);
            if (reason == null && _nm != null && _nm.ConnectedClientsIds.Count >= NetProtocol.MaxPlayers) reason = "full";
            response.Approved = reason == null;
            response.Reason = reason;
        }

        void OnClientConnected(ulong clientId)
        {
            if (_nm == null) return;
            if (_nm.IsServer)
            {
                // The joiner introduces itself with a Hello; nothing to do until then.
                return;
            }
            if (clientId != _nm.LocalClientId) return;
            SetState(NetState.Joined, "You're playing with friends!");
            SendHello();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (_nm == null) return;
            if (_nm.IsServer)
            {
                if (clientId != _nm.LocalClientId) HostRemovePeer(clientId);
                return;
            }

            // On a client this means: we were refused, timed out, or the host went away.
            string reason = _nm.DisconnectReason;
            bool wasIn = State == NetState.Joined;
            string msg;
            if (reason == "version") msg = "Ask a grown-up to update the game, then try again.";
            else if (reason == "full") msg = "That game is full.";
            else if (reason == "scene") msg = "That game is in a different place. Try again later.";
            else if (wasIn) msg = "The host closed the game. You can keep playing on your own!";
            else msg = "Couldn't find that game. Check the code!";
            Stop(msg);
            if (wasIn) Toast?.Invoke(msg);
        }

        /// <summary>Goes up by one whenever the list of games found on this Wi-Fi changes.</summary>
        public int NearbyVersion { get; private set; }

        /// <summary>The menu turns this on while it is showing the list of nearby games.</summary>
        public void SetLookingForGames(bool looking)
        {
            if (looking && State == NetState.Offline) Beacon.StartListening();
            else if (Beacon.IsListening) { Beacon.StopListening(); NearbyVersion++; }
        }

        void Update()
        {
            if (Beacon.IsBroadcasting || Beacon.IsListening)
            {
                if (Beacon.Tick(SceneName)) NearbyVersion++;
            }

            // A shared world can't freeze because one player opened the pause menu.
            var session = GameSession.Instance;
            if (session != null && session.PauseStopsTime == IsActive)
            {
                session.PauseStopsTime = !IsActive;
                if (IsActive && Time.timeScale == 0f) Time.timeScale = 1f;
            }
            if (State == NetState.Joining && Time.unscaledTime > _joinDeadline)
            {
                Stop("Couldn't find that game. Check the code!");
                return;
            }
            if (!IsActive || _nm == null || !_nm.IsListening) return;
            TickPlayers();
            TickWorld();
        }

        // ------------------------------------------------------------------ rules

        /// <summary>Host only: allow or forbid players biting each other.</summary>
        public void SetAllowPlayerDamage(bool allow)
        {
            if (State != NetState.Offline && !IsHost) return;
            AllowPlayerDamage = allow;
            PlayerPrefs.SetInt(PrefDamage, allow ? 1 : 0);
            if (IsHost) BroadcastSettings();
            Changed?.Invoke();
        }

        void BroadcastSettings()
        {
            var w = NewWriter(NetProtocol.Settings, 8);
            try
            {
                w.WriteValueSafe((byte)(AllowPlayerDamage ? 1 : 0));
                Broadcast(w, true);
            }
            finally { w.Dispose(); }
        }

        // ------------------------------------------------------------------ messaging

        void RegisterHandler()
        {
            if (_handlerRegistered || _nm == null || _nm.CustomMessagingManager == null) return;
            _nm.CustomMessagingManager.RegisterNamedMessageHandler(NetProtocol.MessageName, OnMessage);
            _handlerRegistered = true;
        }

        static FastBufferWriter NewWriter(byte kind, int size)
        {
            var w = new FastBufferWriter(size, Allocator.Temp, 1100);
            w.WriteValueSafe(kind);
            return w;
        }

        void SendTo(ulong clientId, FastBufferWriter w, bool reliable)
        {
            if (_nm == null || !_nm.IsListening || _nm.CustomMessagingManager == null) return;
            if (clientId == _nm.LocalClientId) return;
            try
            {
                _nm.CustomMessagingManager.SendNamedMessage(NetProtocol.MessageName, clientId, w,
                    reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);
            }
            catch (Exception e) { Debug.LogWarning($"[Net] Send failed: {e.Message}"); }
        }

        void SendToHost(FastBufferWriter w, bool reliable) => SendTo(NetworkManager.ServerClientId, w, reliable);

        /// <summary>Host: send to every client, optionally skipping one (the original sender).</summary>
        void Broadcast(FastBufferWriter w, bool reliable, ulong except = ulong.MaxValue)
        {
            if (_nm == null || !_nm.IsServer) return;
            var ids = _nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                ulong id = ids[i];
                if (id == _nm.LocalClientId || id == except) continue;
                SendTo(id, w, reliable);
            }
        }

        void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (_nm == null || (!IsActive && State != NetState.Joining)) return;
            bool fromHost = sender == NetworkManager.ServerClientId;
            try
            {
                reader.ReadValueSafe(out byte kind);
                switch (kind)
                {
                    case NetProtocol.Hello: if (IsHost) OnHello(sender, ref reader); break;
                    case NetProtocol.Welcome: if (!IsHost && fromHost) OnWelcome(ref reader); break;
                    case NetProtocol.Info: OnInfo(sender, ref reader); break;
                    case NetProtocol.Pose: OnPose(sender, ref reader); break;
                    case NetProtocol.Left: if (!IsHost && fromHost) OnLeft(ref reader); break;
                    case NetProtocol.Emote: OnEmote(sender, ref reader); break;
                    case NetProtocol.FoodBite: if (IsHost) OnFoodBite(ref reader); break;
                    case NetProtocol.FoodState: if (!IsHost && fromHost) OnFoodState(ref reader); break;
                    case NetProtocol.CarcassSpawn: if (!IsHost && fromHost) OnCarcassSpawn(ref reader); break;
                    case NetProtocol.CarcassGone: if (!IsHost && fromHost) OnCarcassGone(ref reader); break;
                    case NetProtocol.Damage: OnDamage(sender, ref reader); break;
                    case NetProtocol.Settings:
                        if (!IsHost && fromHost)
                        {
                            reader.ReadValueSafe(out byte allow);
                            AllowPlayerDamage = allow != 0;
                            Changed?.Invoke();
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                // A malformed message must never break the game; drop it.
                Debug.LogWarning($"[Net] Bad message from {sender}: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ small helpers

        void SetState(NetState state, string status)
        {
            State = state;
            Status = status ?? "";
            Changed?.Invoke();
        }

        void SetStatus(string status)
        {
            Status = status ?? "";
            Changed?.Invoke();
        }

        void RaiseChanged() => Changed?.Invoke();
        void RaiseToast(string text) => Toast?.Invoke(text);
    }
}
