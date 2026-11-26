"""
dinogen.py - procedural dinosaur builder for Blender (tested on 5.2).

Usage:
  blender -b -P dinogen.py -- <species> <stage> [outdir]
  stages: mesh | bake | rig | anim | export | all

Everything about a species lives in a SPEC dict (see specs.py), so one body
plan ("biped hunter") can be reshaped into several species by changing numbers.
Blender space: Z up, animal faces -Y, left side is +X, metres, feet on Z=0.
"""
import bpy, bmesh, math, sys, os
import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


# ----------------------------------------------------------------------------
# small helpers
# ----------------------------------------------------------------------------
def log(*a):
    print("[dinogen]", *a, flush=True)


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, mod):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def new_object(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    return ob


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def duplicate(obj, name):
    ob = obj.copy()
    ob.data = obj.data.copy()
    ob.name = name
    ob.data.name = name
    bpy.context.scene.collection.objects.link(ob)
    return ob


# ----------------------------------------------------------------------------
# lofting: a body part is a list of (x, y, z, half_width, half_height) rings
# ----------------------------------------------------------------------------
def cr_interp(ctrl, per=8):
    P = np.array(ctrl, float)
    Q = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    out = []
    for i in range(1, len(Q) - 2):
        p0, p1, p2, p3 = Q[i - 1], Q[i], Q[i + 1], Q[i + 2]
        for k in range(per):
            t = k / per
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-1])
    out = np.array(out)
    out[:, 3:] = np.maximum(out[:, 3:], 0.004)
    return out


def loft(name, ctrl, ref=(0, 0, 1), segs=28, per=8, n=2.3, mirror=False):
    S = cr_interp(ctrl, per)
    if mirror:
        S = S.copy()
        S[:, 0] *= -1
    ref = np.array(ref, float)
    m = len(S)
    verts, faces = [], []
    for i in range(m):
        t = S[min(i + 1, m - 1), :3] - S[max(i - 1, 0), :3]
        t /= np.linalg.norm(t)
        side = np.cross(t, ref)
        side /= np.linalg.norm(side)
        up = np.cross(side, t)
        for k in range(segs):
            a = 2 * math.pi * k / segs
            c, s = math.cos(a), math.sin(a)
            cx = math.copysign(abs(c) ** (2 / n), c)
            sz = math.copysign(abs(s) ** (2 / n), s)
            verts.append(S[i, :3] + side * S[i, 3] * cx + up * S[i, 4] * sz)
    for i in range(m - 1):
        for k in range(segs):
            a = i * segs + k
            b = i * segs + (k + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    verts.append(S[0, :3])
    c0 = len(verts) - 1
    verts.append(S[-1, :3])
    c1 = len(verts) - 1
    for k in range(segs):
        faces.append((c0, (k + 1) % segs, k))
        faces.append((c1, (m - 1) * segs + k, (m - 1) * segs + (k + 1) % segs))
    return new_object(name, verts, faces)


def ellipsoid(name, center, radii, rot=(0, 0, 0), mirror=False, box=False):
    c = Vector(center)
    e = Euler([math.radians(a) for a in rot])
    if mirror:
        c.x *= -1
        e = Euler((e.x, -e.y, -e.z))
    bm = bmesh.new()
    M = Matrix.Translation(c) @ e.to_matrix().to_4x4() @ Matrix.Diagonal((*radii, 1))
    if box:
        bmesh.ops.create_cube(bm, size=2.0, matrix=M)
    else:
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0, matrix=M)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def cone(bm, base, direction, length, radius, sides=5, hook=0.0, hook_dir=(0, 0, -1)):
    """Add a (slightly hooked) cone to bm. Returns created verts."""
    d = Vector(direction).normalized()
    base = Vector(base)
    ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
    u = d.cross(ref).normalized()
    v = d.cross(u)
    hk = Vector(hook_dir) * hook * length
    rings = []
    for f, rs in ((0.0, 1.0), (0.55, 0.62)):
        ring = [bm.verts.new(base + d * length * f + hk * f * f + (u * math.cos(2 * math.pi * k / sides)
                                                                 + v * math.sin(2 * math.pi * k / sides)) * radius * rs)
                for k in range(sides)]
        rings.append(ring)
    tip = bm.verts.new(base + d * length + hk)
    for k in range(sides):
        a, b = rings[0][k], rings[0][(k + 1) % sides]
        c, e = rings[1][(k + 1) % sides], rings[1][k]
        bm.faces.new((a, b, c, e))
        bm.faces.new((e, c, tip))
    return [x for r in rings for x in r] + [tip]


