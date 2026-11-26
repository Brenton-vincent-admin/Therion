# Core contracts (from the "Build the core Unity game" thread)

Status: live on main. Changes to these contracts go through a PR touching this file.

## Basics
- Unity **6000.0.58f2** (Unity 6.0 LTS). URP, Input System, Netcode for GameObjects 2.x, Multiplayer Services (already in Packages/manifest.json).
- Root namespace **`Dino`** (not the game name, so a rename never touches code). Product name, company and Android
  package id live only in `Dino.Core.GameInfo` (Assets/_Game/Scripts/Core/GameInfo.cs). Package id is a placeholder
  `com.brenton.therion` (final, never change). Game name: Therion. Repo: Brenton-vincent-admin/therion.
- Base branch: `main`. Work on your own branch, open PRs into main.
- One runtime assembly `Dino.Runtime` covers `Assets/_Game/Scripts/**`. Put your code in your folder below; if you need
  your own asmdef (e.g. Net), reference `Dino.Runtime`.

## Folder layout
```
Assets/_Game/
  Scripts/Core        Dino.Core        GameInfo, GameMode, GameEvents, Util
  Scripts/Species     Dino.Species     SpeciesDefinition, SpeciesCatalog, enums      (core owns the format; species thread owns content, variants, skins, AI)
  Scripts/Survival    Dino.Survival    Stat, SurvivalStats, IDamageable
  Scripts/World       Dino.World       IFoodSource, IWaterSource, FoodSource, Carcass, WaterVolume, PlayerSpawnPoint, Nest, WorldRegistry
  Scripts/Player      Dino.Player      DinoActor, DinoMotor, DinoInteractor, DinoGrowth, DinoAttack
  Scripts/Input       Dino.Input       DinoInputState, touch controls, keyboard/gamepad
  Scripts/Camera      Dino.CameraRig   ThirdPersonCamera
  Scripts/Progression Dino.Progression PlayerProfile, XP
  Scripts/Save        Dino.Save        SaveData, SaveSystem
  Scripts/Session     Dino.Session     GameSession (spawn, faint, respawn, modes)
  Scripts/UI          Dino.UI
  Scripts/Net         Dino.Net         <- multiplayer thread
  Scripts/Species/AI  Dino.Species.AI  <- species thread (WildDinoBrain)
  Data/Species        one SpeciesDefinition asset per species  <- species thread
  World/              terrain, biome scenes, world prefabs     <- map thread
  Art/ Prefabs/ Scenes/ Audio/
```

## Species data: `Dino.Species.SpeciesDefinition` (ScriptableObject)
Real units (m, m/s, units/min), adult values; growth stages scale them. UI can derive 1-5 stars from these.
- identity: `id` (stable save id), `displayName`, `kidDescription`, `diet` (Herbivore/Omnivore/Carnivore), `moveType` (Land/SemiAquatic/Glider/Flyer), `size` (Tiny..Huge), `icon`
- model: `modelPrefab` (null = placeholder capsule), `adultHeight`, `adultRadius`, `adultModelScale`, `placeholderColor`
- unlock: `isStarter`, `unlockXp` (lifetime XP threshold; XP is never spent or lost), `sortOrder`
- survival: `maxHealth`, `maxStamina`, `maxFood`, `maxWater`, `foodDrainPerMinute`, `waterDrainPerMinute`, `staminaRegenPerSecond`, `runStaminaPerSecond`, `healthRegenPerSecond`
- movement: `walkSpeed`, `runSpeed`, `swimSpeed`, `sneakSpeed`, `turnSpeed`, `jumpHeight`
- eating/combat: `foods` (List<FoodPreference{type, nutrition}>), `biteSize`, `biteInterval`, `reach`, `feedHeight`, `attackDamage`, `attackCooldown`
- stealth: `noticeRadius`, `sneakNoticeMultiplier`
- growth: `growthStages` (List<GrowthStage{name, scale, statMultiplier, minutesToGrow, xpOnReach}>; default 4 stages)
- `abilityIds` (List<string>), `variants` (List<StatVariant{id, displayName, unlockXp, health/speed/swim/stamina/damage/noticeRadius/drain multipliers}>)
- `SpeciesCatalog` asset at `Assets/_Game/Resources/SpeciesCatalog.asset`, `SpeciesCatalog.Load()`.
Species thread: add fields you need (skins etc.) rather than a parallel class; keep existing names.

