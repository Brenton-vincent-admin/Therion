"""previews.py - renders of the finished game asset (LOD0 with baked textures) and its animation clips."""
import bpy, os, math
import dinogen as dg
import pipeline as pl

log = dg.log


def tint_material(spec, body, belly, name):
    sid = spec["id"]
    base = bpy.data.materials[sid + "_Mat"]
    mat = base.copy()
    mat.name = name
    nt = mat.node_tree
    N, L = nt.nodes, nt.links
    bsdf = N["Principled BSDF"]
    alb = N["Albedo"]
    tdir = os.path.join(TEXDIR, sid)
    alb.image = bpy.data.images.load(os.path.join(tdir, sid + "_Albedo.png"), check_existing=True)
    msk = N.new("ShaderNodeTexImage")
    msk.image = bpy.data.images.load(os.path.join(tdir, sid + "_Mask.png"), check_existing=True)
    msk.image.colorspace_settings.name = 'Non-Color'
    sep = N.new("ShaderNodeSeparateColor")
    L.new(msk.outputs[0], sep.inputs[0])
    tint = N.new("ShaderNodeMix"); tint.data_type = 'RGBA'
    tint.inputs[6].default_value = (*body, 1)
    tint.inputs[7].default_value = (*belly, 1)
    L.new(sep.outputs[0], tint.inputs[0])
    mul = N.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
    mul.inputs[0].default_value = 1.0
    L.new(alb.outputs[0], mul.inputs[6])
    L.new(tint.outputs[2], mul.inputs[7])
    gain = N.new("ShaderNodeMix"); gain.data_type = 'RGBA'; gain.blend_type = 'MULTIPLY'
    gain.inputs[0].default_value = 1.0
    L.new(mul.outputs[2], gain.inputs[6])
    gain.inputs[7].default_value = (2.2, 2.2, 2.2, 1)
    fin = N.new("ShaderNodeMix"); fin.data_type = 'RGBA'
    L.new(msk.outputs["Alpha"], fin.inputs[0])
    L.new(alb.outputs[0], fin.inputs[6])
    L.new(gain.outputs[2], fin.inputs[7])
    L.new(fin.outputs[2], bsdf.inputs["Base Color"])
    return mat


TEXDIR = ""


def run(spec, out, what=("stills", "lods", "tints", "clips")):
    global TEXDIR
    TEXDIR = os.path.join(out, "unity")
    bpy.ops.wm.open_mainfile(filepath=pl.blend_path(spec, out, "4_anim"))
    sid = spec["id"]
    sc = bpy.context.scene
    rig = bpy.data.objects["Rig"]
    lods = {k: bpy.data.objects[f"{sid}_{k}"] for k in ("LOD0", "LOD1", "LOD2")}
    rdir = os.path.join(out, "renders")
    dg.setup_cycles(48)
    dg.setup_stage()

    def show(key):
        for k, o in lods.items():
            o.hide_render = k != key
    show("LOD0")
    pl.set_action(rig, None)
    V = spec["views"]
    if "stills" in what:
        for name, v in V.items():
            dg.render_view(os.path.join(rdir, f"{sid}_game_{name}.png"), **v)
    if "lods" in what:
        for k in lods:
            show(k)
            dg.render_view(os.path.join(rdir, f"{sid}_{k}.png"), res=(960, 600), **V["hero"])
        show("LOD0")
    if "tints" in what:
        base = lods["LOD0"].data.materials[0]
        for name, body, belly in (("green", (0.30, 0.42, 0.16), (0.75, 0.70, 0.45)),
                                  ("rust", (0.55, 0.26, 0.12), (0.80, 0.66, 0.48)),
                                  ("slate", (0.25, 0.30, 0.38), (0.70, 0.70, 0.68))):
            lods["LOD0"].data.materials[0] = tint_material(spec, body, belly, "Tint_" + name)
            dg.render_view(os.path.join(rdir, f"{sid}_tint_{name}.png"), res=(960, 600), **V["hero"])
        lods["LOD0"].data.materials[0] = base
    if "clips" in what:
        sc.cycles.samples = 20
        k = spec.get("size", 1.0)
        view = dict(target=(0, 0.6 * k, 2.2 * k), dist=17.5 * k, az=-62, el=7, lens=50)
        for act in bpy.data.actions:
            if "frames" not in act:
                continue
            pl.set_action(rig, act.name)
            n = int(act["frames"])
            step = 1 if n <= 30 else (2 if n <= 75 else 3)
            d = os.path.join(rdir, "clips", act.name)
            os.makedirs(d, exist_ok=True)
            for i, f in enumerate(range(0, n + (0 if act.name in ("Idle", "Walk", "Run", "Rest") else 1), step)):
                sc.frame_set(f)
                dg.render_view(os.path.join(d, f"{i:04d}.png"), res=(800, 500), **view)
            with open(os.path.join(d, "fps.txt"), "w") as fh:
                fh.write(str(pl.FPS / step))
