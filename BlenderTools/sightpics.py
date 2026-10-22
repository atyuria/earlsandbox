# Sight pictures: each exported gun seen from the shooter's eye on its sight line (the "Iron" point's height),
# so the rear and front sights can be checked for lining up and nothing blocking the view.
# blender --background --python sightpics.py -- <outdir> [Gun_AK47 ...]
import bpy, json, sys, math, os
from mathutils import Vector
M = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/Assets/CarlSandbox/Resources/Models/"
args = sys.argv[sys.argv.index("--") + 1:]
out = args[0]; names = args[1:] or ["Gun_Glock", "Gun_Beretta", "Gun_Taser", "Gun_AR15", "Gun_AK47", "Gun_Shotgun"]
COL = {"Polymer": (0.07, 0.07, 0.075), "Metal": (0.2, 0.21, 0.22), "Steel": (0.62, 0.63, 0.66), "Anod": (0.13, 0.13, 0.14),
       "Wood": (0.42, 0.2, 0.09), "Olive": (0.25, 0.28, 0.18), "Glass": (0.15, 0.25, 0.3), "Rubber": (0.03, 0.03, 0.03),
       "Brass": (0.75, 0.55, 0.2), "Lens": (0.2, 0.3, 0.4), "Tan": (0.55, 0.47, 0.33), "Yellow": (0.9, 0.75, 0.05),
       "Tritium": (0.3, 1, 0.3), "White": (1, 1, 1), "Orange": (1, 0.45, 0.05)}
sc = bpy.context.scene
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True
sh.background_type = "VIEWPORT"; sh.background_color = (0.75, 0.82, 0.9)
sc.render.resolution_x = 900; sc.render.resolution_y = 600
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
for n in names:
    d = json.load(open(M + n + ".json"))
    v = d["verts"]; V = [(v[i], v[i + 2], v[i + 1]) for i in range(0, len(v), 3)]
    me = bpy.data.meshes.new(n); faces = []; fm = []
    for si, s in enumerate(d["subs"]):
        t = s["tris"]
        mt = bpy.data.materials.get(s["mat"]) or bpy.data.materials.new(s["mat"])
        c = COL.get(s["mat"], (0.5, 0.5, 0.5)); mt.diffuse_color = (*c, 1); me.materials.append(mt)
        for k in range(0, len(t), 3): faces.append((t[k], t[k + 1], t[k + 2])); fm.append(si)
    me.from_pydata(V, [], faces); me.polygons.foreach_set("material_index", fm)
    ob = bpy.data.objects.new(n, me); sc.collection.objects.link(ob)
    p = d["points"]; ir = p.get("Iron"); g = p["Grip"]
    pistol = n in ("Gun_Glock", "Gun_Beretta", "Gun_Taser")
    ez = -0.5 if pistol else g[2] - 0.2
    cam.location = (0, ez, ir[1]); cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.angle = math.radians(8 if pistol else 10); cam.data.clip_start = 0.01
    sc.render.filepath = os.path.join(out, "sight_" + n + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(ob)
