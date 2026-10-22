# Builds the TRENCHES map terrain: a tree-line trench system as dug in Ukraine (narrow, deep zig-zag slots,
# fighting positions pushed toward the enemy, dugout bays, communication trenches winding back, spoil heaped
# along the lips, shell craters in the field between). Exported for Unity as Resources/Models/Trench.bytes,
# with the leaf-litter / earth / mud textures written to Resources/Textures.
#
# Coordinates: Unity metres. x across the front, z toward the enemy, trench floors at height 0, field ~1.8 m.
import bpy, bmesh, math, random, struct, os
import numpy as np

# the project's Resources folder, wherever the project is checked out (this script lives in BlenderTools/)
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "CarlSandbox", "Resources"))
rng = random.Random(7)

# ------------------------------------------------------------------ layout
lines = []   # (kind, [(x,z)...], halfwidth)   kind 0 friendly main, 1 enemy main, 2 communication/rear, 3 spur
cells = []   # (kind, x, z, r)                kind 0 fighting position, 1 dugout
def zigzag(z0, toward, x0=-45.0, x1=46.0, a=1.0, b=1.8, step=(5.0, 7.5)):
    pts, x, k = [], x0, 0
    while x < x1:
        pts.append((x, z0 + (toward * rng.uniform(a, b) if k % 2 else rng.uniform(-0.3, 0.3))))
        x += rng.uniform(*step); k += 1
    return pts

for side, z0, kind in ((1.0, -22.0, 0), (-1.0, 24.0, 1)):
    main = zigzag(z0, side)
    lines.append((kind, main, 0.5))
    for k in range(1, len(main) - 1):
        px, pz = main[k]
        if k % 2 == 1:   # the forward corners: a sap out to a fighting position
            L = rng.uniform(2.0, 3.4)
            e = (px + rng.uniform(-0.8, 0.8), pz + side * L)
            lines.append((3, [(px, pz), e], 0.42)); cells.append((0, e[0], e[1], 0.85))
        elif k % 4 == 2:  # behind the line: a dugout bay
            e = (px + rng.uniform(-0.5, 0.5), pz - side * 2.0)
            lines.append((3, [(px, pz), e], 0.45)); cells.append((1, e[0], e[1], 1.35))
    # communication trenches winding back to a second line
    zr = z0 - side * 22.0
    for xs in (-16.0, 17.0):
        # join at the nearest point of the main line
        best = min(main, key=lambda p: abs(p[0] - xs))
        pts, x, z = [best], best[0], best[1]
        while (z - zr) * side > 0:
            z -= side * rng.uniform(4.0, 6.0); x += rng.uniform(-2.5, 2.5)
            pts.append((x, max(min(z, 58.0), -58.0)))
        lines.append((2, pts, 0.42))
    rear = zigzag(zr, side, -32.0, 33.0, 0.6, 1.2, (4.0, 6.0))
    lines.append((2, rear, 0.45))
    for k in range(2, len(rear) - 1, 3):
        px, pz = rear[k]; e = (px, pz - side * 1.8)
        lines.append((3, [(px, pz), e], 0.45)); cells.append((1, e[0], e[1], 1.3))

# communication trenches across the field joining the two front lines, so you can work your way over to the
# other line below ground instead of going over the top: each leaves the nearest corner of one main line and
# zig-zags (traverses every few metres, so nothing can fire straight down it) to the nearest corner of the other.
# Fixed points, no random numbers: the rest of the layout stays as it was.
fr_main = [l for l in lines if l[0] == 0][0][1]
en_main = [l for l in lines if l[0] == 1][0][1]
for xs, jog in ((-9.0, 2.2), (19.0, -2.4)):
    a = min(fr_main, key=lambda p: abs(p[0] - xs))
    b = min(en_main, key=lambda p: abs(p[0] - xs))
    n = 9
    pts = [a]
    for k in range(1, n):
        t = k / n
        x = a[0] + (b[0] - a[0]) * t + (jog if k % 2 else -jog) * (0.6 + 0.4 * math.sin(k * 1.7))
        z = a[1] + (b[1] - a[1]) * t
        pts.append((x, z))
    pts.append(b)
    lines.append((2, pts, 0.45))

