# The gunsmith's attachments, modelled on the real things (the same ones Tarkov has): optics, weapon lights,
# aiming lasers and foregrips. Origin on the top of the rail they clamp to, y up (lights and grips hang below it,
# y negative), z forward, sizes in mm. Optics: their sight line is the "Eye" point's height, the glass is the
# "Glass" material (its rearmost face is the eyepiece the game draws on). Lights: "Lens". Lasers: "Beam".
#   blender -b --factory-startup --python BlenderTools/build_attachments.py -- [--preview]
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
K = bpy.app.driver_namespace["gunkit3"]
A, P, Gl, R, S, T, Ln = "Anod", "Polymer", "Glass", "Rubber", "Steel", "Tan", "Lens"
MATS = [A, P, Gl, R, S, T, Ln, "Olive"]
shown = []
res = {}


def new(name):
    return K.Gun3(name, list(MATS))


def finish(G, pts):
    res[G.name] = G.export(os.path.join(OUT, G.name + ".json"), {k: [v[0] / 1000, v[1] / 1000, v[2] / 1000] for k, v in pts.items()})
    shown.append(G.name)


def clamp(G, z0, z1, w=22, mat=A):
    """A Picatinny clamp: a flat base on the rail, the jaws either side, a crossbolt nut on the right."""
    G.box((0, 3, (z0 + z1) / 2), (w, 6, z1 - z0), mat, bevel=1.0)
    for s in (-1, 1):
        G.prof([(z0 + 3, 6), (z1 - 3, 6), (z1 - 5, -3), (z0 + 5, -3)], s * 11.2, s * 14.2, mat, bevel=0.6)
    zc = z0 + (z1 - z0) * 0.35
    G.cyl((14, 0.5, zc), (19, 0.5, zc), 5, S, seg=6)
    G.cyl((-14, 0.5, zc), (-16, 0.5, zc), 3.2, S, seg=10)


def clamp_under(G, z0, z1, mat=A):
    """The same clamp upside down: for things that hang under a rail (lights, lasers on the side rail, grips)."""
    G.box((0, -3, (z0 + z1) / 2), (22, 6, z1 - z0), mat, bevel=1.0)
    for s in (-1, 1):
        G.prof([(z0 + 3, -6), (z1 - 3, -6), (z1 - 5, 3), (z0 + 5, 3)], s * 11.2, s * 14.2, mat, bevel=0.6)
    G.cyl((14, -1, (z0 + z1) / 2), (19, -1, (z0 + z1) / 2), 5, S, seg=6)


# ============================================================ optics
# ---- Trijicon RMR Type 2 on a low riser: the small forged housing with its two "ears" over the window
G = new("Att_RMR")
clamp(G, -20, 20)
G.box((0, 12, 0), (24, 12, 42), A, bevel=1.2)                                       # riser
G.prof([(-22, 18), (22, 18), (22, 23), (12, 25, 3), (-18, 25, 3), (-22, 23)], -12, 12, A, bevel=1.2, seg=2)   # body
fr = G.xsec([(-12.5, 22), (12.5, 22), (12.5, 35, 5), (7, 41.5, 4), (-7, 41.5, 4), (-12.5, 35, 5)], 4, 12, A, bevel=1.0, seg=2)
G.cut(fr, [G.C("xsec", [(-9.5, 24.5, 3), (9.5, 24.5, 3), (9.5, 34.5, 4), (5, 38.5, 3), (-5, 38.5, 3), (-9.5, 34.5, 4)], -10, 30, A, bevel=0)])
G.xsec([(-9.5, 24.5, 3), (9.5, 24.5, 3), (9.5, 34.5, 4), (5, 38.5, 3), (-5, 38.5, 3), (-9.5, 34.5, 4)], 7.6, 8.2, Gl, bevel=0)
for s in (-1, 1): G.box((s * 12.6, 21.5, -6), (1.5, 4, 8), P, bevel=0.6)            # the brightness buttons
G.cyl((0, 25, -12), (0, 27.5, -12), 3, S, seg=10)                                   # elevation screw
finish(G, {"Eye": (0, 31, 0), "Reticle": (0, 31, 8.4)})

