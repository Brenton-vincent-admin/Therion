# Realistic dinosaur models: where to get them legally

Brenton's direction (2026-11-26): 3D and as realistic-looking as possible. Budget: $0. Cheap paid options are listed
separately so he can decide.

## The short version
- Only about **3 of the 13** species have a free, realistic, rigged model whose license is actually clean.
- Most "free realistic" dinosaurs on Sketchfab are **ripped from commercial games** (Jurassic World Evolution, ARK,
  Primal Carnage, Dino Hunter, Jurassic World Alive, Carnivores...). The uploader doesn't own them, so the CC license
  shown is not valid. **Never use these, whatever license they show.**
- The cheapest legal way to cover most of the roster is a paid set (Romandy packs, about $30 each, mobile-friendly).
- The code doesn't care where models come from: drop a prefab into `SpeciesDefinition.modelPrefab`. Until then each
  species shows a placeholder, so nothing is blocked.

## Free options with a clean origin

| Species | Model | License | Notes |
|---|---|---|---|
| Velociraptor | absol_cg "Velociraptor" (Sketchfab) | CC-BY 4.0 (credit needed) | Original ZBrush/Substance work, 33k faces (needs decimating + LODs), 5 animations |
| Velociraptor, Pachycephalosaurus | Ferocious Industries "PBR Animated Dinosaurs" (Unity Asset Store, free) | Unity Asset Store EULA | Built-in pipeline materials, convert to URP. Raptor 15k tris / 26 anims, Pachy 26k faces / 21 anims. Exact species list unverified |
| Triceratops | victory_ "Triceratops" (Sketchfab) | CC-BY | 53k faces (heavy for phones), 5 animations. Origin not fully verified |
| T. rex, Triceratops, Stegosaurus, Velociraptor, Apatosaurus | Quaternius Animated LowPoly Dinosaurs | CC0 | **Not realistic.** Fallback/stand-in only |

Not free-and-clean for: Parasaurolophus, Psittacosaurus, Stegosaurus, Brachiosaurus, Oviraptor, Troodon,
Microraptor, Dilophosaurus, T. rex (realistic). Gallimimus has only a weak free model (author calls it low quality).

## Decision (2026-11-26): $0 permanently
Brenton chose to stay at $0 for good, so nothing below this section will be bought. The plan:
1. Use the three free, clean realistic models (absol_cg Velociraptor, Ferocious Industries Raptor + Pachy, victory_ Triceratops), decimated to phone budgets with LODs, materials moved to `Dino/DinoSkin`.
2. Quaternius CC0 models stand in for T. rex, Stegosaurus and Brachiosaurus (Apatosaurus) until a free realistic option appears.
3. Everything else stays a placeholder for now. Free routes to fill gaps later: free-tier AI 3D generators (credit required under CC-BY, check terms first), community CC0/CC-BY releases (re-check origin every time), or a volunteer/family-made Blender model.

## Cheap paid options (for reference only; not being bought)

| Pack | Price | Covers | Notes |
|---|---|---|---|
| Romandy Development "Low Poly + Realistic Dinosaurs - Animated" | **$29.99** (Superhive; also on Fab) | T. rex, Triceratops, Stegosaurus, Brachiosaurus, Velociraptor, Parasaurolophus (+5 others) | Realistic PBR versions 2.7k-5.9k tris, 2K textures, shared rig, Attack/Death/Eat/Idle/Run/Walk. Best fit for phones; looks closer to "stylized-realistic" than film-quality |
| Romandy "...Dinosaurs II" (Fab) | price not shown | adds Dilophosaurus, Pachycephalosaurus | Same specs |
| Muelsa "JP" series (Unity Asset Store) | **$19.90 each** | Oviraptor, Gallimimus, Troodon (also Parasaur, Pachy, Raptor) | Realistic film style, LODs, feathers absent. Needs URP material conversion |
| CGTrader Microraptor (3dlowpoly) | $18 | Microraptor | Royalty-free, 50k polys, no rig stated |

Suggested if buying: Romandy I + II (~8 species), then Muelsa Oviraptor, Gallimimus, Troodon ($59.70). That still
leaves **Psittacosaurus and Microraptor**, which would need a commissioned model (often $50-200) or a static model
rigged in Blender.

## License rules for this project
- OK: CC0, CC-BY (add to `CREDITS.md`), Unity Asset Store EULA, Fab Standard License (check the listing isn't
  "Reference Only" or "Legacy UE Marketplace").
- Avoid: CC-NC / CC-ND, "editorial only", anything game-ripped, AI tools' free tiers that require credit unless credited.
- Every asset goes in `CREDITS.md` with source URL, author, license and date, from day one.

## Making realistic models run on phones
- Target per adult dino: **8k-15k triangles** for LOD0 (realistic needs more than low-poly), LOD1 ~50%, LOD2 ~20%,
  cull beyond ~120 m. About 20 dinos on screen.
- Textures: **1024 px** albedo + normal (2K only for the player's dino if a phone can take it), ASTC compression.
  One material per dino.
- Use the `Dino/DinoSkin` shader: it keeps the model's realistic texture detail and recolors it by zone, so skins
  still work. Each model needs a **zone mask** painted on its UVs (R belly, G accent, B eyes, A pattern area);
  a 256-512 px mask is enough. Set Tint Strength to 0 to see the original texture.
- URP mobile: one real-time shadow, baked lighting, GPU skinning, Animator culling "Cull Update Transforms".
- Animations needed per species: idle, walk, run, eat, drink, attack, special, hurt, rest, faint. Packs usually have
  idle/walk/run/attack/eat/death; drink/rest/special can be retimed from eat/idle at first.

## Carnotaurus (added 2026-11-26)
Brenton wants a Carnotaurus and is having an original, realistic model built in Blender on his PC (biped-hunter base
shared with the T. rex). Id `carnotaurus`, files under `Assets/_Game/Art/Dinos/carnotaurus/`, real size 8 m long, 3 m
head height. The model must be our own design: reference photos of film or game dinosaurs are for anatomy and mood
only, never traced or copied (no film-specific horn shapes, color schemes or scar patterns).
