# AK-47 (AKM pattern), from the published figures: 880 mm long, 415 mm barrel, 378 mm sight radius. Stamped
# receiver with the ribbed top cover, the big stamped selector lever on the right, charging handle on the bolt
# carrier, rear tangent sight on its block ahead of the receiver, wooden upper and lower handguards (finger
# grooves along the lower), gas tube, front sight base with the hooded post, slant brake, cleaning rod, curved
# steel 30-round magazine, wood grip and stock with a steel butt plate. Bore axis y = 0.
import bpy, math
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_AK47", ["Metal", "Steel", "Wood"])
M, S, W = "Metal", "Steel", "Wood"

# ---- receiver (z -180 .. 58), stamped: flat sides, a groove along each side, rivets front and back
rc = G.xsec([(-15, -30, 1.2), (15, -30, 1.2), (15, 8), (13, 12), (-13, 12), (-15, 8)], -180, 58, M, bevel=0.8)
G.cut(rc, [G.C("box", (14, 4, -30), (8, 13, 64), M, bevel=0),                     # ejection port, right
           G.C("prof", [(-40, -31), (22, -31), (22, -12), (-40, -12)], -11.5, 11.5, M, bevel=0)] +   # magazine well
          [G.C("box", (s * 15.4, -8, -60), (1.4, 3, 200), M, bevel=0) for s in (-1, 1)])   # the stamped side groove
for s in (-1, 1):
    for z, y in ((30, -20), (44, -20), (30, -6), (44, -6), (-160, -20), (-170, -20), (-160, -8), (-150, -20)):
        G.screw((s * 15.1, y, z), 1.7, "x", S, depth=0.5)
# top cover: arched, with three stamped transverse ribs and the release button at the back
G.xsec([(-14, 10), (14, 10), (14, 17, 6), (6, 24, 6), (-6, 24, 6), (-14, 17, 6)], -172, 40, M, bevel=0.8)
for z in (-150, -120, -90): G.xsec([(-12.5, 21), (12.5, 21), (6, 25.5, 4), (-6, 25.5, 4)], z, z + 9, M, bevel=0.6)
G.cyl((0, 14, -184), (0, 14, -176), 4.6, S, seg=14, bevel=0.5)
# the selector lever along the right side, its finger tab at the front, pivot at the back
G.prof([(-170, 7), (-62, 3), (-56, -6, 2), (-62, -8), (-70, -2), (-170, 0)], 15.1, 16.8, S, bevel=0.5)
G.cyl((15.2, 3, -168), (17.4, 3, -168), 5, S, seg=14)
# bolt carrier and its charging handle, in the port
G.box((9.5, 5, -30), (8, 9, 60), S, bevel=0.5)
G.prof([(-4, 2), (6, 2), (8, 10, 3), (-2, 10, 3)], 15, 30, S, bevel=1.4, seg=2)
G.cyl((30, 6, 2), (36, 6, 2), 5, S, seg=12, bevel=1.0)
# rear sight block and tangent leaf (notch 34 mm above the bore)
G.box((0, 14, 72), (24, 16, 34), M, bevel=1.4)
lf = G.prof([(56, 22), (88, 22), (86, 31), (58, 36)], -7, 7, S, bevel=0.6)
G.cut(lf, [G.C("box", (0, 36, 58), (3, 6, 6), S, bevel=0)])
G.box((0, 26, 72), (16, 4, 6), S, bevel=0.8)                                # range slider
# ---- barrel, gas system, front sight, brake, cleaning rod
G.cyl((0, 0, 58), (0, 0, 458), 8.6, S, seg=20)
G.box((0, 2, 76), (26, 26, 12), M, bevel=1.2)                                 # trunnion front / handguard retainer
G.prof([(310, -10), (346, -10), (346, 26), (316, 26)], -12, 12, M, bevel=1.4, seg=2)   # gas block
G.cyl((0, 19, 280), (0, 19, 320), 7.5, M, seg=16)                            # gas tube past the upper handguard
fsb = G.prof([(432, -14), (458, -14), (458, 18), (452, 30), (440, 30), (432, 18)], -11, 11, M, bevel=1.2, seg=2)
G.cut(fsb, [G.C("prof", [(436, 22), (454, 22), (454, 40), (436, 40)], -6.5, 6.5, M, bevel=0)])
G.cyl((0, 30.5, 437), (0, 30.5, 452), 10, M, seg=20, r2=10)                  # hood (open at the top, see below)
G.cut(G.parts[-1], [G.C("cyl", (0, 30.5, 430), (0, 30.5, 460), 8.3, M, seg=20), G.C("box", (0, 41, 445), (14, 8, 30), M, bevel=0)])
G.cyl((0, 22, 445), (0, 34, 445), 1.4, S, seg=10)                             # the post
G.box((0, -17, 430), (10, 7, 18), M, bevel=1.0)                              # bayonet lug
br = G.cyl((0, 0, 458), (0, 0, 488), 10, S, seg=20, bevel=0.6)
G.cut(br, [G.C("cyl", (0, 0, 455), (0, 0, 495), 5.2, S, seg=16),
           G.C("box", (-6, 0, 494), (30, 30, 14), S, bevel=0, rot=(0, -30, 0))])   # the slant
