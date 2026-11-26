using Dino.Species;
using Dino.Util;
using Dino.World;
using UnityEngine;

namespace Dino.Session
{
    /// <summary>
    /// Press Play in any scene and it becomes playable:
    ///  - adds a GameSession if the scene has none (put a NoAutoSession component in a scene to opt out),
    ///  - builds a small test meadow if the scene has no world (no spawn points and no terrain).
    /// </summary>
    public static class SessionBootstrap
    {
        /// <summary>Set true to stop sessions being created automatically (tests, custom flows).</summary>
        public static bool Disabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Disabled) return;
            // Unity's test runner plays in a scene named "InitTestScene..."; tests build their own world
            // and must never touch the real save file.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return;
            if (Object.FindAnyObjectByType<NoAutoSession>() != null) return;
            if (Object.FindAnyObjectByType<GameSession>() != null) return;

            bool hasWorld = WorldRegistry.SpawnPoints.Count > 0 || Terrain.activeTerrain != null;
            if (!hasWorld) SandboxWorld.Build();
            new GameObject("GameSession").AddComponent<GameSession>();
        }
    }

    /// <summary>Marker: don't auto-create a GameSession in this scene (menus, multiplayer lobby).</summary>
    public class NoAutoSession : MonoBehaviour { }

    /// <summary>A tiny test world made of primitives: grass, berry bushes, leafy trees, bug mounds, a pond and a nest.</summary>
    public static class SandboxWorld
    {
        public static void Build()
        {
            var root = new GameObject("SandboxWorld").transform;

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);
                sun.transform.SetParent(root);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(20, 1, 20); // 200 x 200 m
            ground.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(new Color(0.45f, 0.7f, 0.35f));

            // A few hills to climb.
            for (int i = 0; i < 6; i++)
            {
                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = "Hill";
                hill.transform.SetParent(root);
                float a = i * 60f * Mathf.Deg2Rad;
                hill.transform.position = new Vector3(Mathf.Cos(a) * 70f, -6f, Mathf.Sin(a) * 70f);
                hill.transform.localScale = new Vector3(30, 16, 30);
                hill.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(new Color(0.4f, 0.62f, 0.32f));
            }

            var rng = new System.Random(7);
            float R(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

            for (int i = 0; i < 18; i++) MakeBush(root, new Vector3(R(-45, 45), 0, R(-45, 45)));
            for (int i = 0; i < 12; i++) MakeTree(root, new Vector3(R(-55, 55), 0, R(-55, 55)));
            for (int i = 0; i < 8; i++) MakeBugMound(root, new Vector3(R(-40, 40), 0, R(-40, 40)));
            for (int i = 0; i < 6; i++) MakeLowLeaves(root, new Vector3(R(-35, 35), 0, R(-35, 35)));

            // Pond
            var pond = new GameObject("Pond");
            pond.transform.SetParent(root);
            pond.transform.position = new Vector3(25, -1.5f, 20);
            var wv = pond.AddComponent<WaterVolume>();
            wv.size = new Vector3(16, 3f, 12);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(surface.GetComponent<Collider>());
            surface.transform.SetParent(pond.transform, false);
            surface.transform.localPosition = new Vector3(0, 1.45f, 0);
            surface.transform.localScale = new Vector3(16, 0.1f, 12);
            surface.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(new Color(0.25f, 0.5f, 0.9f));

            // Nest + spawn point
            var nest = new GameObject("Nest");
            nest.transform.SetParent(root);
            nest.transform.position = new Vector3(-8, 0, -8);
            nest.AddComponent<Nest>().nestId = "sandbox_nest";
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(nest.transform, false);
            ring.transform.localScale = new Vector3(4, 0.15f, 4);
            ring.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(new Color(0.6f, 0.45f, 0.25f));

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root);
            spawn.transform.position = new Vector3(-5, 0, -5);
            spawn.AddComponent<PlayerSpawnPoint>().biomeId = "meadow";
        }

        static void MakeBush(Transform root, Vector3 p)
        {
            var go = new GameObject("BerryBush");
            go.transform.SetParent(root); go.transform.position = p;
            Prim(go.transform, PrimitiveType.Sphere, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 1.4f), new Color(0.2f, 0.5f, 0.2f), true);
            var berries = Prim(go.transform, PrimitiveType.Sphere, new Vector3(0, 1.1f, 0), new Vector3(1.0f, 0.5f, 1.0f), new Color(0.75f, 0.15f, 0.4f), false);
            var f = go.AddComponent<FoodSource>();
            f.foodType = FoodType.Berries; f.feedHeight = 0.9f; f.radius = 0.8f; f.foodVisual = berries;
        }

        static void MakeTree(Transform root, Vector3 p)
        {
            var go = new GameObject("LeafyTree");
            go.transform.SetParent(root); go.transform.position = p;
            Prim(go.transform, PrimitiveType.Cylinder, new Vector3(0, 2.5f, 0), new Vector3(0.6f, 2.5f, 0.6f), new Color(0.45f, 0.3f, 0.2f), true);
            var leaves = Prim(go.transform, PrimitiveType.Sphere, new Vector3(0, 5.5f, 0), new Vector3(4f, 3f, 4f), new Color(0.25f, 0.6f, 0.25f), false);
            var f = go.AddComponent<FoodSource>();
            f.foodType = FoodType.Leaves; f.feedHeight = 4.5f; f.radius = 1.5f; f.capacity = 150f; f.foodVisual = leaves;
        }

        static void MakeLowLeaves(Transform root, Vector3 p)
        {
            var go = new GameObject("Fern");
            go.transform.SetParent(root); go.transform.position = p;
            var leaves = Prim(go.transform, PrimitiveType.Sphere, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 1.6f), new Color(0.35f, 0.7f, 0.3f), false);
            var f = go.AddComponent<FoodSource>();
            f.foodType = FoodType.Leaves; f.feedHeight = 0.4f; f.radius = 0.8f; f.foodVisual = leaves;
        }

        static void MakeBugMound(Transform root, Vector3 p)
        {
            var go = new GameObject("BugMound");
            go.transform.SetParent(root); go.transform.position = p;
            var mound = Prim(go.transform, PrimitiveType.Sphere, new Vector3(0, 0.2f, 0), new Vector3(1.2f, 0.6f, 1.2f), new Color(0.5f, 0.38f, 0.25f), false);
            var f = go.AddComponent<FoodSource>();
            f.foodType = FoodType.Bugs; f.feedHeight = 0.2f; f.radius = 0.6f; f.capacity = 30f; f.foodVisual = mound;
        }

        static Transform Prim(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, bool keepCollider)
        {
            var p = GameObject.CreatePrimitive(t);
            if (!keepCollider) Object.Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos; p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterials.Get(c);
            return p.transform;
        }
    }
}
