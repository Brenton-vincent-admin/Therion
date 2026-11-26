"""
Species specs. Blender space: Z up, animal faces -Y, left side +X, metres.
Loft rings are (x, y, z, half_width, half_height). Parts with mirror=True are
defined for the LEFT side (+X) and mirrored.

trex is the first "biped hunter" base body. Other biped hunters reuse the same
part list, skeleton and animation code with different numbers.
"""

# ---- T. rex -----------------------------------------------------------------
BODY = [  # neck -> torso -> tail (one continuous skin)
    (0, -3.95, 4.02, 0.36, 0.42),
    (0, -3.60, 3.78, 0.42, 0.50),
    (0, -3.15, 3.42, 0.54, 0.66),
    (0, -2.50, 3.04, 0.74, 0.86),
    (0, -1.70, 2.90, 0.86, 0.98),
    (0, -0.85, 2.93, 0.84, 0.95),
    (0, 0.00, 3.08, 0.74, 0.86),
    (0, 0.80, 3.14, 0.64, 0.80),
    (0, 1.80, 3.12, 0.58, 0.72),
    (0, 3.00, 3.06, 0.47, 0.59),
    (0, 4.30, 2.98, 0.33, 0.43),
    (0, 5.50, 2.90, 0.20, 0.26),
    (0, 6.40, 2.84, 0.09, 0.12),
    (0, 6.90, 2.80, 0.02, 0.03),
]
HEAD = [  # back of skull -> snout tip
    (0, -3.70, 4.06, 0.46, 0.48),
    (0, -4.00, 4.12, 0.52, 0.50),
    (0, -4.28, 4.10, 0.40, 0.46),
    (0, -4.58, 4.03, 0.28, 0.40),
    (0, -4.90, 3.95, 0.245, 0.34),
    (0, -5.16, 3.89, 0.215, 0.28),
    (0, -5.29, 3.86, 0.15, 0.20),
    (0, -5.33, 3.85, 0.06, 0.08),
]
JAW = [  # hinge -> chin
    (0, -3.78, 3.52, 0.44, 0.34),
    (0, -4.10, 3.40, 0.36, 0.29),
    (0, -4.45, 3.33, 0.26, 0.23),
    (0, -4.80, 3.33, 0.21, 0.20),
    (0, -5.08, 3.35, 0.17, 0.16),
    (0, -5.22, 3.37, 0.09, 0.10),
]
# leg joints (left)
HIP, KNEE, ANKLE, BALL, TOE = (0.52, 0.10, 2.98), (0.74, -0.62, 1.88), (0.66, 0.38, 0.74), (0.62, -0.14, 0.13), (0.62, -0.90, 0.05)
SHOULDER, ELBOW, WRIST, FINGER = (0.60, -2.32, 2.42), (0.70, -2.42, 2.02), (0.66, -2.76, 1.90), (0.64, -2.95, 1.80)


def _toe(ang_deg, length, r0=0.105):
    import math
    a = math.radians(ang_deg)
    bx, by, bz = BALL
    dx, dy = math.sin(a), -math.cos(a)
    return [
        (bx - dx * 0.12, by - dy * 0.12, bz + 0.02, r0, r0 * 0.95),
        (bx + dx * length * 0.45, by + dy * length * 0.45, 0.11, r0 * 0.85, r0 * 0.8),
        (bx + dx * length * 0.85, by + dy * length * 0.85, 0.075, r0 * 0.7, r0 * 0.65),
        (bx + dx * length, by + dy * length, 0.06, r0 * 0.45, r0 * 0.45),
    ]


def _toe_tip(ang_deg, length):
    import math
    a = math.radians(ang_deg)
    return (BALL[0] + math.sin(a) * length * 0.97, BALL[1] - math.cos(a) * length * 0.97, 0.075), (math.sin(a), -math.cos(a), -0.12)


LEG = (0, 1, 0)   # reference axis for parts that run mostly vertically

