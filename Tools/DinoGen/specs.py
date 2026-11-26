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


def _toe(ang_deg, length, r0=0.105, ball=None, k=1.0):
    import math
    a = math.radians(ang_deg)
    bx, by, bz = ball or BALL
    dx, dy = math.sin(a), -math.cos(a)
    return [
        (bx - dx * 0.12 * k, by - dy * 0.12 * k, bz + 0.02 * k, r0, r0 * 0.95),
        (bx + dx * length * 0.45, by + dy * length * 0.45, 0.11 * k, r0 * 0.85, r0 * 0.8),
        (bx + dx * length * 0.85, by + dy * length * 0.85, 0.075 * k, r0 * 0.7, r0 * 0.65),
        (bx + dx * length, by + dy * length, 0.06 * k, r0 * 0.45, r0 * 0.45),
    ]


def _toe_tip(ang_deg, length, ball=None, k=1.0):
    import math
    a = math.radians(ang_deg)
    bx, by, bz = ball or BALL
    return (bx + math.sin(a) * length * 0.97, by - math.cos(a) * length * 0.97, 0.075 * k), (math.sin(a), -math.cos(a), -0.12)


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
        {"name": "Brow", "c": (0.35, -4.25, 4.405), "r": (0.11, 0.25, 0.07), "rot": (14, 0, -20)},
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
        {"name": "EyeSocket", "c": (0.405, -4.25, 4.27), "r": (0.058, 0.078, 0.058)},
        {"name": "Nostril", "c": (0.20, -5.18, 4.03), "r": (0.035, 0.06, 0.032)},
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
    "eye": {"c": (0.40, -4.25, 4.27), "r": 0.054, "look": (0.9, -0.42, 0.05)},
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
        "face": dict(target=(0, -4.4, 4.0), dist=5.5, az=-28, el=2, lens=60),
    },
}

# ---- Carnotaurus ----------------------------------------------------------------
# 8 m long, head carried at about 3 m. Own design: stout swept horns, ochre hide with charcoal saddle bands.
C_BODY = [
    (0, -2.55, 2.62, 0.20, 0.24),
    (0, -2.30, 2.50, 0.22, 0.28),
    (0, -1.95, 2.30, 0.28, 0.36),
    (0, -1.55, 2.12, 0.40, 0.50),
    (0, -1.05, 2.00, 0.50, 0.60),
    (0, -0.50, 2.00, 0.50, 0.58),
    (0, 0.00, 2.10, 0.44, 0.50),
    (0, 0.50, 2.14, 0.40, 0.48),
    (0, 1.20, 2.12, 0.34, 0.42),
    (0, 2.00, 2.08, 0.26, 0.33),
    (0, 2.90, 2.02, 0.17, 0.22),
    (0, 3.80, 1.96, 0.09, 0.12),
    (0, 4.50, 1.92, 0.035, 0.045),
    (0, 4.70, 1.91, 0.010, 0.012),
]
C_HEAD = [
    (0, -2.50, 2.74, 0.20, 0.22),
    (0, -2.68, 2.78, 0.235, 0.245),
    (0, -2.86, 2.76, 0.20, 0.235),
    (0, -3.04, 2.71, 0.165, 0.21),
    (0, -3.18, 2.66, 0.14, 0.17),
    (0, -3.26, 2.63, 0.10, 0.12),
    (0, -3.29, 2.62, 0.04, 0.05),
]
C_JAW = [
    (0, -2.52, 2.46, 0.19, 0.14),
    (0, -2.70, 2.41, 0.17, 0.115),
    (0, -2.90, 2.385, 0.14, 0.09),
    (0, -3.08, 2.375, 0.11, 0.075),
    (0, -3.20, 2.385, 0.08, 0.06),
    (0, -3.25, 2.395, 0.04, 0.04),
]
C_HIP, C_KNEE, C_ANKLE, C_BALL, C_TOE = (0.30, 0.05, 2.05), (0.40, -0.40, 1.22), (0.36, 0.28, 0.52), (0.34, -0.08, 0.09), (0.34, -0.57, 0.035)
C_SHOULDER, C_ELBOW, C_WRIST, C_FINGER = (0.36, -1.42, 1.80), (0.40, -1.36, 1.62), (0.40, -1.30, 1.52), (0.40, -1.27, 1.43)
CK = 0.65


