"""Python prototype / preview of the Dino Valley island generator.
Mirrors Assets/_Game/Scripts/World/Generation/*.cs (same constants and steps) so the
layout can be iterated on and previewed without Unity. Not shipped in the game.
Heights are metres relative to sea level (0)."""
import sys, time
import numpy as np
from PIL import Image, ImageDraw

SEED = 1987
SIZE = 2000.0          # terrain width/length in metres
RES = 1025             # heightmap resolution

# ---------------- noise (identical integer hash in C# IslandNoise) ----------------
def hash01(ix, iy, seed):
    ix = np.asarray(ix).astype(np.int64) & 0xFFFFFFFF
    iy = np.asarray(iy).astype(np.int64) & 0xFFFFFFFF
    h = ((ix * 73856093) ^ (iy * 19349663) ^ ((seed * 83492791) & 0xFFFFFFFF)) & 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 0x5bd1e995) & 0xFFFFFFFF
    h ^= h >> 15
    return (h & 0xFFFFFF).astype(np.float64) / 16777216.0

def vnoise(x, y, seed):
    x = np.asarray(x, np.float64); y = np.asarray(y, np.float64)
    x0 = np.floor(x); y0 = np.floor(y)
    fx = x - x0; fy = y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    x0 = x0.astype(np.int64); y0 = y0.astype(np.int64)
    a = hash01(x0, y0, seed); b = hash01(x0 + 1, y0, seed)
    c = hash01(x0, y0 + 1, seed); d = hash01(x0 + 1, y0 + 1, seed)
    ab = a + (b - a) * fx; cd = c + (d - c) * fx
    return ab + (cd - ab) * fy

def fbm(x, y, seed, octaves=4):
    s = 0.0; amp = 0.5; f = 1.0; norm = 0.0
    for o in range(octaves):
        s = s + amp * vnoise(x * f, y * f, seed + o * 101)
        norm += amp; amp *= 0.5; f *= 2.0
    return s / norm

def snoise(x, y, seed, octaves=4):
    """fbm re-centred to roughly -1..1"""
    return np.clip((fbm(x, y, seed, octaves) - 0.5) * 3.5, -1.5, 1.5)

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)

# ---------------- layout (normalised 0..1, x = east, y = north) ----------------
BIOMES = ["meadow", "riverbanks", "forest", "swamp", "highlands", "lake"]
VORONOI = ["meadow", "forest", "swamp", "highlands", "lake"]
BIOME_SEEDS = {
    "meadow":    [(0.40, 0.42), (0.37, 0.32)],
    "forest":    [(0.17, 0.50), (0.21, 0.27), (0.38, 0.14), (0.62, 0.50)],
    "swamp":     [(0.74, 0.28), (0.63, 0.15), (0.84, 0.40)],
    "highlands": [(0.72, 0.70), (0.58, 0.82), (0.84, 0.60)],
    "lake":      [(0.32, 0.77), (0.17, 0.70), (0.44, 0.87)],
}
BIOME_SCALE = {"meadow": 0.80, "forest": 1.0, "swamp": 1.0, "highlands": 1.0, "lake": 1.0}
BIOME_STAGE = {"meadow": 0, "riverbanks": 0, "forest": 1, "swamp": 2, "highlands": 2, "lake": 3}
VOLCANO = (0.69, 0.68, 0.13, 165.0)    # x, y, radius, height
LAKE = (0.31, 0.77, 0.095, 0.062)      # cx, cy, rx, ry
LAKE_LEVEL = 36.0
SWAMP_LEVEL = 1.6
SEABED = -14.0

RIVERS = [
    dict(name="BigRiver", pts=[(0.405, 0.715), (0.43, 0.675), (0.445, 0.655), (0.47, 0.60), (0.505, 0.52), (0.50, 0.45),
                               (0.485, 0.39), (0.52, 0.30), (0.535, 0.22), (0.515, 0.13), (0.50, 0.02)],
         hw=(7.0, 12.0), depth=(1.0, 2.6), fall=(0.075, 0.105), top=LAKE_LEVEL - 0.3, below=11.0),
    dict(name="WestCreek", pts=[(0.235, 0.665), (0.25, 0.61), (0.275, 0.52), (0.285, 0.44), (0.272, 0.36),
                                (0.252, 0.27), (0.222, 0.18), (0.185, 0.08), (0.17, 0.03)],
         hw=(4.0, 6.0), depth=(1.8, 2.2), fall=None),
    dict(name="EastCreek", pts=[(0.585, 0.615), (0.615, 0.53), (0.645, 0.44), (0.672, 0.36), (0.70, 0.29), (0.72, 0.24)],
         hw=(3.5, 5.0), depth=(1.0, 1.4), fall=None),
]
PONDS = [("MeadowPond", 0.385, 0.455, 20.0), ("HotSpringA", 0.79, 0.575, 9.0), ("HotSpringB", 0.585, 0.79, 8.0)]
GREAT_TREE = (0.335, 0.50)