## Survival stats: `Dino.Survival.SurvivalStats` (MonoBehaviour, implements `IDamageable`)
- `Stat Health, Stamina, Food, Water` -> `Current, Max, Normalized, IsEmpty, Changed` event
- `Configure(species, stageIndex, variant, keepRatio)`
- `Eat(units)`, `Drink(units)`, `Heal(amount)`, `TryUseStamina(amount)`, `CanRun`, `IsRunning`
- `TakeDamage(DamageInfo{amount, attacker, point})`, `IsFainted`, `Faint(attacker)`, `Revive(fill)`
- events `OnFainted(attacker)`, `OnRevived`, `OnDamaged(info)`
- `SimulateLocally` (false on net clients that mirror host values), `NoDrain` (Explorer mode), `SetValues(h,s,f,w)` for save/net, `Tick(dt)`
- Global signals: `Dino.Core.GameEvents` (Ate, Drank, GrewUp, Fainted, XpGained, Unlocked)
- "Dinos faint, they don't die": a fainted WILD dino should spawn a meat pile via `Carcass.Spawn(pos, meat)` and despawn.

## World: `Dino.World`
- `IFoodSource { FoodType FoodType; Vector3 GetFeedPoint(from); float FeedHeight; bool HasFood; float Remaining; float Bite(units, eater) }`
- `IWaterSource { Vector3 GetDrinkPoint(from); bool CanDrink; float Drink(units, drinker) }`
- Register in OnEnable / unregister in OnDisable with `WorldRegistry.Register(this)`. Lookups: `WorldRegistry.FindNearestFood(pos, radius, edibleMask, maxHeight)`, `FindNearestWater`, `PickSpawn(size, biomeId)`.
- Ready-made components (just put them on prefabs): `FoodSource` (foodType, capacity, regrowPerMinute, feedHeight, radius, foodVisual), `Carcass`, `WaterVolume` (box; top face = water surface; drink + swim), `PlayerSpawnPoint` (biomeId, min/maxSize, radius), `Nest` (Survival respawn point).
- FoodType flags: Berries, Leaves, Bugs, Meat, Fish, Eggs. Tall leaves: set `feedHeight` high so only tall dinos reach.

## Player dino (generic, data-driven)
- `Dino.Player.DinoActor`: root component. `Species`, `StageIndex`, `VariantId`, `Stats` (SurvivalStats), `Motor`, `Growth`. `Setup(species, stage, variantId)`. Builds the model or placeholder.
- `Dino.Player.DinoMotor`: CharacterController movement. Driven each frame by a `Dino.Input.DinoInputState` (Move, Look, Run, Sneak, Jump, Action, Attack, Call, Ability, MoveIsWorldSpace) via `motor.Input = state`. Player input, wild-dino AI and network replicas all drive the same motor.
- `DinoInteractor` (eat/drink/nest), `DinoGrowth`, `DinoAttack` (player only; wild AI bites in WildDinoBrain), `DinoStatusEffects` (ability hooks the motor reads).
- `Dino.Session.GameSession`: spawn, faint -> Survival respawn at nest / Hardcore restart, Explorer no drain. `GameSession.Instance`, `Player`, `Mode`, `Save`, `Progression`, event `PlayerSpawned(DinoActor)`. Auto-created in any scene without one (`SessionBootstrap`; add `NoAutoSession` to opt out); builds a test meadow when the scene has no world.
- `Dino.Progression`: `XpCurve.XpForLevel/LevelForXp`, `ProgressionService.AddXp/IsUnlocked/IsVariantUnlocked`, `XpRewards`.
- `Dino.Save.SaveSystem`: JSON in persistentDataPath, atomic write, autosave + on app pause.
