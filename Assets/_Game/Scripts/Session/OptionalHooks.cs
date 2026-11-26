using System;
using Dino.Player;
using UnityEngine;

namespace Dino.Session
{
    /// <summary>
    /// Attaches components from the species and map work (DinoStealth, BiomeTracker) when they exist,
    /// without a compile-time dependency, so the core builds no matter which PR merges first.
    /// Once those are on main this can be replaced with direct AddComponent calls.
    /// </summary>
    static class OptionalHooks
    {
        public static void AttachToPlayer(DinoActor player)
        {
            Add(player.gameObject, "Dino.World.BiomeTracker");
            var stealth = Add(player.gameObject, "Dino.Species.DinoStealth");
            if (stealth != null) SetField(stealth, "isPlayer", true);
            Add(player.gameObject, "Dino.Species.SignatureAbilityRunner");
        }

        static Component Add(GameObject go, string typeName)
        {
            var t = FindType(typeName);
            if (t == null || !typeof(Component).IsAssignableFrom(t)) return null;
            var existing = go.GetComponent(t);
            return existing != null ? existing : go.AddComponent(t);
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static void SetField(object target, string name, object value)
        {
            var t = target.GetType();
            var f = t.GetField(name);
            if (f != null && f.FieldType.IsInstanceOfType(value)) { f.SetValue(target, value); return; }
            var p = t.GetProperty(name);
            if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(value)) p.SetValue(target, value);
        }
    }
}
