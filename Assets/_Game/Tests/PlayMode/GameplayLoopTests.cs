using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dino.Core;
using Dino.Input;
using Dino.Player;
using Dino.Save;
using Dino.Session;
using Dino.Species;
using Dino.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dino.Tests
{
    /// <summary>
    /// Plays the core loops for real (physics, frames, components together): moving, running,
    /// swimming, eating, drinking, growing, fainting and saving. Run from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class GameplayLoopTests
    {
        readonly List<Object> _spawned = new List<Object>();
        SpeciesDefinition _species;
        string _saveDir;

        [SetUp]
        public void SetUp()
        {
            SessionBootstrap.Disabled = true;
            _saveDir = Path.Combine(Application.temporaryCachePath, "dino_playmode_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.Folder = _saveDir; // never touch the real save
            _species = MakeSpecies("testosaur", starter: true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var o in _spawned) if (o) Object.Destroy(o);
            _spawned.Clear();
            var session = GameSession.Instance;
            if (session)
            {
                if (session.Player) Object.Destroy(session.Player.gameObject);
                Object.Destroy(session.gameObject);
            }
            var sandbox = GameObject.Find("SandboxWorld");
            if (sandbox) Object.Destroy(sandbox);
            SaveSystem.Folder = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            SessionBootstrap.Disabled = false;
        }

        // ---------- helpers ----------

        SpeciesDefinition MakeSpecies(string id, bool starter = false, int unlockXp = 0)
        {
            var s = ScriptableObject.CreateInstance<SpeciesDefinition>();
            s.id = id; s.displayName = id; s.isStarter = starter; s.unlockXp = unlockXp;
            s.adultHeight = 2f; s.adultRadius = 0.5f;
            s.walkSpeed = 3f; s.runSpeed = 7f; s.swimSpeed = 2f;
            s.reach = 1.5f; s.feedHeight = 1.5f; s.biteInterval = 0.1f; s.biteSize = 10f;
            s.foods = new List<FoodPreference>
            {
                new FoodPreference(FoodType.Berries, 1f), new FoodPreference(FoodType.Leaves, 1f), new FoodPreference(FoodType.Meat, 1f),
            };
            _spawned.Add(s);
            return s;
        }

        void MakeGround()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "TestGround";
            g.transform.position = new Vector3(0, -0.5f, 0);
            g.transform.localScale = new Vector3(200, 1, 200);
            _spawned.Add(g);
        }

        DinoActor MakeDino(int stage, Vector3 pos)
        {
            var a = DinoActor.Spawn(_species, stage, null, pos, Quaternion.identity);
            a.gameObject.AddComponent<DinoInteractor>();
            a.gameObject.AddComponent<DinoGrowth>();
            a.gameObject.AddComponent<DinoAttack>();
            _spawned.Add(a.gameObject);
            return a;
        }

        int Adult => _species.StageCount - 1;

        FoodSource MakeFood(FoodType type, Vector3 pos, float feedHeight)
        {
            var go = new GameObject("TestFood_" + type);
            go.transform.position = pos;
            var f = go.AddComponent<FoodSource>();
            f.foodType = type; f.feedHeight = feedHeight; f.radius = 0.3f; f.capacity = 60f; f.regrowPerMinute = 0f;
            _spawned.Add(go);
            return f;
        }

        WaterVolume MakeWater(Vector3 center, Vector3 size, bool drinkable = true)
        {
            var go = new GameObject("TestWater");
            go.transform.position = center;
            var w = go.AddComponent<WaterVolume>();
            w.size = size; w.drinkable = drinkable;
            _spawned.Add(go);
            return w;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static DinoInputState World(Vector2 move, bool run = false, bool action = false) =>
            new DinoInputState { Move = move, Run = run, Action = action, MoveIsWorldSpace = true };

        // ---------- movement ----------

        [UnityTest]
        public IEnumerator Walks_Forward_At_Roughly_Walk_Speed()
        {
            MakeGround();
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.3f); // settle on the ground
            Vector3 start = dino.transform.position;
            dino.Motor.Input = World(new Vector2(0, 1));
            yield return Wait(2f);
            float moved = dino.transform.position.z - start.z;
            Assert.Greater(moved, 4f, "should have walked forward");
            Assert.Less(moved, 7f, "walking should not be faster than walk speed");
            Assert.IsTrue(dino.Motor.IsGrounded);
            Assert.Less(Mathf.Abs(dino.transform.position.x - start.x), 0.5f, "should walk straight");
        }

        [UnityTest]
        public IEnumerator Running_Empties_Stamina_Then_Needs_A_Breather()
        {
            MakeGround();
            _species.runStaminaPerSecond = 200f;   // empty in half a second
            _species.staminaRegenPerSecond = 25f;
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.3f);
            dino.Motor.Input = World(new Vector2(0, 1), run: true);
            yield return Wait(0.2f);
            Assert.IsTrue(dino.Motor.IsRunning, "should start running");
            yield return Wait(0.8f);
            Assert.IsTrue(dino.Stats.IsExhausted, "ran until empty");
            Assert.IsFalse(dino.Motor.IsRunning, "can't run while exhausted");

            // Still holding run: it must not stutter back into a run at 1% stamina.
            int runFramesWhileLow = 0;
            float end = Time.time + 5f;
            while (dino.Stats.Stamina.Normalized < 0.2f && Time.time < end)
            {
                if (dino.Motor.IsRunning) runFramesWhileLow++;
                yield return null;
            }
            Assert.AreEqual(0, runFramesWhileLow, "no running before stamina recovers");
            yield return Wait(2f);
            Assert.IsFalse(dino.Stats.IsExhausted && dino.Stats.Stamina.Normalized > 0.3f, "exhaustion clears after recovering");
        }

        [UnityTest]
        public IEnumerator Swims_At_The_Surface_Without_Flicker()
        {
            MakeWater(new Vector3(0, -5f, 0), new Vector3(60, 10, 60)); // surface at y = 0, no ground
            var dino = MakeDino(Adult, new Vector3(0, -3f, 0));
            yield return Wait(2f);
            int notSwimming = 0;
            for (int i = 0; i < 60; i++)
            {
                if (!dino.Motor.IsSwimming) notSwimming++;
                yield return null;
            }
            Assert.AreEqual(0, notSwimming, "should stay swimming while floating");
            float y = dino.transform.position.y;
            Assert.Less(y, -0.6f, "body should be in the water");
            Assert.Greater(y, -2.2f, "should float near the surface, not sink");

            Vector3 start = dino.transform.position;
            dino.Motor.Input = World(new Vector2(1, 0));
            yield return Wait(1.5f);
            Assert.Greater(dino.transform.position.x - start.x, 1f, "should be able to swim along");
        }

        // ---------- eating and drinking ----------

        [UnityTest]
        public IEnumerator Eats_Berries_While_Action_Is_Held()
        {
            MakeGround();
            var bush = MakeFood(FoodType.Berries, new Vector3(0, 0, 1.4f), 0.5f);
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.4f);
            dino.Stats.Food.Set(dino.Stats.Food.Max * 0.3f);
            var interactor = dino.GetComponent<DinoInteractor>();
            yield return Wait(0.3f);
            Assert.AreEqual(InteractKind.Food, interactor.Kind, "bush should be in reach");
            Assert.AreEqual("Eat berries", interactor.ActionLabel);

            float before = dino.Stats.Food.Current;
            yield return Wait(0.3f);
            Assert.AreEqual(60f, bush.Remaining, 0.01f, "nothing is eaten until the button is held");

            dino.Motor.Input = World(Vector2.zero, action: true);
            yield return Wait(0.6f);
            Assert.Greater(dino.Stats.Food.Current, before + 15f, "food meter should go up");
            Assert.Less(bush.Remaining, 50f, "bush should lose berries");
        }

        [UnityTest]
        public IEnumerator Will_Not_Eat_Food_It_Cannot_Digest()
        {
            MakeGround();
            MakeFood(FoodType.Bugs, new Vector3(0, 0, 1.4f), 0.2f); // test species has no Bugs
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.4f);
            dino.Stats.Food.Set(10f);
            yield return Wait(0.3f);
            Assert.AreEqual(InteractKind.None, dino.GetComponent<DinoInteractor>().Kind);
        }

        [UnityTest]
        public IEnumerator Hatchling_Cannot_Reach_Tall_Leaves_But_Adult_Can()
        {
            MakeGround();
            MakeFood(FoodType.Leaves, new Vector3(0, 0, 0.6f), 1.5f);
            var baby = MakeDino(0, new Vector3(0, 0.2f, 0));
            yield return Wait(0.4f);
            baby.Stats.Food.Set(1f);
            yield return Wait(0.3f);
            Assert.AreEqual(InteractKind.None, baby.GetComponent<DinoInteractor>().Kind, "hatchling is too short");
            Object.Destroy(baby.gameObject);
            yield return null;

            var adult = MakeDino(Adult, new Vector3(0, 0.2f, -0.6f));
            yield return Wait(0.4f);
            adult.Stats.Food.Set(1f);
            yield return Wait(0.3f);
            Assert.AreEqual(InteractKind.Food, adult.GetComponent<DinoInteractor>().Kind, "adult can reach");
        }

        [UnityTest]
        public IEnumerator Eating_A_Meat_Pile_Until_It_Is_Gone_Is_Safe()
        {
            MakeGround();
            var pile = Carcass.Spawn(new Vector3(0, 0, 1.6f), 25f);
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.4f);
            dino.Stats.Food.Set(5f);
            dino.Motor.Input = World(Vector2.zero, action: true);
            yield return Wait(1.5f); // any exception from a destroyed target fails the test
            Assert.IsTrue(pile == null, "meat pile should be gone");
            Assert.Greater(dino.Stats.Food.Current, 25f);
            Assert.AreEqual(InteractKind.None, dino.GetComponent<DinoInteractor>().Kind);
        }

        [UnityTest]
        public IEnumerator Drinks_At_The_Shore_But_Not_Seawater_Or_From_A_High_Bank()
        {
            MakeGround();
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.4f);
            dino.Stats.Water.Set(20f);
            var interactor = dino.GetComponent<DinoInteractor>();

            // Seawater: swim-only.
            var sea = MakeWater(new Vector3(0, -1.5f, 0), new Vector3(20, 3, 20), drinkable: false);
            yield return Wait(0.3f);
            Assert.AreNotEqual(InteractKind.Water, interactor.Kind, "seawater is not drinkable");
            Object.Destroy(sea.gameObject);
            yield return null;

            // Water far below a bank the dino stands on.
            var low = MakeWater(new Vector3(0, -4.5f, 0), new Vector3(20, 3, 20)); // surface at y = -3
            yield return Wait(0.3f);
            Assert.AreNotEqual(InteractKind.Water, interactor.Kind, "can't drink from 3 m above the water");
            Object.Destroy(low.gameObject);
            yield return null;

            // Fresh water at its feet.
            MakeWater(new Vector3(0, -1.5f, 0), new Vector3(20, 3, 20)); // surface at y = 0
            yield return Wait(0.3f);
            Assert.AreEqual(InteractKind.Water, interactor.Kind);
            Assert.AreEqual("Drink", interactor.ActionLabel);
            dino.Motor.Input = World(Vector2.zero, action: true);
            yield return Wait(0.5f);
            Assert.Greater(dino.Stats.Water.Current, 40f, "water meter should go up");
        }

        // ---------- growth and fainting ----------

        [UnityTest]
        public IEnumerator Grows_Through_Every_Stage_And_Gets_Bigger()
        {
            MakeGround();
            var dino = MakeDino(0, new Vector3(0, 0.2f, 0));
            int grewEvents = 0;
            System.Action<GameObject, int> onGrew = (g, s) => { if (g == dino.gameObject) grewEvents++; };
            GameEvents.GrewUp += onGrew;
            try
            {
                yield return Wait(0.2f);
                float babyHeight = dino.Controller.height;
                float babyMaxHealth = dino.Stats.Health.Max;
                dino.Stats.Food.Set(dino.Stats.Food.Max * 0.5f);
                var growth = dino.GetComponent<DinoGrowth>();

                growth.speedMultiplier = 0f;
                dino.Stats.Food.Set(0f);
                yield return Wait(0.2f);
                Assert.IsFalse(growth.IsGrowing, "a starving dino does not grow");

                dino.Stats.Food.Set(dino.Stats.Food.Max * 0.5f);
                growth.speedMultiplier = 100000f;
                yield return Wait(0.5f);
                Assert.IsTrue(dino.IsAdult, "should be fully grown");
                Assert.AreEqual(_species.StageCount - 1, grewEvents);
                Assert.Greater(dino.Controller.height, babyHeight * 2f);
                Assert.Greater(dino.Stats.Health.Max, babyMaxHealth * 2f);
                Assert.AreEqual(0.5f, dino.Stats.Food.Normalized, 0.1f, "meters keep their fill when growing");
                Assert.AreEqual(1f, growth.Progress01, 0.001f);
            }
            finally { GameEvents.GrewUp -= onGrew; }
        }

        [UnityTest]
        public IEnumerator Starving_Leads_To_A_Faint_And_A_Fainted_Dino_Stops()
        {
            MakeGround();
            var dino = MakeDino(Adult, new Vector3(0, 0.2f, 0));
            yield return Wait(0.3f);
            dino.Stats.Food.Set(0f);
            dino.Stats.Health.Set(0.2f);
            dino.Motor.Input = World(new Vector2(0, 1));
            yield return Wait(0.6f);
            Assert.IsTrue(dino.Stats.IsFainted);
            Vector3 p = dino.transform.position;
            yield return Wait(0.5f);
            Assert.Less(Vector3.Distance(p, dino.transform.position), 0.2f, "fainted dinos don't walk");
            dino.Stats.Revive(0.6f);
            yield return Wait(0.5f);
            Assert.Greater(Vector3.Distance(p, dino.transform.position), 0.5f, "revived dinos walk again");
        }

        // ---------- whole session: modes, XP, saving ----------

        GameSession MakeSession(out SpeciesCatalog catalog, out SpeciesDefinition locked)
        {
            SandboxWorld.Build();
            catalog = ScriptableObject.CreateInstance<SpeciesCatalog>();
            locked = MakeSpecies("lockedosaur", starter: false, unlockXp: 300);
            catalog.species.Add(_species);
            catalog.species.Add(locked);
            _spawned.Add(catalog);

            var go = new GameObject("TestSession");
            go.SetActive(false);               // so we can hand over the catalog before Awake runs
            var session = go.AddComponent<GameSession>();
            session.catalog = catalog;
            session.faintWakeSeconds = 0.2f;
            go.SetActive(true);
            return session;
        }

        [UnityTest]
        public IEnumerator Survival_Faint_Wakes_You_Up_And_Keeps_Your_Growth()
        {
            var session = MakeSession(out _, out _);
            yield return null;
            Assert.IsNull(session.Player, "starts at the dino picker");
            session.SetMode(GameMode.Survival);
            session.StartNewRun(_species, null);
            yield return Wait(0.5f);
            Assert.IsNotNull(session.Player);
            Assert.AreEqual(0, session.Player.StageIndex, "new dinos are hatchlings");

            session.Player.GetComponent<DinoGrowth>().GrowUp();
            session.Player.Stats.Faint(null);
            yield return Wait(0.8f);
            Assert.IsNotNull(session.Player, "Survival keeps the dino");
            Assert.IsFalse(session.Player.Stats.IsFainted, "woke up");
            Assert.AreEqual(1, session.Player.StageIndex, "growth is kept");
            Assert.IsTrue(session.Save.run.active);
        }

        [UnityTest]
        public IEnumerator Hardcore_Faint_Ends_The_Run_But_Keeps_Xp()
        {
            var session = MakeSession(out _, out var locked);
            yield return null;
            session.SetMode(GameMode.Hardcore);
            session.StartNewRun(_species, null);
            yield return Wait(0.3f);
            session.Progression.AddXp(350, "test");
            Assert.IsTrue(session.Progression.IsUnlocked(locked));

            session.Player.Stats.Faint(null);
            yield return Wait(0.8f);
            Assert.IsTrue(session.Player == null, "Hardcore run is over");
            Assert.IsFalse(session.Save.run.active);
            Assert.GreaterOrEqual(session.Save.profile.lifetimeXp, 350, "XP is permanent");
            Assert.IsTrue(session.Progression.IsUnlocked(locked), "unlocks are permanent");

            var onDisk = SaveSystem.Load();
            Assert.GreaterOrEqual(onDisk.profile.lifetimeXp, 350);
            Assert.IsFalse(onDisk.run.active);
        }

        [UnityTest]
        public IEnumerator Explorer_Mode_Never_Drains_Or_Faints()
        {
            var session = MakeSession(out _, out _);
            yield return null;
            session.SetMode(GameMode.Explorer);
            session.StartNewRun(_species, null);
            yield return Wait(0.3f);
            var stats = session.Player.Stats;
            stats.Tick(600f);
            stats.TakeDamage(new Survival.DamageInfo(9999f, null, Vector3.zero));
            Assert.AreEqual(stats.Food.Max, stats.Food.Current, 0.01f);
            Assert.IsFalse(stats.IsFainted);
        }

        [UnityTest]
        public IEnumerator Saved_Run_Comes_Back_After_A_Restart()
        {
            var session = MakeSession(out var catalog, out _);
            yield return null;
            session.SetMode(GameMode.Survival);
            session.StartNewRun(_species, null);
            yield return Wait(0.4f);
            session.Player.GetComponent<DinoGrowth>().GrowUp();
            session.Player.Stats.Food.Set(session.Player.Stats.Food.Max * 0.4f);
            session.Progression.AddXp(120, "test");
            yield return Wait(0.2f);
            Vector3 where = session.Player.transform.position;
            float food = session.Player.Stats.Food.Current;
            session.WriteSave();
            Object.Destroy(session.Player.gameObject);
            Object.Destroy(session.gameObject);
            yield return null;
            yield return null;

            // "Restart the app": a brand-new session reading the same save folder.
            var go = new GameObject("TestSession2");
            go.SetActive(false);
            var again = go.AddComponent<GameSession>();
            again.catalog = catalog;
            go.SetActive(true);
            yield return Wait(0.4f);

            Assert.IsNotNull(again.Player, "the saved dino is back without visiting the picker");
            Assert.AreEqual(_species.id, again.Player.Species.id);
            Assert.AreEqual(1, again.Player.StageIndex);
            Assert.AreEqual(food, again.Player.Stats.Food.Current, 3f);
            Assert.Less(Vector3.Distance(where, again.Player.transform.position), 2f);
            Assert.GreaterOrEqual(again.Save.profile.lifetimeXp, 120);
        }

        [UnityTest]
        public IEnumerator Pause_Stops_Time_And_Picking_A_New_Dino_Keeps_Xp()
        {
            var session = MakeSession(out _, out _);
            yield return null;
            session.StartNewRun(_species, null);
            yield return Wait(0.3f);
            session.Progression.AddXp(77, "test");
            session.OpenPause();
            Assert.IsTrue(session.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            session.ClosePause();
            Assert.AreEqual(1f, Time.timeScale);

            session.OpenPause();
            session.ReleaseDinoAndPickNew();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(session.Player == null);
            Assert.IsFalse(session.Save.run.active);
            Assert.GreaterOrEqual(session.Save.profile.lifetimeXp, 77);
        }

        [UnityTest]
        public IEnumerator Falling_Out_Of_The_World_Puts_You_Back_On_Land()
        {
            var session = MakeSession(out _, out _);
            yield return null;
            session.StartNewRun(_species, null);
            yield return Wait(0.3f);
            session.Player.Motor.Teleport(new Vector3(500, -100, 500), Quaternion.identity);
            yield return Wait(0.5f);
            Assert.Greater(session.Player.transform.position.y, -5f);
            Assert.Less(Mathf.Abs(session.Player.transform.position.x), 50f);
        }
    }
}
