# Builds the first-person HAND (only the hand: palm, four fingers, thumb - not the wrist, not the arm) as a set of
# pieces that PoseHand poses joint by joint round whatever you hold, and writes each one to
# Assets/CarlSandbox/Resources/Models/Hand_*.json. Also lays the pieces out as a flat open hand in the scene and
# saves BlenderTools/hand.blend plus a preview render, so the shape can be looked at and edited here.
#
# Proportions of an adult hand (metres): palm 8.2 cm long, 7.4 cm across the knuckles; fingers index 8.3, middle
# 9.1, ring 8.5, little 6.9 cm; thumb 6.8 cm past its base. Every finger tapers from the knuckle to a rounded
# fingertip with a pad under it and a nail on top.
#
# Unity space (what the game uses): a finger piece runs along +Y from its joint (0) to its length, the back of the
# finger (nail side) is +Z. The palm and the thumb's base: +Z toward the fingers, +Y toward the thumb, +X the back
# of the (right) hand. Blender shows the same thing with Y and Z swapped.
import bpy, bmesh, math, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "CarlSandbox", "Resources", "Models"))

# --- must match PoseHand.cs
PALM_LEN, PALM_W, PALM_T = 0.082, 0.074, 0.028
FINGER_R = 0.0082
FINGERS = [[0.039, 0.024, 0.020], [0.043, 0.027, 0.021], [0.040, 0.025, 0.020], [0.032, 0.020, 0.017]]
ACROSS = [0.027, 0.009, -0.009, -0.026]
THUMB = [0.038, 0.030]
THUMB_R = 0.0098

AROUND = 20

def gauss(x, c, w): return math.exp(-((x - c) / w) ** 2)

# ------------------------------------------------------------------ a mesh from rings (Unity coordinates)
class Piece:
    def __init__(self, name):
        self.name = name
        self.verts = []       # unity (x, y, z)
        self.faces = {"Skin": [], "Nail": []}

    def v(self, p): self.verts.append(p); return len(self.verts) - 1

    def loft(self, rings, mat="Skin", close_start=True, close_end=True):
        """rings: list of lists of unity points, all the same count; quads between them, fans at the ends."""
        n = len(rings[0])
        idx = [[self.v(p) for p in r] for r in rings]
        for a, b in zip(idx, idx[1:]):
            for k in range(n):
                k2 = (k + 1) % n
                self.faces[mat].append((a[k], a[k2], b[k2], b[k]))
        def fan(r, flip):
            c = [sum(self.verts[i][j] for i in r) / n for j in range(3)]
            ci = self.v(tuple(c))
            for k in range(n):
                k2 = (k + 1) % n
                self.faces[mat].append((ci, r[k2], r[k]) if not flip else (ci, r[k], r[k2]))
        if close_start: fan(idx[0], False)
        if close_end: fan(idx[-1], True)

    def build(self):
        """The Blender object (Blender coords = unity x, z, y), normals pointed outward."""
        me = bpy.data.meshes.new(self.name)
        bm = bmesh.new()
        bv = [bm.verts.new((p[0], p[2], p[1])) for p in self.verts]
        mats = ["Skin", "Nail"]
        for mi, m in enumerate(mats):
            for f in self.faces[m]:
                try:
                    face = bm.faces.new([bv[i] for i in f])
                    face.material_index = mi
                    face.smooth = True
                except ValueError:
                    pass
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free()
        for m in mats:
            mat = bpy.data.materials.get(m) or bpy.data.materials.new(m)
            mat.diffuse_color = (0.86, 0.71, 0.58, 1) if m == "Skin" else (0.93, 0.8, 0.76, 1)
            me.materials.append(mat)
        o = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(o)
        return o

def export(o, path):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    verts = []
    for v in bm.verts: verts += [round(v.co.x, 6), round(v.co.z, 6), round(v.co.y, 6)]
    names = [m.name.split(".")[0] for m in me.materials]
    subs = {n: [] for n in names}
    for f in bm.faces:
        i = [v.index for v in f.verts]
        subs[names[f.material_index]] += [i[0], i[2], i[1]]     # unity is a mirror of blender: winding flips
    bm.free()
    data = {"name": o.name, "verts": verts, "subs": [{"mat": m, "tris": t} for m, t in subs.items() if t], "points": {}}
    with open(path, "w") as fh: fh.write(json.dumps(data, separators=(",", ":")))
    return len(verts) // 3

