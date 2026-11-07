"""
Builds the forest meshes for the Forest world.

 - the CC0 Quaternius Nature Pack vol.3 in third_party/quaternius-nature/OBJ (trees, bushes, grass, rocks)
   -> Assets/Resources/Models/Nature/<name>.json.  The source models are plain gray, so the color is a part code in the
   vertex color: r = 0 for trunk / rock, 1 for foliage;  g = height up the model (0 at the base, 1 at the top).
   The forest shader colors the parts per season.  Base of every model sits at y = 0.
 - two animals generated here in the same faceted style: Bird (wings fold and flap) and Deer (legs swing)
   -> Assets/Resources/Models/Animals/<name>.json.  Nose +Z, up +Y, standing on y = 0, 2 units long.
   uv = (L, lat) like the fish: L is -1 at the tail to +1 at the nose, lat is |x| / widest x.
   Legs carry a swing weight in vertex alpha (1 at the hip, 0 at the hoof) and their diagonal pair in uv.y.

Run:  python tools/convert_nature.py
"""
import json, math, os
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OBJ = os.path.join(ROOT, 'third_party', 'quaternius-nature', 'OBJ')
OUT_N = os.path.join(ROOT, 'Assets', 'Resources', 'Models', 'Nature')
OUT_A = os.path.join(ROOT, 'Assets', 'Resources', 'Models', 'Animals')


def r3(x): return round(float(x), 4)


# ---------------- nature pack ----------------

def load_obj(name):
    path = os.path.join(OBJ, name + '.obj')
    V, tris, mats, cur = [], [], [], None
    for l in open(path):
        p = l.split()
        if not p: continue
        if p[0] == 'v': V.append([float(x) for x in p[1:4]])
        elif p[0] == 'usemtl':
            cur = p[1]
            if cur not in mats: mats.append(cur)
        elif p[0] == 'f':
            idx = [int(x.split('/')[0]) - 1 for x in p[1:]]
            for i in range(1, len(idx) - 1):
                tris.append((idx[0], idx[i], idx[i + 1], mats.index(cur) if cur in mats else 0))
    return np.array(V), tris, mats


def convert_nature():
    os.makedirs(OUT_N, exist_ok=True)
    for f in sorted(os.listdir(OBJ)):
        if not f.endswith('.obj'): continue
        name = f[:-4]
        V, tris, mats = load_obj(name)
        V = V.copy()
        V[:, 1] -= V[:, 1].min()
        top = max(V[:, 1].max(), 1e-6)
        # which material is the trunk: the one lower down on average
        mean_y = {}
        for a, b, c, m in tris: mean_y.setdefault(m, []).append((V[a][1] + V[b][1] + V[c][1]) / 3)
        order = sorted(mean_y, key=lambda m: np.mean(mean_y[m]))
        if name.startswith('Rock'): part = {m: 0.0 for m in mean_y}
        elif name.startswith('Grass'): part = {m: 1.0 for m in mean_y}
        else: part = {m: (0.0 if m == order[0] and len(order) > 1 else 1.0) for m in mean_y}
        v, c = [], []
        for a, b, cc, m in tris:
            for k in (a, b, cc):
                p = V[k]
                v += [r3(p[0]), r3(p[1]), r3(p[2])]
                c += [part[m], round(float(p[1] / top), 3), 0.0]
        with open(os.path.join(OUT_N, name + '.json'), 'w') as fh:
            json.dump({'v': v, 'c': c}, fh, separators=(',', ':'))
        print(name, len(tris), 'tris, height', round(float(top), 2), 'parts', part)


# ---------------- generated animals ----------------

def basis(d):
    d = np.array(d, float); d /= np.linalg.norm(d)
    u = np.cross(d, [1, 0, 0])
    if np.linalg.norm(u) < 0.01: u = np.cross(d, [0, 0, 1])
    u /= np.linalg.norm(u)
    return u, np.cross(d, u)