def catmull(points, per=24):
    p = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = map(np.array, (p[i - 1], p[i], p[i + 1], p[i + 2]))
        for k in range(per):
            t = k / per
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(np.array(points[-1], np.float64))
    return np.array(out)

def polyline_dist(U, V, P, S):
    best = np.full(U.shape, 1e9); bs = np.zeros(U.shape)
    for i in range(len(P) - 1):
        ax, ay = P[i]; bx, by = P[i + 1]
        dx, dy = bx - ax, by - ay
        L2 = dx * dx + dy * dy
        if L2 < 1e-12: continue
        t = np.clip(((U - ax) * dx + (V - ay) * dy) / L2, 0, 1)
        d = np.hypot(U - (ax + t * dx), V - (ay + t * dy))
        m = d < best
        best = np.where(m, d, best)
        bs = np.where(m, S[i] + t * (S[i + 1] - S[i]), bs)
    return best, bs

def sample_grid(H, u, v):
    n = H.shape[0] - 1
    x = np.clip(u * n, 0, n); y = np.clip(v * n, 0, n)
    x0 = np.minimum(np.floor(x).astype(int), n - 1); y0 = np.minimum(np.floor(y).astype(int), n - 1)
    fx = x - x0; fy = y - y0
    return (H[y0, x0] * (1 - fx) * (1 - fy) + H[y0, x0 + 1] * fx * (1 - fy) +
            H[y0 + 1, x0] * (1 - fx) * fy + H[y0 + 1, x0 + 1] * fx * fy)