# ------------------------------------------------------------------ a finger bone
def segment(name, L, r0, r1, distal=False, knuckle=1.0):
    """Along +Y from the joint at 0 to L; back of the finger +Z. Rounded knuckle ball at the base overlapping the
    piece before it; a rounded end (non-tip) or the fingertip with its pad and nail (tip)."""
    P = Piece(name)
    rings = []
    def ring(y, r, dors=0.9, palm=1.04, flat=1.0, bump=0.0):
        pts = []
        for k in range(AROUND):
            a = 2 * math.pi * k / AROUND
            c, s = math.cos(a), math.sin(a)
            rz = (r * dors * flat + bump) if s > 0 else r * palm
            pts.append((r * 1.02 * c, y, rz * s))
        return pts
    # base: the knuckle, a ball the previous bone tucks into
    rb = r0 * 1.03 * knuckle
    for i in range(6):
        th = math.pi / 2 * (1 - i / 6)
        rings.append(ring(-rb * 0.85 * math.sin(th), max(rb * math.cos(th), 1e-4)))
    if not distal:
        N = 10
        for i in range(N + 1):
            s = i / N
            r = r0 + (r1 - r0) * s
            bump = 0.12 * r0 * gauss(s, 0.03, 0.14) + 0.07 * r1 * gauss(s, 1.0, 0.12)   # knuckle and the next joint
            rings.append(ring(s * L, r, palm=1.03 + 0.06 * math.sin(math.pi * s), bump=bump))
        for i in range(1, 6):
            th = math.pi / 2 * i / 6
            rings.append(ring(L + r1 * 0.75 * math.sin(th), max(r1 * math.cos(th), 1e-4)))
        P.loft(rings, close_start=True, close_end=True)
        return P
    # fingertip: the bone tapers, the pad fills out underneath, the end rounds off
    N = 8
    body = 0.6
    for i in range(N + 1):
        s = i / N * body
        r = r0 + (r1 - r0) * (s / body)
        rings.append(ring(s * L, r, dors=0.82, palm=1.06 + 0.08 * (s / body), bump=0.1 * r0 * gauss(s, 0.03, 0.14)))
    for i in range(1, 9):
        th = math.pi / 2 * i / 9
        y = body * L + (1 - body) * L * math.sin(th)
        r = r1 * math.cos(th) ** 0.8
        rings.append(ring(y, max(r, 1e-4), dors=0.8, palm=1.14))
    P.loft(rings, close_start=True, close_end=True)
    # the nail: a slightly curved plate lying on the back of the tip
    nr, nc = 7, 7
    grid = []
    for i in range(nr):
        s = 0.42 + 0.53 * i / (nr - 1)
        y = s * L
        if s <= body: r = r0 + (r1 - r0) * (s / body)
        else:
            th = math.asin(min(1.0, (s - body) / (1 - body)))
            r = r1 * math.cos(th) ** 0.8
        row = []
        for j in range(nc):
            u = -0.62 + 1.24 * j / (nc - 1)
            a = math.acos(max(-1, min(1, u)))
            x = r * 1.02 * math.cos(a)
            z = r * 0.82 * math.sin(a) + 0.0007
            row.append((x, y, z))
        grid.append([P.v(p) for p in row])
    for i in range(nr - 1):
        for j in range(nc - 1):
            P.faces["Nail"].append((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    return P

# ------------------------------------------------------------------ the palm
def palm():
    P = Piece("Hand_Palm")
    hz = PALM_LEN / 2
    rings = []
    def section(z, scale):
        t = (z + hz) / PALM_LEN                                   # 0 at the wrist .. 1 at the knuckles
        hw = (0.0305 + 0.0065 * t) * scale
        pts = []
        for k in range(AROUND + 8):
            a = 2 * math.pi * k / (AROUND + 8)
            c, s = math.cos(a), math.sin(a)
            p = 2.6
            y = hw * math.copysign(abs(c) ** (2 / p), c)
            ht = 0.0125 if s > 0 else 0.0148                       # back of the hand flatter, palm fuller
            x = ht * scale * math.copysign(abs(s) ** (2 / p), s)
            if s < 0:
                # the ball of the thumb and the heel of the hand under the little finger
                x -= 0.0062 * scale * gauss(y, 0.019, 0.012) * gauss(z, -0.012, 0.024)
                x -= 0.0032 * scale * gauss(y, -0.022, 0.009) * gauss(z, -0.008, 0.03)
                x += 0.0025 * scale * gauss(y, 0.0, 0.012) * gauss(z, 0.0, 0.03)      # the hollow in the middle
            else:
                for ac in ACROSS:
                    x += 0.0028 * scale * gauss(y, ac * 1.0, 0.0065) * gauss(z, hz - 0.006, 0.01)   # knuckles
                    x += 0.0008 * scale * gauss(y, ac * 0.85, 0.004) * gauss(z, 0.0, 0.03)         # tendons
            pts.append((x, y, z))
        return pts
    # wrist end rounded off, the knuckle end rounded off
    for i in range(5):
        th = math.pi / 2 * (1 - i / 5)
        rings.append(section(-hz - 0.008 * math.sin(th), max(math.cos(th) ** 0.6, 0.05)))
    N = 14
    for i in range(N + 1): rings.append(section(-hz + PALM_LEN * i / N, 1.0))
    for i in range(1, 5):
        th = math.pi / 2 * i / 5
        rings.append(section(hz + 0.006 * math.sin(th), max(math.cos(th) ** 0.6, 0.05)))
    P.loft(rings)
    return P

def thumb_base():
    """The ball of the thumb: the muscle over its metacarpal, an egg narrowing toward the thumb."""
    P = Piece("Hand_ThumbBase")
    rings = []
    L = 0.034
    for i in range(13):
        th = math.pi * i / 12
        z = -L / 2 * math.cos(th)
        sc = math.sin(th) * (1.0 - 0.25 * (z / L + 0.5))
        pts = []
        for k in range(AROUND):
            a = 2 * math.pi * k / AROUND
            pts.append((0.0095 * sc * math.cos(a), 0.011 * sc * math.sin(a), z))
        rings.append(pts)
    P.loft(rings)
    return P

# ------------------------------------------------------------------ build, lay out, export
for o in list(bpy.data.objects):
    if o.name.startswith("Hand_"): bpy.data.objects.remove(o, do_unlink=True)
os.makedirs(OUT, exist_ok=True)
made = {}
pieces = [palm(), thumb_base()]
for f in range(4):
    sc = 0.86 if f == 3 else 1.0
    r = [FINGER_R * 1.06 * sc, FINGER_R * 0.98 * sc, FINGER_R * 0.9 * sc, FINGER_R * 0.8 * sc]
    for k in range(3):
        pieces.append(segment(f"Hand_F{f}{k}", FINGERS[f][k], r[k], r[k + 1], distal=(k == 2), knuckle=1.08 if k == 0 else 1.0))
pieces.append(segment("Hand_T1", THUMB[0], THUMB_R * 1.05, THUMB_R * 0.95))
pieces.append(segment("Hand_T2", THUMB[1], THUMB_R * 0.95, THUMB_R * 0.85, distal=True))
objs = {}
for p in pieces:
    o = p.build()
    objs[p.name] = o
    made[p.name] = export(o, os.path.join(OUT, p.name + ".json"))

# the pieces laid out in a row to look at: the palm, the thumb's ball, then each finger's bones end to end
# (each finger piece points up, +Z in blender, its nail toward -Y... i.e. unity +Z = blender +Y)
objs["Hand_Palm"].location = (0.0, 0.0, 0.0)
objs["Hand_ThumbBase"].location = (0.0, -0.06, 0.0)
for f in range(4):
    z = 0.05
    for k in range(3):
        objs[f"Hand_F{f}{k}"].location = (0.03 + f * 0.022, 0.0, z)
        z += FINGERS[f][k]
z = 0.05
for k, n in enumerate(["Hand_T1", "Hand_T2"]):
    objs[n].location = (-0.03, 0.0, z); z += THUMB[k]

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hand.blend"))

# preview render (workbench, from above and a little to the side)
scn = bpy.context.scene
scn.render.engine = "BLENDER_WORKBENCH"
scn.render.resolution_x, scn.render.resolution_y = 900, 900
cam_data = bpy.data.cameras.new("PreviewCam"); cam_data.lens = 60
cam = bpy.data.objects.new("PreviewCam", cam_data); scn.collection.objects.link(cam)
cam.location = (0.16, 0.30, 0.20)
look = (0.04, 0.0, 0.08)
import mathutils
d = mathutils.Vector(look) - cam.location
cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
scn.camera = cam
shots = os.path.abspath(os.path.join(HERE, "..", "Screenshots", "featuretest"))
os.makedirs(shots, exist_ok=True)
scn.render.filepath = os.path.join(shots, "hand_blender.png")
bpy.ops.render.render(write_still=True)
print("HAND_BUILD", made, OUT)
