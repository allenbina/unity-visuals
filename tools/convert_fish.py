"""
Builds the underwater creature meshes used by the Underwater world.

 - the 7 CC0 Quaternius models in third_party/quaternius-fish/OBJ are converted (flat colors from their .mtl files)
 - 4 more reef fish are generated here in the same faceted style (tang, yellow tang, needlefish, pufferfish)

Every model comes out the same way: nose toward +Z, up is +Y, centered, 2 units long (z from -1 to +1),
one color per triangle, vertices not shared (the shader flat-shades from screen-space derivatives).
Per vertex uv = (L, lat): L is -1 at the tail to +1 at the nose, lat is |x| / widest x. The vertex shader wags the tail
or flaps the wings from these.

Output: Assets/Resources/Models/Fish/<name>.json (loaded as TextAssets at run time).
Run:  python tools/convert_fish.py
"""
import json, math, os, sys
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OBJ = os.path.join(ROOT, 'third_party', 'quaternius-fish', 'OBJ')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Models', 'Fish')

# models whose nose points to -Z in the source, if any (set after looking at the side-view sheet)
FLIP = {}


def load_obj(name):
    path = os.path.join(OBJ, name + '.obj')
    mats, cur = {}, None
    for l in open(path[:-4] + '.mtl'):
        p = l.split()
        if p and p[0] == 'newmtl': cur = p[1]
        elif p and p[0] == 'Kd': mats[cur] = [float(x) for x in p[1:4]]
    V, T, cm = [], [], None
    for l in open(path):
        p = l.split()
        if not p: continue
        if p[0] == 'v': V.append([float(x) for x in p[1:4]])
        elif p[0] == 'usemtl': cm = p[1]
        elif p[0] == 'f':
            idx = [int(x.split('/')[0]) - 1 for x in p[1:]]
            for i in range(1, len(idx) - 1):
                T.append((idx[0], idx[i], idx[i + 1], mats.get(cm, [0.7, 0.7, 0.7])))
    return np.array(V), T


def finish(name, tris):
    """tris: list of (p0, p1, p2, color). Normalize and write."""
    P = np.array([[p for p in t[:3]] for t in tris], dtype=float).reshape(-1, 3)
    if FLIP.get(name): P[:, 2] *= -1
    mn, mx = P.min(0), P.max(0)
    P[:, 0] -= (mn[0] + mx[0]) / 2
    P[:, 1] -= (mn[1] + mx[1]) / 2
    P[:, 2] -= (mn[2] + mx[2]) / 2
    s = 2.0 / (mx[2] - mn[2])
    P *= s
    maxx = max(np.abs(P[:, 0]).max(), 1e-6)
    flip = FLIP.get(name)
    v, c, uv = [], [], []
    for i, t in enumerate(tris):
        for k in range(3):
            p = P[i * 3 + k]
            v += [round(float(p[0]), 4), round(float(p[1]), 4), round(float(p[2]), 4)]
            c += [round(float(t[3][0]), 3), round(float(t[3][1]), 3), round(float(t[3][2]), 3)]
            uv += [round(float(p[2]), 4), round(float(abs(p[0]) / maxx), 4)]
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + '.json'), 'w') as f:
        json.dump({'v': v, 'c': c, 'uv': uv}, f, separators=(',', ':'))
    print(name, len(tris), 'tris, source size', [round(x * s, 2) for x in (mx - mn)])


def convert_quaternius():
    for name in ['Dolphin', 'Fish1', 'Fish2', 'Fish3', 'Manta ray', 'Shark', 'Whale']:
        V, T = load_obj(name)
        tris = [(V[a], V[b], V[c], col) for a, b, c, col in T]
        finish(name.replace(' ', ''), tris)


# ---------------- generated reef fish, in the same faceted style ----------------

