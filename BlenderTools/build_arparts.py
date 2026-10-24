# The gunsmith's AR-15 parts: runs gun_ar15.py once per combination and writes each section as its own model, all
# in the gun's space, so the game can put any set of them together:
#   AR_Core_<L>        upper and lower receivers, buffer tube, rear sight, barrel of that length (mm)
#   AR_Ch_<kind>       charging handle         AR_Hg_<kind>   handguard (with its front sight)
#   AR_Muzzle_<kind>   muzzle device, its origin at the end of the barrel ("Front": its front)
#   AR_Grip_<kind>, AR_Stock_<kind>, AR_Mag_<kind>
#   blender -b --factory-startup --python BlenderTools/build_arparts.py -- [--preview]
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

LENGTHS = [267, 368, 406, 508]
HGS = ["mlok13", "mlok9", "m4", "ras"]
DEVS = ["a2", "sf3p", "pa", "socom"]
GRIPS = ["a2", "moe", "hogue", "k2"]
STOCKS = ["ctr", "moe", "m4", "prs"]
MAGS = ["stanag20", "pmag30", "pmag40", "sf60", "d60"]
CHS = ["milspec", "raptor"]
done, shown, res = set(), [], {}


def export(objs, name, points, shift=0.0):
    if not objs or name in done: return
    done.add(name)
    if shift:
        for o in objs:
            for v in o.data.vertices: v.co.y -= shift * 0.001
            o.data.update()
    G = dn["ar_G"]
    keep_parts, keep_name = G.parts, G.name
    G.parts, G.name = list(objs), name
    res[name] = G.export(os.path.join(OUT, name + ".json"), points)
    G.parts, G.name = keep_parts, keep_name
    shown.append(name)


def run(over, want):
    marks = []
    dn["armark"] = lambda n: marks.append((n, len(dn["ar_G"].parts)))
    dn["ar_over"] = over
    ns = {}
    exec(open(os.path.join(HERE, "gun_ar15.py"), encoding="utf-8").read(), ns)
    G = dn["ar_G"]
    groups = {}
    for i, (n, a) in enumerate(marks):
        b = marks[i + 1][1] if i + 1 < len(marks) else len(G.parts)
        groups.setdefault(n, []).extend(G.parts[a:b])
    mm = 0.001
    for sec, name in want.items():
        if sec == "core":
            export(groups.get("core", []), name, {"Grip": [0, -0.068, -0.14], "Eject": [0.016, 0.003, -0.066], "MagTop": [0, -0.04, -0.04], "Charge": [0, 0.015, -0.172],
                                                  "MuzzleBase": [0, 0, round(ns["zm"] * mm, 4)]})
        elif sec == "muzzle":
            export(groups.get("muzzle", []), name, {"Front": [0, 0, round(ns["dl"] * mm, 4)]}, shift=ns["zm"])
        elif sec == "handguard":
            export(groups.get("handguard", []), name, {"Fore": [0, -0.02, round(dn["ar_fore"] * mm, 4)], "Iron": [0, 0.0605, round(ns["iron_z"] * mm, 4)]})
        elif sec == "mag":
            export(list(G.magparts), name, {"MagBottom": ns["mag_bottom"]})
        elif sec == "stock":
            export(groups.get("stock", []), name, {})
        else:
            export(groups.get(sec, []), name, {})
    for o in list(bpy.data.objects):
        if o.name not in shown and o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)


for k in range(max(len(LENGTHS), len(HGS), len(DEVS), len(GRIPS), len(STOCKS), len(MAGS), len(CHS))):
    over = {"barrel": LENGTHS[k % 4], "hg": HGS[k % len(HGS)], "dev": DEVS[k % len(DEVS)], "grip": GRIPS[k % len(GRIPS)],
            "stock": STOCKS[k % len(STOCKS)], "mag": MAGS[k % len(MAGS)], "ch": CHS[k % len(CHS)]}
    run(over, {"core": f"AR_Core_{over['barrel']}", "handguard": "AR_Hg_" + over["hg"], "muzzle": "AR_Muzzle_" + over["dev"], "grip": "AR_Grip_" + over["grip"],
               "stock": "AR_Stock_" + over["stock"], "mag": "AR_Mag_" + over["mag"], "cover": "AR_Ch_" + over["ch"]})
print("BUILT", len(res), sorted(res))

scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
scn.display.shading.light = 'STUDIO'; scn.display.shading.color_type = 'MATERIAL'
scn.display.shading.show_cavity = True; scn.display.shading.cavity_type = 'BOTH'
COLORS = {"Anod": (0.04, 0.04, 0.045), "Polymer": (0.03, 0.03, 0.03), "Rubber": (0.02, 0.02, 0.02), "Steel": (0.12, 0.12, 0.13), "Metal": (0.05, 0.05, 0.055), "Tan": (0.45, 0.38, 0.25)}
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