# ---- Holosun HS510C: open reflex, a big squared window in a shroud, solar panel on top, QD base, side buttons
G = new("Att_HS510C")
clamp(G, -30, 30, w=26)
G.box((0, 10, 0), (30, 8, 64), A, bevel=1.4)                                        # base housing
G.box((14, 8, 18), (6, 6, 12), S, bevel=1.0)                                        # QD lever
fr = G.xsec([(-17, 13), (17, 13), (17, 41, 4), (12, 46, 4), (-12, 46, 4), (-17, 41, 4)], 14, 26, A, bevel=1.2, seg=2)
G.cut(fr, [G.C("xsec", [(-14, 15.5, 2), (14, 15.5, 2), (14, 40, 3), (10, 43, 3), (-10, 43, 3), (-14, 40, 3)], 0, 40, A, bevel=0)])
for s in (-1, 1): G.prof([(-6, 13), (14, 13), (14, 44), (8, 44)], s * 15.5, s * 17, A, bevel=0.6)   # shroud sides sweeping back
G.box((0, 46.5, 20), (22, 1.2, 10), Gl, bevel=0.2)                                 # solar panel
G.xsec([(-14, 15.5, 2), (14, 15.5, 2), (14, 40, 3), (10, 43, 3), (-10, 43, 3), (-14, 40, 3)], 21, 21.6, Gl, bevel=0)
for s in (-1, 1): G.cyl((s * 15, 10, -16), (s * 17.5, 10, -16), 3, P, seg=10)       # +/- buttons
G.cyl((15, 10, -24), (19, 10, -24), 7, A, seg=16)                                   # battery tray
finish(G, {"Eye": (0, 30, 0), "Reticle": (0, 30, 21.8)})

# ---- Kobra EKP-8-18: the Russian reflex sight, a deep square hood and the big brightness dial on the right
G = new("Att_Kobra")
clamp(G, -30, 30, w=24)
G.box((0, 13, -6), (30, 14, 78), "Olive", bevel=1.6)                               # body
hood = G.xsec([(-19, 14, 2), (19, 14, 2), (19, 52, 4), (-19, 52, 4)], 12, 38, "Olive", bevel=1.4, seg=2)
G.cut(hood, [G.C("xsec", [(-15.5, 18), (15.5, 18), (15.5, 48.5, 2), (-15.5, 48.5, 2)], 0, 50, "Olive", bevel=0)])
G.box((0, 16, 25), (31, 3, 22), "Olive", bevel=0.6)                                 # hood floor
G.xsec([(-15.5, 18), (15.5, 18), (15.5, 48.5, 2), (-15.5, 48.5, 2)], 24, 24.6, Gl, bevel=0)
dial = G.cyl((15, 18, -22), (24, 18, -22), 10, P, seg=20, bevel=0.8)
for k in range(12):
    a = k * 30
    G.box((24.2, 18 + 9.5 * math.cos(math.radians(a)), -22 + 9.5 * math.sin(math.radians(a))), (1.2, 2, 2), P, bevel=0)
G.cyl((-15, 14, -30), (-18, 14, -30), 6, S, seg=12)                                  # battery cap
finish(G, {"Eye": (0, 33, 0), "Reticle": (0, 33, 24.8)})

# ---- Trijicon ACOG TA31 4x32: the forged housing over the tube, the eyepiece behind, the bell in front
G = new("Att_ACOG")
clamp(G, -24, 24, w=24)
G.box((0, 9, 0), (26, 10, 56), A, bevel=1.2)                                        # TA51 flattop mount
for z in (-14, 14): G.cyl((15, 6, z), (21, 6, z), 5, S, seg=12)                     # thumbscrews
G.lathe([(-72, 11.5), (-70, 14), (-46, 14.5), (-40, 16), (16, 16), (28, 19.5), (52, 19.5), (54, 18)], A, seg=28, y=40)
hs = G.xsec([(-17, 16, 4), (17, 16, 4), (17, 48, 8), (8, 58, 6), (-8, 58, 6), (-17, 48, 8)], -38, 18, A, bevel=2.0, seg=2)
for z in (-28, -12, 4): G.box((0, 58.5, z), (12, 1.5, 4), A, bevel=0.3)            # cooling fins on top
G.cyl((17, 40, -12), (21, 40, -12), 7, A, seg=16); G.cyl((0, 58, -12), (0, 62, -12), 7, A, seg=16)   # adjuster caps
G.lathe([(-72.5, 11.2), (-72, 11.2)], Gl, seg=24, y=40)
G.lathe([(53.5, 17), (54.2, 17)], Gl, seg=24, y=40)
finish(G, {"Eye": (0, 40, 0)})