def generate(res=RES):
    xs = np.linspace(0, 1, res)
    U, V = np.meshgrid(xs, xs)  # rows = north (v), cols = east (u)
    # 1. coastline
    r = np.hypot(U - 0.5, V - 0.5)
    R = 0.425 + 0.065 * snoise(U * 2.2, V * 2.2, SEED + 1, 5)
    land = smoothstep(0.0, 0.07, R - r)

    # 2. biome voronoi with domain warp
    wu = U + 0.07 * snoise(U * 3, V * 3, SEED + 7)
    wv = V + 0.07 * snoise(U * 3, V * 3, SEED + 9)
    D = np.array([np.min([np.hypot(wu - x, wv - y) for (x, y) in BIOME_SEEDS[b]], axis=0) * BIOME_SCALE[b] for b in VORONOI])
    W = np.exp(-(D - D.min(axis=0)) / 0.035); W /= W.sum(axis=0)

    # 3. per-biome base heights, blended
    hl = 45.0 + 30.0 * (1 - np.abs(2 * fbm(U * 4, V * 4, SEED + 19) - 1))
    q = hl / 9.0; fl = np.floor(q)
    hl = (fl + smoothstep(0.35, 0.65, q - fl)) * 9.0           # ledges
    base = [10.0 + 5.0 * fbm(U * 9, V * 9, SEED + 11),         # meadow
            14.0 + 16.0 * fbm(U * 6, V * 6, SEED + 13),        # forest
            0.5 + 2.0 * fbm(U * 14, V * 14, SEED + 17),        # swamp
            hl,                                                # highlands
            40.0 + 8.0 * fbm(U * 7, V * 7, SEED + 23)]         # lake basin plateau
    H = sum(W[i] * base[i] for i in range(len(VORONOI)))

    # 4. volcano
    vx, vy, vr, vh = VOLCANO
    dv = np.hypot(U - vx, V - vy)
    H = H + np.clip(1 - dv / vr, 0, 1) ** 1.6 * vh - np.clip(1 - dv / 0.022, 0, 1) ** 0.8 * 35.0
    # lava gullies and rough rock on the cone (same as IslandGenerator.cs)
    cone = np.clip(1 - dv / vr, 0, 1)
    mid = cone * (1 - cone) * 4
    ang = np.arctan2(V - vy, U - vx)
    around = fbm(np.cos(ang) * 6 + 10, np.sin(ang) * 6 + 10, SEED + 61, 3)
    gully = 1 - np.minimum(1, np.abs(around - 0.5) * 5)
    H = H - gully * gully * 13 * mid + (fbm(U * 70, V * 70, SEED + 63, 3) - 0.5) * 9 * mid

    # 5. lake
    lw = ((U - LAKE[0]) / LAKE[2]) ** 2 + ((V - LAKE[1]) / LAKE[3]) ** 2 + 0.35 * (fbm(U * 10, V * 10, SEED + 29) - 0.5)
    lake_floor = LAKE_LEVEL - 9.0 * np.clip(1 - lw, 0, 1) ** 0.6 - 0.6
    shore = LAKE_LEVEL + 0.4 + (lw - 1) * 30
    H = np.where(lw < 1.0, np.minimum(H, lake_floor), np.minimum(H, np.maximum(shore, lake_floor)))

    # 6. coast
    H = SEABED + (H - SEABED) * land
    raw = H.copy()

    level = np.full(H.shape, np.nan)           # water surface per cell (nan = none)
    level = np.where(lw < 1.15, LAKE_LEVEL, level)

    # 7. rivers
    river_info = []
    riverband = np.zeros(H.shape, bool)
    for ri, rv in enumerate(RIVERS):
        P = catmull(rv["pts"])
        seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
        S = np.concatenate([[0], np.cumsum(seg)]) / seg.sum()
        if rv["fall"]:
            a, b = rv["fall"]
            top = rv["top"] - (rv["top"] - 34.0) * np.clip(S / a, 0, 1)
            fall = 34.0 + (rv["below"] - 34.0) * smoothstep(a, b, S)
            down = (rv["below"] - 0.3) * (1 - np.clip((S - b) / (1 - b), 0, 1)) ** 1.15 + 0.3
            Lp = np.where(S < a, top, np.where(S < b, fall, down))
        else:
            hs = sample_grid(raw, P[:, 0], P[:, 1]) - 1.3
            Lp = np.maximum(np.minimum.accumulate(hs), 0.25)
        d, s = polyline_dist(U, V, P, S)
        dm = d * SIZE
        Lw = np.interp(s, S, Lp)
        hw = rv["hw"][0] + (rv["hw"][1] - rv["hw"][0]) * s + 1.5 * snoise(s * 30, ri * 7.0, SEED + 31, 2)
        depth = rv["depth"][0] + (rv["depth"][1] - rv["depth"][0]) * fbm(s * 25, ri * 3.0 + 0.5, SEED + 37)
        bed = Lw - depth
        inch = bed + np.clip(dm / hw, 0, 1) ** 2 * (Lw + 0.4 - bed)
        bank = Lw + 0.4 + (dm - hw) * 0.22 + np.maximum(dm - hw - 35, 0) ** 2 * 0.04
        carve = np.where(dm < hw, inch, bank)
        H = np.where(land > 0.02, np.minimum(H, carve), H)
        inside = (dm < hw + 1.5) & (land > 0.02)
        level = np.where(inside & (np.isnan(level) | (Lw < level)), Lw, level)
        if ri == 0:
            riverband |= (dm < hw + 30) & (s > rv["fall"][1])
        else:
            riverband |= (dm < hw + 12)
        river_info.append(dict(name=rv["name"], P=P, S=S, level=Lp))

    # 8. ponds & hot springs
    ponds = []
    for (name, cx, cy, rad) in PONDS:
        lvl = float(sample_grid(H, np.array([cx]), np.array([cy]))[0]) - 0.4
        dd = np.hypot(U - cx, V - cy) * SIZE
        floor = lvl - 1.4 * np.clip(1 - (dd / rad) ** 2, 0, 1)
        rim = lvl + 0.3 + np.maximum(dd - rad, 0) * 0.25
        H = np.where(dd < rad * 3, np.minimum(H, np.where(dd < rad, floor, rim)), H)
        level = np.where(dd < rad + 1, lvl, level)
        ponds.append((name, cx, cy, rad, lvl))

    # 9. biome ids
    ids = np.argmin(D, axis=0)
    biome = np.array(VORONOI, dtype=object)[ids]
    in_lake = lw < 1.25
    biome = np.where(in_lake, "lake", biome)
    biome = np.where(riverband & ~in_lake & (biome != "swamp"), "riverbanks", biome)
    biome = np.where(H < -0.5, "ocean", biome)
    level = np.where((biome == "swamp") & np.isnan(level), SWAMP_LEVEL, level)
    level = np.where(np.isnan(level) & (H < 0.0), 0.0, level)  # sea
    sea = np.isnan(level) == False
    sea &= (level == 0.0)
    water = (~np.isnan(level)) & (H < np.nan_to_num(level, nan=-1e9))
    return dict(U=U, V=V, H=H, biome=biome, level=level, water=water, sea=water & sea,
                rivers=river_info, ponds=ponds, lake_w=lw)

