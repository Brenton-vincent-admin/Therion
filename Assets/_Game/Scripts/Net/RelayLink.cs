using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Dino.Net
{
    /// <summary>
    /// Playing from different homes. Unity Relay passes the traffic along so nobody opens router ports,
    /// and hands the host a 6-character join code. Sign-in is anonymous: no email, no name, no account.
    /// Needs the project linked to Unity Cloud (free): see docs/MULTIPLAYER.md.
    /// </summary>
    public static class RelayLink
    {
        static async Task SignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Reserves a relay spot for the host and returns the join code to share.</summary>
        public static async Task<string> HostAsync(UnityTransport transport, int friends)
        {
            await SignInAsync();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(friends);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            PickEndpoint(allocation.ServerEndpoints, out string host, out int port, out bool secure);
            transport.SetRelayServerData(host, (ushort)port, allocation.AllocationIdBytes, allocation.Key,
                allocation.ConnectionData, null, secure);
            return code;
        }

        /// <summary>Points the transport at the host's relay spot. Throws if the code is wrong or expired.</summary>
        public static async Task JoinAsync(UnityTransport transport, string joinCode)
        {
            await SignInAsync();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            PickEndpoint(allocation.ServerEndpoints, out string host, out int port, out bool secure);
            transport.SetRelayServerData(host, (ushort)port, allocation.AllocationIdBytes, allocation.Key,
                allocation.ConnectionData, allocation.HostConnectionData, secure);
        }

        // Prefer encrypted UDP (dtls), fall back to plain UDP.
        static void PickEndpoint(List<RelayServerEndpoint> endpoints, out string host, out int port, out bool secure)
        {
            RelayServerEndpoint best = null;
            if (endpoints != null)
            {
                foreach (var e in endpoints)
                {
                    if (e == null) continue;
                    if (e.ConnectionType == "dtls") { best = e; break; }
                    if (e.ConnectionType == "udp" && best == null) best = e;
                }
            }
            if (best == null) throw new InvalidOperationException("Relay gave no usable server address.");
            host = best.Host;
            port = best.Port;
            secure = best.ConnectionType == "dtls";
        }
    }
}
