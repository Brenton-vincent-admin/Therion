"""refrender.py - clean studio render of the procedural body (closed mouth, no teeth) on a transparent background.
Used as the pose/proportion guide for the image-to-image and image-to-3D steps.

    blender -b -P refrender.py -- carnotaurus out/ref/carnotaurus_ref.png
"""
import bpy, sys, os, math, copy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dinogen as dg, specs
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
spec = copy.deepcopy(specs.SPECS[argv[0]])
dst = os.path.abspath(argv[1])
az = float(argv[2]) if len(argv) > 2 else -58
os.makedirs(os.path.dirname(dst), exist_ok=True)
# close the mouth: lift the jaw until it meets the upper head
head, jaw = spec["head_ctrl"], spec["jaw_ctrl"]
gap = max(0.0, min((dg.interp_ctrl(head, j[1])[2] - dg.interp_ctrl(head, j[1])[4]) - (j[2] + j[4]) for j in jaw[2:-1]))
lifted = [(j[0], j[1], j[2] + gap + 0.012 * spec.get("size", 1.0), j[3], j[4]) for j in jaw]
for p in spec["lofts"]:
    if p["name"] == "Jaw":
        p["ctrl"] = lifted
spec["jaw_ctrl"] = lifted
bpy.ops.wm.read_factory_settings(use_empty=True)
high = dg.build_high(spec)
acc = dg.build_accessories(spec, high)
high.data.materials.clear()
high.data.materials.append(dg.skin_material(spec))
claw = bpy.data.materials.new("Claw"); claw.use_nodes = True
claw.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.045, 0.04, 1)
eye = bpy.data.materials.new("Eye"); eye.use_nodes = True
eye.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.12, 0.02, 1)
eye.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.1
for o in acc:
    if o.name.startswith("Teeth"):
        o.hide_render = True
    o.data.materials.append(claw if o.name.startswith("Claws") else eye)
sc = bpy.context.scene
dg.setup_cycles(64)
sc.render.film_transparent = True
w = bpy.data.worlds.new("Studio"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("Key", bpy.data.lights.new("Key", 'SUN'))
sun.data.energy = 2.2; sun.data.angle = math.radians(25)
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
sc.collection.objects.link(sun)
co = [high.matrix_world @ Vector(c) for c in high.bound_box]
mid = sum(co, Vector()) / 8
size = max((max(c[i] for c in co) - min(c[i] for c in co)) for i in range(3))
dg.render_view(dst, target=tuple(mid), dist=size * 2.6, az=az, el=7, lens=85, res=(1280, 896))
