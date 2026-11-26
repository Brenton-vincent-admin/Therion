using Dino.Net;
using Dino.World;
using NUnit.Framework;
using UnityEngine;

namespace Dino.Tests
{
    public class NetTests
    {
        [Test]
        public void WifiCode_RoundTripsHomeAddresses()
        {
            foreach (var ip in new[] { "192.168.1.23", "192.168.0.1", "10.0.0.7", "172.16.5.200", "192.168.255.255" })
            {
                string code = LanCode.Encode(ip);
                Assert.AreEqual(LanCode.Length, code.Length, ip);
                Assert.AreEqual(ip, LanCode.Decode(code), ip);
                Assert.AreEqual(ip, LanCode.Decode(code.ToLowerInvariant()), "codes are not case sensitive");
            }
        }

        [Test]
        public void WifiCode_RefusesAddressesOutsideTheHome()
        {
            Assert.IsNull(LanCode.Decode(LanCode.Encode(8, 8, 8, 8)), "public internet address");
            Assert.IsNull(LanCode.Decode(LanCode.Encode(172, 32, 0, 1)));
            Assert.IsNull(LanCode.Decode("ABC"));
            Assert.IsNull(LanCode.Decode("ABCDEF1"), "1 is not in the alphabet");
            Assert.IsNull(LanCode.Decode(null));
            Assert.IsNull(LanCode.Encode("not an address"));
        }

        [Test]
        public void JoinPayload_ChecksVersionAndScene()
        {
            byte[] ok = NetProtocol.BuildJoinPayload("0.1.7", "Island");
            Assert.IsNull(NetProtocol.CheckJoinPayload(ok, "0.1.7", "Island"));
            Assert.AreEqual("version", NetProtocol.CheckJoinPayload(ok, "0.1.8", "Island"));
            Assert.AreEqual("scene", NetProtocol.CheckJoinPayload(ok, "0.1.7", "Sandbox"));
            Assert.AreEqual("version", NetProtocol.CheckJoinPayload(null, "0.1.7", "Island"));
            Assert.AreEqual("version", NetProtocol.CheckJoinPayload(new byte[] { 1, 2, 3 }, "0.1.7", "Island"));
            // Playing from the Unity editor against a phone build is allowed.
            Assert.IsNull(NetProtocol.CheckJoinPayload(ok, NetProtocol.EditorVersion, "Island"));
            Assert.IsNull(NetProtocol.CheckJoinPayload(NetProtocol.BuildJoinPayload(NetProtocol.EditorVersion, "Island"), "0.1.7", "Island"));
        }

        [Test]
        public void FoodId_IsStableAndSeparatesNeighbours()
        {
            var p = new Vector3(12.34f, 0.5f, -80.1f);
            Assert.AreEqual(NetProtocol.StaticFoodId(p, 1), NetProtocol.StaticFoodId(p, 1));
            Assert.AreNotEqual(NetProtocol.StaticFoodId(p, 1), NetProtocol.StaticFoodId(p, 2));
            Assert.AreNotEqual(NetProtocol.StaticFoodId(p, 1), NetProtocol.StaticFoodId(p + Vector3.right, 1));
            Assert.AreEqual(0, NetProtocol.StaticFoodId(p, 1) & NetProtocol.CarcassIdBit, "plant ids never look like carcass ids");
        }

        [Test]
        public void PoseBytes_RoundTrip()
        {
            Assert.AreEqual(0.5f, NetProtocol.FromByte01(NetProtocol.ToByte01(0.5f)), 0.01f);
            Assert.AreEqual(1f, NetProtocol.FromByte01(NetProtocol.ToByte01(3f)), 0.001f);
            Assert.AreEqual(270f, NetProtocol.ShortToYaw(NetProtocol.YawToShort(-90f)), 0.05f);
        }

        [Test]
        public void Names_ComeFromTheWordLists()
        {
            Assert.AreEqual(DinoNames.Make(0, 0), DinoNames.Make(DinoNames.FirstCount, DinoNames.SecondCount));
            StringAssert.Contains(" ", DinoNames.Make(200, 255));
        }

        [Test]
        public void FoodSource_TakesTheHostsAmount()
        {
            var go = new GameObject("bush");
            try
            {
                var f = go.AddComponent<FoodSource>();
                f.capacity = 60f;
                f.NetSetRemaining(25f);
                Assert.AreEqual(25f, f.Remaining, 0.001f);
                f.NetSetRemaining(999f);
                Assert.AreEqual(60f, f.Remaining, 0.001f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
