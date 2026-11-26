using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Dino.Net
{
    /// <summary>
    /// Finds games on the same Wi-Fi. The host shouts a tiny "I'm here" packet once a second on the home
    /// network only (UDP broadcast never leaves the router); other phones listen and list what they hear.
    /// If a router blocks this, the 7-letter Wi-Fi code still works.
    /// </summary>
    public class LanBeacon
    {
        public class FoundGame
        {
            public string Address;
            public string HostName;
            public int Players;
            public float LastSeen;
        }

        public readonly List<FoundGame> Games = new List<FoundGame>();

        UdpClient _sender, _listener;
        byte[] _payload;
        float _nextSend;
        string _subnetBroadcast;

        public bool IsBroadcasting => _sender != null;
        public bool IsListening => _listener != null;

        public void StartBroadcast(string hostName, int players, string scene)
        {
            StopBroadcast();
            SetInfo(hostName, players, scene);
            try
            {
                _sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                string ip = GetLocalIPv4();
                if (LanCode.TryParseIp(ip, out byte a, out byte b, out byte c, out _)) _subnetBroadcast = $"{a}.{b}.{c}.255";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Wi-Fi beacon could not start: {e.Message}");
                _sender = null;
            }
        }

        public void SetInfo(string hostName, int players, string scene)
        {
            _payload = Encoding.UTF8.GetBytes(
                $"{NetProtocol.Magic}|{NetProtocol.Version}|{NetProtocol.AppVersion}|{scene}|{hostName}|{players}");
        }

        public void StopBroadcast()
        {
            try { _sender?.Close(); } catch (Exception) { }
            _sender = null;
        }

        public void StartListening()
        {
            if (_listener != null) return;
            try
            {
                _listener = new UdpClient(AddressFamily.InterNetwork);
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, NetProtocol.BeaconPort));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Can't listen for Wi-Fi games: {e.Message}");
                try { _listener?.Close(); } catch (Exception) { }
                _listener = null;
            }
        }

        public void StopListening()
        {
            try { _listener?.Close(); } catch (Exception) { }
            _listener = null;
            Games.Clear();
        }

        public void Stop() { StopBroadcast(); StopListening(); }

        /// <summary>Call every frame. Returns true when the list of nearby games changed.</summary>
        public bool Tick(string myScene)
        {
            float now = Time.unscaledTime;
            if (_sender != null && now >= _nextSend && _payload != null)
            {
                _nextSend = now + 1f;
                Send(IPAddress.Broadcast);
                if (_subnetBroadcast != null && IPAddress.TryParse(_subnetBroadcast, out var sub)) Send(sub);
            }

            bool changed = false;
            if (_listener != null)
            {
                try
                {
                    int guard = 0;
                    while (_listener.Available > 0 && guard++ < 32)
                    {
                        IPEndPoint from = null;
                        byte[] data = _listener.Receive(ref from);
                        if (from != null && Accept(data, from.Address.ToString(), myScene, now)) changed = true;
                    }
                }
                catch (Exception) { /* a bad packet or a sleeping radio: try again next frame */ }

                for (int i = Games.Count - 1; i >= 0; i--)
                    if (now - Games[i].LastSeen > 4f) { Games.RemoveAt(i); changed = true; }
            }
            return changed;
        }

        void Send(IPAddress to)
        {
            try { _sender.Send(_payload, _payload.Length, new IPEndPoint(to, NetProtocol.BeaconPort)); }
            catch (Exception) { /* some phones refuse one of the two broadcast addresses */ }
        }

        bool Accept(byte[] data, string address, string myScene, float now)
        {
            if (data == null || data.Length < 8 || data.Length > 200) return false;
            if (!LanCode.IsHomeNetwork(address)) return false;
            string[] p;
            try { p = Encoding.UTF8.GetString(data).Split('|'); } catch (Exception) { return false; }
            if (p.Length < 6 || p[0] != NetProtocol.Magic) return false;
            if (p[1] != NetProtocol.Version.ToString() || p[3] != myScene) return false;
            int.TryParse(p[5], out int players);
            string name = Sanitize(p[4]);

            foreach (var g in Games)
            {
                if (g.Address != address) continue;
                bool same = g.HostName == name && g.Players == players;
                g.HostName = name; g.Players = players; g.LastSeen = now;
                return !same;
            }
            Games.Add(new FoundGame { Address = address, HostName = name, Players = players, LastSeen = now });
            return true;
        }

        // Names come from the fixed word lists; anything else on the wire is cut down to plain letters.
        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (sb.Length >= 24) break;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == ' ') sb.Append(c);
            }
            return sb.Length == 0 ? "A friend" : sb.ToString();
        }

        /// <summary>This phone's address on the home network, or null when it isn't on Wi-Fi.</summary>
        public static string GetLocalIPv4()
        {
            // Asking the system which address it would use to reach the outside. Nothing is sent.
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("10.255.255.255", 65530);
                    if (s.LocalEndPoint is IPEndPoint ep)
                    {
                        string ip = ep.Address.ToString();
                        if (LanCode.IsHomeNetwork(ip)) return ip;
                    }
                }
            }
            catch (Exception) { }

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (LanCode.IsHomeNetwork(ip)) return ip;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