def _ctoe(ang, length, r0):
    return _toe(ang, length, r0, ball=C_BALL, k=CK)


def _ctip(ang, length):
    return _toe_tip(ang, length, ball=C_BALL, k=CK)


CARNO = {
    "id": "carnotaurus_guide",
    "size": CK,
    "voxel": 0.014,
    "smooth_iters": 12,
    "lofts": [
        {"name": "Body", "ctrl": C_BODY, "n": 2.25, "segs": 40},
        {"name": "Head", "ctrl": C_HEAD, "n": 2.8, "segs": 36},
        {"name": "Jaw", "ctrl": C_JAW, "n": 2.6, "segs": 32},
        {"name": "Horn", "mirror": True, "ref": (0, 1, 0), "n": 2.0, "segs": 16, "ctrl": [
            (0.15, -2.66, 2.90, 0.085, 0.075), (0.22, -2.67, 2.99, 0.075, 0.065), (0.30, -2.685, 3.07, 0.055, 0.048),
            (0.36, -2.71, 3.12, 0.032, 0.028), (0.40, -2.74, 3.145, 0.010, 0.010)]},
        {"name": "Thigh", "mirror": True, "ref": LEG, "n": 2.2, "ctrl": [
            (0.28, 0.28, 2.30, 0.24, 0.50), (0.36, 0.10, 1.95, 0.30, 0.54), (0.42, -0.14, 1.58, 0.25, 0.40),
            (0.42, -0.36, 1.28, 0.17, 0.22), (0.41, -0.41, 1.14, 0.14, 0.17), (0.40, -0.37, 1.02, 0.08, 0.09)]},
        {"name": "Shin", "mirror": True, "ref": LEG, "n": 2.1, "ctrl": [
            (0.41, -0.37, 1.28, 0.15, 0.20), (0.40, -0.16, 0.98, 0.16, 0.23), (0.38, 0.06, 0.72, 0.115, 0.15),
            (0.36, 0.28, 0.52, 0.085, 0.10)]},
        {"name": "Metatarsus", "mirror": True, "ref": LEG, "n": 2.1, "ctrl": [
            (0.36, 0.30, 0.56, 0.085, 0.10), (0.35, 0.12, 0.32, 0.075, 0.085), (0.34, -0.05, 0.13, 0.09, 0.075),
            (0.34, -0.16, 0.09, 0.10, 0.065)]},
        {"name": "ToeMid", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _ctoe(0, 0.52, 0.075)},
        {"name": "ToeIn", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _ctoe(-26, 0.42, 0.066)},
        {"name": "ToeOut", "mirror": True, "n": 2.1, "segs": 14, "ctrl": _ctoe(26, 0.43, 0.066)},
        {"name": "UpperArm", "mirror": True, "ref": LEG, "n": 2.0, "segs": 14, "ctrl": [
            (0.33, -1.44, 1.88, 0.08, 0.10), (0.38, -1.40, 1.72, 0.065, 0.075), (0.40, -1.36, 1.62, 0.05, 0.055)]},
        {"name": "Forearm", "mirror": True, "ref": LEG, "n": 2.0, "segs": 14, "ctrl": [
            (0.40, -1.36, 1.63, 0.05, 0.05), (0.40, -1.32, 1.55, 0.045, 0.045), (0.40, -1.29, 1.49, 0.04, 0.04)]},
        {"name": "Finger1", "mirror": True, "ref": LEG, "n": 2.0, "segs": 8, "ctrl": [
            (0.40, -1.29, 1.50, 0.022, 0.022), (0.385, -1.30, 1.44, 0.017, 0.017), (0.375, -1.30, 1.40, 0.010, 0.010)]},
        {"name": "Finger2", "mirror": True, "ref": LEG, "n": 2.0, "segs": 8, "ctrl": [
            (0.405, -1.28, 1.50, 0.022, 0.022), (0.42, -1.265, 1.44, 0.017, 0.017), (0.43, -1.26, 1.40, 0.010, 0.010)]},
        {"name": "Finger3", "mirror": True, "ref": LEG, "n": 2.0, "segs": 8, "ctrl": [
            (0.40, -1.275, 1.50, 0.022, 0.022), (0.40, -1.245, 1.445, 0.017, 0.017), (0.40, -1.23, 1.41, 0.010, 0.010)]},
    ],
    "ellipsoids": [
        {"name": "Brow", "c": (0.185, -2.80, 2.925), "r": (0.06, 0.14, 0.04), "rot": (16, 0, -18)},
        {"name": "Cheek", "c": (0.17, -2.56, 2.58), "r": (0.075, 0.13, 0.16)},
        {"name": "NeckMuscle", "c": (0.12, -2.25, 2.62), "r": (0.14, 0.34, 0.16), "rot": (-28, 0, 0)},
        {"name": "Knee", "c": (0.42, -0.44, 1.22), "r": (0.12, 0.10, 0.13)},
        {"name": "Calf", "c": (0.39, 0.00, 1.00), "r": (0.12, 0.15, 0.25), "rot": (-28, 0, 0)},
        {"name": "Heel", "c": (0.36, 0.31, 0.53), "r": (0.09, 0.09, 0.10)},
        {"name": "Hallux", "c": (0.28, 0.12, 0.24), "r": (0.03, 0.04, 0.07), "rot": (25, 0, 0)},
        {"name": "Shoulder", "c": (0.36, -1.45, 1.92), "r": (0.11, 0.17, 0.20)},
        {"name": "Ilium", "c": (0.20, 0.05, 2.46), "r": (0.20, 0.55, 0.14)},
        {"name": "TailRoot", "c": (0.22, 0.75, 2.02), "r": (0.20, 0.60, 0.30)},
        {"name": "Throat", "c": (0.0, -2.48, 2.40), "r": (0.14, 0.24, 0.13)},
    ],
    "cuts": [
        {"name": "EyeSocket", "c": (0.205, -2.80, 2.83), "r": (0.034, 0.046, 0.034)},
        {"name": "Nostril", "c": (0.115, -3.20, 2.745), "r": (0.02, 0.035, 0.018)},
    ],
    "osteoderms": {     # rows of conical feature scales, placed on the skin by ray casting
        "ctrl": "Body", "y": (-2.2, 3.9), "spacing": 0.17,
        "rows": [{"angle": 0, "r": 0.040, "h": 0.045}, {"angle": 24, "r": 0.034, "h": 0.030},
                 {"angle": 50, "r": 0.030, "h": 0.024}, {"angle": 78, "r": 0.026, "h": 0.018}],
    },
    "head_ctrl": C_HEAD,
    "jaw_ctrl": C_JAW,
    "teeth": {
        "upper_count": 15, "upper_y": (-2.80, -3.25), "upper_len": (0.022, 0.050), "upper_x": 0.80, "upper_z": 0.66,
        "lower_count": 13, "lower_y": (-2.83, -3.21), "lower_len": (0.018, 0.034), "lower_x": 0.70, "lower_z": 0.55,
    },
    "claws": [
        {"bone": "Toe", "base": _ctip(0, 0.52)[0], "dir": _ctip(0, 0.52)[1], "len": 0.15, "r": 0.036},
        {"bone": "Toe", "base": _ctip(-26, 0.42)[0], "dir": _ctip(-26, 0.42)[1], "len": 0.125, "r": 0.031},
        {"bone": "Toe", "base": _ctip(26, 0.43)[0], "dir": _ctip(26, 0.43)[1], "len": 0.125, "r": 0.031},
        {"bone": "Metatarsus", "base": (0.28, 0.10, 0.18), "dir": (-0.1, -0.5, -1), "len": 0.06, "r": 0.018},
    ],
    "eye": {"c": (0.20, -2.80, 2.83), "r": 0.031, "look": (0.9, -0.42, 0.05)},
    "skin": {
        "back": (0.085, 0.070, 0.050), "flank": (0.42, 0.29, 0.13), "belly": (0.66, 0.58, 0.42),
        "stripe": (0.035, 0.030, 0.026), "ridge": (0.05, 0.04, 0.03),
        "stripe_freq": 8.5, "stripe_strength": 0.85,
        "scale_small": 85.0, "scale_big": 30.0,
        "belly_height": (1.6, 2.4),
        "bump_strength": 0.6, "bump_distance": 0.008,
        "accent_points": [((0.30, -2.685, 3.07), 0.16)],          # horns count as accent (mirrored)
        "accent_color": (0.035, 0.030, 0.028),
    },
    "joints": {"hip": C_HIP, "knee": C_KNEE, "ankle": C_ANKLE, "ball": C_BALL, "toe": C_TOE,
               "shoulder": C_SHOULDER, "elbow": C_ELBOW, "wrist": C_WRIST, "finger": C_FINGER},
    "spine_y": [0.22, -0.28, -0.78, -1.25, -1.72],
    "neck_y": [-2.02, -2.30, -2.56],
    "head_bone": ((0, -2.56, 2.70), (0, -3.27, 2.64)),
    "jaw_bone": ((0, -2.58, 2.50), (0, -3.24, 2.39)),
    "tail": {"y0": 0.22, "y1": 4.70, "bones": 8},
    "mouth_split_y": -2.70,
    "tris": {"LOD0": 9500, "LOD1": 4000, "LOD2": 1500},
    "texture": 2048,
    "gaits": {
        "Walk": {"speed": 2.0, "seconds": 1.2, "duty": 0.62, "lift": 0.30, "bob": 0.035},
        "Run": {"speed": 9.0, "seconds": 0.62, "duty": 0.36, "lift": 0.52, "bob": 0.075},
    },
    "ability": "bull_rush",
    "views": {
        "hero": dict(target=(0, 0.3, 1.6), dist=10.3, az=-38, el=9, lens=50),
        "side": dict(target=(0, 0.6, 1.55), dist=11.2, az=-90, el=4, lens=50),
        "head": dict(target=(0, -2.85, 2.68), dist=2.5, az=-48, el=4, lens=60),
        "front": dict(target=(0, -0.6, 1.8), dist=8.6, az=-8, el=6, lens=50),
        "face": dict(target=(0, -2.9, 2.7), dist=3.2, az=-28, el=2, lens=60),
    },
}