TREX = {
    "id": "trex",
    "voxel": 0.02,
    "smooth_iters": 14,
    "lofts": [
        {"name": "Body", "ctrl": BODY, "n": 2.25, "segs": 40},
        {"name": "Head", "ctrl": HEAD, "n": 2.7, "segs": 36},
        {"name": "Jaw", "ctrl": JAW, "n": 2.6, "segs": 32},
        {"name": "DorsalRidge", "n": 2.0, "segs": 12, "ctrl": [
            (0, -3.6, 4.22, 0.06, 0.06), (0, -3.15, 4.01, 0.07, 0.07), (0, -2.5, 3.87, 0.08, 0.07),
            (0, -1.7, 3.85, 0.08, 0.07), (0, -0.85, 3.85, 0.08, 0.07), (0, 0.0, 3.91, 0.08, 0.07),
            (0, 0.8, 3.91, 0.07, 0.07), (0, 1.8, 3.79, 0.07, 0.06), (0, 3.0, 3.59, 0.05, 0.05),
            (0, 4.3, 3.35, 0.03, 0.03)]},
        {"name": "NasalRidge", "n": 2.0, "segs": 12, "ctrl": [
            (0, -4.33, 4.52, 0.13, 0.05), (0, -4.60, 4.41, 0.12, 0.05), (0, -4.90, 4.27, 0.10, 0.045),
            (0, -5.16, 4.15, 0.07, 0.03)]},
        {"name": "Thigh", "mirror": True, "ref": LEG, "n": 2.2, "ctrl": [
            (0.50, 0.45, 3.35, 0.42, 0.85), (0.64, 0.15, 2.90, 0.54, 0.94), (0.74, -0.22, 2.38, 0.46, 0.70),
            (0.75, -0.55, 1.95, 0.32, 0.42), (0.74, -0.62, 1.76, 0.27, 0.32), (0.73, -0.56, 1.58, 0.15, 0.17)]},
        {"name": "Shin", "mirror": True, "ref": LEG, "n": 2.1, "ctrl": [
            (0.74, -0.56, 1.98, 0.29, 0.38), (0.72, -0.25, 1.52, 0.30, 0.42), (0.69, 0.08, 1.10, 0.22, 0.28),
            (0.66, 0.38, 0.74, 0.16, 0.19)]},
        {"name": "Metatarsus", "mirror": True, "ref": LEG, "n": 2.1, "ctrl": [
            (0.66, 0.41, 0.80, 0.16, 0.18), (0.64, 0.16, 0.45, 0.145, 0.16), (0.62, -0.09, 0.19, 0.17, 0.14),
            (0.62, -0.24, 0.13, 0.18, 0.12)]},
        {"name": "ToeMid", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _toe(0, 0.82, 0.13)},
        {"name": "ToeIn", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _toe(-27, 0.66, 0.115)},
        {"name": "ToeOut", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _toe(27, 0.68, 0.115)},
        {"name": "UpperArm", "mirror": True, "ref": LEG, "n": 2.0, "segs": 14, "ctrl": [
            (0.56, -2.28, 2.60, 0.15, 0.19), (0.66, -2.36, 2.26, 0.12, 0.14), (0.70, -2.42, 2.02, 0.085, 0.095)]},
        {"name": "Forearm", "mirror": True, "ref": (0, 0, 1), "n": 2.0, "segs": 14, "ctrl": [
            (0.70, -2.40, 2.03, 0.085, 0.09), (0.68, -2.60, 1.95, 0.075, 0.08), (0.66, -2.78, 1.89, 0.055, 0.06)]},
        {"name": "Finger1", "mirror": True, "n": 2.0, "segs": 10, "ctrl": [
            (0.66, -2.76, 1.90, 0.045, 0.045), (0.61, -2.88, 1.83, 0.035, 0.035), (0.59, -2.96, 1.77, 0.025, 0.025)]},
        {"name": "Finger2", "mirror": True, "n": 2.0, "segs": 10, "ctrl": [
            (0.67, -2.76, 1.89, 0.045, 0.045), (0.69, -2.89, 1.82, 0.035, 0.035), (0.70, -2.97, 1.76, 0.025, 0.025)]},
    ],
    "ellipsoids": [
        {"name": "Brow", "c": (0.36, -4.10, 4.44), "r": (0.11, 0.26, 0.075), "rot": (-10, 0, 16)},
        {"name": "PostOrbital", "c": (0.41, -3.84, 4.30), "r": (0.08, 0.16, 0.12)},
        {"name": "Cheek", "c": (0.36, -3.90, 3.75), "r": (0.15, 0.28, 0.32)},
        {"name": "Knee", "c": (0.75, -0.66, 1.88), "r": (0.22, 0.19, 0.24)},
        {"name": "Calf", "c": (0.71, 0.00, 1.55), "r": (0.23, 0.28, 0.44), "rot": (-28, 0, 0)},
        {"name": "Heel", "c": (0.66, 0.43, 0.76), "r": (0.16, 0.16, 0.18)},
        {"name": "Hallux", "c": (0.52, 0.20, 0.36), "r": (0.05, 0.07, 0.13), "rot": (25, 0, 0)},
        {"name": "Shoulder", "c": (0.66, -2.30, 2.62), "r": (0.20, 0.30, 0.36)},
        {"name": "Ilium", "c": (0.36, 0.10, 3.66), "r": (0.34, 0.95, 0.22)},
        {"name": "Throat", "c": (0.0, -3.72, 3.46), "r": (0.24, 0.40, 0.22)},
    ],
    "cuts": [   # carved out of the skin after the first remesh (mirrored)
        {"name": "EyeSocket", "c": (0.49, -4.07, 4.30), "r": (0.055, 0.075, 0.055)},
        {"name": "AntorbitalHollow", "c": (0.335, -4.48, 4.14), "r": (0.06, 0.20, 0.14), "rot": (0, 0, -16)},
        {"name": "Nostril", "c": (0.20, -5.18, 4.03), "r": (0.035, 0.06, 0.032)},
        {"name": "Ear", "c": (0.50, -3.80, 4.14), "r": (0.04, 0.035, 0.05)},
    ],
    "head_ctrl": HEAD,
    "jaw_ctrl": JAW,
    "teeth": {
        "upper_count": 15, "upper_y": (-4.24, -5.27), "upper_len": (0.07, 0.17), "upper_x": 0.80, "upper_z": 0.66,
        "lower_count": 13, "lower_y": (-4.30, -5.18), "lower_len": (0.05, 0.10), "lower_x": 0.70, "lower_z": 0.55,
    },
    "claws": [
        {"bone": "Toe", "base": _toe_tip(0, 0.82)[0], "dir": _toe_tip(0, 0.82)[1], "len": 0.25, "r": 0.062},
        {"bone": "Toe", "base": _toe_tip(-27, 0.66)[0], "dir": _toe_tip(-27, 0.66)[1], "len": 0.21, "r": 0.055},
        {"bone": "Toe", "base": _toe_tip(27, 0.68)[0], "dir": _toe_tip(27, 0.68)[1], "len": 0.21, "r": 0.055},
        {"bone": "Metatarsus", "base": (0.52, 0.16, 0.27), "dir": (-0.1, -0.5, -1), "len": 0.10, "r": 0.03},
        {"bone": "Hand", "base": (0.59, -2.95, 1.78), "dir": (-0.2, -0.7, -0.7), "len": 0.09, "r": 0.024},
        {"bone": "Hand", "base": (0.70, -2.96, 1.77), "dir": (0.1, -0.7, -0.7), "len": 0.09, "r": 0.024},
    ],
    "eye": {"c": (0.40, -4.07, 4.30), "r": 0.05, "look": (0.9, -0.42, 0.05)},
    "skin": {
        # kept fairly neutral: the game shader recolours from luminance
        "back": (0.070, 0.058, 0.028), "flank": (0.215, 0.160, 0.075), "belly": (0.52, 0.43, 0.27),
        "stripe": (0.028, 0.022, 0.014), "ridge": (0.040, 0.032, 0.02),
        "stripe_freq": 7.0, "stripe_strength": 0.75,
        "scale_small": 55.0, "scale_big": 20.0,
        "belly_height": (2.6, 3.7),
        "bump_strength": 0.6, "bump_distance": 0.012,
    },
    "joints": {"hip": HIP, "knee": KNEE, "ankle": ANKLE, "ball": BALL, "toe": TOE,
               "shoulder": SHOULDER, "elbow": ELBOW, "wrist": WRIST, "finger": FINGER},
    "spine_y": [0.35, -0.45, -1.25, -2.00, -2.75],      # hips head, then spine joints
    "neck_y": [-3.15, -3.50, -3.85],
    "head_bone": ((0, -3.85, 4.00), (0, -5.30, 3.87)),
    "jaw_bone": ((0, -3.90, 3.66), (0, -5.22, 3.36)),
    "tail": {"y0": 0.35, "y1": 6.85, "bones": 8},
    "mouth_split_y": -4.05,
    "tris": {"LOD0": 9000, "LOD1": 4000, "LOD2": 1500},
    "texture": 2048,
    "gaits": {
        "Walk": {"speed": 2.4, "seconds": 1.6, "duty": 0.62, "lift": 0.42, "bob": 0.05},
        "Run": {"speed": 7.0, "seconds": 0.9, "duty": 0.40, "lift": 0.75, "bob": 0.11},
    },
    "views": {
        "hero": dict(target=(0, 0.3, 2.4), dist=15.5, az=-38, el=9, lens=50),
        "side": dict(target=(0, 0.7, 2.3), dist=17.0, az=-90, el=4, lens=50),
        "head": dict(target=(0, -4.3, 3.95), dist=4.4, az=-48, el=4, lens=60),
        "front": dict(target=(0, -1.0, 2.6), dist=13.0, az=-8, el=6, lens=50),
    },
}

SPECS = {"trex": TREX}
