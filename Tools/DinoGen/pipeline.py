"""
pipeline.py - turns the sculpted high-poly from dinogen.py into a game asset:
  bake  : low-poly body + UVs + baked Albedo / Normal / Mask textures
  rig   : skeleton, skin weights, teeth/claws/eyes, LOD1 + LOD2
  anim  : procedural clips (Idle, Walk, Run, Attack, Ability, Eat, Drink, Rest, Hurt, Faint)
  export: FBX + textures for Unity, preview renders
"""
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion
import dinogen as dg

log = dg.log
PATCH = {"teeth": 0.1, "claws": 0.3, "iris": 0.5, "pupil": 0.7, "gum": 0.9}   # v of flat colour patches at u=0.975
PATCH_RGB = {"teeth": (0.80, 0.74, 0.58), "claws": (0.10, 0.09, 0.08), "iris": (0.85, 0.55, 0.12),
             "pupil": (0.01, 0.01, 0.01), "gum": (0.35, 0.12, 0.10)}
UV_SCALE = 0.94


def blend_path(spec, out, tag):
    return os.path.join(out, f"{spec['id']}_{tag}.blend")


# ----------------------------------------------------------------------------
# bake
# ----------------------------------------------------------------------------
def decimate_to(obj, tris, group=None):
    dg.activate(obj)
    m = obj.modifiers.new("Tri", 'TRIANGULATE')
    dg.apply_modifier(obj, m)
    n = len(obj.data.polygons)
    if n <= tris:
        return
    m = obj.modifiers.new("Dec", 'DECIMATE')
    m.ratio = tris / n
    if group:
        m.vertex_group = group
        m.vertex_group_factor = 1.0
    dg.apply_modifier(obj, m)
    log(obj.name, "tris:", len(obj.data.polygons))