G.cyl((0, -15, 110), (0, -15, 458), 3, S, seg=10)                             # cleaning rod
G.cyl((0, -15, 458), (0, -15, 462), 4, S, seg=10)
# ---- handguards: wooden upper over the gas tube, lower around the barrel with finger grooves
G.xsec([(-12, 12, 4), (12, 12, 4), (12, 22, 8), (-12, 22, 8)], 95, 282, W, bevel=2, seg=2)
G.xsec([(-15, 22), (15, 22), (12, 30, 6), (-12, 30, 6)], 95, 282, W, bevel=1.5, seg=2)
lh = G.xsec([(-21, 10), (21, 10), (23, -6, 6), (16, -24, 8), (-16, -24, 8), (-23, -6, 6)], 90, 296, W, bevel=3, seg=2)
G.cut(lh, [G.C("box", (s * 23, -9, 193), (4, 7, 150), W, bevel=0) for s in (-1, 1)] +
          [G.C("box", (s * 21.6, -18, 193), (4, 4, 150), W, bevel=0, rot=(0, 0, s * 35)) for s in (-1, 1)])
G.box((0, -6, 300), (34, 30, 10), M, bevel=1.4)                               # lower handguard retainer
# ---- trigger group: guard, trigger, mag catch paddle
tg = G.prof([(-44, -30), (-124, -30), (-124, -44, 4), (-44, -44, 4)], -5.5, 5.5, S, bevel=1.0)
G.cut(tg, [G.C("prof", [(-50, -32), (-118, -32), (-118, -40, 3), (-50, -40, 3)], -10, 10, S, bevel=0)])
G.tube([(0, -28, -78), (0, -34, -76), (0, -40, -79)], 2.6, S, seg=10)
G.prof([(-40, -30), (-48, -30), (-50, -46, 2), (-44, -46, 2)], -6, 6, S, bevel=0.8)
# ---- the curved steel 30-rounder, ribbed
mg = G.prof([(22, -31), (-38, -31), (-36, -70), (-16, -150, 60), (24, -236), (82, -212), (54, -138, 60), (24, -70)], -12, 12, M, bevel=1.4, seg=2)
G.cut(mg, [G.C("box", (s * 12.5, -60 - k * 22, -6 + k * 9), (2, 4, 50), M, bevel=0, rot=(-14 - k * 4, 0, 0)) for s in (-1, 1) for k in range(7)])
G.prof([(26, -238), (84, -214), (86, -221, 2), (27, -246, 2)], -14, 14, M, bevel=1.4, seg=2)
# ---- grip and stock (wood), butt plate (steel)
G.prof([(-94, -30), (-134, -30), (-162, -122, 8), (-150, -132, 6), (-124, -128, 6), (-108, -64, 8), (-98, -40, 3)], -13, 13, W, bevel=3.5, seg=3)
G.prof([(-178, 12), (-178, -30), (-212, -44, 14), (-392, -124), (-392, -12), (-260, 8, 40)], -18, 18, W, bevel=4, seg=3)
G.prof([(-392, -10), (-400, -10, 2), (-400, -126, 2), (-392, -126)], -19.5, 19.5, S, bevel=1.6, seg=2)
G.screw((19.5, -66, -396), 2.4, "x", S)

pts = {"Muzzle": [0, 0, 0.488], "Grip": [0, -0.074, -0.122], "Fore": [0, -0.008, 0.235], "Iron": [0, 0.034, 0.445],
       "Eject": [0.016, 0.006, -0.03]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