# The shipped Carnotaurus: an AI-sculpted base body (made from a render of the guide above, so pose and proportions
# match), dressed with the same procedural horns, osteoderms, teeth, claws, eyes, skin, rig and clips.
import copy as _copy
CARNO_AI = _copy.deepcopy(CARNO)
A_BODY = [   # guide rings fitted to the sculpt (bones, osteoderm rows and lying-down heights read these)
    (0, -2.32, 2.55, 0.22, 0.33), (0, -2.00, 2.33, 0.22, 0.28), (0, -1.50, 2.12, 0.28, 0.37),
    (0, -1.00, 2.05, 0.40, 0.55), (0, -0.50, 2.00, 0.52, 0.70), (0, 0.00, 2.00, 0.58, 0.80),
    (0, 0.50, 2.12, 0.54, 0.72), (0, 1.00, 2.28, 0.42, 0.53), (0, 1.50, 2.36, 0.33, 0.41),
    (0, 2.00, 2.37, 0.28, 0.35), (0, 2.50, 2.37, 0.22, 0.27), (0, 3.00, 2.33, 0.17, 0.21),
    (0, 3.50, 2.30, 0.12, 0.15), (0, 4.00, 2.29, 0.07, 0.09), (0, 4.60, 2.25, 0.025, 0.03), (0, 4.70, 2.24, 0.01, 0.01),
]
A_HEAD = [(0, -2.45, 2.77, 0.25, 0.24), (0, -2.70, 2.71, 0.20, 0.19), (0, -2.90, 2.655, 0.16, 0.165),
          (0, -3.10, 2.59, 0.12, 0.13), (0, -3.22, 2.535, 0.085, 0.085), (0, -3.28, 2.50, 0.04, 0.04)]