def img_get(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def img_set(img, a):
    img.pixels.foreach_set(a.astype(np.float32).ravel())
    img.update()


def img_save(img, path):
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    log("saved", path)


def paint_patches(a, colors):
    """colors: {patch: rgba}; paints the flat colour strip at the right edge."""
    h, w, _ = a.shape
    x0 = int(w * (UV_SCALE + 0.01))
    a[:, x0:, :] = (0.5, 0.5, 0.5, 1)
    for name, v in PATCH.items():
        if name in colors:
            y0, y1 = int(h * (v - 0.1)), int(h * (v + 0.1))
            a[y0:y1, x0:, :] = colors[name]
    return a


def stage_bake(spec, out):
    bpy.ops.wm.open_mainfile(filepath=blend_path(spec, out, "1_mesh"))
    sc = bpy.context.scene
    high = bpy.data.objects["High"]
    acc = [o for o in bpy.data.objects if o.name.startswith(("Teeth", "Claws", "Eye"))]
    acc_tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in acc)

    # --- low poly: keep more triangles on head, hands and feet ------------------
    low = dg.duplicate(high, "Body")
    low.data.materials.clear()
    vg = low.vertex_groups.new(name="Coarse")
    co = np.empty(len(low.data.vertices) * 3, dtype=np.float32)
    low.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    K = spec.get("size", 1.0)
    head_y = spec["head_bone"][0][1] + 0.3 * K
    w = np.ones(len(co), dtype=np.float32)
    wrist = np.array(spec["joints"]["wrist"])
    hand = np.linalg.norm(np.abs(co) * (1, 0, 0) + co * (0, 1, 1) - wrist, axis=1) < 0.45 * K
    detail = (co[:, 1] < head_y) | (co[:, 2] < 0.95 * K) | hand
    fine = np.zeros(len(co), dtype=bool)        # horns and other accent shapes keep the most triangles
    for (pt, rad) in spec["skin"].get("accent_points", []):
        fine |= np.linalg.norm(np.abs(co) * (1, 0, 0) + co * (0, 1, 1) - np.array(pt), axis=1) < rad * 1.6
    w[detail] = 0.25
    w[fine] = 0.08
    for i, x in enumerate(w):
        vg.add([i], float(x), 'REPLACE')
    decimate_to(low, spec["tris"]["LOD0"] - acc_tris, "Coarse")
    low.vertex_groups.remove(low.vertex_groups["Coarse"])
    for p in low.data.polygons:
        p.use_smooth = True

    # --- UVs -----------------------------------------------------------------------
    dg.activate(low)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(72), island_margin=0.003, area_weight=0.0)
    bpy.ops.uv.select_all(action='SELECT')
    try:
        bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    except Exception as ex:
        log("pack_islands", ex)
    bpy.ops.object.mode_set(mode='OBJECT')
    uv = low.data.uv_layers.active
    uvs = np.empty(len(uv.data) * 2, dtype=np.float32)
    uv.data.foreach_get("uv", uvs)
    uv.data.foreach_set("uv", uvs * UV_SCALE)
    uv.name = "UV0"

    # --- bake targets --------------------------------------------------------------
    res = spec.get("texture", 2048)
    imgs = {}
    for name, cs in (("Albedo", "sRGB"), ("Normal", "Non-Color"), ("AO", "Non-Color"), ("Mask", "Non-Color")):
        im = bpy.data.images.new(f"{spec['id']}_{name}", res, res, alpha=True)
        im.colorspace_settings.name = cs
        imgs[name] = im
    mat = bpy.data.materials.new(spec["id"] + "_Mat")
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.name = "BakeTarget"
    nt.nodes.active = tex
    low.data.materials.append(mat)

    dg.setup_cycles(16)
    for o in acc:
        o.hide_render = True
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    kw = dict(use_selected_to_active=True, cage_extrusion=0.06 * K, max_ray_distance=0.2 * K, margin=12)

    def bake(name, **extra):
        tex.image = imgs[name]
        nt.nodes.active = tex
        bpy.ops.object.bake(**kw, **extra)
        log("baked", name)

    bake("Albedo", type='DIFFUSE', pass_filter={'COLOR'})
    bake("Normal", type='NORMAL', normal_space='TANGENT')
    sc.cycles.samples = 96
    bake("AO", type='AO')
    sc.cycles.samples = 16
    hm = high.data.materials[0]
    outn = next(n for n in hm.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
    old = outn.inputs[0].links[0].from_socket
    hm.node_tree.links.new(hm.node_tree.nodes["MaskEmit"].outputs[0], outn.inputs[0])
    bake("Mask", type='EMIT')
    hm.node_tree.links.new(old, outn.inputs[0])

    # --- compose textures ------------------------------------------------------------
    tdir = os.path.join(out, "unity", spec["id"])
    os.makedirs(tdir, exist_ok=True)
    alb = img_get(imgs["Albedo"])
    ao = img_get(imgs["AO"])[..., :1]
    alb[..., :3] *= (0.25 + 0.75 * ao)
    alb[..., 3] = 1
    nat = paint_patches(alb.copy(), {k: (*v, 1) for k, v in PATCH_RGB.items()})
    natural = bpy.data.images.new(f"{spec['id']}_AlbedoNatural", res, res, alpha=False)
    img_set(natural, nat)
    img_save(natural, os.path.join(tdir, f"{spec['id']}_Albedo_natural.png"))
    # neutral version for the recolour shader: luminance only, stretched for strong value detail
    lum = (alb[..., :3] * (0.2126, 0.7152, 0.0722)).sum(-1)
    lo, hi = np.percentile(lum, 1), np.percentile(lum, 99.5)
    lum = np.clip((lum - lo) / (hi - lo), 0, 1) * 0.80 + 0.08
    neu = np.dstack([lum, lum * 0.985, lum * 0.96, np.ones_like(lum)])
    neu = paint_patches(neu, {k: (*v, 1) for k, v in PATCH_RGB.items()})
    img_set(imgs["Albedo"], neu)
    img_save(imgs["Albedo"], os.path.join(tdir, f"{spec['id']}_Albedo.png"))
    nrm = paint_patches(img_get(imgs["Normal"]), {k: (0.5, 0.5, 1, 1) for k in PATCH})
    nrm[..., 3] = 1
    img_set(imgs["Normal"], nrm)
    img_save(imgs["Normal"], os.path.join(tdir, f"{spec['id']}_Normal.png"))
    # mask: R belly, G accent (ridge, claws, eye ring), B eyes, A pattern area (0 on teeth/claws/eyes/mouth)
    msk = img_get(imgs["Mask"])
    msk[..., 2] = 0
    msk[..., 3] = 1
    msk = paint_patches(msk, {"teeth": (0, 0, 0, 0), "claws": (0, 1, 0, 0), "iris": (0, 0, 1, 0),
                              "pupil": (0, 0, 1, 0), "gum": (0, 0, 0, 0)})
    x0 = int(res * (UV_SCALE + 0.01))
    msk[:, x0:, 3] = 0
    img_set(imgs["Mask"], msk)
    imgs["Mask"].scale(res // 2, res // 2)
    img_save(imgs["Mask"], os.path.join(tdir, f"{spec['id']}_Mask.png"))

    # --- game material (for previews; Unity gets its own) --------------------------------
    nt.nodes.remove(tex)
    bsdf = nt.nodes["Principled BSDF"]
    ta = nt.nodes.new("ShaderNodeTexImage")
    ta.name = "Albedo"
    ta.image = natural
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.name = "Normal"
    tn.image = imgs["Normal"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ta.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs[0], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.6
    for im in (natural, imgs["Albedo"], imgs["Normal"], imgs["Mask"]):
        im.pack()

    # --- accessories: flat-colour UVs, same material -----------------------------------------
    for o in acc:
        o.hide_render = False
        me = o.data
        me.materials.clear()
        me.materials.append(mat)
        uvl = me.uv_layers.new(name="UV0")
        look = Vector(o["look"]) if "look" in o else None
        for p in me.polygons:
            if o.name.startswith("Teeth"):
                key = "teeth"
            elif o.name.startswith("Claws"):
                key = "claws"
            else:
                key = "pupil" if p.normal.dot(look) > 0.93 else "iris"
            for li in p.loop_indices:
                uvl.data[li].uv = (0.975, PATCH[key])
    bpy.data.objects.remove(high, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path(spec, out, "2_baked"))


# ----------------------------------------------------------------------------
# rig
# ----------------------------------------------------------------------------
def mirror(p):
    return (-p[0], p[1], p[2])


def build_armature(spec):
    arm = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    dg.activate(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.edit_bones
    body = [p["ctrl"] for p in spec["lofts"] if p["name"] == "Body"][0]

    def cz(y):
        return float(dg.interp_ctrl(body, y)[2])

    def bone(name, head, tail, parent=None, connect=False, deform=True):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        b.roll = 0
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        b.use_deform = deform
        return b

    bone("Root", (0, 0, 0), (0, 0, 0.6), deform=False)
    sy = spec["spine_y"]
    bone("Hips", (0, sy[0], cz(sy[0])), (0, sy[1], cz(sy[1])), "Root")
    prev = "Hips"
    for i in range(1, len(sy) - 1):
        bone(f"Spine{i}", (0, sy[i], cz(sy[i])), (0, sy[i + 1], cz(sy[i + 1])), prev, True)
        prev = f"Spine{i}"
    chest = prev
    ys = [sy[-1]] + spec["neck_y"]
    for i in range(len(ys) - 1):
        bone(f"Neck{i + 1}", (0, ys[i], cz(ys[i])), (0, ys[i + 1], cz(ys[i + 1])), prev, True)
        prev = f"Neck{i + 1}"
    bone("Head", *spec["head_bone"], prev)
    bone("Jaw", *spec["jaw_bone"], "Head")
    T = spec["tail"]
    ty = np.linspace(T["y0"], T["y1"], T["bones"] + 1)
    prev = "Hips"
    for i in range(T["bones"]):
        bone(f"Tail{i + 1}", (0, ty[i], cz(ty[i])), (0, ty[i + 1], cz(ty[i + 1])), prev, i > 0)
        prev = f"Tail{i + 1}"
    J = spec["joints"]
    for side, f in (("L", lambda p: p), ("R", mirror)):
        bone("Thigh." + side, f(J["hip"]), f(J["knee"]), "Hips")
        bone("Shin." + side, f(J["knee"]), f(J["ankle"]), "Thigh." + side, True)
        bone("Metatarsus." + side, f(J["ankle"]), f(J["ball"]), "Shin." + side, True)
        bone("Toe." + side, f(J["ball"]), f(J["toe"]), "Metatarsus." + side, True)
        bone("UpperArm." + side, f(J["shoulder"]), f(J["elbow"]), chest)
        bone("Forearm." + side, f(J["elbow"]), f(J["wrist"]), "UpperArm." + side, True)
        bone("Hand." + side, f(J["wrist"]), f(J["finger"]), "Forearm." + side, True)
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


def fix_head_weights(body, spec):
    """Mouth must open cleanly: in front of the mouth corner, upper = Head, lower = Jaw."""
    head, jaw = spec["head_ctrl"], spec["jaw_ctrl"]
    split = spec["mouth_split_y"]
    gh, gj = body.vertex_groups["Head"], body.vertex_groups["Jaw"]
    others = [g for g in body.vertex_groups if g.name not in ("Head", "Jaw")]
    n = 0
    for v in body.data.vertices:
        y = v.co.y
        if y > split + 0.25:
            continue
        h, j = dg.interp_ctrl(head, y), dg.interp_ctrl(jaw, y)
        zm = 0.5 * ((h[2] - h[4]) + (j[2] + j[4]))
        lower = v.co.z < zm
        f = min(1.0, max(0.0, (split + 0.25 - y) / 0.25))     # 0 at the hinge side, 1 in front
        if f >= 1.0:
            for g in others:
                g.remove([v.index])
            (gj if lower else gh).add([v.index], 1.0, 'REPLACE')
            (gh if lower else gj).remove([v.index])
        else:
            (gj if lower else gh).add([v.index], f * 2.0, 'ADD')
        n += 1
    log("head/jaw weights fixed on", n, "verts")


def stage_rig(spec, out):
    bpy.ops.wm.open_mainfile(filepath=blend_path(spec, out, "2_baked"))
    body = bpy.data.objects["Body"]
    acc = [o for o in bpy.data.objects if o.name.startswith(("Teeth", "Claws", "Eye"))]
    rig = build_armature(spec)

    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    weighted = sum(1 for v in body.data.vertices if v.groups)
    log("auto weights:", weighted, "/", len(body.data.vertices))
    if weighted < len(body.data.vertices):
        # fallback for stray vertices: nearest bone
        bones = [(b.name, b.head_local.copy(), b.tail_local.copy()) for b in rig.data.bones if b.use_deform]
        for v in body.data.vertices:
            if v.groups:
                continue
            best = min(bones, key=lambda b: (v.co - (b[1] + b[2]) * 0.5).length)
            body.vertex_groups[best[0]].add([v.index], 1.0, 'REPLACE')
    fix_head_weights(body, spec)
    dg.activate(body)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

    sid = spec["id"]
    # LOD2 from the bare body, LOD0 = body + teeth/claws/eyes, LOD1 = decimated LOD0
    lod2 = dg.duplicate(body, sid + "_LOD2")
    for o in acc:
        o.parent = rig
        m = o.modifiers.new("Armature", 'ARMATURE')
        m.object = rig
    lod0 = dg.join([body] + acc, sid + "_LOD0")
    lod1 = dg.duplicate(lod0, sid + "_LOD1")
    for ob, key in ((lod1, "LOD1"), (lod2, "LOD2")):
        ob.parent = rig
        for m in list(ob.modifiers):
            ob.modifiers.remove(m)
        decimate_to(ob, spec["tris"][key])
        m = ob.modifiers.new("Armature", 'ARMATURE')
        m.object = rig
    for ob in (lod0, lod1, lod2):
        for p in ob.data.polygons:
            p.use_smooth = True
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        log(ob.name, "tris", tris, "verts", len(ob.data.vertices))
        ob["tris"] = tris
    bpy.ops.wm.save_as_mainfile(filepath=blend_path(spec, out, "3_rigged"))


# ----------------------------------------------------------------------------
# animation
# ----------------------------------------------------------------------------
FPS = 30


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def curve(t, keys):
    """Smooth interpolation through (time, value) keys."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            return v0 + (v1 - v0) * smooth((t - t0) / (t1 - t0))
    return keys[-1][1]


def rot_x(v, deg):
    a = math.radians(deg)
    return Vector((v.x, v.y * math.cos(a) - v.z * math.sin(a), v.y * math.sin(a) + v.z * math.cos(a)))


class Animator:
    def __init__(self, rig, spec):
        self.rig, self.spec = rig, spec
        self.k = spec.get("size", 1.0)      # clip distances are written for the T. rex and scaled by this
        self.ks = self.k
        J = spec["joints"]
        self.J = {k: Vector(v) for k, v in J.items()}
        self.targets = {}
        self.rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.setup_ik()

    def empty(self, name, loc):
        e = bpy.data.objects.new(name, None)
        e.location = loc
        bpy.context.scene.collection.objects.link(e)
        return e

    def setup_ik(self):
        rig, J = self.rig, self.J
        for side, sx in (("L", 1), ("R", -1)):
            f = lambda p: Vector((p.x * sx, p.y, p.z))
            t = {"ankle": self.empty("IK_Ankle." + side, f(J["ankle"])),
                 "ball": self.empty("IK_Ball." + side, f(J["ball"])),
                 "tip": self.empty("IK_Tip." + side, f(J["toe"])),
                 "pole": self.empty("IK_Pole." + side, f(J["knee"]) + Vector((sx * 0.25, -4.0, 0.6)) * self.k)}
            self.targets[side] = t
            pb = rig.pose.bones["Shin." + side]
            c = pb.constraints.new('IK')
            c.target, c.chain_count, c.pole_target = t["ankle"], 2, t["pole"]
            best = (1e9, 0)
            knee = f(J["knee"])
            for ang in range(-180, 180, 15):
                c.pole_angle = math.radians(ang)
                bpy.context.view_layer.update()
                d = (rig.pose.bones["Shin." + side].head - knee).length
                best = min(best, (d, ang))
            for ang in range(best[1] - 14, best[1] + 15):
                c.pole_angle = math.radians(ang)
                bpy.context.view_layer.update()
                d = (rig.pose.bones["Shin." + side].head - knee).length
                best = min(best, (d, ang))
            c.pole_angle = math.radians(best[1])
            log("pole angle", side, best)
            c = rig.pose.bones["Metatarsus." + side].constraints.new('IK')
            c.target, c.chain_count = t["ball"], 1
            c = rig.pose.bones["Toe." + side].constraints.new('DAMPED_TRACK')
            c.target, c.track_axis = t["tip"], 'TRACK_Y'

    def foot(self, side, ball=None, heel=0.0, toe=0.0, xf=None):
        """Place a foot. ball: rest-relative offset (x,y,z); heel/toe: pitch degrees."""
        sx = 1 if side == "L" else -1
        J = self.J
        f = lambda p: Vector((p.x * sx, p.y, p.z))
        b = f(J["ball"]) + (Vector(ball) * self.ks if ball else Vector())
        a = b + rot_x(f(J["ankle"]) - f(J["ball"]), heel)
        t = b + rot_x(f(J["toe"]) - f(J["ball"]), toe)
        if xf is not None:
            a, b, t = xf @ a, xf @ b, xf @ t
        T = self.targets[side]
        T["ankle"].location, T["ball"].location, T["tip"].location = a, b, t

    def apply(self, frame, pose, key=True):
        rig = self.rig
        self.ks = 1.0 if pose.get("real") else self.k
        rots = pose.get("rot", {})
        for pb in rig.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            r = rots.get(pb.name)
            if r is None and pb.name[-2:] in (".L", ".R"):
                r = rots.get(pb.name[:-2])
            R = self.rest[pb.name]
            if r is None:
                q = Quaternion()
            else:
                D = (Matrix.Rotation(math.radians(r[2]), 3, 'Z') @ Matrix.Rotation(math.radians(r[0]), 3, 'X')
                     @ Matrix.Rotation(math.radians(r[1]), 3, 'Y'))
                q = (R.inverted() @ D @ R).to_quaternion()
            pb.rotation_quaternion = q
            pb.location = (0, 0, 0)
        jr = self.spec.get("jaw_rest", 0.0)      # closes a mouth that is modelled slightly open
        if jr:
            jb = rig.pose.bones["Jaw"]
            R = self.rest["Jaw"]
            jq = (R.inverted() @ Matrix.Rotation(math.radians(jr), 3, 'X') @ R).to_quaternion()
            jb.rotation_quaternion = jq @ jb.rotation_quaternion
        hips = rig.pose.bones["Hips"]
        hips.location = self.rest["Hips"].inverted() @ (Vector(pose.get("hips", (0, 0, 0))) * self.ks)
        for side in ("L", "R"):
            self.foot(side, **pose.get("foot" + side, {}))
        if not key:
            bpy.context.view_layer.update()
            return
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame)
        hips.keyframe_insert("location", frame=frame)
        for T in self.targets.values():
            for e in T.values():
                e.keyframe_insert("location", frame=frame)

    def bake(self, name, frames, fn):
        rig = self.rig
        for o in [rig] + [e for T in self.targets.values() for e in T.values()]:
            if o.animation_data:
                o.animation_data_clear()
        before = set(bpy.data.actions)
        for f in range(frames + 1):
            fn(self, f, f / frames)
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 0, frames
        src = rig.animation_data.action
        dg.activate(rig)
        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='SELECT')
        bpy.ops.nla.bake(frame_start=0, frame_end=frames, step=1, only_selected=False, visual_keying=True,
                         clear_constraints=False, use_current_action=False, bake_types={'POSE'})
        bpy.ops.object.mode_set(mode='OBJECT')
        act = rig.animation_data.action
        assert act != src, "bake did not create a new action"
        act.name = name
        act.use_fake_user = True
        act["frames"] = frames
        for a in set(bpy.data.actions) - before:
            if a != act:
                bpy.data.actions.remove(a)
        log("clip", name, frames, "frames")
        return act

    def finish(self):
        rig = self.rig
        rig.animation_data.action = None
        for pb in rig.pose.bones:
            for c in list(pb.constraints):
                pb.constraints.remove(c)
            pb.rotation_quaternion = Quaternion()
            pb.location = (0, 0, 0)
        for T in self.targets.values():
            for e in T.values():
                bpy.data.objects.remove(e, do_unlink=True)


# ---- clip definitions ------------------------------------------------------------
def tail_wave(rot, n, t, amp, lag=0.09, cycles=1, pitch=0.0, base=(0, 0, 0)):
    for i in range(n):
        k = 0.5 + 0.5 * i / max(1, n - 1)
        rot[f"Tail{i + 1}"] = (base[0] + pitch, 0, base[2] + amp * k * math.sin(2 * math.pi * (cycles * t - lag * i)))


def spine_set(rot, pitch=0.0, yaw=0.0, roll=0.0, n=3):
    for i in range(1, n + 1):
        rot[f"Spine{i}"] = (pitch, roll, yaw)


def neck_set(rot, pitch=0.0, yaw=0.0, n=3):
    for i in range(1, n + 1):
        rot[f"Neck{i}"] = (pitch, 0, yaw)


def gait_pose(A, name, t):
    """Walk / run cycle pose at phase t. Lengths here are real metres (pose['real'])."""
    g = A.spec["gaits"][name]
    k = A.k
    run = name == "Run"
    duty, S = g["duty"], g["speed"] * g["seconds"] * g["duty"]
    lead = g.get("lead", 0.05 * k)
    pose = {"rot": {}, "real": True}
    for side, off in (("L", 0.0), ("R", 0.5)):
        p = (t + off) % 1.0
        if p < duty:
            u = p / duty
            y = -S / 2 + S * u
            heel = -8 * (1 - smooth(u / 0.18)) + (34 if run else 28) * smooth((u - 0.55) / 0.45)
            pose["foot" + side] = dict(ball=(0, y + lead, 0), heel=heel, toe=0)
        else:
            w = (p - duty) / (1 - duty)
            e = w * w * w * (w * (6 * w - 15) + 10)
            y = S / 2 - S * e
            z = g["lift"] * math.sin(math.pi * w) ** 0.9 + 0.04 * k * math.sin(math.pi * w)
            heel = (1 - w) * (34 if run else 28) + w * -8 + (30 if run else 20) * math.sin(math.pi * w)
            toe = 42 * math.sin(math.pi * w) * (1 - w) * 1.6
            pose["foot" + side] = dict(ball=(0, y + lead, z), heel=heel, toe=toe)
    ph = 2 * math.pi * (t - duty / 2)
    bob = g["bob"]
    crouch = (0.22 if run else 0.07) * k
    dz = -crouch + (bob * math.cos(2 * ph) if not run else -bob * math.cos(2 * ph)) - bob
    pose["hips"] = (0.06 * k * math.cos(ph), 0.0, dz)
    yaw = -(7 if run else 5) * math.cos(2 * math.pi * t)
    roll = -(3.5 if run else 2.5) * math.cos(ph)
    lean = 7.0 if run else 1.0
    pose["rot"]["Hips"] = (lean, roll, yaw)
    spine_set(pose["rot"], pitch=-lean * 0.12, yaw=-yaw * 0.27, roll=-roll * 0.27)
    nod = (2.5 if run else 1.5) * math.cos(2 * ph + 0.6)
    neck_set(pose["rot"], pitch=-lean * 0.16 + nod * 0.4, yaw=-yaw * 0.06)
    pose["rot"]["Head"] = (-lean * 0.12 - nod, 0, 0)
    tail_wave(pose["rot"], A.spec["tail"]["bones"], t + 0.25, amp=(4.0 if run else 3.0), pitch=lean * 0.1,
              base=(0, 0, -yaw * 0.12))
    sw = (9 if run else 5) * math.sin(2 * math.pi * t)
    pose["rot"]["UpperArm.L"] = (sw, 0, 0)
    pose["rot"]["UpperArm.R"] = (-sw, 0, 0)
    pose["rot"]["Jaw"] = (4.0 if run else 0.0, 0, 0)
    return pose


def gait(name):
    def fn(A, f, t):
        A.apply(f, gait_pose(A, name, t))
    return fn


def bull_rush(A, f, t):
    """Ability for horned hunters: drop the head, sprint two strides, then toss the horns up into the target."""
    g = A.spec["gaits"]["Run"]
    strides = 2.0
    w = smooth(t / 0.18) * (1 - smooth((t - 0.86) / 0.14))          # how much we are in the charge
    pose = gait_pose(A, "Run", (t * strides * 1.0) % 1.0)
    for key, val in list(pose["rot"].items()):
        pose["rot"][key] = tuple(v * w for v in val)
    for side in ("L", "R"):
        fpose = pose["foot" + side]
        fpose["ball"] = tuple(v * w for v in fpose["ball"])
        fpose["heel"] *= w
        fpose["toe"] *= w
    pose["hips"] = tuple(v * w for v in pose["hips"])
    down = smooth(t / 0.2) * (1 - smooth((t - 0.70) / 0.06))       # head lowered, horns forward
    toss = smooth((t - 0.70) / 0.07) * (1 - smooth((t - 0.84) / 0.16))   # upward ram
    hx, hy, hz = pose["hips"]
    pose["hips"] = (hx, hy - 0.35 * A.k * toss, hz - 0.10 * A.k * down)
    r = pose["rot"]
    r["Hips"] = (r["Hips"][0] + 4 * down - 5 * toss, r["Hips"][1], r["Hips"][2])
    for i in (1, 2, 3):
        n = r[f"Neck{i}"]
        r[f"Neck{i}"] = (n[0] + 11 * down - 9 * toss, n[1], n[2])
    r["Head"] = (r["Head"][0] + 20 * down - 22 * toss, 0, 0)
    r["Jaw"] = (2 + 10 * toss, 0, 0)
    A.apply(f, pose)


def idle(A, f, t):
    s = lambda k=1, ph=0.0: math.sin(2 * math.pi * (k * t + ph))
    pose = {"rot": {}, "hips": (0.02 * s(), 0, -0.02 + 0.015 * s(2))}
    pose["rot"]["Hips"] = (0.4 * s(2), 0.4 * s(), 0.6 * s())
    spine_set(pose["rot"], pitch=-0.5 * s(2, 0.05), yaw=0.5 * s(1, -0.05))
    neck_set(pose["rot"], pitch=0.8 * s(2, 0.1), yaw=2.6 * s(1, -0.1))
    pose["rot"]["Head"] = (1.5 * s(2, 0.2), 1.0 * s(1, 0.3), 5.0 * s(1, -0.18))
    pose["rot"]["Jaw"] = (2.0 + 1.6 * s(2), 0, 0)
    tail_wave(pose["rot"], A.spec["tail"]["bones"], t, amp=4.0, lag=0.07, pitch=0.3 * s(2))
    pose["rot"]["UpperArm"] = (2 * s(2, 0.1), 0, 0)
    pose["rot"]["Hand"] = (4 * s(1, 0.3), 0, 0)
    A.apply(f, pose)


def attack(A, f, t):
    pose = {"rot": {}}
    lunge = curve(t, [(0, 0), (0.22, 0.20), (0.45, -0.75), (0.65, -0.4), (1, 0)])
    pose["hips"] = (0, lunge, curve(t, [(0, 0), (0.22, -0.05), (0.45, -0.18), (1, 0)]))
    pose["rot"]["Hips"] = (curve(t, [(0, 0), (0.22, -3), (0.45, 4), (1, 0)]), 0, 0)
    spine_set(pose["rot"], pitch=curve(t, [(0, 0), (0.22, -1.5), (0.45, 2), (1, 0)]))
    neck_set(pose["rot"], pitch=curve(t, [(0, 0), (0.22, -5), (0.45, 4), (0.62, 3), (1, 0)]))
    pose["rot"]["Head"] = (curve(t, [(0, 0), (0.22, -14), (0.45, -15), (0.56, 5), (1, 0)]), 0,
                           curve(t, [(0, 0), (0.5, 0), (0.62, 6), (0.75, -4), (1, 0)]))
    pose["rot"]["Jaw"] = (curve(t, [(0, 0), (0.2, 34), (0.43, 42), (0.52, 0), (1, 0)]), 0, 0)
    tail_wave(pose["rot"], A.spec["tail"]["bones"], t, amp=3.0, pitch=curve(t, [(0, 0), (0.45, 2.0), (1, 0)]))
    step = curve(t, [(0, 0), (0.25, 0), (0.45, -0.6), (0.7, -0.6), (1, 0.0)])
    up = 0.25 * math.sin(math.pi * (t - 0.25) / 0.2) if 0.25 < t < 0.45 else (0.2 * math.sin(math.pi * (t - 0.7) / 0.3) if t > 0.7 else 0)
    pose["footL"] = dict(ball=(0, step, up))
    A.apply(f, pose)


def ability(A, f, t):   # roar
    pose = {"rot": {}}
    hold = smooth((t - 0.30) / 0.08) * (1 - smooth((t - 0.80) / 0.1))
    shake = hold * math.sin(2 * math.pi * 11 * t)
    pose["hips"] = (0, curve(t, [(0, 0), (0.2, 0.1), (0.36, -0.2), (0.8, -0.15), (1, 0)]),
                    curve(t, [(0, 0), (0.2, -0.16), (0.8, -0.12), (1, 0)]))
    pose["rot"]["Hips"] = (curve(t, [(0, 0), (0.2, 4), (0.36, -3), (0.8, -2.5), (1, 0)]), 0, 0)
    spine_set(pose["rot"], pitch=curve(t, [(0, 0), (0.2, 1.5), (0.36, -1.5), (0.8, -1.5), (1, 0)]))
    neck_set(pose["rot"], pitch=curve(t, [(0, 0), (0.2, 4), (0.36, -4), (0.8, -3.5), (1, 0)]), yaw=1.2 * shake)
    pose["rot"]["Head"] = (curve(t, [(0, 0), (0.2, 6), (0.36, -9), (0.8, -8), (1, 0)]), 2.0 * shake, 1.5 * shake)
    pose["rot"]["Jaw"] = (curve(t, [(0, 0), (0.22, 5), (0.36, 46), (0.78, 43), (0.9, 5), (1, 0)]) + 1.5 * shake, 0, 0)
    tail_wave(pose["rot"], A.spec["tail"]["bones"], t, amp=3 + 7 * hold, cycles=3, lag=0.1,
              pitch=curve(t, [(0, 0), (0.36, 3), (0.8, 2.5), (1, 0)]))
    flare = 25 * hold
    pose["rot"]["UpperArm.L"] = (-flare * 0.6, 0, flare)
    pose["rot"]["UpperArm.R"] = (-flare * 0.6, 0, -flare)
    A.apply(f, pose)


def body_numbers(A):
    """Real-size numbers the lying-down clips need, read from the body rings."""
    body = np.array([q["ctrl"] for q in A.spec["lofts"] if q["name"] == "Body"][0], float)
    belly = float((body[:, 2] - body[:, 4]).min())
    hip = dg.interp_ctrl(body, 0.0)
    return {"rest_drop": (belly - 0.04 * A.k) / A.k, "faint_drop": (hip[2] - 1.05 * hip[3]) / A.k}


def calibrate_reach(A, target_z=None):
    target_z = 0.22 * A.k if target_z is None else target_z
    """Find how far the body must tip so the chin just reaches the ground (works for any species)."""
    base = {"hips": 20, "spine": 4.5, "neck": 9.5, "head": 2, "drop": 0.5}
    lo, hi = 0.2, 2.0
    for _ in range(18):
        s = 0.5 * (lo + hi)
        A.spec["reach"] = {k: v * (s if k != "drop" else min(s, 1.2)) for k, v in base.items()}
        A.apply(0, reach_down(A, 1.0), key=False)
        z = (A.rig.matrix_world @ A.rig.pose.bones["Jaw"].tail).z
        if z > target_z:
            lo = s
        else:
            hi = s
    log("reach scale", round(s, 3), "chin z", round(z, 3))


def reach_down(A, k, jaw=0.0, yaw=0.0, extra_head=0.0):
    """Pose with the head lowered toward the ground; k=1 is fully down."""
    P = A.spec["reach"]
    pose = {"rot": {}, "hips": (0, -0.25 * k, -P["drop"] * k)}
    pose["rot"]["Hips"] = (P["hips"] * k, 0, 0)
    spine_set(pose["rot"], pitch=P["spine"] * k)
    neck_set(pose["rot"], pitch=P["neck"] * k, yaw=yaw)
    pose["rot"]["Head"] = (P["head"] * k + extra_head, 0, yaw)
    pose["rot"]["Jaw"] = (jaw, 0, 0)
    for i in range(A.spec["tail"]["bones"]):
        pose["rot"][f"Tail{i + 1}"] = (-P["hips"] * k * (0.5 if i == 0 else 0.07), 0, 0)
    pose["rot"]["UpperArm"] = (-10 * k, 0, 0)
    return pose


def eat(A, f, t):
    k = curve(t, [(0, 0), (0.22, 1), (0.8, 1), (1, 0)])
    chew = smooth((t - 0.2) / 0.05) * (1 - smooth((t - 0.78) / 0.05))
    bite = chew * (0.5 - 0.5 * math.cos(2 * math.pi * 3 * (t - 0.22) / 0.56))
    tug = chew * math.sin(2 * math.pi * 3 * (t - 0.22) / 0.56)
    pose = reach_down(A, k - 0.06 * bite, jaw=6 + 26 * bite, yaw=3.0 * tug, extra_head=-4 * tug)
    A.apply(f, pose)


def drink(A, f, t):
    k = curve(t, [(0, 0), (0.2, 1.05), (0.82, 1.05), (1, 0)])
    on = smooth((t - 0.2) / 0.05) * (1 - smooth((t - 0.8) / 0.05))
    lap = on * (0.5 - 0.5 * math.cos(2 * math.pi * 6 * (t - 0.2) / 0.6))
    pose = reach_down(A, k, jaw=5 + 9 * lap, extra_head=-6 * k + 2 * lap)
    A.apply(f, pose)


def rest(A, f, t):      # lying down, breathing (loop)
    s = lambda k=1, ph=0.0: math.sin(2 * math.pi * (k * t + ph))
    drop = A.spec.get("rest", {}).get("drop", A.numbers["rest_drop"])
    pose = {"rot": {}, "hips": (0, 0.25, -drop + 0.02 * s(2))}
    pose["rot"]["Hips"] = (-3.0, 0, 0)
    spine_set(pose["rot"], pitch=1.5 + 0.5 * s(2, 0.05))
    neck_set(pose["rot"], pitch=5.0 + 0.4 * s(2, 0.1), yaw=1.5 * s(1))
    pose["rot"]["Head"] = (4.0 + 0.8 * s(2, 0.2), 0, 2.5 * s(1, -0.1))
    pose["rot"]["Jaw"] = (1.0 + 1.0 * s(2), 0, 0)
    n = A.spec["tail"]["bones"]
    for i in range(n):
        pose["rot"][f"Tail{i + 1}"] = (-2.2 if i < 4 else 1.5, 0, 6.0 + 1.5 * s(1, -0.08 * i))
    for side in ("L", "R"):
        pose["foot" + side] = dict(ball=(0.12 if side == "L" else -0.12, -0.35, 0), heel=-38, toe=0)
    pose["rot"]["UpperArm"] = (20, 0, 0)
    A.apply(f, pose)


def hurt(A, f, t):
    k = curve(t, [(0, 0), (0.25, 1), (0.5, 0.5), (1, 0)])
    pose = {"rot": {}, "hips": (-0.12 * k, 0.15 * k, -0.14 * k)}
    pose["rot"]["Hips"] = (-3 * k, 4 * k, -5 * k)
    spine_set(pose["rot"], pitch=-2 * k, yaw=-3 * k)
    neck_set(pose["rot"], pitch=-6 * k, yaw=-7 * k)
    pose["rot"]["Head"] = (-8 * k, 6 * k, -10 * k)
    pose["rot"]["Jaw"] = (22 * k, 0, 0)
    tail_wave(pose["rot"], A.spec["tail"]["bones"], t, amp=6 * k + 1, cycles=1)
    A.apply(f, pose)


def faint(A, f, t):     # stagger, sink, flop onto the side (ends lying still)
    F = {"drop": A.numbers["faint_drop"], "roll": -78}
    F.update(A.spec.get("faint", {}))
    sink = curve(t, [(0, 0), (0.15, 0.05), (0.55, 0.75), (0.75, 1.0), (1, 1.0)])
    roll = curve(t, [(0, 0), (0.2, 0.06), (0.45, 0.2), (0.75, 1.0), (0.82, 0.96), (1, 1.0)])
    sway = math.sin(2 * math.pi * 2.0 * t) * (1 - smooth(t / 0.5))
    ang = F["roll"] * roll
    pose = {"rot": {}, "hips": (-0.5 * roll + 0.15 * sway, 0.1 * sink, -F["drop"] * sink)}
    pose["rot"]["Hips"] = (-2 * sink, ang + 3 * sway, 4 * roll)
    spine_set(pose["rot"], pitch=1.0 * sink, yaw=-2.0 * roll)
    neck_set(pose["rot"], pitch=6.0 * sink, yaw=-5.0 * roll)
    pose["rot"]["Head"] = (-3 * sink, 10 * roll, -6 * roll + 6 * sway)
    pose["rot"]["Jaw"] = (curve(t, [(0, 0), (0.2, 14), (0.75, 8), (1, 6)]), 0, 0)
    n = A.spec["tail"]["bones"]
    for i in range(n):
        pose["rot"][f"Tail{i + 1}"] = (-1.0 * sink, 0, 5.0 * roll + 4 * sway * (i / n))
    # feet follow the body as it tips over
    hip_rest = Vector((0, A.spec["spine_y"][0], 0))
    hz = A.rig.data.bones["Hips"].head_local.z
    pivot = Vector((0, 0, hz))
    w = smooth((roll - 0.15) / 0.5)
    xf = (Matrix.Translation(Vector(pose["hips"]) * w * A.k) @ Matrix.Translation(pivot)
          @ Matrix.Rotation(math.radians(ang * w), 4, 'Y') @ Matrix.Translation(-pivot))
    for side in ("L", "R"):
        pose["foot" + side] = dict(ball=(0, 0.25 * w, 0), heel=-10 * w, toe=18 * w, xf=xf)
    pose["rot"]["UpperArm"] = (15 * sink, 0, 0)
    A.apply(f, pose)
    # keep feet from sinking below ground
    for T in A.targets.values():
        for e in T.values():
            if e.location.z < 0.1 * A.k:
                e.location.z = 0.1 * A.k
                e.keyframe_insert("location", frame=f)


def stage_anim(spec, out):
    bpy.ops.wm.open_mainfile(filepath=blend_path(spec, out, "3_rigged"))
    sc = bpy.context.scene
    sc.render.fps = FPS
    rig = bpy.data.objects["Rig"]
    A = Animator(rig, spec)
    A.numbers = body_numbers(A)
    calibrate_reach(A)
    g = spec["gaits"]
    clips = [
        ("Idle", 120, idle),
        ("Walk", round(g["Walk"]["seconds"] * FPS), gait("Walk")),
        ("Run", round(g["Run"]["seconds"] * FPS), gait("Run")),
        ("Attack", 24, attack),
        ("Ability", 42, bull_rush) if spec.get("ability") == "bull_rush" else ("Ability", 75, ability),
        ("Eat", 105, eat),
        ("Drink", 120, drink),
        ("Rest", 120, rest),
        ("Hurt", 15, hurt),
        ("Faint", 60, faint),
    ]
    only = spec.get("only_clips")
    for name, frames, fn in clips:
        if only and name not in only:
            continue
        A.bake(name, frames, fn)
    A.finish()
    bpy.ops.wm.save_as_mainfile(filepath=blend_path(spec, out, "4_anim"))


# ----------------------------------------------------------------------------
# export + previews
# ----------------------------------------------------------------------------
def set_action(rig, name):
    act = bpy.data.actions.get(name) if name else None
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = act
    if act is not None:
        try:
            if rig.animation_data.action_slot is None and len(act.slots):
                rig.animation_data.action_slot = act.slots[0]
        except Exception as ex:
            log("slot", ex)
    return act


def stage_export(spec, out):
    bpy.ops.wm.open_mainfile(filepath=blend_path(spec, out, "4_anim"))
    sid = spec["id"]
    rig = bpy.data.objects["Rig"]
    set_action(rig, None)
    tdir = os.path.join(out, "unity", sid)
    os.makedirs(tdir, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    lods = [o for o in bpy.data.objects if o.name.startswith(sid + "_LOD")]
    for o in [rig] + lods:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx = os.path.join(tdir, sid + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='Z', axis_up='Y',   # Unity then sees the head toward +Z
        add_leaf_bones=False, use_armature_deform_only=False, mesh_smooth_type='FACE', use_tspace=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5,
        path_mode='AUTO', embed_textures=False)
    log("exported", fbx, round(os.path.getsize(fbx) / 1e6, 2), "MB")


def run(spec, stage, out):
    if stage in ("bake", "all"):
        stage_bake(spec, out)
    if stage in ("rig", "all"):
        stage_rig(spec, out)
    if stage in ("anim", "all"):
        stage_anim(spec, out)
    if stage in ("export", "all"):
        stage_export(spec, out)
    if stage in ("preview", "export", "all"):
        import previews
        previews.run(spec, out, what=tuple(os.environ.get("DINO_PREVIEW", "stills,lods,tints,clips").split(",")))