segA, segB, segW = [], [], []
for kind, pts, hw in lines:
    for i in range(len(pts) - 1):
        segA.append(pts[i]); segB.append(pts[i + 1]); segW.append(hw)
for kind, x, z, r in cells:
    segA.append((x, z)); segB.append((x + 1e-3, z)); segW.append(r)
segA, segB, segW = np.array(segA), np.array(segB), np.array(segW)

def trench_dist(X, Z):
    out = np.empty(X.shape)
    for s in range(0, len(X), 20000):
        P = np.stack([X[s:s + 20000], Z[s:s + 20000]], 1)[:, None, :]
        AB = (segB - segA)[None]
        t = np.clip(((P - segA[None]) * AB).sum(-1) / (AB * AB).sum(-1), 0, 1)
        d = np.linalg.norm(P - segA[None] - t[..., None] * AB, axis=-1) - segW[None]
        out[s:s + 20000] = d.min(1)
    return out

# shell craters: mostly in the field, a few in the tree lines (not on the trenches)
craters = []
while len(craters) < 34:
    x, z = rng.uniform(-44, 44), rng.uniform(-15, 15) if len(craters) < 26 else rng.uniform(-50, 50)
    r = rng.uniform(1.0, 3.2)
    if trench_dist(np.array([x]), np.array([z]))[0] > r + 1.5: craters.append((x, z, r))

# ------------------------------------------------------------------ noise
def hash2(i, j):
    return np.modf(np.sin(i * 127.1 + j * 311.7) * 43758.5453)[0] % 1.0
def vnoise(x, y):
    i, j = np.floor(x), np.floor(y); fx, fy = x - i, y - j
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b, c, d = hash2(i, j), hash2(i + 1, j), hash2(i, j + 1), hash2(i + 1, j + 1)
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy
def fbm(x, y, o=4):
    s, a, n = 0.0, 0.5, 0.0
    for k in range(o):
        s = s + a * vnoise(x * 2 ** k + 17 * k, y * 2 ** k - 31 * k); n += a; a *= 0.5
    return s / n
def sstep(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def height(X, Z):
    S = 1.8 + 0.22 * (fbm(X * 0.04, Z * 0.04) - 0.5) + 0.06 * (fbm(X * 0.45, Z * 0.45) - 0.5)
    d = trench_dist(X, Z) + 0.07 * (vnoise(X * 1.4, Z * 1.4) - 0.5) + 0.04 * (vnoise(X * 4.3, Z * 4.3) - 0.5)
    # spoil thrown up along both lips, lumpy and uneven
    berm = (0.5 * np.exp(-((d - 0.9) / 0.5) ** 2) + 0.16 * np.exp(-((d - 1.9) / 0.9) ** 2)) * (0.45 + 0.9 * fbm(X * 0.3, Z * 0.3, 3))
    berm += 0.08 * (vnoise(X * 2.2, Z * 2.2) - 0.5) * np.exp(-((d - 1.0) / 0.8) ** 2)
    top = S + np.where(d > 0, berm, 0.0)
    for cx, cz, r in craters:
        q = np.hypot(X - cx, Z - cz) / r; dep = 0.35 * r
        top -= np.where(q < 1, dep * (1 - q * q) ** 1.3, 0.0)
        top += 0.22 * dep * np.exp(-((q - 1.1) / 0.28) ** 2)
    e = np.maximum(np.abs(X) - 47.0, 0) + np.maximum(np.abs(Z) - 57.0, 0)
    top += np.minimum(e, 4.0) ** 1.3 * 1.2
    floor = 0.06 * (vnoise(X * 0.8, Z * 0.8) - 0.5) - 0.07 * sstep(0, 0.3, -d)
    return floor + (top - floor) * sstep(0.0, 0.1, d)

# ------------------------------------------------------------------ mesh
for o in list(bpy.data.objects):
    if o.name.startswith("Trench"): bpy.data.objects.remove(o, do_unlink=True)
bm = bmesh.new()
X0, Z0, STEP, NX, NZ = -52.0, -62.0, 0.5, 208, 248
vs = [[bm.verts.new((X0 + i * STEP, Z0 + j * STEP, 0.0)) for j in range(NZ + 1)] for i in range(NX + 1)]
faces = []
for i in range(NX):
    for j in range(NZ):
        faces.append(bm.faces.new((vs[i][j], vs[i + 1][j], vs[i + 1][j + 1], vs[i][j + 1])))
cx = np.array([f.calc_center_median()[0] for f in faces]); cz = np.array([f.calc_center_median()[1] for f in faces])
near = trench_dist(cx, cz) < 2.0
edges = set()
for f, n in zip(faces, near):
    if n: edges.update(f.edges)
bmesh.ops.subdivide_edges(bm, edges=list(edges), cuts=2, use_grid_fill=True)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("TrenchMesh"); bm.to_mesh(me); bm.free()
co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
co[:, 2] = height(co[:, 0], co[:, 1])
me.vertices.foreach_set("co", co.ravel()); me.update()
obj = bpy.data.objects.new("Trench", me); bpy.context.scene.collection.objects.link(obj)
dec = obj.modifiers.new("dec", 'DECIMATE'); dec.ratio = 0.55
dg = bpy.context.evaluated_depsgraph_get()
me2 = bpy.data.meshes.new_from_object(obj.evaluated_get(dg)); obj.modifiers.clear(); obj.data = me2
bm = bmesh.new(); bm.from_mesh(me2); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me2); bm.free()
for p in me2.polygons: p.use_smooth = True
me2.update()

