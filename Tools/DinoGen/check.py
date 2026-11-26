"""check.py - quick frames of chosen clips: blender -b -P check.py -- <species> Walk:0,9 Run:5 rest:hero,head"""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dinogen as dg, pipeline as pl, specs
argv = sys.argv[sys.argv.index("--") + 1:]
spec = specs.SPECS[argv[0]]
sid = spec["id"]; k = spec.get("size", 1.0)
out = os.path.join(dg.HERE, "out")
bpy.ops.wm.open_mainfile(filepath=pl.blend_path(spec, out, "4_anim"))
rig = bpy.data.objects["Rig"]
for key in ("LOD1", "LOD2"):
    bpy.data.objects[f"{sid}_{key}"].hide_render = True
dg.setup_cycles(20); dg.setup_stage()
view = dict(target=(0, 0.6 * k, 2.2 * k), dist=17.5 * k, az=-62, el=7, lens=50)
os.makedirs(os.path.join(out, "check"), exist_ok=True)
for a in argv[1:]:
    name, frames = a.split(":")
    if name == "rest":
        pl.set_action(rig, None)
        for vname in frames.split(","):
            dg.render_view(os.path.join(out, "check", f"{sid}_rest_{vname}.png"), res=(960, 600), **spec["views"][vname])
        continue
    pl.set_action(rig, name)
    for f in frames.split(","):
        bpy.context.scene.frame_set(int(f))
        dg.render_view(os.path.join(out, "check", f"{sid}_{name}_{int(f):03d}.png"), res=(640, 400), **view)
