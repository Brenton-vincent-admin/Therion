# Species (14 launch dinosaurs)

Owned by the species thread. Data format (`SpeciesDefinition`, `SpeciesCatalog`) is owned by core; this folder
adds content, variants, skins, stealth, abilities and wild AI.

## Where things are

| What | Path |
|---|---|
| The 14 species as the docs define them (stars, diet, ability, unlock level, default look) | `Assets/_Game/Scripts/Species/Content/LaunchRoster.cs` |
| All tuning (stars -> m/s, HP, drain; growth stages; XP curve; variants) | `Assets/_Game/Scripts/Species/Content/SpeciesTuning.cs` |
| Extra fields on `SpeciesDefinition` (skin, ability, temperament, stealth) | `Assets/_Game/Scripts/Species/SpeciesDefinition.Content.cs` |
| Stealth rules + per-dino component + tall-grass volume | `Content/StealthRules.cs`, `Stealth/DinoStealth.cs`, `Stealth/CoverVolume.cs` |
| Signature abilities (14, levels 1-3) | `Abilities/SignatureAbilityRunner.cs`, `Abilities/AbilityTuning.cs` |
| Skins (6-byte look, palette, unlocks, shader driver) | `Skins/`, shader `Assets/_Game/Art/Shaders/DinoSkin.shader` |
| Wild dino AI and spawner | `AI/WildDinoBrain.cs`, `AI/WildDinoDecisions.cs`, `AI/WildDinoSpawner.cs` |
| Editor tools | `Assets/_Game/Scripts/Editor/Species/` (in the Dino.Editor assembly) |
| Tests (39, pure logic, in Dino.Tests.EditMode) | `Assets/_Game/Tests/EditMode/Species/` |

## First run in Unity

1. **Dino > Species > Rebuild Species Assets**: writes `Assets/_Game/Data/Species/Species_<id>.asset` for all 14
   and fills `Assets/_Game/Resources/SpeciesCatalog.asset`. Re-run after changing tuning; it keeps model/icon links.
2. **Dino > Species > Generate Skin Pattern Masks**: writes the shared pattern `Texture2DArray` (procedural, no license).
3. Window > General > Test Runner > EditMode > run `Dino.Species.Tests`.

## Hooking up a dino prefab

Player dino: GameSession adds `DinoStealth` (isPlayer) and `SignatureAbilityRunner` automatically. The runner fires on
`DinoInputState.Ability` (the Power button); show its cooldown ring from `Readiness`. Put `DinoSkin` on the model root.

Wild dino prefab: same, but `DinoStealth.isPlayer` off, plus `WildDinoBrain`. Place `WildDinoSpawner`s in each
biome with a species list. `WildDinoSettings` reads the mode and the friendly-predators setting from GameSession; the day/night cycle sets `IsNight`.

Map thread: put `CoverVolume` (trigger) on tall grass and shade. Optional: implement `IShoveable` on boulders
(Trike Lv3) and `IShakeable` on fruit trees (Brachio Lv3).

## Numbers at a glance (adult, Survival)

| Species | Diet | HP | Bite | Run m/s | Food lasts | Notice | Ability | Unlock |
|---|---|---|---|---|---|---|---|---|
| Parasaur | Herb | 100 | 5 | 12 | 7 min | 25 m | Call | Starter |
| Pachy | Herb | 160 | 18 | 10.5 | 7 min | 25 m | Head-butt | Starter |
| Psittaco | Herb | 60 | 5 | 10.5 | 7.5 min | 10 m (stealth) | Hide | Lv 2 |
| Stego | Herb | 250 | 18 | 7.5 | 7 min | 25 m | Tail swipe | Lv 6 |
| Trike | Herb | 400 | 18 | 7.5 | 6.5 min | 25 m | Horn charge | Lv 9 |
| Brachio | Herb | 400 | 10 | 6 | 6 min | 25 m | High reach + stomp | Lv 12 |
| Oviraptor | Omni | 100 | 10 | 10.5 | 7.5 min | 25 m | Snatch | Starter |
| Gallimimus | Omni | 100 | 5 | 12 | 7.5 min | 25 m | Sprint burst | Lv 3 |
| Troodon | Omni | 100 | 10 | 10.5 | 7.5 min | 10 m (stealth) | Night vision | Lv 5 |
| Microraptor | Carn | 60 | 5 | 10.5 | 7.5 min | 10 m (stealth) | Glide | Lv 4 |
| Raptor | Carn | 100 | 10 | 12 | 7 min | 10 m (stealth) | Pounce | Starter |
| Dilo | Carn | 160 | 18 | 9 | 7 min | 10 m (stealth) | Frill flash | Lv 8 |
| Carno | Carn | 160 | 30 | 10.5 | 6.5 min | 25 m | Bull rush | Lv 10 |
| T. rex | Carn | 400 | 50 | 7.5 | 6 min | 25 m | Roar | Lv 15 |

Growth: Hatchling (20% size) -> Juvenile at 18 min -> Adolescent at 50 min -> Adult at 150 min (T. rex x1.5,
Brachio x1.4, Carno x1.2, Trike x1.15). Ability Lv 1 from hatching, Lv 2 as adolescent, Lv 3 as adult.

Variants (lifetime XP): Swift (Lv 2: +12% speed, +10% stamina, -5% HP), Aquatic (Lv 3: +25% swim, -5% land speed),
Fierce (Lv 4: +15% bite, +10% hunger), Sturdy (Lv 5: +15% HP, -5% hunger, -5% speed), Sneaky (Lv 6: 60% notice range, -10% HP).

## Kid-safety rules the code enforces
- Predators give up a chase after 8 s in Survival (20 s Hardcore), then wait before hunting again.
- Nests are safe zones: nothing attacks a dino standing in one.
- Explorer mode or the parent "friendly predators" toggle: wild dinos never chase or bite players.
- No player-vs-player damage from abilities (`SignatureAbilityRunner.PlayerVsPlayerDamage = false`).
- Fainted wild dinos turn into a cartoon meat pile (`Carcass.Spawn`), never a corpse.
- Skins are earned only (`SkinUnlocks`); locked swatches show a hint, never a price.
