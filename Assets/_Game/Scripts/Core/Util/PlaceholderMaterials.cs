using System.Collections.Generic;
using UnityEngine;

namespace Dino.Util
{
    /// <summary>Colored materials for placeholder shapes. Uses URP Lit when available, else Standard.</summary>
    public static class PlaceholderMaterials
    {
        static readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();

        public static Material Get(Color c)
        {
            if (_cache.TryGetValue(c, out var m) && m) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            _cache[c] = m;
            return m;
        }
    }
}
