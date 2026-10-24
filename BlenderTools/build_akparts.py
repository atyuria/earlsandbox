# The gun builder's AK parts: runs gun_akfamily.py once per combination and writes each section as its own model,
# all in the gun's space (receiver front face at z 58, bore y 0), so the game can put any set of them together:
#   AK_Core_<L>        receiver, trigger group, barrel of that length with its gas block and front sight
#   AK_Cover_<kind>    top cover (ribbed AKM / hinged AKS-74U with its rear sight / railed AK-12)
#   AK_Rail            the side rail for optics
#   AK_Hg_<kind>_<L>   handguard for that barrel length
#   AK_Muzzle_<kind>   muzzle device, its origin at the end of the barrel ("Front": its front)
#   AK_Grip_<kind>, AK_Stock_<kind>, AK_Mag_<kind>
#   blender -b --factory-startup --python BlenderTools/build_akparts.py -- [--preview]
import bpy, sys, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "CarlSandbox", "Resources", "Models")
PREV = os.path.join(HERE, "previews")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--preview" in args: OUT = os.path.join(PREV, "json"); os.makedirs(OUT, exist_ok=True)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for f in ("gunkit_base.py", "gunkit2.py", "gunkit3.py"):
    exec(open(os.path.join(HERE, f), encoding="utf-8").read(), {})
dn = bpy.app.driver_namespace
dn["gun_dir"] = HERE

LENGTHS = [206, 314, 415, 590]
HGS = [("wood", {"hg": "wood", "hgm": "Wood"}), ("plum", {"hg": "wood", "hgm": "Plum"}), ("poly", {"hg": "poly"}),
       ("ak12", {"hg": "ak12"}), ("zhukov", {"hg": "zhukov"}), ("b10", {"hg": "b10"})]
COVERS = ["ribbed", "hinged", "railed"]
DEVS = ["slant", "ak74", "short", "booster", "ak12", "dtk1", "pbs1", "pbs4", "thread"]
GRIPS = [("wood", {"grip": "std", "gripm": "Wood"}), ("plum", {"grip": "std", "gripm": "Plum"}), ("poly", {"grip": "std", "gripm": "Polymer"}),
         ("ak12", {"grip": "ak12", "gripm": "Polymer"}), ("moe", {"grip": "moe"}), ("rk3", {"grip": "rk3"})]
STOCKS = [("akm", {"stock": "akm", "stockm": "Wood"}), ("ak74", {"stock": "ak74", "stockm": "Plum"}), ("fold", {"stock": "fold", "stockm": "Polymer"}),
          ("skel", {"stock": "skel"}), ("ak12", {"stock": "ak12", "stockm": "Polymer"}), ("rpk", {"stock": "rpk", "stockm": "Plum"}),
          ("zhukov", {"stock": "zhukov"})]
MAGS = ["762", "762x40", "762x75", "545p", "545b", "545x45", "545x60"]

done = set()
res = {}
shown = []


def export(objs, name, points, shift=0.0):
    if not objs or name in done: return
    done.add(name)
    if shift:
        for o in objs:
            for v in o.data.vertices: v.co.y -= shift * 0.001
            o.data.update()
    G = dn["ak_G"]
    keep_parts, keep_name = G.parts, G.name
    G.parts, G.name = list(objs), name
    res[name] = G.export(os.path.join(OUT, name + ".json"), points)
    G.parts, G.name = keep_parts, keep_name
    shown.append(name)


