using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Applies a <see cref="SkinLoadout"/> to a dino model through the "Dino/DinoSkin" shader, using a
    /// MaterialPropertyBlock (no material copies, so many dinos still batch well on phones).
    ///
    /// Works with realistic textures: the shader keeps the model's own albedo detail (scales, wrinkles,
    /// shading) and recolors it by zone, so one realistic texture set per species gives many looks.
    /// Set <see cref="tintStrength"/> to 0 to show the model's original texture untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public class DinoSkin : MonoBehaviour
    {
        [Tooltip("0 = the model's original texture, 1 = fully recolored by the skin.")]
        [Range(0f, 1f)] public float tintStrength = 0.85f;
        [Tooltip("Renderers to tint. Empty = every renderer under this object.")]
        public Renderer[] renderers;

        public SkinLoadout Current { get; private set; }

        static readonly int BaseTint = Shader.PropertyToID("_BaseTint");
        static readonly int BellyTint = Shader.PropertyToID("_BellyTint");
        static readonly int PatternTint = Shader.PropertyToID("_PatternTint");
        static readonly int AccentTint = Shader.PropertyToID("_AccentTint");
        static readonly int EyeTint = Shader.PropertyToID("_EyeTint");
        static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        static readonly int TintStrength = Shader.PropertyToID("_TintStrength");

        MaterialPropertyBlock _block;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
        }

        bool _applied;

        void Start()
        {
            // Nobody chose a look yet: show the species' default one.
            if (_applied) return;
            var actor = GetComponentInParent<Dino.Player.DinoActor>();
            if (actor != null && actor.Species != null) Apply(actor.Species.defaultSkin);
        }

        public void Apply(SkinLoadout skin)
        {
            skin = skin.Sanitized();
            Current = skin;
            _applied = true;
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            if (_block == null) _block = new MaterialPropertyBlock();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseTint, ToColor(skin.Base));
                _block.SetColor(BellyTint, ToColor(skin.Belly));
                _block.SetColor(PatternTint, ToColor(skin.PatternColor));
                _block.SetColor(AccentTint, ToColor(skin.Accent));
                _block.SetColor(EyeTint, ToColor(skin.Eyes));
                _block.SetFloat(PatternIndex, skin.pattern);
                _block.SetFloat(TintStrength, tintStrength);
                r.SetPropertyBlock(_block);
            }
        }

        public static Color ToColor(SkinColor c)
        {
            SkinPalette.ToRgb01(c, out var r, out var g, out var b);
            return new Color(r, g, b, 1f); // sRGB; Unity converts for linear color space
        }
    }
}
