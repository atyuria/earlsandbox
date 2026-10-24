# PKM, 7.62x54R general-purpose machine gun. Published figures: 1173 mm long, 605 mm barrel, 7.5 kg. Long
# stamped receiver with the feed cover on top (rear sight leaf at its front), the gas piston UNDER the barrel
# to a gas block with the bipod pivot, the barrel's carrying handle (for quick barrel changes), tall front sight
# post and slotted conical flash hider at the muzzle, skeleton "thumbhole" butt, and the 100-round belt box
# hanging on the right. Bore y = 0, receiver front face z = 80.
import bpy, math
K = bpy.app.driver_namespace["gunkit3"]
G = K.Gun3("Gun_PKM", ["Blued", "Steel", "Polymer", "Olive"])
BL, S, P, O = "Blued", "Steel", "Polymer", "Olive"

# ---- receiver and feed cover
rc = G.xsec([(-18, -38, 2), (18, -38, 2), (18, 14), (16, 18), (-16, 18), (-18, 14)], -200, 80, BL, bevel=1.0)
G.cut(rc, [G.C("box", (-18, -4, 10), (8, 16, 70), BL, bevel=0)] +                             # the feed opening (left)
          [G.C("box", (s * 18.3, -20, -60), (1.4, 3, 230), BL, bevel=0) for s in (-1, 1)])
for s in (-1, 1):
    for z, y in ((-180, -30), (-180, 4), (60, -30), (60, 4), (-60, -30), (0, -30)): G.screw((s * 18.1, y, z), 2, "x", BL, depth=0.5)
G.xsec([(-17, 14), (17, 14), (17, 22, 4), (10, 30, 6), (-10, 30, 6), (-17, 22, 4)], -170, 64, BL, bevel=0.8)   # feed cover
G.cyl((-19, 22, 64), (19, 22, 64), 4, S, seg=12)                                               # cover hinge
G.box((0, 34, 74), (22, 10, 22), BL, bevel=1.5)                                                 # rear sight base
lf = G.prof([(62, 36), (92, 36), (90, 44), (64, 47)], -7, 7, BL, bevel=0.5)
G.cut(lf, [G.C("box", (0, 47, 76), (3.4, 6, 40), BL, bevel=0)])
# charging handle (right), the feed tray stub on the left
G.prof([(-40, -8), (-10, -8), (-6, 2, 3), (-40, 2)], 18, 34, S, bevel=1.4, seg=2)
G.box((-24, -4, 10), (12, 4, 60), S, bevel=0.6)
# ---- barrel, gas system, carrying handle, sights, flash hider
G.lathe([(80, 16), (96, 16), (96, 12.5), (150, 11), (685, 9.5)], S, seg=22)
G.cyl((0, -24, 84), (0, -24, 432), 8.5, BL, seg=16)                                             # gas cylinder below
G.prof([(410, -36), (446, -36), (446, 12), (410, 12)], -13, 13, BL, bevel=1.5, seg=2)           # gas block / bipod mount
G.box((0, -4, 140), (30, 34, 26), BL, bevel=2)                                                   # barrel lock collar
G.tube([(-14, 8, 130), (-26, 36, 150), (-24, 46, 200), (-12, 32, 232), (-10, 10, 238)], 5.5, P, seg=12)   # carrying handle
# front sight: a tall post in its block near the muzzle, with its protecting wings
G.prof([(650, -12), (680, -12), (680, 22), (650, 22)], -12, 12, BL, bevel=1.4, seg=2)
for s in (-1, 1): G.prof([(654, 18), (676, 18), (676, 46), (660, 46)], s * 11, s * 8, BL, bevel=0.5)
G.cyl((0, 20, 665), (0, 45, 665), 1.6, BL, seg=10)
fh = G.lathe([(682, 9.5), (690, 12), (760, 16.5), (764, 16.5)], BL, seg=24)
G.cut(fh, [G.C("cyl", (0, 0, 676), (0, 0, 772), 7, BL, seg=16, r2=14)] +
          [G.C("box", (math.cos(a) * 16, math.sin(a) * 16, 735), (4.5, 4.5, 50), BL, bevel=0, rot=(0, 0, math.degrees(a))) for a in [k * math.tau / 5 for k in range(5)]])
# ---- bipod folded back under the barrel
for s in (-1, 1):
    G.tube([(s * 8, -36, 428), (s * 12, -40, 690)], 4.5, BL, seg=10)
    G.box((s * 12, -40, 694), (8, 7, 14), BL, bevel=1.5)
# ---- trigger group and grip
tg = G.prof([(-66, -38), (-150, -38), (-150, -52, 4), (-66, -52, 4)], -6, 6, BL, bevel=1.0)
G.cut(tg, [G.C("prof", [(-72, -40), (-144, -40), (-144, -48, 3), (-72, -48, 3)], -10, 10, BL, bevel=0)])
G.tube([(0, -36, -100), (0, -42, -98), (0, -48, -101)], 2.8, S, seg=10)
gp = G.prof([(-114, -38), (-158, -38), (-186, -132, 8), (-172, -142, 6), (-146, -138, 6), (-130, -72, 8), (-120, -48, 3)], -14, 14, P, bevel=3.5, seg=3)
G.cut(gp, [G.C("box", (s * 14, -78 - k * 12, -150 - k * 3.2), (2, 2.2, 32), P, bevel=0, rot=(-15, 0, 0)) for s in (-1, 1) for k in range(4)])
G.taper_y(gp, -38, 1.0, -142, 0.86)
# ---- skeleton butt with the thumbhole
sp = G.prof([(-198, 14), (-198, -40), (-246, -64, 16), (-494, -112, 6), (-500, -112), (-500, 2, 6), (-320, 14, 30)], -19, 19, P, bevel=3.5, seg=3)
G.cut(sp, [G.C("prof", [(-262, -30), (-450, -78, 18), (-450, -14, 14), (-300, -4, 18)], -30, 30, P, bevel=0)])
G.taper(sp, -200, 0.8, -500, 1.0)
G.prof([(-500, 4), (-508, 4, 2), (-508, -114, 2), (-500, -114)], -20, 20, BL, bevel=1.4, seg=2)
# ---- the 100-round belt box on the right, the belt leading up into the feed
bx = [G.box((30, -78, -10), (42, 92, 120), O, bevel=4, seg=2), G.box((30, -30, -10), (44, 6, 124), O, bevel=1.2)]
G.box((9, -32, -10), (4, 10, 30), BL, bevel=0.6)                                                # the box's bracket
for k in range(5): bx.append(G.box((-24 - k * 1.5, -6 + k * 4, 40 - k * 8), (10, 6, 9), "Steel", bevel=0.8))   # links into the feed
G.as_mag(*bx)

pts = {"Muzzle": [0, 0, 0.764], "Grip": [0, -0.082, -0.144], "Fore": [0, -0.03, 0.12], "Iron": [0, 0.045, 0.665],
       "Eject": [-0.02, -0.02, -0.02], "MagTop": [0.03, -0.032, -0.01], "MagBottom": [0.03, -0.124, -0.01], "Charge": [0.026, -0.003, -0.025]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