# ---- PSO-1 4x24: the long Soviet scope, rubber eyecup, turrets on top and left, the battery cap, on a rail adapter
G = new("Att_PSO1")
clamp(G, -30, 30, w=24)
G.box((0, 12, 0), (26, 12, 66), "Olive", bevel=1.2)                                 # Picatinny adapter
G.prof([(-26, 14), (26, 14), (22, 34), (-22, 34)], -8, 8, "Olive", bevel=1.0)       # the bracket up to the tube
G.lathe([(-150, 12), (-120, 14), (-112, 13), (110, 13), (126, 15.5), (150, 15.5), (152, 14.5)], "Olive", seg=28, y=46)
G.lathe([(-176, 17), (-170, 18), (-150, 16), (-148, 13)], R, seg=28, y=46, cap=False)   # rubber eyecup (open)
G.cyl((0, 58, -10), (0, 72, -10), 10, "Olive", seg=20, bevel=0.8)                   # elevation turret
G.cyl((-12, 46, -10), (-26, 46, -10), 10, "Olive", seg=20, bevel=0.8)               # windage turret
G.cyl((10, 54, -50), (18, 60, -50), 6, S, seg=12)                                   # reticle-light battery cap
G.lathe([(-170.5, 12), (-170, 12)], Gl, seg=24, y=46)
G.lathe([(151.5, 13.5), (152, 13.5)], Gl, seg=24, y=46)
finish(G, {"Eye": (0, 46, 0)})

# ============================================================ lights (hang under the rail, lens forward)
# ---- SureFire M600 Scout: a 1" tube on an offset mount, a knurled bezel, the tailcap switch
G = new("Att_M600")
clamp_under(G, -14, 14)
G.box((0, -10, 0), (12, 10, 26), A, bevel=1.0)                                      # offset arm
G.lathe([(-62, 9), (-58, 12.7), (40, 12.7), (46, 15), (66, 15), (67, 14)], A, seg=24, y=-24)
for k in range(5): G.lathe([(48 + k * 3.6, 15.6), (49.8 + k * 3.6, 15.6)], A, seg=24, y=-24, cap=False)
G.lathe([(-70, 8), (-62, 9.5)], R, seg=20, y=-24)                                    # tailcap switch
G.lathe([(66.5, 12.5), (67.2, 12.5)], Ln, seg=24, y=-24)
finish(G, {"Lens": (0, -24, 67)})

# ---- Zenit Klesch-2P: a stubby Russian tactical light, wide head, a lever-locked rail mount, a pressure pad
G = new("Att_Klesch")
clamp_under(G, -16, 16, mat="Olive")
G.box((0, -11, -4), (16, 10, 30), "Olive", bevel=1.0)
G.lathe([(-48, 10), (-44, 14), (18, 14), (30, 18.5), (50, 18.5), (51, 17.5)], A, seg=24, y=-28)
G.box((14, -11, -10), (5, 4, 22), S, bevel=0.8)                                     # mount lever
G.lathe([(-52, 6), (-48, 9)], R, seg=16, y=-28)
G.lathe([(50.6, 15.5), (51.3, 15.5)], Ln, seg=24, y=-28)
finish(G, {"Lens": (0, -28, 51.3)})

# ============================================================ lasers (sit on the rail)
# ---- Zenit Perst-4: a compact laser box, visible and IR apertures, the battery tube along the side
G = new("Att_Perst")
clamp(G, -22, 22)
G.box((0, 20, 0), (30, 26, 64), "Olive", bevel=2.5)
G.cyl((19, 18, -26), (19, 18, 30), 6.5, "Olive", seg=16)                             # battery tube
G.cyl((19, 18, -30), (19, 18, -26), 7.2, S, seg=16)
for (x, y, r) in [(-7, 26, 4), (6, 26, 3.4), (-7, 14, 3)]:
    G.cyl((x, y, 31), (x, y, 33), r + 1.2, A, seg=14); G.cyl((x, y, 33), (x, y, 33.5), r, Ln, seg=14)