# ------------------------------------------------------------------ export
V = np.empty(len(me2.vertices) * 3); me2.vertices.foreach_get("co", V); V = V.reshape(-1, 3)
N = np.empty(len(me2.vertices) * 3); me2.vertices.foreach_get("normal", N); N = N.reshape(-1, 3)
T = np.empty(len(me2.polygons) * 3, dtype=np.int32); me2.polygons.foreach_get("vertices", T); T = T.reshape(-1, 3)
fn = np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]]); fn /= np.linalg.norm(fn, axis=1)[:, None] + 1e-12
ch = V[T].mean(1)[:, 2]
cls = np.where(fn[:, 2] < 0.8, 0, np.where(ch < 0.4, 1, 2))   # 0 earth wall, 1 mud floor, 2 leaf litter
to_u = lambda a: a[:, [0, 2, 1]]
blob = bytearray(b"TRN1"); blob += struct.pack("<i", 3)
stats = {}
for c in range(3):
    tris = T[cls == c]
    if c == 0:   # walls: every triangle its own vertices (Unity maps the texture per triangle, side-on)
        idx = tris.ravel(); vv, nn = V[idx], N[idx]; tri = np.arange(len(idx), dtype=np.int32).reshape(-1, 3)
    else:
        used, inv = np.unique(tris.ravel(), return_inverse=True); vv, nn = V[used], N[used]; tri = inv.reshape(-1, 3).astype(np.int32)
    tri = tri[:, [0, 2, 1]]     # handedness flips with the axis swap
    blob += struct.pack("<i", len(vv)) + to_u(vv).astype("<f4").tobytes() + to_u(nn).astype("<f4").tobytes()
    blob += struct.pack("<i", len(tri)) + tri.astype("<i4").tobytes()
    stats[c] = (len(vv), len(tri))
blob += struct.pack("<i", len(lines))
for kind, pts, hw in lines:
    blob += struct.pack("<ii", kind, len(pts)) + np.array(pts, dtype="<f4").tobytes()
blob += struct.pack("<i", len(cells))
for kind, x, z, r in cells: blob += struct.pack("<ifff", kind, x, z, r)
blob += struct.pack("<i", len(craters))
for x, z, r in craters: blob += struct.pack("<fff", x, z, r)
os.makedirs(ROOT + r"\Models", exist_ok=True)
with open(ROOT + r"\Models\Trench.bytes", "wb") as f: f.write(blob)
result = {"verts": len(me2.vertices), "tris": len(T), "subs": stats, "bytes": len(blob), "lines": len(lines), "cells": len(cells)}
print("TRENCH_BUILD", result, ROOT)