A_JAW = [(0, -2.50, 2.365, 0.22, 0.165), (0, -2.70, 2.38, 0.18, 0.14), (0, -2.90, 2.38, 0.15, 0.11),
         (0, -3.10, 2.375, 0.11, 0.085), (0, -3.22, 2.39, 0.075, 0.06), (0, -3.27, 2.40, 0.04, 0.03)]
CARNO_AI.update({
    "id": "carnotaurus",
    "base": {"file": "bases/carnotaurus_base.glb", "length": 8.0, "snout_to_hip": 3.29, "symmetrize": "-",
             "scale_xz": (0.84, 0.84)},
    "smooth_iters": 2,
    "lofts": [
        {"name": "Body", "ctrl": A_BODY},
        {"name": "Head", "ctrl": A_HEAD},
        {"name": "Jaw", "ctrl": A_JAW},
        {"name": "Horn", "with_base": True, "mirror": True, "ref": (0, 1, 0), "n": 2.0, "segs": 16, "ctrl": [
            (0.15, -2.55, 2.90, 0.085, 0.075), (0.23, -2.545, 2.965, 0.075, 0.065), (0.30, -2.54, 3.01, 0.058, 0.050),
            (0.345, -2.535, 3.04, 0.036, 0.031), (0.372, -2.53, 3.056, 0.016, 0.015)]},
    ],
    "ellipsoids": [
        {"name": "Brow", "with_base": True, "c": (0.165, -2.71, 2.835), "r": (0.055, 0.13, 0.035), "rot": (14, 0, -15)},
    ],
    "cuts": [
        {"name": "Mouth", "box": True, "c": (0, -3.045, 2.476), "r": (0.36, 0.41, 0.021), "rot": (7, 0, 0)},
        {"name": "EyeSocket", "c": (0.19, -2.72, 2.77), "r": (0.040, 0.052, 0.040)},
        {"name": "Nostril", "c": (0.07, -3.20, 2.60), "r": (0.015, 0.03, 0.014)},
    ],
    "osteoderms": {"ctrl": "Body", "y": (-2.0, 4.0), "spacing": 0.2,
                   "rows": [{"angle": 0, "r": 0.036, "h": 0.040}, {"angle": 22, "r": 0.032, "h": 0.028},
                            {"angle": 46, "r": 0.028, "h": 0.022}, {"angle": 72, "r": 0.024, "h": 0.016}]},
    "head_ctrl": A_HEAD,
    "jaw_ctrl": A_JAW,
    "teeth": {
        "upper_count": 16, "upper_y": (-2.72, -3.24), "upper_len": (0.032, 0.068), "upper_x": 0.80, "upper_z": 0.66,
        "lower_count": 14, "lower_y": (-2.75, -3.20), "lower_len": (0.024, 0.042), "lower_x": 0.70, "lower_z": 0.55,
    },
    "claws": [
        {"bone": "Toe", "base": (0.672, -0.27, 0.045), "dir": (0.1, -1, -0.1), "len": 0.14, "r": 0.036},
        {"bone": "Toe", "base": (0.48, -0.09, 0.045), "dir": (-0.1, -1, -0.1), "len": 0.12, "r": 0.031},
        {"bone": "Toe", "base": (0.92, 0.035, 0.045), "dir": (0.8, -0.5, -0.1), "len": 0.12, "r": 0.031},
    ],
    "eye": {"c": (0.19, -2.72, 2.77), "r": 0.034, "inset": 0.3, "look": (0.9, -0.42, 0.05)},
    "joints": {"hip": (0.42, 0.35, 2.05), "knee": (0.60, 0.00, 1.18), "ankle": (0.55, 0.68, 0.56),
               "ball": (0.62, 0.36, 0.10), "toe": (0.72, -0.28, 0.035),
               "shoulder": (0.48, -0.62, 1.75), "elbow": (0.50, -0.76, 1.55), "wrist": (0.45, -0.93, 1.38),
               "finger": (0.43, -0.90, 1.22)},
    "spine_y": [0.55, 0.10, -0.40, -0.90, -1.40],
    "neck_y": [-1.75, -2.05, -2.32],
    "head_bone": ((0, -2.32, 2.72), (0, -3.27, 2.52)),
    "jaw_bone": ((0, -2.42, 2.50), (0, -3.25, 2.38)),
    "tail": {"y0": 0.55, "y1": 4.70, "bones": 8},
    "mouth_split_y": -2.72,
})
CARNO_AI["skin"].update({"accent_points": [((0.29, -2.54, 3.00), 0.15)], "accent_color": (0.13, 0.11, 0.085),
                         "belly_height": (1.5, 2.3), "back": (0.10, 0.072, 0.045), "flank": (0.50, 0.30, 0.10),
                         "belly": (0.72, 0.61, 0.40), "stripe": (0.045, 0.034, 0.026), "ridge": (0.06, 0.045, 0.03)})
