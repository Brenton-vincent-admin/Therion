using System;
using Dino.Species;
using Dino.Survival;
using Dino.Util;
using UnityEngine;

namespace Dino.Player
{
    /// <summary>
    /// Root of any dino (player, wild, or network replica). Holds species, growth stage and variant,
    /// builds the model (or a placeholder) and sizes the CharacterController.
    /// Create one with DinoActor.Spawn(species, stage, variantId, position, rotation).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(SurvivalStats))]
    [RequireComponent(typeof(DinoMotor))]
    [RequireComponent(typeof(DinoStatusEffects))]
    public class DinoActor : MonoBehaviour
    {
        public SpeciesDefinition Species { get; private set; }
        public int StageIndex { get; private set; }
        public string VariantId { get; private set; }
        public StatVariant Variant { get; private set; }
        public bool IsLocalPlayer { get; set; }

        public SurvivalStats Stats { get; private set; }
        public DinoMotor Motor { get; private set; }
        public DinoStatusEffects Effects { get; private set; }
        public CharacterController Controller { get; private set; }
        public Animator Animator { get; private set; }
        public Transform ModelRoot { get; private set; }

        /// <summary>Raised after Setup or a growth stage change.</summary>
        public event Action<DinoActor> Changed;

        public GrowthStage Stage => Species.Stage(StageIndex);
        public float Scale => Stage.scale;
        /// <summary>Current standing height in meters.</summary>
        public float Height => Species.adultHeight * Scale;
        public bool IsAdult => StageIndex >= Species.StageCount - 1;

        void Awake() => CacheComponents();

        void CacheComponents()
        {
            if (Stats) return;
            Stats = GetComponent<SurvivalStats>();
            Motor = GetComponent<DinoMotor>();
            Effects = GetComponent<DinoStatusEffects>();
            Controller = GetComponent<CharacterController>();
        }

        public static DinoActor Spawn(SpeciesDefinition species, int stage, string variantId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject($"Dino_{species.id}");
            go.transform.SetPositionAndRotation(position, rotation);
            var actor = go.AddComponent<DinoActor>();
            actor.Setup(species, stage, variantId);
            return actor;
        }

        public void Setup(SpeciesDefinition species, int stage, string variantId)
        {
            CacheComponents();
            Species = species;
            VariantId = variantId;
            Variant = species.FindVariant(variantId);
            StageIndex = Mathf.Clamp(stage, 0, species.StageCount - 1);
            BuildModel();
            ApplySize();
            Stats.Configure(species, StageIndex, Variant, keepRatio: false);
            Changed?.Invoke(this);
        }

        /// <summary>Moves to a growth stage, keeping the current fill % of each meter.</summary>
        public void SetStage(int stage)
        {
            StageIndex = Mathf.Clamp(stage, 0, Species.StageCount - 1);
            ApplySize();
            Stats.Configure(Species, StageIndex, Variant, keepRatio: true);
            Changed?.Invoke(this);
        }

        void ApplySize()
        {
            float s = Scale;
            float h = Mathf.Max(0.3f, Species.adultHeight * s);
            float r = Mathf.Clamp(Species.adultRadius * s, 0.1f, h * 0.5f);
            Controller.height = h;
            Controller.radius = r;
            Controller.center = new Vector3(0f, h * 0.5f + Controller.skinWidth, 0f);
            Controller.stepOffset = Mathf.Min(0.3f + 0.3f * s, h * 0.5f);
            Controller.slopeLimit = 50f;
            if (ModelRoot) ModelRoot.localScale = Vector3.one * (Species.adultModelScale * s);
        }

        void BuildModel()
        {
            if (ModelRoot) Destroy(ModelRoot.gameObject);
            if (Species.modelPrefab)
            {
                var model = Instantiate(Species.modelPrefab, transform);
                model.name = "Model";
                foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
                ModelRoot = model.transform;
                Animator = model.GetComponentInChildren<Animator>();
            }
            else
            {
                ModelRoot = BuildPlaceholder(Species).transform;
                ModelRoot.SetParent(transform, false);
                Animator = null;
            }
            ModelRoot.localPosition = Vector3.zero;
            ModelRoot.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// A simple stand-in dino built from primitives, sized to the adult capsule at scale 1.
        /// Body, head, tail and legs, so you can tell which way it faces.
        /// </summary>
        public static GameObject BuildPlaceholder(SpeciesDefinition sp)
        {
            var root = new GameObject("Model (placeholder)");
            float h = sp.adultHeight, r = sp.adultRadius;
            // Model is built at adultModelScale = 1 in "adult meters"; DinoActor scales by adultModelScale * stage.
            float inv = 1f / Mathf.Max(0.01f, sp.adultModelScale);
            Color body = sp.placeholderColor;
            Color dark = Color.Lerp(body, Color.black, 0.35f);

            GameObject Part(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, string name)
            {
                var p = GameObject.CreatePrimitive(t);
                p.name = name;
                UnityEngine.Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = pos * inv;
                p.transform.localScale = scale * inv;
                p.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(c);
                return p;
            }

            float bodyLen = Mathf.Max(r * 2.6f, h * 0.9f);
            Part(PrimitiveType.Sphere, new Vector3(0, h * 0.55f, 0), new Vector3(r * 1.8f, h * 0.5f, bodyLen), body, "Body");
            Part(PrimitiveType.Sphere, new Vector3(0, h * 0.85f, bodyLen * 0.55f), new Vector3(r * 1.1f, h * 0.3f, r * 1.4f), body, "Head");
            Part(PrimitiveType.Sphere, new Vector3(r * 0.35f, h * 0.92f, bodyLen * 0.55f + r * 0.5f), Vector3.one * r * 0.25f, Color.white, "EyeR");
            Part(PrimitiveType.Sphere, new Vector3(-r * 0.35f, h * 0.92f, bodyLen * 0.55f + r * 0.5f), Vector3.one * r * 0.25f, Color.white, "EyeL");
            Part(PrimitiveType.Sphere, new Vector3(0, h * 0.5f, -bodyLen * 0.75f), new Vector3(r * 0.8f, h * 0.25f, bodyLen * 0.9f), dark, "Tail");
            Part(PrimitiveType.Cylinder, new Vector3(r * 0.5f, h * 0.18f, 0), new Vector3(r * 0.5f, h * 0.18f, r * 0.5f), dark, "LegR");
            Part(PrimitiveType.Cylinder, new Vector3(-r * 0.5f, h * 0.18f, 0), new Vector3(r * 0.5f, h * 0.18f, r * 0.5f), dark, "LegL");
            return root;
        }
    }
}