def run(over, want):
    """Build once with these parts; export the sections named in want (section -> model name)."""
    marks = []
    dn["akmark"] = lambda n: marks.append((n, len(dn["ak_G"].parts)))
    dn["ak"] = "AK74M"
    dn["ak_over"] = over
    ns = {}
    exec(open(os.path.join(HERE, "gun_akfamily.py"), encoding="utf-8").read(), ns)
    G = dn["ak_G"]
    groups = {}
    for i, (n, a) in enumerate(marks):
        b = marks[i + 1][1] if i + 1 < len(marks) else len(G.parts)
        groups.setdefault(n, []).extend(G.parts[a:b])
    mm = 0.001
    for sec, name in want.items():
        if sec == "core":
            export(groups.get("core", []) + groups.get("bipod", []), name,
                   {"Grip": [0, -0.074, -0.122], "Eject": [0.016, 0.006, -0.03], "MagTop": [0, -0.031, -0.008], "Charge": [0.033, 0.006, 0.002],
                    "Iron": [0, 0.034, round(ns["iron_z"] * mm, 4)], "MuzzleBase": [0, 0, round(ns["zm"] * mm, 4)]})
        elif sec == "muzzle":
            export(groups.get("muzzle", []), name, {"Front": [0, 0, round(ns["dl"] * mm, 4)]}, shift=ns["zm"])
        elif sec == "handguard":
            export(groups.get("handguard", []), name, {"Fore": [0, -0.008, round(dn["ak_fore"] * mm, 4)]})
        elif sec == "mag":
            export(list(G.magparts), name, {"MagBottom": ns["mag_bottom"]})
        elif sec == "cover":
            export(groups.get("cover", []), name, {"Rail": [0, 0.0275, -0.06]} if over.get("cover") == "railed" else {})
        elif sec == "stock":
            export(groups.get("stock", []), name, {"Back": [0, -0.06, round(ns["stock_back"] * mm, 4)]})
        else:
            export(groups.get(sec, []), name, {})
    # what wasn't exported goes
    for o in list(bpy.data.objects):
        if o.name not in shown and o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)


k = 0
for L in LENGTHS:
    for hname, hov in HGS:
        over = {"barrel": L, "short": L < 250, "heavy": L > 500, "bipod": L > 500, "rail": True}
        over.update(hov)
        over["cover"] = COVERS[k % len(COVERS)]
        over["dev"] = DEVS[k % len(DEVS)]
        gname, gov = GRIPS[k % len(GRIPS)]; over.update(gov)
        sname, sov = STOCKS[k % len(STOCKS)]; over.update(sov)
        over["mag"] = MAGS[k % len(MAGS)]
        run(over, {"core": f"AK_Core_{L}", "handguard": f"AK_Hg_{hname}_{L}", "cover": "AK_Cover_" + over["cover"], "rail": "AK_Rail",
                   "muzzle": "AK_Muzzle_" + over["dev"], "grip": "AK_Grip_" + gname, "stock": "AK_Stock_" + sname, "mag": "AK_Mag_" + over["mag"]})
        k += 1
print("BUILT", len(res), sorted(res))

# ---- preview sheets: every part from the side, laid out in a grid, one sheet per kind
COLORS = {"Metal": (0.05, 0.05, 0.055), "Steel": (0.12, 0.12, 0.13), "Blued": (0.025, 0.025, 0.03), "Anod": (0.04, 0.04, 0.045), "Wood": (0.32, 0.12, 0.05), "Polymer": (0.03, 0.03, 0.03),
          "Plum": (0.22, 0.07, 0.05), "Glass": (0.1, 0.2, 0.25), "Rubber": (0.02, 0.02, 0.02), "Tan": (0.45, 0.38, 0.25)}
scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
scn.display.shading.light = 'STUDIO'; scn.display.shading.color_type = 'MATERIAL'
scn.display.shading.show_cavity = True; scn.display.shading.cavity_type = 'BOTH'
for m in bpy.data.materials:
    c = COLORS.get(m.name.replace("pv_", "").split(".")[0], (0.3, 0.3, 0.3)); m.diffuse_color = (c[0], c[1], c[2], 1)
cam = bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam")); scn.collection.objects.link(cam); scn.camera = cam
cam.data.type = 'ORTHO'
scn.render.resolution_x, scn.render.resolution_y = 640, 360
IND = os.path.join(PREV, "parts"); os.makedirs(IND, exist_ok=True)
for n in shown:
    o = bpy.data.objects.get(n)
    if o is None: continue
    for q in bpy.data.objects:
        if q.type == 'MESH': q.hide_render = q is not o
    bb = [o.matrix_world @ Vector(v) for v in o.bound_box]
    lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    c = (lo + hi) / 2; ext = hi - lo
    cam.data.ortho_scale = max(ext.y, ext.x, ext.z * 1.78) * 1.25
    d = Vector((0.85, -0.4, 0.35)).normalized()
    cam.location = c + d * 3.0
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = os.path.join(IND, n + ".png")
    bpy.ops.render.render(write_still=True)
