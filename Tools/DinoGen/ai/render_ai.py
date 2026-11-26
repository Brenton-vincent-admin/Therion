import bpy, sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import dinogen as dg, specs
from mathutils import Vector, Matrix
argv = sys.argv[sys.argv.index("--") + 1:]
src, outdir, yaw = argv[0], argv[1], float(argv[2])
mode = argv[3] if len(argv) > 3 else "clay"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
dg.activate(ob)
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
ob.rotation_mode = 'XYZ'
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
ob.rotation_euler = (0, 0, math.radians(yaw))
bpy.ops.object.transform_apply(rotation=True)
bb = [Vector(c) for c in ob.bound_box]
mn = Vector((min(c.x for c in bb), min(c.y for c in bb), min(c.z for c in bb)))
mx = Vector((max(c.x for c in bb), max(c.y for c in bb), max(c.z for c in bb)))
print("DIMS", mx - mn, flush=True)
s = 12.4 / (mx.y - mn.y)
ob.scale = (s, s, s)
bpy.ops.object.transform_apply(scale=True)
bb = [Vector(c) for c in ob.bound_box]
ob.location = (-(min(c.x for c in bb) + max(c.x for c in bb)) / 2, -(min(c.y for c in bb) + 5.5 * 0 + max(c.y for c in bb)) / 2 + 0.7, -min(c.z for c in bb))
bpy.ops.object.transform_apply(location=True)
for p in ob.data.polygons:
    p.use_smooth = True
print("TRIS", sum(len(p.vertices) - 2 for p in ob.data.polygons), flush=True)
ob.data.materials.clear()
if mode == "skin":
    ob.data.materials.append(dg.skin_material(specs.SPECS["trex"]))
else:
    m = bpy.data.materials.new("Clay"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.5, 0.45, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.7
    ob.data.materials.append(m)
dg.setup_cycles(48); dg.setup_stage()
V = specs.SPECS["trex"]["views"]
for name in ("hero", "side", "head", "front"):
    v = dict(V[name])
    if name == "head":
        v = dict(target=(0, -4.6, 3.4), dist=5.0, az=-48, el=4, lens=60)
    dg.render_view(os.path.join(outdir, f"ai_{mode}_{name}.png"), **v)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(outdir, "ai_trex.blend"))