def interp_ctrl(ctrl, y):
    """Interpolate a loft control list at a given y (ctrl must be monotonic in y)."""
    P = np.array(ctrl, float)
    order = np.argsort(P[:, 1])
    P = P[order]
    return np.array([np.interp(y, P[:, 1], P[:, j]) for j in range(5)])


# ----------------------------------------------------------------------------
# stage: mesh
# ----------------------------------------------------------------------------
def import_base(spec):
    """Optional sculpted base body (glb/obj). It is oriented, scaled to real size, stood on the ground,
    centred and made symmetric, then dressed with the same procedural parts as a lofted body."""
    B = spec["base"]
    path = B["file"] if os.path.isabs(B["file"]) else os.path.join(HERE, B["file"])
    before = set(bpy.data.objects)
    if path.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        bpy.ops.wm.obj_import(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    ob = [o for o in new if o.type == 'MESH'][0]
    M = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = M
    for o in new:
        if o != ob:
            bpy.data.objects.remove(o, do_unlink=True)
    me = ob.data
    R = Euler([math.radians(a) for a in B.get("rot", (0, 0, 0))]).to_matrix().to_4x4()
    me.transform(R @ M)
    ob.matrix_world = Matrix.Identity(4)
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3).astype(np.float64)
    if B.get("auto_orient", True):
        # the body's long axis (PCA in the ground plane) becomes Y; the thick, high end is the head and faces -Y
        xy = co[:, :2] - co[:, :2].mean(0)
        w, v = np.linalg.eigh(np.cov(xy.T))
        axis = v[:, np.argmax(w)]
        ang = math.atan2(axis[0], axis[1])
        c_, s_ = math.cos(ang), math.sin(ang)
        co[:, :2] = xy @ np.array([[c_, s_], [-s_, c_]])
        y = co[:, 1]
        span = y.max() - y.min()
        lo, hi = y.min() + 0.10 * span, y.max() - 0.10 * span
        a_end, b_end = co[y < lo], co[y > hi]
        bulk = lambda e: (e[:, 0].max() - e[:, 0].min()) * (e[:, 2].max() - e[:, 2].min())
        if bulk(b_end) > bulk(a_end):       # head is at +Y: turn around
            co[:, 0] *= -1
            co[:, 1] *= -1
        log("auto orient: yaw", round(math.degrees(ang), 1))
    s = B["length"] / (co[:, 1].max() - co[:, 1].min())
    co *= s
    sx_, sz_ = B.get("scale_xz", (1.0, 1.0))      # optional slimming / lowering to hit the species' real height
    co[:, 0] *= sx_
    co[:, 2] *= sz_
    co[:, 2] -= co[:, 2].min() + B.get("sink", 0.0)
    co[:, 1] -= co[:, 1].min() + B["snout_to_hip"]          # snout tip at y = -snout_to_hip, hips near y = 0
    # straighten the spine sideways: generated bodies are often slightly bent or turned, which would put the
    # head and tail off the mirror plane. Find the midline of the upper body in slices along Y and remove it.
    nb = 72
    edges = np.linspace(co[:, 1].min(), co[:, 1].max(), nb + 1)
    which = np.clip(np.digitize(co[:, 1], edges) - 1, 0, nb - 1)
    mids = np.zeros(nb)
    for b in range(nb):
        sl = co[which == b]
        if len(sl) < 8:
            mids[b] = np.nan
            continue
        up = sl[sl[:, 2] > sl[:, 2].min() + 0.5 * (sl[:, 2].max() - sl[:, 2].min())]
        mids[b] = 0.5 * (up[:, 0].min() + up[:, 0].max())
    ok = ~np.isnan(mids)
    centres = 0.5 * (edges[:-1] + edges[1:])
    mids = np.interp(centres, centres[ok], mids[ok])
    mids = np.convolve(np.pad(mids, 3, mode='edge'), np.ones(7) / 7, mode='valid')
    co[:, 0] -= np.interp(co[:, 1], centres, mids) + B.get("x_shift", 0.0)
    log("midline offset range", round(float(mids.min()), 3), round(float(mids.max()), 3))
    me.vertices.foreach_set("co", co.astype(np.float32).ravel())
    me.update()
    if B.get("symmetrize", "+") in ("+", "-"):
        keep_pos = B.get("symmetrize", "+") == "+"
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0),
                               plane_no=(1, 0, 0), clear_inner=keep_pos, clear_outer=not keep_pos)
        bm.to_mesh(me)
        bm.free()
        m = ob.modifiers.new("Mirror", 'MIRROR')
        m.use_axis = (True, False, False)
        m.use_clip = True
        m.merge_threshold = 0.002
        apply_modifier(ob, m)
    ob.name = "Base"
    log("base mesh:", len(me.polygons), "faces, scale", round(float(s), 3))
    return ob


