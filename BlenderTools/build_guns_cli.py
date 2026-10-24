# Builds guns headless and exports them to the game, with preview renders to check them by eye.
#   blender -b --factory-startup --python BlenderTools/build_guns_cli.py -- gun_ak74m.py gun_pkm.py ...
# Each gun script leaves (G, points) in bpy.app.driver_namespace["lastgun"], as export_guns.py expects.
# Writes Assets/CarlSandbox/Resources/Models/<name>.json and BlenderTools/previews/<name>_side.png / _34.png.
import bpy, sys, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "CarlSandbox", "Resources", "Models")
PREV = os.path.join(HERE, "previews")
os.makedirs(PREV, exist_ok=True)

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
render = "--norender" not in args
# --preview: render only, export to BlenderTools/previews/json (never touches the game's models)
if "--preview" in args: OUT = os.path.join(PREV, "json"); os.makedirs(OUT, exist_ok=True)
args = [a for a in args if not a.startswith("--")]

# (the factory scene's cube, camera and lamp would sit right over the gun)
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for f in ("gunkit_base.py", "gunkit2.py"):
    exec(open(os.path.join(HERE, f), encoding="utf-8").read(), {})
if os.path.exists(os.path.join(HERE, "gunkit3.py")):
    exec(open(os.path.join(HERE, "gunkit3.py"), encoding="utf-8").read(), {})
bpy.app.driver_namespace["gun_dir"] = HERE

COLORS = {"Metal": (0.05, 0.05, 0.055), "Steel": (0.12, 0.12, 0.13), "Blued": (0.025, 0.025, 0.03), "Anod": (0.04, 0.04, 0.045), "Wood": (0.32, 0.12, 0.05), "Polymer": (0.03, 0.03, 0.03),
          "Plum": (0.22, 0.07, 0.05), "Olive": (0.2, 0.2, 0.12), "Glass": (0.1, 0.2, 0.25), "Rubber": (0.02, 0.02, 0.02),
          "Tan": (0.45, 0.38, 0.25), "Brass": (0.6, 0.45, 0.15)}


def preview(obj, name):
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_WORKBENCH'
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.color_type = 'MATERIAL'
    scn.display.shading.show_cavity = True
    scn.display.shading.cavity_type = 'BOTH'
    scn.render.resolution_x, scn.render.resolution_y = 1400, 700
    scn.render.film_transparent = False
    for m in obj.data.materials:
        if m is None: continue
        c = COLORS.get(m.name.replace("pv_", ""), (0.3, 0.3, 0.3))
        m.diffuse_color = (c[0], c[1], c[2], 1)
    bb = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    c = (lo + hi) / 2; ext = hi - lo; size = max(ext.y, ext.x, ext.z * 2.0)   # (2:1 frame: fit length and height)
    cam = bpy.data.objects.get("PrevCam")
    if cam is None:
        cam = bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam"))
        scn.collection.objects.link(cam)
    scn.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = size * 1.1
    for tag, d in (("side", Vector((1, 0, 0))), ("34", Vector((0.8, -0.45, 0.4)).normalized())):
        cam.location = c + d * 3.0
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scn.render.filepath = os.path.join(PREV, name + "_" + tag + ".png")
        bpy.ops.render.render(write_still=True)


res = {}
for f in args:
    ns = {}
    exec(open(os.path.join(HERE, f), encoding="utf-8").read(), ns)
    G, pts = bpy.app.driver_namespace["lastgun"]
    mags = list(getattr(G, "magparts", []))
    st = G.export(os.path.join(OUT, G.name + ".json"), pts)
    if mags:
        # the magazine, in the gun's own space (it sits in the gun as it is, and comes out on a reload)
        main = G.name
        G.parts = mags; G.name = main + "_Mag"
        G.export(os.path.join(OUT, G.name + ".json"), {})
        G.name = main
    res[G.name] = st
    if render:
        o = bpy.data.objects.get(G.name)
        if o is not None: preview(o, G.name)
    # clear the work collection for the next gun
    col = bpy.data.collections.get("GunWork")
    if col:
        for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
print("BUILT", res)
