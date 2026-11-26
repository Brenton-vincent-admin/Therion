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

Every art slot is in **Dino > World > Select World Art Set**. Empty slots use generated placeholders, so the
island always builds; fill them and press Build again.

1. **Ground:** **Dino > World > Download Realistic Ground Textures (Poly Haven, CC0)** fetches photo-scanned
   grass, forest floor, sand, mud and rock (1K, compressed for Android) and fills the art set. Poly Haven
   assets are CC0: free for any use, no credit needed.
2. **Trees, plants and rocks:** import a free realistic pack from the Unity Asset Store (Window > Package
   Manager > My Assets after adding it to your account; free Asset Store packs may be used in your own game),
   then drag its prefabs into the matching art-set slots (`Tree`, `Pine`, `SwampTree`, `Rock`, `Log`, and the
   food slots `BerryBush`, `LeafShrub`, `Fern`, `TallLeafTree`). Good free options: Unity's own
   **Terrain Sample Asset Pack**, and **Nature Starter Kit 2**. Food prefabs need a `FoodSource` component
   (copy one from the placeholders in `Generated/Placeholder`). Tree, rock and log prefabs must have a
   `LODGroup` or `MeshRenderer` on the root and a `CapsuleCollider` (terrain trees only use capsules).
3. **Grass:** put a grass mesh prefab in `grassDetail`.
4. **Sky and water:** optional custom `skybox`, `water`, `ocean` materials.

Keep phones smooth: prefer assets with 2-3 LODs, textures at 1K, and under ~5k triangles for trees.

## Performance budget (mid-range Android, 30 fps)

- Terrain: 1025 heightmap, pixel error 6, 4 ground layers (one pass; a 5th "forest floor" layer adds a pass).
- Trees, rocks, logs are terrain tree instances (GPU instanced, LOD'd, culled at 320 m).
- Grass: instanced detail meshes, 55 m draw distance.
- Food: plain GameObjects whose renderers are hidden beyond 110 m by `DistanceCuller` (CullingGroup).
- Water: tiled meshes (256 m tiles, frustum culled), one shared material.
- Fog (exp², density 0.0024) hides the far edge; set the game camera's far clip to ~800 m.
- Post-processing: ACES tonemapping, light bloom, colour grading. Tick "Post Processing" on the game camera.