def build_high(spec):
    parts = []
    if "base" in spec:
        parts.append(import_base(spec))
    based = "base" in spec        # with a sculpted base, lofts are only guides unless marked with_base
    for p in spec.get("lofts", []):
        if based and not p.get("with_base"):
            continue
        kw = dict(ref=p.get("ref", (0, 0, 1)), n=p.get("n", 2.3), segs=p.get("segs", 28))
        parts.append(loft(p["name"], p["ctrl"], **kw))
        if p.get("mirror"):
            parts.append(loft(p["name"] + "_R", p["ctrl"], mirror=True, **kw))
    for e in spec.get("ellipsoids", []):
        if based and not e.get("with_base"):
            continue
        parts.append(ellipsoid(e["name"], e["c"], e["r"], e.get("rot", (0, 0, 0))))
        if e.get("mirror", True) and abs(e["c"][0]) > 1e-6:
            parts.append(ellipsoid(e["name"] + "_R", e["c"], e["r"], e.get("rot", (0, 0, 0)), mirror=True))
    high = join(parts, "High") if len(parts) > 1 else parts[0]
    high.name = "High"
    vox = spec.get("voxel", 0.02)
    m = high.modifiers.new("Remesh", 'REMESH')
    m.mode = 'VOXEL'
    m.voxel_size = vox
    m.adaptivity = 0.0
    m.use_smooth_shade = True
    apply_modifier(high, m)
    m = high.modifiers.new("Smooth", 'SMOOTH')
    m.factor = 0.5
    m.iterations = spec.get("smooth_iters", 12)
    apply_modifier(high, m)
    extra = []
    O = spec.get("osteoderms")
    if O:
        bpy.context.view_layer.update()
        ctrl = [p["ctrl"] for p in spec["lofts"] if p["name"] == O["ctrl"]][0]
        bm = bmesh.new()
        n = 0
        for ri, row in enumerate(O["rows"]):
            ang = math.radians(row["angle"])
            y = O["y"][0] + (ri % 2) * O["spacing"] * 0.5
            i = 0
            while y < O["y"][1]:
                c = interp_ctrl(ctrl, y)
                taper = min(1.0, max(0.25, c[4] / 0.45))
                jit = ((i * 7919 + ri * 104729) % 100) / 100.0
                for sx in ((1, -1) if row["angle"] > 0 else (1,)):
                    dirv = Vector((sx * math.sin(ang), 0, math.cos(ang)))
                    origin = Vector((0, y + (jit - 0.5) * O["spacing"] * 0.4, c[2])) + dirv * 3.0
                    ok, loc, nrm, _ = high.ray_cast(origin, -dirv, distance=3.0)
                    if not ok:
                        continue
                    r = row["r"] * taper * (0.8 + 0.4 * jit)
                    h = row["h"] * taper * (0.8 + 0.4 * jit)
                    cone(bm, loc - nrm * r * 0.4, nrm, h + r * 0.4, r, sides=8)
                    n += 1
                y += O["spacing"] * (0.85 + 0.3 * jit)
                i += 1
        me = bpy.data.meshes.new("Osteoderms")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        # close the cone bases so the voxel remesh treats them as solids
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary])
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new("Osteoderms", me)
        bpy.context.scene.collection.objects.link(ob)
        extra.append(ob)
        log("osteoderms:", n)
    cuts = []
    for e in spec.get("cuts", []):
        cuts.append(ellipsoid(e["name"], e["c"], e["r"], e.get("rot", (0, 0, 0)), box=e.get("box", False)))
        if abs(e["c"][0]) > 1e-6:
            cuts.append(ellipsoid(e["name"] + "_R", e["c"], e["r"], e.get("rot", (0, 0, 0)), mirror=True))
    if extra:
        high = join([high] + extra, "High")
    if cuts:
        cutter = join(cuts, "Cutter")
        if extra:       # fuse the added bumps first so the boolean sees one clean solid
            m = high.modifiers.new("Remesh", 'REMESH')
            m.mode = 'VOXEL'
            m.voxel_size = vox
            apply_modifier(high, m)
        m = high.modifiers.new("Cut", 'BOOLEAN')
        m.operation = 'DIFFERENCE'
        m.object = cutter
        try:
            m.solver = 'EXACT'
        except Exception:
            pass
        apply_modifier(high, m)
        bpy.data.objects.remove(cutter, do_unlink=True)
    if cuts or extra:
        m = high.modifiers.new("Remesh", 'REMESH')
        m.mode = 'VOXEL'
        m.voxel_size = vox
        m.use_smooth_shade = True
        apply_modifier(high, m)
        m = high.modifiers.new("Smooth", 'SMOOTH')
        m.factor = 0.5
        m.iterations = spec.get("final_smooth", 3)
        apply_modifier(high, m)
    for poly in high.data.polygons:
        poly.use_smooth = True
    log("high poly faces:", len(high.data.polygons))
    return high