def tube(p0, p1, r0, r1, sides, col, tag=None):
    p0 = np.array(p0, float); p1 = np.array(p1, float)
    u, w = basis(p1 - p0)
    out = []
    for k in range(sides):
        a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
        q = [p0 + (u * math.cos(a) + w * math.sin(a)) * r0 for a in (a0, a1)] + [p1 + (u * math.cos(a) + w * math.sin(a)) * r1 for a in (a1, a0)]
        out.append((q[0], q[1], q[2], col, tag)); out.append((q[0], q[2], q[3], col, tag))
    out.append((p1 + (u * 0) * 0, p1 + u * r1, p1 + w * r1, col, tag))   # tip cap (rough)
    return out


def loft(z0, z1, cy, wfun, hfun, rings, sides, colfun, cx=0.0):
    pts = []
    for i in range(rings):
        u = i / (rings - 1)
        z = z0 + (z1 - z0) * u
        w, h = wfun(u), hfun(u)
        pts.append([np.array([cx + math.cos(2 * math.pi * k / sides) * w, cy + math.sin(2 * math.pi * k / sides) * h, z]) for k in range(sides)])
    out = []
    for i in range(rings - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            a, b, c, d = pts[i][k], pts[i][k2], pts[i + 1][k], pts[i + 1][k2]
            col = colfun((a[1] + b[1] + c[1] + d[1]) / 4 - cy, (i + 0.5) / (rings - 1))
            out.append((a, c, b, col, None)); out.append((b, c, d, col, None))
    for end, ring, idx in ((0, pts[0], 0), (1, pts[-1], -1)):
        z = z0 - 0.02 if end == 0 else z1 + 0.02
        ctr = np.array([cx, cy, z])
        for k in range(sides):
            col = colfun(ring[k][1] - cy, end)
            out.append((ring[k], ctr, ring[(k + 1) % sides], col, None))
    return out


def finish_animal(name, tris, ground, hip_y=None):
    allp = np.array([[p for p in t[:3]] for t in tris], float).reshape(-1, 3)
    mn, mx = allp.min(0), allp.max(0)
    s = 2.0 / (mx[2] - mn[2])
    cx, cz = (mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2
    cy = mn[1] if ground else (mn[1] + mx[1]) / 2
    maxx = max(np.abs(allp[:, 0] - cx).max(), 1e-6)
    v, c, a, uv = [], [], [], []
    for t in tris:
        tag = t[4]
        for k in range(3):
            p = np.array(t[k], float)
            q = [(p[0] - cx) * s, (p[1] - cy) * s, (p[2] - cz) * s]
            v += [r3(q[0]), r3(q[1]), r3(q[2])]
            c += [round(float(x), 3) for x in t[3]]
            if tag in ('legA', 'legB'):
                wgt = 1.0 - min(max((hip_y - p[1]) / hip_y, 0.0), 1.0)    # 1 at the hip, 0 at the hoof
                a.append(round(wgt, 3))
                uv += [r3(q[2]), 1.0 if tag == 'legA' else 0.0]
            else:
                a.append(1.0)
                uv += [r3(q[2]), r3(abs(p[0] - cx) / maxx)]
    os.makedirs(OUT_A, exist_ok=True)
    with open(os.path.join(OUT_A, name + '.json'), 'w') as fh:
        json.dump({'v': v, 'c': c, 'a': a, 'uv': uv}, fh, separators=(',', ':'))
    print(name, len(tris), 'tris')


def sinp(u): return math.sin(math.pi * min(max(u, 0), 1))


def make_bird():
    back, belly, wing, beak = [0.07, 0.09, 0.14], [0.35, 0.36, 0.4], [0.04, 0.05, 0.1], [0.9, 0.45, 0.05]
    tris = loft(-0.28, 0.28, 0.0, lambda u: 0.02 + 0.09 * sinp(u) ** 0.7, lambda u: 0.02 + 0.095 * sinp(u) ** 0.7, 6, 6,
                lambda y, u: back if y > -0.02 else belly)
    tris += loft(0.28, 0.42, 0.05, lambda u: 0.02 + 0.055 * sinp(0.3 + u * 0.7), lambda u: 0.02 + 0.055 * sinp(0.3 + u * 0.7), 4, 5,
                 lambda y, u: back)
    tris.append((np.array([-0.022, 0.05, 0.42]), np.array([0.022, 0.05, 0.42]), np.array([0, 0.03, 0.56]), beak, None))
    tris.append((np.array([-0.022, 0.05, 0.42]), np.array([0, 0.02, 0.42]), np.array([0, 0.03, 0.56]), beak, None))
    # tail wedge
    tris.append((np.array([-0.07, 0.0, -0.26]), np.array([0.07, 0.0, -0.26]), np.array([0.0, 0.02, -0.58]), back, None))
    tris.append((np.array([-0.07, 0.0, -0.26]), np.array([0.0, 0.02, -0.58]), np.array([-0.1, 0.0, -0.5]), wing, None))
    tris.append((np.array([0.07, 0.0, -0.26]), np.array([0.1, 0.0, -0.5]), np.array([0.0, 0.02, -0.58]), wing, None))
    # wings: two panels each side, angled down a little
    for sg in (-1, 1):
        a = np.array([sg * 0.07, 0.05, 0.14]); b = np.array([sg * 0.07, 0.05, -0.12])
        c = np.array([sg * 0.26, 0.04, -0.02]); d = np.array([sg * 0.24, 0.04, -0.20])
        e = np.array([sg * 0.46, 0.0, -0.2]); f = np.array([sg * 0.42, 0.0, -0.32])
        tris += [(a, c, b, wing, None), (b, c, d, wing, None), (c, e, d, wing, None), (d, e, f, wing, None)]
    finish_animal('Bird', tris, ground=False)


def make_deer():
    tan, pale, dark, white, horn = [0.32, 0.18, 0.08], [0.6, 0.45, 0.3], [0.12, 0.07, 0.04], [0.9, 0.88, 0.8], [0.55, 0.45, 0.3]
    cy = 0.95
    tris = loft(-0.55, 0.55, cy, lambda u: 0.07 + 0.11 * sinp(0.08 + u * 0.84) ** 0.6, lambda u: 0.08 + 0.15 * sinp(0.08 + u * 0.84) ** 0.6, 7, 6,
                lambda y, u: tan if y > -0.04 else pale)
    tris += tube([0, 1.03, 0.48], [0, 1.45, 0.72], 0.1, 0.06, 5, tan)          # neck
    tris += tube([0, 1.45, 0.72], [0, 1.4, 1.0], 0.065, 0.032, 5, tan)         # head
    for sg in (-1, 1):
        tris.append((np.array([sg * 0.04, 1.5, 0.76]), np.array([sg * 0.13, 1.6, 0.74]), np.array([sg * 0.05, 1.56, 0.7]), dark, None))   # ears
        tris += tube([sg * 0.035, 1.5, 0.77], [sg * 0.1, 1.82, 0.72], 0.016, 0.01, 4, horn)    # antlers
        tris += tube([sg * 0.08, 1.7, 0.74], [sg * 0.2, 1.82, 0.7], 0.012, 0.008, 4, horn)
        tris += tube([sg * 0.07, 1.62, 0.75], [sg * 0.17, 1.66, 0.82], 0.012, 0.008, 4, horn)
    tris.append((np.array([0, 1.08, -0.52]), np.array([0, 1.0, -0.7]), np.array([0.04, 1.02, -0.52]), white, None))      # tail
    tris.append((np.array([0, 1.08, -0.52]), np.array([-0.04, 1.02, -0.52]), np.array([0, 1.0, -0.7]), white, None))
    hip = 0.88
    for sx in (-1, 1):
        for sz in (-1, 1):
            tag = 'legA' if sx * sz > 0 else 'legB'
            x, z = sx * 0.1, sz * 0.4
            tris += tube([x, hip, z], [x, 0.0, z + sz * 0.02], 0.05, 0.022, 5, dark, tag)
    finish_animal('Deer', tris, ground=True, hip_y=hip)


if __name__ == '__main__':
    convert_nature()
    make_bird()
    make_deer()
