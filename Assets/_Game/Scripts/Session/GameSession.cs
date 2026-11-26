using System;
using Dino.CameraRig;
using Dino.Core;
using Dino.Input;
using Dino.Player;
using Dino.Progression;
using Dino.Save;
using Dino.Species;
using Dino.UI;
using Dino.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Dino.Session
{
    /// <summary>
    /// Runs single-player: species select, spawning, faint/respawn rules per mode, XP, autosave.
    /// Created automatically in any scene that doesn't have one (see SessionBootstrap), so the
    /// map thread's Island scene just works when you press Play.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public SpeciesCatalog catalog;
        [Tooltip("Spawn biome for new hatchlings.")]
        public string startBiome = "meadow";
        public float autosaveSeconds = 30f;
        public float faintWakeSeconds = 3f;

        public SaveData Save { get; private set; }
        public ProgressionService Progression { get; private set; }
        public DinoActor Player { get; private set; }
        public GameMode Mode => Save.settings.mode;

        /// <summary>Raised when the local player dino is spawned (or replaced). Net/other systems hook here.</summary>
        public event Action<DinoActor> PlayerSpawned;

        PlayerInputRouter _router;
        TouchControls _touch;
        ThirdPersonCamera _cam;
        Hud _hud;
        SpeciesSelectScreen _select;
        PauseMenu _pause;
        float _autosaveTimer, _minuteTimer, _faintTimer;
        float _hintCooldown;
        bool _hungryHinted, _thirstyHinted;

        /// <summary>True while the pause menu is open (time is stopped).</summary>
        public bool IsPaused => _pause && _pause.IsOpen;
        /// <summary>Set false when playing with others, where one player must not freeze the world.</summary>
        public bool PauseStopsTime = true;
        float _foodXpBank, _waterXpBank;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (catalog == null) catalog = SpeciesCatalog.Load();
            Save = SaveSystem.Load();
            Progression = new ProgressionService(Save.profile, catalog);

            _router = gameObject.AddComponent<PlayerInputRouter>();
            _cam = SetupCamera();
            _touch = new GameObject("TouchControls").AddComponent<TouchControls>();
            _touch.transform.SetParent(transform, false);
            _touch.lookSensitivity = Save.settings.lookSensitivity;
            _hud = new GameObject("HUD").AddComponent<Hud>();
            _hud.transform.SetParent(transform, false);
            _hud.GetXp = () => Save.profile.lifetimeXp;
            _select = new GameObject("SpeciesSelect").AddComponent<SpeciesSelectScreen>();
            _select.transform.SetParent(transform, false);
            _select.SpeciesChosen += s => StartNewRun(s, null);
            _select.ModeChanged += m => { Save.settings.mode = m; SaveSystem.Save(Save); };
            _pause = new GameObject("PauseMenu").AddComponent<PauseMenu>();
            _pause.transform.SetParent(transform, false);
            _pause.ResumeClicked += ClosePause;
            _pause.NewDinoConfirmed += ReleaseDinoAndPickNew;
            _hud.PauseClicked += OpenPause;

            GameEvents.Ate += OnAte;
            GameEvents.Drank += OnDrank;
            GameEvents.GrewUp += OnGrewUp;
            GameEvents.Fainted += OnAnyFainted;
        }

        void OnDestroy()
        {
            GameEvents.Ate -= OnAte;
            GameEvents.Drank -= OnDrank;
            GameEvents.GrewUp -= OnGrewUp;
            GameEvents.Fainted -= OnAnyFainted;
            if (Instance == this) { Instance = null; Time.timeScale = 1f; }
        }

        void Start()
        {
            var run = Save.run;
            var species = run.active ? catalog?.Find(run.speciesId) : null;
            if (species != null) ResumeRun(species, run);
            else OpenSpeciesSelect();
        }

        ThirdPersonCamera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null || cam.GetComponent("PreviewCamera") != null)
            {
                var go = new GameObject("GameCamera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.farClipPlane = 800f;
            cam.nearClipPlane = 0.1f;
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) urp.renderPostProcessing = true;
            var rig = cam.GetComponent<ThirdPersonCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            return rig;
        }

        // ---------------- runs ----------------

        public void OpenSpeciesSelect()
        {
            _router.InputEnabled = false;
            _touch.SetVisible(false);
            _hud.SetVisible(false);
            _select.Open(catalog, Progression, Mode);
        }

        public void StartNewRun(SpeciesDefinition species, string variantId)
        {
            _select.Close();
            var spawn = FindSpawn(species);
            SpawnPlayer(species, 0, variantId, spawn.pos, spawn.rot);
            var run = Save.run;
            run.active = true;
            run.mode = Mode;
            run.speciesId = species.id;
            run.variantId = variantId;
            run.nestId = null;
            run.minutesAlive = 0f;
            Save.profile.runsStarted++;
            _hud.ShowBanner($"You hatched as a {species.displayName}!");
            WriteSave();
        }

        void ResumeRun(SpeciesDefinition species, RunData run)
        {
            Vector3 pos = run.position;
            if (!Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out _, 200f, ~0, QueryTriggerInteraction.Ignore))
                pos = FindSpawn(species).pos; // world changed since the save
            SpawnPlayer(species, run.stage, run.variantId, pos, Quaternion.Euler(0, run.yaw, 0));
            Player.GetComponent<DinoGrowth>().SecondsInStage = run.secondsInStage;
            if (run.health > 0f) Player.Stats.SetValues(run.health, run.stamina, run.food, run.water);
            else if (Mode == GameMode.Hardcore && run.fainted)
            {
                // The app closed during a Hardcore faint: the run is over, same as if it had stayed open.
                Player.Stats.Faint(null);
                return;
            }
            else if (run.fainted) Player.Stats.Revive(0.6f); // Survival: wake up where you fell
            _hud.ShowBanner($"Welcome back, {species.displayName}!");
        }

        void SpawnPlayer(SpeciesDefinition species, int stage, string variantId, Vector3 pos, Quaternion rot)
        {
            if (Player) Destroy(Player.gameObject);
            Player = DinoActor.Spawn(species, stage, variantId, pos, rot);
            Player.name = "PlayerDino";
            Player.IsLocalPlayer = true;
            Player.gameObject.AddComponent<DinoInteractor>().RestedAtNest += OnRestedAtNest;
            Player.gameObject.AddComponent<DinoGrowth>();
            Player.gameObject.AddComponent<DinoAttack>();
            Player.Motor.CameraRig = _cam;
            Player.Stats.NoDrain = Mode == GameMode.Explorer;
            Player.Stats.OnFainted += OnPlayerFainted;
            Player.Changed += a => _cam.targetSize = a.Height;
            OptionalHooks.AttachToPlayer(Player);

            _cam.target = Player.transform;
            _cam.targetSize = Player.Height;
            _cam.SnapBehindTarget();
            _hud.Bind(Player);
            _hud.SetVisible(true);
            _touch.SetVisible(Application.isMobilePlatform || Save.settings.showTouchControls);
            _router.InputEnabled = true;
            _faintTimer = 0f;
            _hungryHinted = _thirstyHinted = false;
            PlayerSpawned?.Invoke(Player);
        }

        (Vector3 pos, Quaternion rot) FindSpawn(SpeciesDefinition species)
        {
            var sp = WorldRegistry.PickSpawn(species.size, startBiome);
            if (sp) return (sp.GetSpawnPosition(), sp.transform.rotation);
            Vector3 p = Vector3.zero;
            if (Physics.Raycast(new Vector3(0, 500, 0), Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            return (p, Quaternion.identity);
        }

        // ---------------- pause ----------------

        public void OpenPause()
        {
            if (!Player || IsPaused || _select.IsOpen) return;
            WriteSave();
            _router.InputEnabled = false;
            _touch.SetVisible(false);
            if (PauseStopsTime) Time.timeScale = 0f;
            _pause.Open(Player.Species.displayName, Mode, Progression.Level, Progression.Xp);
        }

        public void ClosePause()
        {
            if (!IsPaused) return;
            _pause.Close();
            Time.timeScale = 1f;
            if (Player && !Player.Stats.IsFainted) _router.InputEnabled = true;
            _touch.SetVisible(Player && (Application.isMobilePlatform || Save.settings.showTouchControls));
        }

        /// <summary>Ends the current run by choice and goes back to the dino picker. XP and unlocks are kept.</summary>
        public void ReleaseDinoAndPickNew()
        {
            _pause.Close();
            Time.timeScale = 1f;
            EndRun();
            OpenSpeciesSelect();
        }

        void EndRun()
        {
            Save.run = new RunData(); // XP and unlocks live in the profile and are kept
            if (Player) Destroy(Player.gameObject);
            Player = null;
            _cam.target = null;
            _hud.Bind(null);
            WriteSave();
        }

        // ---------------- loop ----------------

        void Update()
        {
            if (Player)
            {
                Player.Motor.Input = _router.Current;
                var it = Player.GetComponent<DinoInteractor>();
                if (it) { _touch.ActionLabel = it.ActionLabel; _touch.ActionAvailable = it.Kind != InteractKind.None; }

                if (Player.Stats.IsFainted)
                {
                    _faintTimer += Time.deltaTime;
                    if (_faintTimer >= faintWakeSeconds) HandleFaintOutcome();
                }
                else
                {
                    RescueIfOutOfWorld();
                    ShowHints();
                    _minuteTimer += Time.deltaTime;
                    if (_minuteTimer >= 60f)
                    {
                        _minuteTimer -= 60f;
                        Save.run.minutesAlive += 1f;
                        GiveXp(XpRewards.PerMinuteAlive, "survived a minute");
                    }
                }
            }

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= autosaveSeconds) { _autosaveTimer = 0f; WriteSave(); }
        }

        /// <summary>Fell off the edge of the map or through the ground: put the dino back on land.</summary>
        void RescueIfOutOfWorld()
        {
            if (Player.transform.position.y > OutOfWorldY) return;
            var s = FindSpawn(Player.Species);
            Player.Motor.Teleport(s.pos + Vector3.up * 0.5f, s.rot);
            _cam.SnapBehindTarget();
            _hud.ShowBanner("Whoops! Back on dry land.");
        }
        const float OutOfWorldY = -60f;

        /// <summary>One gentle reminder each time a meter gets low, so a young player knows what to look for.</summary>
        void ShowHints()
        {
            if (Mode == GameMode.Explorer) return;
            _hintCooldown -= Time.deltaTime;
            var st = Player.Stats;
            if (st.Food.Normalized > 0.5f) _hungryHinted = false;
            if (st.Water.Normalized > 0.5f) _thirstyHinted = false;
            if (_hintCooldown > 0f) return;

            if (!_thirstyHinted && st.Water.Normalized < 0.25f)
            {
                _thirstyHinted = true; _hintCooldown = 8f;
                _hud.ShowBanner("You're thirsty! Find some water.");
            }
            else if (!_hungryHinted && st.Food.Normalized < 0.25f)
            {
                _hungryHinted = true; _hintCooldown = 8f;
                _hud.ShowBanner(FoodHint(Player.Species));
            }
        }

        static string FoodHint(SpeciesDefinition sp)
        {
            var mask = sp.EdibleMask;
            if ((mask & FoodType.Meat) != 0 && (mask & (FoodType.Berries | FoodType.Leaves)) == 0) return "You're hungry! Find some meat.";
            if ((mask & FoodType.Berries) != 0) return "You're hungry! Find some berries.";
            if ((mask & FoodType.Leaves) != 0) return "You're hungry! Find some leaves.";
            return "You're hungry! Find some food.";
        }

        void OnPlayerFainted(GameObject attacker)
        {
            if (IsPaused) ClosePause();
            _faintTimer = 0f;
            _router.InputEnabled = false;
            _hud.ShowBanner(Mode == GameMode.Hardcore ? "Oh no, you fainted!" : "You fainted! Time for a nap...", faintWakeSeconds);
        }

        void HandleFaintOutcome()
        {
            if (Mode == GameMode.Hardcore)
            {
                Save.profile.bestHardcoreMinutes = Mathf.Max(Save.profile.bestHardcoreMinutes, Save.run.minutesAlive);
                EndRun(); // run over; XP and unlocks are kept
                OpenSpeciesSelect();
                return;
            }

            // Survival: wake up at the nest (or a spawn point), growth kept.
            Vector3 pos; Quaternion rot;
            var nest = string.IsNullOrEmpty(Save.run.nestId) ? null : Nest.FindById(Save.run.nestId);
            if (nest) { pos = nest.transform.position; rot = nest.transform.rotation; }
            else { var s = FindSpawn(Player.Species); pos = s.pos; rot = s.rot; }
            Player.Motor.Teleport(pos + Vector3.up * 0.5f, rot);
            Player.Stats.Revive(0.6f);
            _router.InputEnabled = true;
            _cam.SnapBehindTarget();
            _hud.ShowBanner("You woke up at your nest!");
            WriteSave();
        }

        // ---------------- XP ----------------

        void GiveXp(int amount, string reason)
        {
            foreach (var s in Progression.AddXp(amount, reason))
                _hud.ShowBanner($"New dino unlocked: {s.displayName}!", 4f);
        }

        bool IsPlayer(GameObject go) => Player && go == Player.gameObject;

        void OnAte(GameObject eater, FoodType type, float units)
        {
            if (!IsPlayer(eater)) return;
            _foodXpBank += units;
            int xp = Mathf.FloorToInt(_foodXpBank / XpRewards.FoodUnitsPerXp);
            if (xp > 0) { _foodXpBank -= xp * XpRewards.FoodUnitsPerXp; GiveXp(xp, "ate"); }
        }

        void OnDrank(GameObject drinker, float units)
        {
            if (!IsPlayer(drinker)) return;
            _waterXpBank += units;
            int xp = Mathf.FloorToInt(_waterXpBank / XpRewards.WaterUnitsPerXp);
            if (xp > 0) { _waterXpBank -= xp * XpRewards.WaterUnitsPerXp; GiveXp(xp, "drank"); }
        }

        void OnGrewUp(GameObject dino, int stage)
        {
            if (!IsPlayer(dino)) return;
            var st = Player.Species.Stage(stage);
            if (Player.IsAdult) Save.profile.timesGrownToAdult++;
            _hud.ShowBanner($"You grew into a {st.name}!");
            GiveXp(st.xpOnReach, $"grew into {st.name}");
            WriteSave();
        }

        void OnAnyFainted(GameObject dino, GameObject attacker)
        {
            if (Player && attacker == Player.gameObject && !IsPlayer(dino))
                GiveXp(XpRewards.FaintedAWildDino, "won a scuffle");
        }

        void OnRestedAtNest(Nest nest)
        {
            if (Save.run.nestId == nest.nestId) return;
            Save.run.nestId = nest.nestId;
            _hud.ShowBanner("This is your nest now!");
            GiveXp(XpRewards.RestedAtNewNest, "found a nest");
            WriteSave();
        }

        // ---------------- saving ----------------

        public void WriteSave()
        {
            if (Save == null) return;
            if (Player && Save.run.active)
            {
                var r = Save.run;
                r.stage = Player.StageIndex;
                r.secondsInStage = Player.GetComponent<DinoGrowth>().SecondsInStage;
                r.fainted = Player.Stats.IsFainted;
                r.health = Player.Stats.Health.Current;
                r.stamina = Player.Stats.Stamina.Current;
                r.food = Player.Stats.Food.Current;
                r.water = Player.Stats.Water.Current;
                r.position = Player.transform.position;
                r.yaw = Player.transform.eulerAngles.y;
            }
            SaveSystem.Save(Save);
        }

        void OnApplicationPause(bool paused) { if (paused) WriteSave(); }
        void OnApplicationQuit() => WriteSave();

        /// <summary>Parent menu: switch mode mid-run (Explorer stops drain immediately).</summary>
        public void SetMode(GameMode mode)
        {
            Save.settings.mode = mode;
            Save.run.mode = mode;
            if (Player) Player.Stats.NoDrain = mode == GameMode.Explorer;
            WriteSave();
        }
    }
}