def build_accessories(spec, high=None):
    """Teeth, claws and eyes as separate small meshes with ready-made vertex groups."""
    objs = []
    bpy.context.view_layer.update()

    def cast(origin, direction, maxd):
        if high is None:
            return None
        ok, loc, nrm, idx = high.ray_cast(Vector(origin), Vector(direction).normalized(), distance=maxd)
        return loc if ok else None
    head = spec["head_ctrl"]
    jaw = spec["jaw_ctrl"]
    T = spec["teeth"]

    def finish(bm, name, groups):
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        for gname, idx in groups.items():
            vg = ob.vertex_groups.new(name=gname)
            vg.add(idx, 1.0, 'REPLACE')
        for poly in me.polygons:
            poly.use_smooth = True
        bm.free()
        return ob

    # upper teeth -> Head
    bm = bmesh.new()
    n = T["upper_count"]
    for i in range(n):
        f = i / (n - 1)
        y = T["upper_y"][0] + (T["upper_y"][1] - T["upper_y"][0]) * f
        c = interp_ctrl(head, y)
        size = T["upper_len"][0] + (T["upper_len"][1] - T["upper_len"][0]) * math.sin(math.pi * min(1, f * 1.25)) ** 1.5
        size *= 0.85 + 0.3 * ((i * 7919) % 5) / 4
        for sx in (1, -1):
            x = sx * T["upper_x"] * c[3]
            ob_ = T.get("overbite")
            if ob_:     # teeth sit on the outer lip and hang down over the lower jaw
                zc = ob_["z0"] + (y - ob_["y0"]) * ob_["slope"] + ob_.get("lift", 0.02)
                hit = cast((sx * 1.5, y, zc), (-sx, 0, 0), 1.5)
                if not hit or abs(hit.x) < 0.02:
                    continue
                base = (hit.x - sx * ob_.get("inset", 0.012), y, zc)
                cone(bm, base, (sx * 0.04, 0.06, -1), size, size * 0.30, hook=0.15, hook_dir=(0, 1, 0))
                continue
            hit = cast((x, y, c[2]), (0, 0, -1), c[4] * 1.3)
            base = (x, y, (hit.z + 0.025) if hit else c[2] - T["upper_z"] * c[4])
            cone(bm, base, (sx * 0.02, 0.10, -1), size + 0.025, size * 0.33, hook=0.18, hook_dir=(0, 1, 0))
    bm.verts.ensure_lookup_table()
    objs.append(finish(bm, "TeethUpper", {"Head": list(range(len(bm.verts)))}))

    # lower teeth -> Jaw
    bm = bmesh.new()
    n = T["lower_count"]
    for i in range(n):
        f = i / (n - 1)
        y = T["lower_y"][0] + (T["lower_y"][1] - T["lower_y"][0]) * f
        c = interp_ctrl(jaw, y)
        size = T["lower_len"][0] + (T["lower_len"][1] - T["lower_len"][0]) * math.sin(math.pi * min(1, f * 1.2))
        size *= 0.85 + 0.3 * ((i * 104729) % 5) / 4
        for sx in (1, -1):
            x = sx * T["lower_x"] * c[3]
            hit = cast((x, y, c[2]), (0, 0, 1), c[4] * 1.3)
            base = (x, y, (hit.z - 0.025) if hit else c[2] + T["lower_z"] * c[4])
            cone(bm, base, (-sx * 0.02, 0.08, 1), size + 0.025, size * 0.34, hook=0.15, hook_dir=(0, 1, 0))
    bm.verts.ensure_lookup_table()
    objs.append(finish(bm, "TeethLower", {"Jaw": list(range(len(bm.verts)))}))

    # claws
    for side, sx in (("L", 1), ("R", -1)):
        bm = bmesh.new()
        for cl in spec["claws"]:
            base = Vector(cl["base"])
            d = Vector(cl["dir"])
            cone(bm, (base.x * sx, base.y, base.z), (d.x * sx, d.y, d.z), cl["len"], cl["r"], sides=6,
                 hook=0.35, hook_dir=(0, 0, -1))
        bm.verts.ensure_lookup_table()
        # split verts by bone (7 verts per cone at 6 sides: 6+6+1 = 13)
        groups = {}
        per = 13
        for ci, cl in enumerate(spec["claws"]):
            groups.setdefault(cl["bone"] + "." + side, []).extend(range(ci * per, ci * per + per))
        objs.append(finish(bm, "Claws" + side, groups))

    # eyes -> Head
    E = spec["eye"]
    for side, sx in (("L", 1), ("R", -1)):
        bm = bmesh.new()
        look = Vector((E["look"][0] * sx, E["look"][1], E["look"][2])).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(look).to_matrix().to_4x4()
        ex = E["c"][0] * sx
        hit = cast((sx * 3.0, E["c"][1], E["c"][2]), (-sx, 0, 0), 4.0)
        if hit:
            ex = hit.x - sx * E["r"] * E.get("inset", 0.15)
        M = Matrix.Translation((ex, E["c"][1], E["c"][2])) @ rot
        bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=E["r"], matrix=M)
        bm.verts.ensure_lookup_table()
        ob = finish(bm, "Eye" + side, {"Head": list(range(len(bm.verts)))})
        ob["look"] = tuple(look)
        ob["center"] = (ex, E["c"][1], E["c"][2])
        objs.append(ob)
    return objs


