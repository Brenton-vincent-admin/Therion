"""show.py - open a finished dinosaur in the Blender window and play its clips in turn.

    blender -P show.py -- trex
"""
import bpy, os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dinogen as dg, pipeline as pl, specs
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
spec = specs.SPECS[argv[0] if argv else "trex"]
out = os.path.join(HERE, "out")
bpy.ops.wm.open_mainfile(filepath=pl.blend_path(spec, out, "4_anim"), load_ui=False)
sc = bpy.context.scene
rig = bpy.data.objects["Rig"]
sid = spec["id"]
for k in ("LOD1", "LOD2"):
    bpy.data.objects[f"{sid}_{k}"].hide_set(True)
rig.hide_set(True)
dg.setup_stage()
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try:
        sc.render.engine = eng
        break
    except Exception:
        pass
sc.render.fps = pl.FPS
v = spec["views"]["hero"]
cam = bpy.data.objects.new("ShowCam", bpy.data.cameras.new("ShowCam"))
sc.collection.objects.link(cam)
t = Vector(v["target"])
a, e = math.radians(v["az"]), math.radians(v["el"])
cam.location = t + Vector((v["dist"] * math.cos(e) * math.sin(a), -v["dist"] * math.cos(e) * math.cos(a), v["dist"] * math.sin(e)))
cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 42
sc.camera = cam

ORDER = [("Idle", 1), ("Walk", 4), ("Run", 6), ("Attack", 2), ("Ability", 1), ("Eat", 1), ("Drink", 1),
         ("Rest", 1), ("Hurt", 3), ("Faint", 1)]
state = {"i": -1}


def next_clip():
    state["i"] = (state["i"] + 1) % len(ORDER)
    name, reps = ORDER[state["i"]]
    act = pl.set_action(rig, name)
    if act is None:
        return 1.0
    n = int(act["frames"])
    sc.frame_start, sc.frame_end = 0, n - (1 if name in ("Idle", "Walk", "Run", "Rest") else 0)
    sc.frame_current = 0
    for w in bpy.context.window_manager.windows:
        for area in w.screen.areas:
            if area.type == 'VIEW_3D':
                area.header_text_set(f"Therion DinoGen: {sid}   clip: {name}   (Space pauses, middle mouse orbits)")
    return reps * n / pl.FPS + 0.3


def start():
    for w in bpy.context.window_manager.windows:
        for area in w.screen.areas:
            if area.type != 'VIEW_3D':
                continue
            sp = area.spaces.active
            sp.shading.type = 'RENDERED'
            sp.overlay.show_overlays = False
            sp.region_3d.view_perspective = 'CAMERA'
            region = next(r for r in area.regions if r.type == 'WINDOW')
            with bpy.context.temp_override(window=w, area=area, region=region):
                try:
                    bpy.ops.view3d.view_center_camera()
                except Exception:
                    pass
                if not bpy.context.screen.is_animation_playing:
                    bpy.ops.screen.animation_play()
    bpy.app.timers.register(next_clip, first_interval=0.1)
    return None


bpy.app.timers.register(start, first_interval=1.5)
