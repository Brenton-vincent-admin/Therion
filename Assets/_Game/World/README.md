# The island (world)

The starting island is generated, not hand-sculpted, so it can be rebuilt any time the layout changes.

**Build it:** in Unity, menu **Dino > World > Build Island**. It writes `Assets/_Game/World/Island/Island.unity`
plus everything in `Island/Generated/` (terrain, water meshes, biome map, placeholder art). Rebuilding
replaces `Generated/` completely, so don't hand-edit anything in there. Takes under a minute.

**Look around:** open `Island.unity` and press Play. A free-fly preview camera is included (WASD, Q/E,
Shift, hold right mouse to look). It removes itself as soon as the real player camera exists.

## Layout (from the design doc)

About 1.5 km across, sea level at world Y = 0.

| Biome | id | Opens at | Where |
|---|---|---|---|
| Nest Meadow | `meadow` | Hatchling | Middle of the island, main nest + pond |
| Riverbanks | `riverbanks` | Hatchling | Along the big river and both creeks |
| Fern Forest | `forest` | Juvenile | West and south, plus east of the river |
| Misty Swamp | `swamp` | Sub-adult | South-east lowlands by the sea |
| Red Highlands | `highlands` | Sub-adult | North-east, with the volcano and two hot springs |
| Lake Basin | `lake` | Adult | North-west plateau with the big lake |

Landmarks you can see from most of the map: the volcano, the waterfall where the river drops out of the Lake
Basin, and the Great Old Tree at the meadow's west edge. Each biome's secret from the design doc has a
`Landmark` marker (`secret_*` ids); the forest and swamp secrets are hidden `Nest`s.

Rules the generator enforces (and the tests check): drinkable water within ~115 m (about 25 s of walking) of
anywhere walkable, a nest within ~210 m and in every biome, plant food and bugs in every biome, fish only in
water, rivers never flow uphill.

To tweak: **Dino > World > Select Island Spec**, change numbers in the inspector, Build again.
`Tools/IslandPreview/island.py` draws the same island top-down without Unity (see its README).

## Gameplay hooks

- Food: `FoodSource` prefabs (berries, leaves, ferns, tall treetop leaves for Brachiosaurus, bug mounds, fish
  spots). Everything registers with `WorldRegistry`.
- Water: `WaterVolume` boxes cover lakes, ponds, puddles, swamp pools and each river stretch. The ocean is a
  swim-only volume (`drinkable = false`).
- Nests: every nest has `Nest` + `PlayerSpawnPoint` (biomeId set). The main hatching nest is `nest_main`.
- Biomes: `BiomeMap.Active.IdAt(position)`, `.InfoAt(position)` (stage gate, thirst multiplier, map colour),
  `.IsOpenFor(position, lifeStage)`, `.mapTexture` + `.ToMapUV(position)` for the pause-screen map.
  Put `BiomeTracker` on the player dino: it applies the Red Highlands thirst multiplier and raises
  `BiomeTracker.BiomeChanged` for music, UI and the Field Guide.
- Landmarks: `Landmark.Find(id)`, `Landmark.At(position)`.

## Making it look realistic (all free)

One click: **Dino > World > Make Island Realistic (download free art, then build)**. It needs internet and
takes a few minutes the first time (downloads are kept, so later runs are quick). It does three things:

1. **Graphics levels** (also at Dino > Graphics > Set Up Quality Tiers): Low and Medium for phones, High for
   PC with long soft shadows, ambient occlusion, anti-aliasing and see-through water. The editor is switched
   to High. Dino > Graphics > Use Medium In Editor shows roughly what a phone gets.
2. **Free photo-scanned art from Poly Haven** (CC0: free for any use, no credit needed): a real sky photo
   that also lights the scene, 2K ground textures, and scanned trees, rocks, logs, shrubs and ferns. Each
   model is split into single objects, scaled, given lighter versions for distance (LODs), wind and a
   collider, then put into the art set. The list of what was used is in `Art/PolyHaven/CREDITS.md`.
3. **Build Island** with all of it, plus blade-by-blade grass that sways, water with depth, reflections and
   flowing rivers, fog and colour grading matched to the sky.

Anything that fails to download keeps its generated placeholder, so the island always builds. The Console
prints a summary of what was used.

Not in git: `World/Art/` (downloads) and `World/Island/Generated/` + `Island.unity` (rebuilt by the menu).
On a fresh clone, run the one-click menu once.

To swap any piece by hand: **Dino > World > Select World Art Set**, drag your own prefab, terrain layer or
material into a slot, Build again. Tree, rock and log prefabs need a `LODGroup` on the root and a
`CapsuleCollider`; food prefabs need a `FoodSource`. Free Unity Asset Store packs are fine to use too.
To change which Poly Haven assets are fetched, edit the lists at the top of
`Scripts/World/Editor/PolyHavenLibrary.cs`.

## Performance budget

`WorldQualityTier` (on the Island root) fits the island to the graphics level:

| | Low | Medium (phone default) | High (PC) |
|---|---|---|---|
| Grass distance / density | 28 m / 25% | 48 m / 50% | 95 m / 100% |
| Trees drawn to | 220 m | 380 m | 1200 m |
| Shadows | 50 m, hard | 110 m, soft | 240 m, soft, 4 cascades |
| Water | flat colour + reflections | depth colour, soft shores | + see-through refraction |
| Ambient occlusion, 4x anti-aliasing | no | no | yes |

- Terrain: 1025 heightmap, 4 ground layers in one pass with height blending.
- Trees, rocks, logs are terrain tree instances (GPU instanced, 3 LODs; Low skips the most detailed one).
- Grass: instanced blade tufts, no shadows.
- Food plants: GameObjects hidden beyond 90 to 320 m by `DistanceCuller`.
- Water: tiled meshes (256 m tiles), one material for inland water and one for the sea.
- Fog (exp², density 0.0019) softens the distance. Post-processing: ACES tonemapping, bloom, grading, vignette.