# ----------------------------------------------------------------------------
# rendering helpers (used for previews)
# ----------------------------------------------------------------------------
def setup_cycles(samples=48):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            n = 0
            for d in prefs.devices:
                d.use = d.type != 'CPU'
                n += d.use
            if n:
                sc.cycles.device = 'GPU'
                log("cycles device", kind)
                break
        except Exception as ex:
            log("no", kind, ex)
    sc.cycles.samples = samples
    try:
        sc.view_settings.view_transform = 'AgX'
        sc.view_settings.look = 'AgX - Medium High Contrast'
    except Exception as ex:
        log("view settings", ex)
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass


def setup_stage(length=13.0):
    """Ground, sun and sky for preview renders."""
    sc = bpy.context.scene
    w = bpy.data.worlds.new("World")
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes.get("Background")
    try:
        sky = nt.nodes.new("ShaderNodeTexSky")
        for attr, val in (("sun_elevation", math.radians(38)), ("sun_rotation", math.radians(140)),
                          ("sun_disc", False)):
            if hasattr(sky, attr):
                setattr(sky, attr, val)
        nt.links.new(sky.outputs[0], bg.inputs[0])
        bg.inputs[1].default_value = 0.18
    except Exception as ex:
        log("sky fallback", ex)
        bg.inputs[0].default_value = (0.6, 0.72, 0.9, 1)
        bg.inputs[1].default_value = 1.0
    sun = bpy.data.lights.new("Sun", 'SUN')
    sun.energy = 4.0
    sun.angle = math.radians(3)
    so = bpy.data.objects.new("Sun", sun)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(52), 0, math.radians(-40))
    gm = bpy.data.meshes.new("Ground")
    gm.from_pydata([(-200, -200, 0), (200, -200, 0), (200, 200, 0), (-200, 200, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new("Ground", gm)
    sc.collection.objects.link(g)
    mat = bpy.data.materials.new("GroundMat")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    nz = mat.node_tree.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.6
    nz.inputs["Detail"].default_value = 8
    ramp = mat.node_tree.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.16, 0.13, 0.09, 1)
    ramp.color_ramp.elements[1].color = (0.30, 0.27, 0.19, 1)
    mat.node_tree.links.new(nz.outputs[0], ramp.inputs[0])
    mat.node_tree.links.new(ramp.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.95
    g.data.materials.append(mat)
    return g


def render_view(path, target, dist, az, el, lens=50, res=(1280, 800)):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = lens
    cam.data.clip_end = 1000
    t = Vector(target)
    a, e = math.radians(az), math.radians(el)
    pos = t + Vector((dist * math.cos(e) * math.sin(a), -dist * math.cos(e) * math.cos(a), dist * math.sin(e)))
    cam.location = pos
    cam.rotation_euler = (t - pos).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    log("rendered", path)


# ----------------------------------------------------------------------------
# skin material (procedural, lives on the high poly; baked down to textures)
# ----------------------------------------------------------------------------
def skin_material(spec):
    S = spec["skin"]
    K = spec.get("size", 1.0)          # body size relative to the T. rex the shader was tuned on
    mat = bpy.data.materials.new("SkinProcedural")
    mat.use_nodes = True
    nt = mat.node_tree
    N, L = nt.nodes, nt.links
    bsdf = N["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.62

    def node(kind, **props):
        n = N.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def math_(op, a, b=None, clamp=False):
        n = node("ShaderNodeMath", operation=op)
        n.use_clamp = clamp
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, (int, float)):
                n.inputs[i].default_value = x
            else:
                L.new(x, n.inputs[i])
        return n.outputs[0]

    def maprange(x, a, b, c=0.0, d=1.0, smooth=True):
        n = node("ShaderNodeMapRange")
        if smooth:
            n.interpolation_type = 'SMOOTHSTEP'
        L.new(x, n.inputs[0])
        for i, v in zip((1, 2, 3, 4), (a, b, c, d)):
            n.inputs[i].default_value = v
        return n.outputs[0]

    def mixc(f, a, b, blend='MIX'):
        n = node("ShaderNodeMix", data_type='RGBA', blend_type=blend)
        for sock, x in ((n.inputs[0], f), (n.inputs[6], a), (n.inputs[7], b)):
            if isinstance(x, (int, float)):
                sock.default_value = x if sock == n.inputs[0] else (x, x, x, 1)
            elif isinstance(x, tuple):
                sock.default_value = (*x, 1)
            else:
                L.new(x, sock)
        return n.outputs[2]

    geo = node("ShaderNodeNewGeometry")
    sep_p = node("ShaderNodeSeparateXYZ")
    L.new(geo.outputs["Position"], sep_p.inputs[0])
    sep_n = node("ShaderNodeSeparateXYZ")
    L.new(geo.outputs["Normal"], sep_n.inputs[0])
    px, py, pz = sep_p.outputs
    nz = sep_n.outputs[2]

    def noise(scale, detail=4.0, rough=0.55, dist=0.0):
        n = node("ShaderNodeTexNoise")
        L.new(geo.outputs["Position"], n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale / K
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Distortion"].default_value = dist
        return n.outputs[0]

    def voronoi(scale, feature='DISTANCE_TO_EDGE', rand=1.0, vec=None):
        n = node("ShaderNodeTexVoronoi", feature=feature)
        L.new(vec or geo.outputs["Position"], n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Randomness"].default_value = rand
        return n.outputs[0]

    # slightly warped coordinates so scale rows are not perfectly regular
    warp = node("ShaderNodeTexNoise")
    L.new(geo.outputs["Position"], warp.inputs["Vector"])
    warp.inputs["Scale"].default_value = 1.3 / K
    wmix = node("ShaderNodeMix", data_type='VECTOR')
    wmix.inputs[0].default_value = 0.035 * K
    L.new(geo.outputs["Position"], wmix.inputs[4])
    L.new(warp.outputs["Color"], wmix.inputs[5])
    wpos = wmix.outputs[1]

    # --- masks -------------------------------------------------------------
    top = maprange(nz, -0.15, 0.75)                       # 1 on the back
    belly = maprange(nz, -0.25, -0.8)                     # 1 underneath
    low = maprange(pz, S["belly_height"][0], S["belly_height"][1], 1.0, 0.0)
    belly = math_('MULTIPLY', belly, low)
    ridge = math_('MULTIPLY', maprange(math_('ABSOLUTE', px), 0.06 * K, 0.22 * K, 1.0, 0.0), maprange(nz, 0.55, 0.85))
    # stripes: bands across the body, broken up by noise, only on back/flanks
    band = math_('SINE', math_('ADD', math_('MULTIPLY', py, S["stripe_freq"]),
                               math_('MULTIPLY', noise(0.9, 3.0), 9.0)))
    band = maprange(band, 0.25, 0.8)
    band = math_('MULTIPLY', band, maprange(nz, -0.35, 0.3))
    band = math_('MULTIPLY', band, maprange(noise(2.3, 2.0), 0.35, 0.6))
    mottle = noise(5.0, 5.0, 0.6)
    blotch = maprange(noise(1.6, 3.0, 0.5, 0.8), 0.42, 0.62)

    # --- scales and creases (drive both value and bump) ----------------------
    sc_small = voronoi(S["scale_small"], vec=wpos)
    sc_big = voronoi(S["scale_big"], vec=wpos)
    feet = maprange(pz, 0.35 * K, 0.9 * K, 1.0, 0.0)
    big_mask = math_('MAXIMUM', math_('MAXIMUM', ridge, feet), maprange(noise(0.8, 2.0), 0.58, 0.72))
    edge_small = maprange(sc_small, 0.0, 0.22)
    edge_big = maprange(sc_big, 0.0, 0.14)
    scales = mixc(big_mask, edge_small, edge_big)        # 0 in grooves, 1 on scale tops

    def crease(sx, sy, sz, scale):
        mp = node("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (sx, sy, sz)
        L.new(wpos, mp.inputs["Vector"])
        n = node("ShaderNodeTexNoise")
        L.new(mp.outputs[0], n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale / K
        n.inputs["Detail"].default_value = 3.0
        n.inputs["Roughness"].default_value = 0.6
        n.inputs["Distortion"].default_value = 0.6
        d = math_('ABSOLUTE', math_('SUBTRACT', n.outputs[0], 0.5))
        return maprange(d, 0.0, 0.05, 1.0, 0.0)           # 1 inside a fold line

    limb = maprange(pz, 1.7 * K, 2.1 * K, 1.0, 0.0)               # legs: rings; body: vertical folds
    fold = mixc(limb, crease(0.5, 3.2, 0.35, 2.2), crease(0.6, 0.6, 3.0, 2.6))
    fold = math_('MULTIPLY', fold, maprange(noise(0.9, 2.0), 0.48, 0.68))
    broad = noise(2.2, 3.0, 0.5)

    # --- value / colour -----------------------------------------------------
    col = mixc(top, S["flank"], S["back"])
    col = mixc(belly, col, S["belly"])
    col = mixc(math_('MULTIPLY', band, S["stripe_strength"]), col, S["stripe"])
    col = mixc(math_('MULTIPLY', blotch, 0.22), col, S["stripe"])
    col = mixc(maprange(mottle, 0.3, 0.7, 0.0, 0.35), col, (0.03, 0.03, 0.03), 'MULTIPLY')
    col = mixc(maprange(scales, 0.0, 1.0, 0.38, 0.0), col, (0.3, 0.27, 0.25), 'MULTIPLY')
    col = mixc(math_('MULTIPLY', fold, 0.3), col, (0.3, 0.27, 0.25), 'MULTIPLY')
    col = mixc(math_('MULTIPLY', ridge, 0.5), col, S["ridge"])
    L.new(col, bsdf.inputs["Base Color"])

    # --- bump -----------------------------------------------------------------
    h = math_('ADD', math_('MULTIPLY', maprange(scales, 0.0, 1.0), mixc(big_mask, 0.45, 1.0)),
              math_('MULTIPLY', fold, -1.0))
    h = math_('ADD', h, math_('MULTIPLY', broad, 1.2))
    bump = node("ShaderNodeBump")
    bump.inputs["Strength"].default_value = S.get("bump_strength", 0.55)
    bump.inputs["Distance"].default_value = S.get("bump_distance", 0.012)
    L.new(h, bump.inputs["Height"])
    L.new(bump.outputs[0], bsdf.inputs["Normal"])

    # --- recolour mask for the game shader (R belly, G accent, B eyes) -------
    E = spec["eye"]
    rings = []
    for sx in (1, -1):
        d = node("ShaderNodeVectorMath", operation='DISTANCE')
        L.new(geo.outputs["Position"], d.inputs[0])
        d.inputs[1].default_value = (E["c"][0] * sx, E["c"][1], E["c"][2])
        rings.append(maprange(d.outputs["Value"], E["r"] * 1.6, E["r"] * 2.6, 1.0, 0.0))
    accent = math_('MAXIMUM', ridge, math_('MAXIMUM', rings[0], rings[1]))
    horn = None
    for (pt, rad) in S.get("accent_points", []):
        for sx in (1, -1):
            dn = node("ShaderNodeVectorMath", operation='DISTANCE')
            L.new(geo.outputs["Position"], dn.inputs[0])
            dn.inputs[1].default_value = (pt[0] * sx, pt[1], pt[2])
            m_ = maprange(dn.outputs["Value"], rad * 0.75, rad, 1.0, 0.0)
            horn = m_ if horn is None else math_('MAXIMUM', horn, m_)
    if horn is not None:
        accent = math_('MAXIMUM', accent, horn)
        col2 = mixc(horn, col, S.get("accent_color", (0.04, 0.035, 0.03)))
        L.new(col2, bsdf.inputs["Base Color"])
    comb = node("ShaderNodeCombineColor")
    L.new(belly, comb.inputs[0])
    L.new(accent, comb.inputs[1])
    comb.inputs[2].default_value = 0.0
    emit = node("ShaderNodeEmission")
    emit.name = "MaskEmit"
    L.new(comb.outputs[0], emit.inputs[0])
    mat["has_mask"] = True
    return mat


def stage_mesh(spec, out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    high = build_high(spec)
    acc = build_accessories(spec, high)
    high.data.materials.clear()
    high.data.materials.append(skin_material(spec))
    for poly in high.data.polygons:
        poly.material_index = 0
    # plain preview materials for accessories
    tooth = bpy.data.materials.new("PreviewTooth")
    tooth.use_nodes = True
    tooth.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.38, 0.24, 1)
    tooth.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.35
    claw = bpy.data.materials.new("PreviewClaw")
    claw.use_nodes = True
    claw.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.045, 0.04, 1)
    eye = bpy.data.materials.new("PreviewEye")
    eye.use_nodes = True
    eye.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.08, 0.015, 1)
    eye.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.1
    for o in acc:
        o.data.materials.append(tooth if o.name.startswith("Teeth") else claw if o.name.startswith("Claws") else eye)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, spec["id"] + "_1_mesh.blend"))
    return high


def preview(spec, out, tag, views=None, samples=40):
    setup_cycles(samples)
    if "Ground" not in bpy.data.objects:
        setup_stage()
    V = views or spec["views"]
    only = os.environ.get("DINO_VIEWS")
    for name, v in V.items():
        if only and name not in only.split(","):
            continue
        render_view(os.path.join(out, "renders", f"{spec['id']}_{tag}_{name}.png"), **v)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    species = argv[0] if argv else "trex"
    stage = argv[1] if len(argv) > 1 else "all"
    out = argv[2] if len(argv) > 2 else os.path.join(HERE, "out")
    os.makedirs(os.path.join(out, "renders"), exist_ok=True)
    import specs
    spec = specs.SPECS[species]
    if stage in ("mesh", "all"):
        stage_mesh(spec, out)
        preview(spec, out, "high")
    if stage != "mesh":
        import pipeline
        pipeline.run(spec, stage, out)


if __name__ == "__main__":
    main()
