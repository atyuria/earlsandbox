# Renders the game's own model files (Resources/Models/<name>.json) side-on, to see exactly what is in the game.
#   blender -b --factory-startup --python BlenderTools/preview_json.py -- Gun_AR15 Gun_Glock ...
import bpy, sys, os, json, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.join(os.path.dirname(HERE), "Assets", "CarlSandbox", "Resources", "Models")
PREV = os.path.join(HERE, "previews")
os.makedirs(PREV, exist_ok=True)
names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
COLORS = {"Metal": (0.05, 0.05, 0.055), "Steel": (0.3, 0.3, 0.32), "Blued": (0.025, 0.025, 0.03), "Anod": (0.04, 0.04, 0.045), "Wood": (0.32, 0.12, 0.05),
          "Polymer": (0.03, 0.03, 0.03), "Plum": (0.22, 0.07, 0.05), "Olive": (0.2, 0.2, 0.12), "Glass": (0.1, 0.2, 0.25), "Rubber": (0.02, 0.02, 0.02), "Tan": (0.45, 0.38, 0.25)}
scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
scn.display.shading.light = 'STUDIO'; scn.display.shading.color_type = 'MATERIAL'
scn.display.shading.show_cavity = True
scn.render.resolution_x, scn.render.resolution_y = 1400, 700
for n in names:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for part in (n, n + "_Mag"):
        p = os.path.join(MODELS, part + ".json")
        if not os.path.exists(p): continue
        d = json.load(open(p))
        v = d["verts"]
        verts = [(v[i], v[i + 2], v[i + 1]) for i in range(0, len(v), 3)]
        faces = []; mats = []
        for si, s in enumerate(d["subs"]):
            t = s["tris"]
            for i in range(0, len(t), 3): faces.append((t[i], t[i + 2], t[i + 1])); mats.append(si)
        me = bpy.data.meshes.new(part); me.from_pydata(verts, [], faces); me.update()
        for s in d["subs"]:
            m = bpy.data.materials.get("j_" + s["mat"]) or bpy.data.materials.new("j_" + s["mat"])
            c = COLORS.get(s["mat"], (0.3, 0.3, 0.3)); m.diffuse_color = (c[0], c[1], c[2], 1)
            me.materials.append(m)
        for i, p2 in enumerate(me.polygons): p2.material_index = mats[i]
        o = bpy.data.objects.new(part, me); scn.collection.objects.link(o); objs.append(o)
    if not objs: continue
    bb = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(b.x for b in bb), min(b.y for b in bb), min(b.z for b in bb))); hi = Vector((max(b.x for b in bb), max(b.y for b in bb), max(b.z for b in bb)))
    c = (lo + hi) / 2; ext = hi - lo; size = max(ext.y, ext.x, ext.z * 2.0)   # (2:1 frame: fit length and height)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scn.collection.objects.link(cam); scn.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.1
    for tag, dvec in (("side", Vector((1, 0, 0))), ("34", Vector((0.8, -0.45, 0.4)).normalized())):
        cam.location = c + dvec * 3.0
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scn.render.filepath = os.path.join(PREV, "game_" + n + "_" + tag + ".png")
        bpy.ops.render.render(write_still=True)
print("RENDERED", names)
