# DinoGen: procedural dinosaur models in Blender

Builds a complete game-ready dinosaur from numbers in `specs.py`, at no cost and with no outside services:
sculpted body, teeth, claws, eyes, baked textures, three LODs, a skeleton, ten animation clips and an FBX for Unity.

Tested with Blender 5.2 on Linux (RTX 3060). A full T. rex build takes about one minute.

## Run it

```bash
blender -b -P dinogen.py -- trex all
```

Stages can be run one at a time (each saves a `.blend` in `out/`): `mesh`, `bake`, `rig`, `anim`, `export`, `preview`.
Results land in `out/unity/<id>/` (FBX and textures) and `out/renders/` (preview images).
`DINO_PREVIEW=stills,lods,tints,clips` picks which previews to render, and `./make_video.sh out/renders/clips out/clips.mp4` joins the clip frames into one video (needs ffmpeg).

## How it works

| File | Job |
| --- | --- |
| `specs.py` | One dict per species: body rings, joints, teeth, claws, eye, skin, gaits. |
| `dinogen.py` | Lofts the body parts, fuses them with a voxel remesh, carves eye sockets and nostrils, adds teeth, claws and eyes, and builds the procedural skin shader. |
| `pipeline.py` | Decimates to a low-poly body, unwraps, bakes Albedo / Normal / Mask, builds the skeleton, skins it, makes LOD1 and LOD2, generates the clips and exports the FBX. |
| `previews.py` | Renders stills, LODs, tint tests and clip frames. |

Blender space is Z up, the animal faces -Y, its left side is +X, units are metres and the feet stand on Z = 0.
Body parts are lists of rings `(x, y, z, half_width, half_height)`.

## What comes out (contract agreed with the species code)

- `Assets/_Game/Art/Dinos/<id>/<id>.fbx`, `<id>_Albedo.png`, `<id>_Normal.png`, `<id>_Mask.png` (plus `<id>_Albedo_natural.png` for reference).
- Metres, Y up, facing +Z, origin on the ground between the feet, real adult size (`adultModelScale = 1`).
- Meshes `<id>_LOD0` (about 9k triangles), `_LOD1` (4k), `_LOD2` (1.5k), one material, one 2048 texture set.
- Albedo is neutral (luminance with strong value detail) so `Dino/DinoSkin` can recolour it.
- Mask: R belly, G accent (back ridge, claws, eye ring), B eyes, A pattern area (0 on teeth, claws and eyes).
- Teeth, claws and eyes use small flat-colour patches at the right edge of the texture (u > 0.95).
- Generic rig (not Humanoid): `Root > Hips > Spine1-3 > Neck1-3 > Head > Jaw`, `Hips > Tail1-8`,
  `Spine3 > UpperArm / Forearm / Hand (.L/.R)`, `Hips > Thigh / Shin / Metatarsus / Toe (.L/.R)`.
- Clips: `Idle`, `Walk`, `Run`, `Rest` (loops) and `Attack`, `Ability`, `Eat`, `Drink`, `Hurt`, `Faint` (one-shots).
  Faint ends lying on its side and holds. Clips are in place (no root motion).
  Authored speeds for T. rex: Walk 2.4 m/s over a 1.6 s cycle, Run 7.0 m/s over a 0.9 s cycle.

### Unity import notes (not yet tested in the Unity editor)

- Rig: Generic, root node `Root`. Turn on "Bake Axis Conversion" so the root has no -90 degree rotation.
- The FBX takes are named `Rig|<Clip>`; rename them to the plain clip name in the Animation tab or a postprocessor.
- Loop Time on Idle, Walk, Run and Rest.
- Normal map: mark `<id>_Normal.png` as a normal map (OpenGL / Y+ convention, which is what Unity expects).

## Making another species

Copy the `TREX` dict in `specs.py`, change the rings, joints and skin numbers, and add it to `SPECS`.
All biped hunters share the part list, skeleton and clip code. Quadrupeds and long-necks need front-leg IK and their
own gait added to `pipeline.py` (the same foot-target system, with four feet).

## Known limits

- Shapes are smooth and clean but simpler than a hand sculpt: no individual muscles, skin folds are painted, not sculpted.
- Skin weights are automatic. Extreme poses (the Run thigh, the Faint flop) show some stretching.
- Animations are procedural keyframes, good for a first playable, not a replacement for hand animation.

## Sculpted base bodies (the Carnotaurus route)

Pure code shapes stay clean but toy-like. For more lifelike anatomy a species can set `"base"` in its spec to a
sculpted mesh, and everything else (horns, osteoderm rows, mouth cut, teeth, eyes, claws, skin, bake, rig, clips)
is applied to it unchanged. The Carnotaurus base was made locally and for free:

1. `refrender.py`: render the code-built guide body (`carnotaurus_guide`) with a closed mouth. This fixes pose and proportions.
2. `ai/realify.py`: an SDXL image-to-image pass turns that render into a detailed concept image (`bases/carnotaurus_concept.png`).
3. `ai/gen_triposg.py`: TripoSG (MIT licence, about 6 GB VRAM, 90 seconds) sculpts a mesh from the concept image (`bases/carnotaurus_base.glb`).
4. `dinogen.py` imports it, finds the body axis, straightens the spine sideways, mirrors it, and scales it to real size.
5. `landmarks.py` renders gridded orthographic views for reading joint, mouth and eye coordinates into the spec.

`show.py` opens a finished species in the Blender window and plays its clips; `show_wip.py` opens the sculpt stage.

Licence notes: TripoSG's code and weights are MIT. Its repository notes that parts of its code derive from Tencent
projects, and its default background remover is non-commercial, so `gen_triposg.py` uses rembg/u2net and the standard
decoder instead. The earlier Hunyuan3D test (`ai/gen_shape.py`) is kept only as a record: that model's licence
excludes the EU, UK and South Korea, so its output is not used.