def lofted_fish(name, width_prof, height_prof, back, belly, fin, tail, bands=None, dorsal=None, tail_size=0.5, sides=6, rings=9, tail_fork=0.25):
    """A body lofted along z (tail at -1, nose at +1) plus a tail fin and a dorsal fin, all flat-colored."""
    zs = [-0.7 + 1.7 * i / (rings - 1) for i in range(rings)]
    ring_pts = []
    for z in zs:
        u = (z + 0.7) / 1.7
        w, h = width_prof(u), height_prof(u)
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            ring.append(np.array([math.cos(a) * w, math.sin(a) * h, z]))
        ring_pts.append(ring)
    tris = []

    def color_for(y, u):
        col = np.array(back if y > -0.05 else belly)
        if bands:
            for (u0, u1, bc) in bands:
                if u0 <= u <= u1: col = np.array(bc)
        return col

    for i in range(rings - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            a, b, c, d = ring_pts[i][k], ring_pts[i][k2], ring_pts[i + 1][k], ring_pts[i + 1][k2]
            u = (i + 0.5) / (rings - 1)
            y = (a[1] + b[1] + c[1] + d[1]) / 4
            col = color_for(y, u)
            tris.append((a, c, b, col)); tris.append((b, c, d, col))
    # nose and tail caps
    nose = np.array([0, 0, 1.0]); tcap = np.array([0, 0, -0.72])
    for k in range(sides):
        k2 = (k + 1) % sides
        tris.append((ring_pts[-1][k], nose, ring_pts[-1][k2], np.array(back if ring_pts[-1][k][1] > -0.05 else belly)))
        tris.append((ring_pts[0][k2], tcap, ring_pts[0][k], np.array(tail)))
    # tail fin: a forked triangle pair in the x=0 plane
    z0, z1 = -0.7, -1.0
    ts = tail_size
    tris.append((np.array([0, 0.03, z0]), np.array([0, ts, z1]), np.array([0, tail_fork * ts, z0 - 0.12]), np.array(tail)))
    tris.append((np.array([0, -0.03, z0]), np.array([0, -tail_fork * ts, z0 - 0.12]), np.array([0, -ts, z1]), np.array(tail)))
    # dorsal fin
    if dorsal:
        z_a, z_b, hgt = dorsal
        u_a = (z_a + 0.7) / 1.7
        top = height_prof(u_a)
        tris.append((np.array([0, top * 0.85, z_a]), np.array([0, top + hgt, z_b]), np.array([0, top * 0.85, z_b]), np.array(fin)))
    # pectoral fins (small triangles to each side)
    z_p = 0.35
    u_p = (z_p + 0.7) / 1.7
    wp, hp = width_prof(u_p), height_prof(u_p)
    for sgn in (-1, 1):
        tris.append((np.array([sgn * wp * 0.9, -hp * 0.2, z_p]), np.array([sgn * (wp + 0.22), -hp * 0.55, z_p - 0.28]), np.array([sgn * wp * 0.9, -hp * 0.5, z_p - 0.25]), np.array(fin)))
    # pull the nose cap into the 2-unit range: rescale happens in finish()
    finish(name, [(t[0], t[1], t[2], t[3]) for t in tris])


def bump(u, c, w): return math.exp(-((u - c) / w) ** 2)


def generate():
    def prof_w(a, b, c, w): return lambda u: a + b * bump(u, c, w)
    # (back, belly, fin, tail) colors are linear values like the Quaternius materials
    lofted_fish('Tang', prof_w(0.05, 0.19, 0.55, 0.28), prof_w(0.04, 0.50, 0.55, 0.26),
                [0.03, 0.18, 0.7], [0.1, 0.35, 0.8], [0.03, 0.12, 0.5], [0.9, 0.6, 0.02],
                bands=[(0.50, 0.62, [0.01, 0.02, 0.08])], dorsal=(0.55, -0.55, 0.25), tail_size=0.45)
    lofted_fish('YellowTang', prof_w(0.05, 0.17, 0.5, 0.3), prof_w(0.04, 0.42, 0.5, 0.28),
                [0.95, 0.6, 0.02], [1.0, 0.75, 0.15], [0.9, 0.5, 0.02], [0.95, 0.65, 0.02],
                bands=[(0.0, 0.08, [0.9, 0.9, 0.85])], dorsal=(0.6, -0.5, 0.2), tail_size=0.35)
    lofted_fish('Needlefish', prof_w(0.03, 0.02, 0.5, 0.4), prof_w(0.03, 0.02, 0.5, 0.4),
                [0.12, 0.45, 0.35], [0.8, 0.88, 0.9], [0.1, 0.35, 0.3], [0.25, 0.55, 0.45],
                dorsal=(-0.45, -0.7, 0.08), tail_size=0.14, rings=11)
    lofted_fish('Puffer', prof_w(0.12, 0.5, 0.6, 0.33), prof_w(0.1, 0.46, 0.6, 0.33),
                [0.55, 0.38, 0.12], [0.9, 0.85, 0.6], [0.45, 0.3, 0.1], [0.5, 0.35, 0.1],
                bands=[(0.2, 0.35, [0.3, 0.2, 0.06]), (0.62, 0.72, [0.3, 0.2, 0.06])], dorsal=(-0.1, -0.35, 0.12), tail_size=0.22, sides=7)


if __name__ == '__main__':
    convert_quaternius()
    generate()
