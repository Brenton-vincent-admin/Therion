using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Scrolls the normal map of the shared water materials so lakes ripple and rivers look like they flow.
    /// One component animates every water surface (they share materials), so it costs nothing per object.
    /// </summary>
    public class WaterSurfaceAnimator : MonoBehaviour
    {
        public Material[] materials;
        public Vector2 speed = new Vector2(0.012f, 0.008f);

        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        void Update()
        {
            if (materials == null) return;
            Vector2 offset = speed * Time.time;
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                if (!m) continue;
                if (m.HasProperty(BumpMap)) m.SetTextureOffset(BumpMap, offset);
                if (m.HasProperty(BaseMap)) m.SetTextureOffset(BaseMap, -offset * 0.5f);
            }
        }

#if UNITY_EDITOR
        void OnDisable()
        {
            // Don't leave the material assets modified in the editor.
            if (materials == null) return;
            foreach (var m in materials)
            {
                if (!m) continue;
                if (m.HasProperty(BumpMap)) m.SetTextureOffset(BumpMap, Vector2.zero);
                if (m.HasProperty(BaseMap)) m.SetTextureOffset(BaseMap, Vector2.zero);
            }
        }
#endif
    }
}
