"""show_wip.py - open a species' sculpt stage (1_mesh) in the Blender window: blender -P show_wip.py -- carnotaurus"""
import bpy, os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dinogen as dg, pipeline as pl, specs
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
spec = specs.SPECS[argv[0] if argv else "carnotaurus"]
hide = len(argv) > 1 and argv[1] == "body"
bpy.ops.wm.open_mainfile(filepath=pl.blend_path(spec, os.path.join(HERE, "out"), "1_mesh"), load_ui=False)
sc = bpy.context.scene
if hide:
    for o in bpy.data.objects:
        if o.name.startswith(("Teeth", "Claws", "Eye")):
            o.hide_set(True)
dg.setup_stage()
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try:
        sc.render.engine = eng
        break
    except Exception:
        pass
v = spec["views"]["hero"]
cam = bpy.data.objects.new("ShowCam", bpy.data.cameras.new("ShowCam"))
sc.collection.objects.link(cam)
t = Vector(v["target"]); a, e = math.radians(v["az"]), math.radians(v["el"])
cam.location = t + Vector((v["dist"] * math.cos(e) * math.sin(a), -v["dist"] * math.cos(e) * math.cos(a), v["dist"] * math.sin(e)))
cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 42
sc.camera = cam


def start():
    for w in bpy.context.window_manager.windows:
        for area in w.screen.areas:
            if area.type == 'VIEW_3D':
                sp = area.spaces.active
                sp.shading.type = 'RENDERED'
                sp.overlay.show_overlays = False
                sp.region_3d.view_perspective = 'CAMERA'
                area.header_text_set(f"Therion DinoGen: {spec['id']} sculpt, work in progress (middle mouse orbits)")
    return None


bpy.app.timers.register(start, first_interval=1.5)