G.cyl((0, 33, -14), (0, 35, -14), 4, P, seg=12)                                     # button
finish(G, {"Beam": (-7, 26, 34)})

# ---- Steiner DBAL-A2: tan box, sloped front with its three apertures, the rotary selector on top
G = new("Att_DBAL")
clamp(G, -26, 26)
G.prof([(-46, 6), (40, 6), (48, 14), (48, 36), (40, 40), (-46, 40)], -17, 17, T, bevel=3.0, seg=2)
G.cyl((-10, 40, -24), (-10, 45, -24), 7, T, seg=18)                                 # selector
G.box((-10, 45.5, -24), (2, 1.5, 12), S, bevel=0.3)
for (x, y, r) in [(-8, 30, 4.5), (6, 30, 4), (-8, 18, 3.5)]:
    G.cyl((x, y, 47), (x, y, 49), r, Ln, seg=14)
for s in (-1, 1): G.cyl((s * 17, 22, 10), (s * 19, 22, 10), 4, S, seg=10)           # windage/elevation
finish(G, {"Beam": (-8, 30, 49)})

# ============================================================ foregrips (hang under the handguard)
# ---- Magpul RVG: short, ribbed, slightly tapered, a moulded clamp
G = new("Att_RVG")
clamp_under(G, -20, 20, mat=P)
g = G.xsec([(-13, -6, 4), (13, -6, 4), (12, -70, 8), (-12, -70, 8)], -14, 14, P, bevel=3.0, seg=2)
G.cut(g, [G.C("box", (0, -14 - k * 9, s * 14), (30, 2.4, 3), P, bevel=0) for k in range(6) for s in (-1, 1)])
G.taper_y(g, -6, 1.0, -70, 0.85)
G.lathe([(-70, 0), (-70, 0)], P, seg=8)
finish(G, {"Hand": (0, -40, 0)})

# ---- Zenit RK-2: a stout vertical grip, finger grooves down the front, a swept palm shelf at the bottom
G = new("Att_RK2")
clamp_under(G, -22, 22, mat="Olive")
G.prof([(-16, -6), (16, -6), (14, -24, 4), (18, -32, 4), (14, -42, 4), (18, -50, 4), (14, -60, 4), (16, -78, 6), (28, -86, 4), (24, -92, 4), (-18, -90, 6), (-16, -6)],
       -14, 14, P, bevel=3.0, seg=2)
G.taper_y(G.parts[-1], -6, 1.0, -90, 0.9)
finish(G, {"Hand": (0, -46, 0)})

# ---- Magpul M-LOK hand stop: a low ramp for the heel of the hand
G = new("Att_HandStop")
clamp_under(G, -16, 16, mat=P)
G.prof([(-24, -6), (22, -6), (22, -12, 3), (8, -26, 5), (-4, -26, 4), (-24, -10, 6)], -11, 11, P, bevel=2.4, seg=2)
finish(G, {"Hand": (0, -20, -8)})

print("BUILT", res)

# ---- previews: each on its own, 3/4 view
scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
scn.display.shading.light = 'STUDIO'; scn.display.shading.color_type = 'MATERIAL'
scn.display.shading.show_cavity = True; scn.display.shading.cavity_type = 'BOTH'
COLORS = {"Anod": (0.04, 0.04, 0.045), "Polymer": (0.03, 0.03, 0.03), "Glass": (0.1, 0.25, 0.3), "Rubber": (0.02, 0.02, 0.02), "Steel": (0.12, 0.12, 0.13),
          "Tan": (0.45, 0.38, 0.25), "Lens": (0.6, 0.6, 0.5), "Olive": (0.12, 0.13, 0.08)}
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
    cam.data.ortho_scale = max(ext.y, ext.x, ext.z * 1.78) * 1.3
    d = Vector((0.85, -0.45, 0.35)).normalized()
    cam.location = c + d * 3.0
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = os.path.join(IND, n + ".png")
    bpy.ops.render.render(write_still=True)