COLORS = {"ocean": (52, 112, 170), "meadow": (150, 205, 95), "riverbanks": (225, 205, 140),
          "forest": (45, 125, 60), "swamp": (110, 130, 95), "highlands": (190, 95, 60),
          "lake": (120, 185, 200)}

# ---------------- placement ----------------
def chamfer(mask, cell):
    """2-pass chamfer distance (metres) from True cells; mirrors C# DistanceField."""
    INF = 1e9
    h, w = mask.shape
    D = np.where(mask, 0.0, INF)
    a, b = cell, cell * 1.41421356
    for i in range(h):
        row = D[i].copy()
        if i > 0:
            up = D[i - 1]
            row = np.minimum(row, up + a)
            row[1:] = np.minimum(row[1:], up[:-1] + b)
            row[:-1] = np.minimum(row[:-1], up[1:] + b)
        for j in range(1, w):
            if row[j - 1] + a < row[j]: row[j] = row[j - 1] + a
        D[i] = row
    for i in range(h - 1, -1, -1):
        row = D[i].copy()
        if i < h - 1:
            dn = D[i + 1]
            row = np.minimum(row, dn + a)
            row[1:] = np.minimum(row[1:], dn[:-1] + b)
            row[:-1] = np.minimum(row[:-1], dn[1:] + b)
        for j in range(w - 2, -1, -1):
            if row[j + 1] + a < row[j]: row[j] = row[j + 1] + a
        D[i] = row
    return D

def slope_deg(H, cell):
    gy, gx = np.gradient(H, cell)
    return np.degrees(np.arctan(np.hypot(gx, gy)))

def cover(sources, eligible, target, cell, pick, max_add=80):
    """Greedy: add a point at the farthest eligible cell until all eligible cells are within target metres."""
    m = sources.copy(); added = []
    for _ in range(max_add):
        D = chamfer(m, cell)
        Dm = np.where(eligible, D, -1)
        i, j = np.unravel_index(np.argmax(Dm), Dm.shape)
        if Dm[i, j] <= target: return added, float(Dm[i, j])
        i, j = pick(i, j)
        added.append((i, j)); m[i, j] = True
    D = chamfer(m, cell)
    return added, float(np.where(eligible, D, -1).max())

def best_nearby(score, i, j, rad):
    h, w = score.shape
    i0, i1 = max(0, i - rad), min(h, i + rad + 1); j0, j1 = max(0, j - rad), min(w, j + rad + 1)
    sub = score[i0:i1, j0:j1]
    k = np.unravel_index(np.argmax(sub), sub.shape)
    return i0 + k[0], j0 + k[1]