def _widen(spec, f):
    """Scale a fitted spec sideways by f (used after changing the base mesh's width)."""
    sx = lambda t: (t[0] * f,) + tuple(t[1:])
    for p in spec["lofts"]:
        p["ctrl"] = [(r[0] * f, r[1], r[2], r[3] * f, r[4]) for r in p["ctrl"]]
    spec["head_ctrl"] = [(r[0], r[1], r[2], r[3] * f, r[4]) for r in spec["head_ctrl"]]
    spec["jaw_ctrl"] = [(r[0], r[1], r[2], r[3] * f, r[4]) for r in spec["jaw_ctrl"]]
    for e in spec["ellipsoids"] + spec["cuts"]:
        e["c"] = sx(e["c"])
        e["r"] = sx(e["r"])
    for c in spec["claws"]:
        c["base"] = sx(c["base"])
    spec["eye"]["c"] = sx(spec["eye"]["c"])
    spec["joints"] = {k: sx(v) for k, v in spec["joints"].items()}
    spec["skin"]["accent_points"] = [(sx(pt), r) for pt, r in spec["skin"]["accent_points"]]


# Brenton's notes: a chunky body, a closed mouth with an overbite, black and red hide.
CARNO_AI["base"]["scale_xz"] = (1.0, 0.84)
_widen(CARNO_AI, 1.0 / 0.84)
for _c in CARNO_AI["cuts"]:
    if _c["name"] == "Mouth":
        _c["r"] = (_c["r"][0], _c["r"][1], 0.012)
    if _c["name"] in ("EyeSocket", "Nostril"):      # keep these round
        _c["r"] = (_c["r"][0] * 0.84, _c["r"][1], _c["r"][2])
