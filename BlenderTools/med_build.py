# Builds the two medical items you can hold: a rolled field BANDAGE (a roll of white gauze with its loose tail)
# and a SURGERY KIT (an olive pouch with a red cross on it). Written to Resources/Models/Med_*.json in the same
# format as the guns (unity vertices, triangles per material, named points: Grip where the hand closes, Muzzle the
# far end).
import bpy, bmesh, math, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "CarlSandbox", "Resources", "Models"))

def obj_from_bm(name, bm, mats):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o

def export(o, path, points):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    verts = []
    for v in bm.verts: verts += [round(v.co.x, 5), round(v.co.z, 5), round(v.co.y, 5)]
    names = [m.name.split(".")[0] for m in me.materials]
    subs = {n: [] for n in names}
    for f in bm.faces:
        i = [v.index for v in f.verts]
        subs[names[f.material_index]] += [i[0], i[2], i[1]]
    bm.free()
    data = {"name": o.name, "verts": verts, "subs": [{"mat": m, "tris": t} for m, t in subs.items() if t],
            "points": {k: list(v) for k, v in points.items()}}
    with open(path, "w") as fh: fh.write(json.dumps(data, separators=(",", ":")))

def B(x, y, z): return (x, z, y)   # unity -> blender

for o in list(bpy.data.objects):
    if o.name.startswith("Med_"): bpy.data.objects.remove(o, do_unlink=True)

# ---- bandage: a roll 5.5 cm across, 6 cm long, lying along unity z; the loose end hanging off it
bm = bmesh.new()
r, L = 0.0275, 0.06
ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=r, radius2=r, depth=L)
bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(math.radians(90), 4, "X"))
for f in bm.faces: f.material_index = 0
# the tail of gauze unrolled from it
tail, back = [], []
for i in range(7):
    t = i / 6
    y = -r - 0.0005 - t * 0.05
    z = -0.02 + 0.015 * math.sin(t * 2.2)
    tail.append((bm.verts.new(B(-L / 2 + 0.004, y, z)), bm.verts.new(B(L / 2 - 0.004, y, z))))
    back.append((bm.verts.new(B(-L / 2 + 0.004, y, z - 0.0006)), bm.verts.new(B(L / 2 - 0.004, y, z - 0.0006))))
for (a, b), (c, d) in zip(zip(tail, tail[1:]), zip(back, back[1:])):
    f = bm.faces.new((a[0], a[1], b[1], b[0])); f.material_index = 0
    f2 = bm.faces.new((c[0], d[0], d[1], c[1])); f2.material_index = 0
o = obj_from_bm("Med_Bandage", bm, ["Gauze"])
export(o, os.path.join(OUT, "Med_Bandage.json"), {"Grip": [0, 0, 0], "Muzzle": [0, 0, 0.04]})

# ---- surgery kit: an olive zip pouch 16 x 10 x 5 cm with a red cross patch on top
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bmesh.ops.scale(bm, vec=(0.05, 0.16, 0.10), verts=bm.verts)      # blender (x, y, z) = unity (x 5, z 16, y 10)
bmesh.ops.translate(bm, vec=(0.0, 0.05, 0.035), verts=bm.verts)
for f in bm.faces: f.material_index = 0
bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.008, segments=3, affect="EDGES")
# red cross patch on the outer side (+x), slightly proud
def quad(x, y0, y1, z0, z1, mi):
    vs = [bm.verts.new((x, y0, z0)), bm.verts.new((x, y1, z0)), bm.verts.new((x, y1, z1)), bm.verts.new((x, y0, z1))]
    f = bm.faces.new(vs); f.material_index = mi
quad(0.0256, 0.035, 0.065, 0.025, 0.045, 1)
quad(0.0256, 0.042, 0.058, 0.018, 0.052, 1)
o = obj_from_bm("Med_Surgery", bm, ["Olive", "Red"])
export(o, os.path.join(OUT, "Med_Surgery.json"), {"Grip": [0, 0.035, 0.02], "Muzzle": [0, 0.035, 0.13]})
print("MED_BUILD ok", OUT)