def place(g, step=4):
    H = g["H"][::step, ::step]; water = g["water"][::step, ::step]; biome = g["biome"][::step, ::step]
    sea = g["sea"][::step, ::step]
    cell = SIZE / (RES - 1) * step
    sl = slope_deg(H, cell)
    land = (~water) & (H > 0.4)
    walk = land & (sl < 38) & (H < 110)
    near_water = chamfer(water & ~sea, cell)
    # nests: one per biome first (flattest dry spot near the biome's first seed), then coverage
    flat_score = np.where(land & (sl < 12) & (near_water > 8), -sl - 0.02 * np.abs(near_water - 30), -1e9)
    nests = []
    for b in ["meadow", "riverbanks", "forest", "swamp", "highlands", "lake"]:
        if b == "riverbanks":
            cand = np.where(biome == "riverbanks", flat_score, -1e9)
            # riverbank nest: closest to the meadow nest
            mi, mj = nests[0][0], nests[0][1]
            ii, jj = np.indices(H.shape)
            sc = cand - 0.05 * np.hypot(ii - mi, jj - mj)
            k = np.unravel_index(np.argmax(sc), sc.shape)
        else:
            x, y = BIOME_SEEDS[b][0]
            i, j = int(y * (H.shape[0] - 1)), int(x * (H.shape[1] - 1))
            cand = np.where(biome == b, flat_score, -1e9)
            k = best_nearby(cand, i, j, int(120 / cell))
        nests.append((k[0], k[1], b))
    src = np.zeros(H.shape, bool)
    for (i, j, _) in nests: src[i, j] = True
    extra, worst_nest = cover(src, walk, 210.0, cell, lambda i, j: best_nearby(flat_score, i, j, int(40 / cell)))
    nests += [(i, j, biome[i, j]) for (i, j) in extra]
    # puddles: water coverage
    puddle_score = np.where(land & (sl < 15), -sl, -1e9)
    puddles, worst_water = cover(water & ~sea, walk, 115.0, cell, lambda i, j: best_nearby(puddle_score, i, j, int(20 / cell)))
    return dict(nests=nests, puddles=puddles, worst_nest=worst_nest, worst_water=worst_water, step=step)

def render(g, p, path):
    H = g["H"]
    img = np.zeros(H.shape + (3,), np.float64)
    for b, c in COLORS.items():
        img[g["biome"] == b] = c
    sh = np.gradient(H, axis=1) - np.gradient(H, axis=0)
    img = img * np.clip(1 + np.tanh(sh * 0.08) * 0.45, 0.5, 1.5)[..., None]
    w = g["water"]
    img[w] = img[w] * 0.3 + np.array([60, 140, 215]) * 0.7
    img = np.clip(img, 0, 255).astype(np.uint8)[::-1]
    S = 1026
    im = Image.fromarray(img).resize((S, S), Image.BILINEAR)
    dr = ImageDraw.Draw(im)
    k = S / (H.shape[0] - 1) * p["step"]
    def xy(i, j): return j * k, S - i * k
    for (i, j) in p["puddles"]:
        X, Y = xy(i, j); dr.ellipse([X - 5, Y - 5, X + 5, Y + 5], fill=(40, 120, 255), outline=(255, 255, 255))
    for n, (i, j, b) in enumerate(p["nests"]):
        X, Y = xy(i, j); r = 11 if n == 0 else 7
        dr.ellipse([X - r, Y - r, X + r, Y + r], fill=(255, 215, 60), outline=(90, 60, 0), width=2)
    vx, vy = VOLCANO[0] * S, S - VOLCANO[1] * S
    dr.polygon([(vx, vy - 14), (vx - 12, vy + 8), (vx + 12, vy + 8)], outline=(60, 20, 10), width=3)
    tx, ty = GREAT_TREE[0] * S, S - GREAT_TREE[1] * S
    dr.ellipse([tx - 9, ty - 9, tx + 9, ty + 9], fill=(20, 90, 30), outline=(255, 255, 255), width=2)
    rv = g["rivers"][0]; a, b = 0.075, 0.105
    fx, fy = np.interp((a + b) / 2, rv["S"], rv["P"][:, 0]), np.interp((a + b) / 2, rv["S"], rv["P"][:, 1])
    dr.rectangle([fx * S - 6, S - fy * S - 6, fx * S + 6, S - fy * S + 6], fill=(255, 255, 255), outline=(30, 80, 160), width=2)
    im.save(path)

if __name__ == "__main__":
    t = time.time()
    g = generate()
    H = g["H"]; cell = SIZE / (RES - 1)
    print("height range %.1f..%.1f  gen %.1fs" % (H.min(), H.max(), time.time() - t))
    land = (H > 0) & ~g["water"]
    print("land %.1f%% of square" % (land.mean() * 100))
    for b in COLORS:
        print("  %-11s %5.1f%% of land" % (b, (g["biome"][H > 0] == b).mean() * 100))
    p = place(g)
    print("nests", len(p["nests"]), "worst nest walk %.0f m" % p["worst_nest"])
    print("puddles", len(p["puddles"]), "worst water walk %.0f m" % p["worst_water"])
    for r in g["rivers"]:
        print(r["name"], "level %.1f -> %.1f" % (r["level"][0], r["level"][-1]))
    for pd in g["ponds"]:
        print(pd[0], "level %.1f" % pd[4])
    render(g, p, sys.argv[1] if len(sys.argv) > 1 else "island_preview.png")