CARNO_AI["lofts"].append({"name": "MouthFill", "with_base": True, "n": 2.4, "segs": 20, "ctrl": [
    (0, -2.60, 2.531, 0.215, 0.055), (0, -2.80, 2.506, 0.190, 0.052), (0, -3.00, 2.482, 0.148, 0.048),
    (0, -3.15, 2.463, 0.108, 0.042), (0, -3.25, 2.451, 0.060, 0.032)]})
CARNO_AI["jaw_rest"] = -1.6
CARNO_AI["teeth"].update({"upper_count": 15, "upper_len": (0.045, 0.085),
                          "overbite": {"y0": -3.045, "z0": 2.476, "slope": 0.1228, "lift": 0.018, "inset": 0.012},
                          "lower_len": (0.015, 0.028), "lower_x": 0.55})
CARNO_AI["skin"].update({"back": (0.050, 0.050, 0.054), "flank": (0.36, 0.115, 0.055), "belly": (0.33, 0.21, 0.15),
                         "stripe": (0.028, 0.028, 0.031), "ridge": (0.040, 0.040, 0.043),
                         "accent_color": (0.085, 0.08, 0.075), "stripe_strength": 0.9, "dust": (0.42, 0.34, 0.27),
                         "rugged": 1.0, "roughness": 0.85, "nodule": 15.0, "scale_small": 60.0, "scale_big": 22.0,
                         "bump_strength": 1.0, "bump_distance": 0.022})

CARNO_AI["views"] = {
    "hero": dict(target=(0, 0.5, 1.6), dist=10.3, az=-38, el=9, lens=50),
    "side": dict(target=(0, 0.7, 1.55), dist=11.2, az=-90, el=4, lens=50),
    "head": dict(target=(0, -2.85, 2.66), dist=2.6, az=-48, el=4, lens=60),
    "front": dict(target=(0, -0.6, 1.8), dist=8.6, az=-8, el=6, lens=50),
    "face": dict(target=(0, -2.9, 2.68), dist=3.2, az=-28, el=2, lens=60),
}

SPECS = {"trex": TREX, "carnotaurus_guide": CARNO, "carnotaurus": CARNO_AI}
