"""landmarks.py - gridded orthographic views of a sculpt for reading joint / mouth / eye coordinates.
    blender -b -P landmarks.py -- carnotaurus      (reads out/<id>_1_mesh.blend, writes out/landmarks/*.png)"""
import bpy, sys, os, math, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import dinogen as dg, pipeline as pl, specs
argv = sys.argv[sys.argv.index("--") + 1:]
spec = specs.SPECS[argv[0]]
show_acc = len(argv) > 1
out = os.path.join(HERE, "out", "landmarks"); os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=pl.blend_path(spec, os.path.join(HERE, "out"), "1_mesh"))
h = bpy.data.objects["High"]
clay = bpy.data.materials.new("Clay"); clay.use_nodes = True
clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.6, 0.6, 0.6, 1)
h.data.materials.clear(); h.data.materials.append(clay)
for o in bpy.data.objects:
    if o.name.startswith(("Teeth", "Claws", "Eye")):
        o.hide_render = not show_acc
sc = bpy.context.scene
dg.setup_cycles(24)
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.6
cam = bpy.data.objects.new("C", bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 100
W, H = 1500, 1000
sc.render.resolution_x, sc.render.resolution_y = W, H
FONT = "/usr/share/fonts/liberation/LiberationSans-Bold.ttf"


def shot(name, kind, cu, cv, width, step):
    """kind side: u=-y (snout right... image right = +u), v=z ; top: u=-y, v=x ; front: u=x, v=z"""
    for l in [o for o in bpy.data.objects if o.type == 'LIGHT']:
        bpy.data.objects.remove(l, do_unlink=True)
    sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 3
    cam.data.ortho_scale = width
    height = width * H / W
    if kind == "side":      # camera on the animal's left (+X), looking -X; image right = -Y (forward)
        cam.location = (-30, -cu, cv); cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
        sun.rotation_euler = (math.radians(55), 0, math.radians(60))
        ulab, vlab = "y", "z"; usign = -1
    elif kind == "top":     # looking down; image right = -Y... rotate so forward is right
        cam.location = (cv, -cu, 30); cam.rotation_euler = (0, 0, math.radians(-90))
        sun.rotation_euler = (math.radians(20), 0, 0)
        ulab, vlab = "y", "x"; usign = -1
    else:                   # front: camera at -Y looking +Y; image right = +X?  (camera right = +X when looking +Y)
        cam.location = (cu, -30, cv); cam.rotation_euler = (math.radians(90), 0, 0)
        sun.rotation_euler = (math.radians(55), 0, math.radians(-20))
        ulab, vlab = "x", "z"; usign = 1
    path = os.path.join(out, f"{spec['id']}_{name}.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    px = W / width
    cmd = ["magick", path, "-font", FONT, "-pointsize", "17"]
    import numpy as np
    u0, u1 = cu - width / 2, cu + width / 2
    v0, v1 = cv - height / 2, cv + height / 2
    for u in np.arange(math.ceil(u0 / step) * step, u1, step):
        x = (u - u0) * px
        val = usign * u
        cmd += ["-stroke", "#ff000080", "-strokewidth", "1", "-draw", f"line {x:.0f},0 {x:.0f},{H}",
                "-stroke", "none", "-fill", "blue", "-annotate", f"+{x + 2:.0f}+16", f"{ulab}={val:.2f}"]
    for v in np.arange(math.ceil(v0 / step) * step, v1, step):
        y = (v1 - v) * px
        vv = v if kind != "top" else v
        cmd += ["-stroke", "#ff000080", "-strokewidth", "1", "-draw", f"line 0,{y:.0f} {W},{y:.0f}",
                "-stroke", "none", "-fill", "blue", "-annotate", f"+2+{y - 3:.0f}", f"{vlab}={vv:.2f}"]
    subprocess.run(cmd + [path])
    print("LM", path, flush=True)


k = spec.get("size", 1.0)
hb = spec["head_bone"]
hy = -0.5 * (hb[0][1] + hb[1][1]); hz = hb[0][2]
shot("side_all", "side", -0.6, 2.2 * k, 14 * k, 0.5)
shot("side_head", "side", hy, hz + 0.05, 2.4 * k, 0.1)
shot("top_head", "top", hy, 0.0, 2.4 * k, 0.1)
shot("front", "front", 0.0, 2.2 * k, 6.5 * k, 0.2)
shot("side_legs", "side", 0.1, 1.7 * k, 5.2 * k, 0.2)
