using System;
using System.Text;
using UnityEngine;

namespace Dino.Net
{
    /// <summary>Numbers and message kinds shared by every phone. Bump Version when a message changes shape.</summary>
    public static class NetProtocol
    {
        /// <summary>Phones with a different number can't play together (they see "ask a grown-up to update").</summary>
        public const int Version = 1;
        public const ushort GamePort = 7777;
        public const int BeaconPort = 47777;
        /// <summary>Host plus four friends.</summary>
        public const int MaxPlayers = 5;
        public const string MessageName = "thrn";
        public const string Magic = "THRN";

        public const float PoseSendInterval = 1f / 15f;

        // Message kinds (first byte of every message).
        public const byte Hello = 1;        // client -> host: name
        public const byte Welcome = 2;      // host -> client: settings
        public const byte Info = 3;         // who a dino is: species, stage, variant, name
        public const byte Pose = 4;         // where a dino is + its meters
        public const byte Left = 5;         // a player left
        public const byte Emote = 6;        // bite / roar / jump animation
        public const byte FoodBite = 7;     // client -> host: I ate from this plant
        public const byte FoodState = 8;    // host -> clients: food left in plants
        public const byte CarcassSpawn = 9; // host -> clients
        public const byte CarcassGone = 10; // host -> clients
        public const byte Damage = 11;      // a bite on another player's dino
        public const byte Settings = 12;    // host -> clients: rules changed

        public const byte EmoteAttack = 1, EmoteCall = 2, EmoteJump = 3;

        public const byte FlagRun = 1, FlagSneak = 2, FlagSwim = 4, FlagFainted = 8, FlagEating = 16, FlagGrounded = 32;

        public const int CarcassIdBit = 0x40000000;

        /// <summary>What a joining phone tells the host before it is let in.</summary>
        public static byte[] BuildJoinPayload(string appVersion, string scene)
            => Encoding.UTF8.GetBytes($"{Magic}|{Version}|{appVersion}|{scene}");

        /// <summary>Returns null when the joiner may come in, otherwise a short reason code: "version" or "scene".</summary>
        public static string CheckJoinPayload(byte[] payload, string hostAppVersion, string hostScene)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 256) return "version";
            string[] parts;
            try { parts = Encoding.UTF8.GetString(payload).Split('|'); }
            catch (Exception) { return "version"; }
            if (parts.Length < 4 || parts[0] != Magic) return "version";
            if (parts[1] != Version.ToString()) return "version";
            // Builds from the Unity editor report "editor" and may join anything with the same protocol.
            if (parts[2] != hostAppVersion && parts[2] != EditorVersion && hostAppVersion != EditorVersion) return "version";
            if (parts[3] != hostScene) return "scene";
            return null;
        }

        public const string EditorVersion = "editor";
        public static string AppVersion => Application.isEditor ? EditorVersion : Application.version;

        /// <summary>Same number on every phone for the same plant: its position (to 10 cm) and food type.</summary>
        public static int StaticFoodId(Vector3 p, int foodType)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Mathf.RoundToInt(p.x * 10f);
                h = h * 31 + Mathf.RoundToInt(p.y * 10f);
                h = h * 31 + Mathf.RoundToInt(p.z * 10f);
                h = h * 31 + foodType;
                return h & 0x3FFFFFFF;
            }
        }

        public static byte ToByte01(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
        public static float FromByte01(byte b) => b / 255f;
        public static ushort YawToShort(float yawDegrees) => (ushort)Mathf.RoundToInt(Mathf.Repeat(yawDegrees, 360f) / 360f * 65535f);
        public static float ShortToYaw(ushort s) => s / 65535f * 360f;
    }

    /// <summary>
    /// The 7-letter Wi-Fi code is just the host's home-network address written in letters,
    /// so joining at home needs no internet and no account. Only home-network addresses are accepted.
    /// </summary>
    public static class LanCode
    {
        // No I, O, 0 or 1: they look alike.
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int Length = 7;

        public static string Encode(byte a, byte b, byte c, byte d)
        {
            uint v = ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | d;
            var chars = new char[Length];
            for (int i = Length - 1; i >= 0; i--) { chars[i] = Alphabet[(int)(v & 31)]; v >>= 5; }
            return new string(chars);
        }

        public static string Encode(string ipv4)
        {
            return TryParseIp(ipv4, out byte a, out byte b, out byte c, out byte d) ? Encode(a, b, c, d) : null;
        }

        /// <summary>Returns the address like "192.168.1.23", or null if the code is wrong or not a home-network address.</summary>
        public static string Decode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != Length) return null;
            ulong v = 0;
            foreach (char ch in code.ToUpperInvariant())
            {
                int i = Alphabet.IndexOf(ch);
                if (i < 0) return null;
                v = (v << 5) | (uint)i;
            }
            if (v > uint.MaxValue) return null;
            byte a = (byte)(v >> 24), b = (byte)(v >> 16), c = (byte)(v >> 8), d = (byte)v;
            if (!IsHomeNetwork(a, b)) return null;
            return $"{a}.{b}.{c}.{d}";
        }

        public static bool IsHomeNetwork(byte a, byte b)
            => a == 10 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168) || (a == 169 && b == 254);

        public static bool IsHomeNetwork(string ipv4)
            => TryParseIp(ipv4, out byte a, out byte b, out _, out _) && IsHomeNetwork(a, b);

        public static bool TryParseIp(string ipv4, out byte a, out byte b, out byte c, out byte d)
        {
            a = b = c = d = 0;
            if (string.IsNullOrEmpty(ipv4)) return false;
            var p = ipv4.Split('.');
            return p.Length == 4 && byte.TryParse(p[0], out a) && byte.TryParse(p[1], out b)
                && byte.TryParse(p[2], out c) && byte.TryParse(p[3], out d);
        }
    }

    /// <summary>
    /// Player names are two words picked from fixed lists ("Swift Raptor"). Nobody can type a name,
    /// so no real names or messages ever travel between phones.
    /// </summary>
    public static class DinoNames
    {
        static readonly string[] First =
        {
            "Swift", "Brave", "Tiny", "Mighty", "Sneaky", "Happy", "Stompy", "Clever",
            "Sleepy", "Spiky", "Speedy", "Gentle", "Roaring", "Lucky", "Muddy", "Sunny",
        };
        static readonly string[] Second =
        {
            "Raptor", "Rex", "Hatchling", "Stomper", "Chomper", "Runner", "Tail", "Claw",
            "Egg", "Fern", "Horn", "Scale", "Tooth", "Roar", "Leaf", "Boulder",
        };

        public static int FirstCount => First.Length;
        public static int SecondCount => Second.Length;

        public static string Make(int a, int b)
            => $"{First[Mathf.Abs(a) % First.Length]} {Second[Mathf.Abs(b) % Second.Length]}";

        const string KeyA = "net_name_a", KeyB = "net_name_b";

        public static void GetLocal(out byte a, out byte b)
        {
            if (!PlayerPrefs.HasKey(KeyA)) Reroll();
            a = (byte)PlayerPrefs.GetInt(KeyA, 0);
            b = (byte)PlayerPrefs.GetInt(KeyB, 0);
        }

        public static string Local { get { GetLocal(out byte a, out byte b); return Make(a, b); } }

        public static void Reroll()
        {
            PlayerPrefs.SetInt(KeyA, UnityEngine.Random.Range(0, First.Length));
            PlayerPrefs.SetInt(KeyB, UnityEngine.Random.Range(0, Second.Length));
            PlayerPrefs.Save();
        }
    }
}
