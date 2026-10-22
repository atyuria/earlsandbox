# AR-15 carbine, modern build like the reference photo: 16" barrel, 13" free-float M-LOK handguard (octagonal,
# slots at 3/6/9 o'clock and on the lower bevels, full-length top rail), flat-top upper with forward assist,
# brass deflector, ejection port and dust cover, folding back-up iron sights, A2 birdcage, 30-round curved
# magazine, A2-style grip, collapsible stock on a carbine buffer tube. About 840 mm long collapsed.
# Bore axis y = 0; upper receiver z -165 .. 20.
import bpy
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_AR15", ["Anod", "Steel", "Polymer", "Rubber", "Metal"])
A, S, P, R, M = "Anod", "Steel", "Polymer", "Rubber", "Metal"

def rail(z0, z1, mat=A):
    """Picatinny rail along the top from z0 to z1, slots every 10 mm."""
    r = G.xsec([(-7.8, 19), (7.8, 19), (7.8, 23), (10.6, 25.6), (10.6, 26.6), (8.3, 29), (-8.3, 29), (-10.6, 26.6), (-10.6, 25.6), (-7.8, 23)], z0, z1, mat, bevel=0.35)
    n = int((z1 - z0 - 6) // 10)
    G.cut(r, [G.C("box", (0, 28.6, z0 + 6 + i * 10), (30, 5, 5.3), mat, bevel=0) for i in range(n)])

# ---- upper receiver
up = G.prof([(-165, 19), (20, 19), (20, -14), (-60, -14), (-62, -10), (-150, -10), (-165, -6)], -13.5, 13.5, A, bevel=1.4, seg=2)
G.cut(up, [G.C("box", (12, 2.5, -66), (10, 17, 58), A, bevel=0)])          # ejection port, right side
rail(-163, 18)
G.box((10.6, 2.5, -66), (1.4, 16, 57), A, bevel=0.3)                         # dust cover, closed
for i in range(5): G.box((11.6, -3 + i * 2.6, -66), (0.9, 0.9, 54), A, bevel=0.2)
G.prof([(-108, 6), (-92, 6), (-92, 19), (-104, 19)], 12.5, 17.5, A, bevel=1.6, seg=2)   # brass deflector
G.cyl((12, 10, -128), (21, 8, -104), 6.2, A, seg=16, bevel=0.6)              # forward assist housing
G.cyl((19, 8.6, -110), (23.5, 7.8, -100), 5.2, S, seg=16, bevel=0.8)         # and its plunger
G.box((0, 15, -172), (36, 6.5, 9), A, bevel=1.5)                            # charging handle T
G.box((-14, 15.5, -170), (7, 4, 12), A, bevel=1.0)                          # its latch
G.cyl((0, 0, 18), (0, 0, 36), 17, S, seg=24, bevel=1.0)                     # barrel nut
# ---- handguard: free-float octagon, top rail, M-LOK slots
oct = [(-14, 21), (14, 21), (23, 12), (23, -14), (14, -27), (-14, -27), (-23, -14), (-23, 12)]
hg = G.xsec(oct, 24, 356, A, bevel=1.2, seg=2)
sl = [G.C("box", (0, -8, 345), (32, 22, 30), A, bevel=0)]                  # (open front for the barrel)
sl = []
for k in range(7):
    z = 66 + k * 42
    sl.append(G.C("box", (23, -1, z), (6, 7.5, 32), A, bevel=0))
    sl.append(G.C("box", (-23, -1, z), (6, 7.5, 32), A, bevel=0))
    sl.append(G.C("box", (0, -27, z), (7.5, 6, 32), A, bevel=0))
    sl.append(G.C("box", (18.5, -20.5, z), (7.5, 6, 32), A, bevel=0, rot=(0, 0, -55)))
    sl.append(G.C("box", (-18.5, -20.5, z), (7.5, 6, 32), A, bevel=0, rot=(0, 0, 55)))
    sl.append(G.C("box", (18.5, 16.5, z), (12, 6, 20), A, bevel=0, rot=(0, 0, 45)))     # vent cut on the upper bevels
    sl.append(G.C("box", (-18.5, 16.5, z), (12, 6, 20), A, bevel=0, rot=(0, 0, -45)))
G.cut(hg, sl)
rail(22, 356)
# ---- barrel and A2 flash hider
G.cyl((0, 0, 36), (0, 0, 381), 8.6, S, seg=20)
fh = G.cyl((0, 0, 381), (0, 0, 435), 11, S, seg=24, bevel=0.8)
fc = [G.C("cyl", (0, 0, 395), (0, 0, 440), 7, S, seg=16)]
for k in range(5):
    a = (-60 + k * 30)
    import math
    x = 14 * math.sin(math.radians(a)); y = 14 * math.cos(math.radians(a))
    fc.append(G.C("box", (x, y, 412), (3.2, 9, 30), S, bevel=0, rot=(0, 0, -a)))
G.cut(fh, fc)
G.cyl((0, 0, 377), (0, 0, 381.5), 12.5, S, seg=6)                           # crush washer flats
# ---- folding sights (flip-up, deployed)
G.box((0, 31.5, 340), (21, 5, 22), A, bevel=0.8)
fs = G.prof([(330, 31), (350, 31), (347, 58, 2), (333, 58, 2)], -9, 9, A, bevel=0.8)
G.cut(fs, [G.C("prof", [(334, 38), (346, 38), (345, 60), (335, 60)], -6, 6, A, bevel=0)])
G.box((0, 56, 340), (2, 13, 2.2), S, bevel=0.3)                              # front post
G.box((0, 31.5, -150), (21, 5, 24), A, bevel=0.8)
rsf = G.prof([(-158, 31), (-142, 31), (-143, 53, 3), (-157, 53, 3)], -10, 10, A, bevel=0.8)
G.cut(rsf, [G.C("cyl", (0, 44, -165), (0, 44, -135), 2.2, A, seg=12)])
# ---- lower receiver
lo = G.prof([(-165, -10), (18, -10), (18, -24), (-5, -24), (-5, -32), (-108, -32), (-112, -22), (-150, -22), (-165, -18)], -12.8, 12.8, A, bevel=1.4, seg=2)
mw = G.prof([(-4, -10), (-4, -60, 2), (-1, -66, 2), (-80, -66, 2), (-77, -60, 2), (-77, -10)], -15, 15, A, bevel=2, seg=2)   # magwell, flared
G.cut(mw, [G.C("prof", [(-8, -40), (-73, -40), (-73, -70), (-8, -70)], -11.5, 11.5, A, bevel=0)])
G.box((15.5, -27, -16), (2.5, 9, 9), S, bevel=1.2)                           # mag release (right)
G.box((-14.5, -18, -86), (2.5, 14, 9), A, bevel=1.0)                         # bolt catch (left)
G.cyl((-13.5, -16, -116), (-16, -16, -116), 6.5, S, seg=16)                   # safety selector
G.box((-16.5, -12, -116), (2, 4, 14), S, bevel=0.6, rot=(-30, 0, 0))
for z in (10, -152): G.screw((13.4, -16, z), 2.3, "x", S); G.screw((-13.4, -16, z), 2.3, "x", S)
G.screw((13.4, -24, -96), 1.6, "x", S); G.screw((13.4, -24, -86), 1.6, "x", S)   # trigger and hammer pins
# trigger guard and trigger
G.prof([(-74, -32), (-112, -32), (-112, -46, 3), (-74, -46, 3)], -5.5, 5.5, A, bevel=1.2)
G.cut(G.parts[-1], [G.C("prof", [(-78, -34), (-108, -34), (-108, -42, 2), (-78, -42, 2)], -10, 10, A, bevel=0)])
G.tube([(0, -30, -92), (0, -36, -90.5), (0, -41, -93)], 2.4, S, seg=10)
# ---- the 30-round magazine, curved forward
mg = G.prof([(-8, -40), (-73, -40), (-73, -70), (-67, -126, 30), (-52, -186), (12, -176), (0, -122, 30), (-7, -70)], -11, 11, P, bevel=1.4, seg=2)
G.cut(mg, [G.C("box", (12, -62 - k * 8, -40 - k * 1.4), (3, 1.6, 70), P, bevel=0) for k in range(5)] +
          [G.C("box", (-12, -62 - k * 8, -40 - k * 1.4), (3, 1.6, 70), P, bevel=0) for k in range(5)])
G.prof([(14, -175), (-53, -186), (-54, -193, 3), (15, -182, 3)], -13, 13, P, bevel=1.5, seg=2)   # floorplate
# ---- pistol grip, A2 style with the finger swell
G.prof([(-108, -22), (-146, -22), (-150, -30, 4), (-176, -112, 6), (-166, -122, 6), (-140, -118, 4),
        (-131, -86, 8), (-124, -70, 6), (-122, -52, 8), (-110, -30, 3)], -12, 12, P, bevel=3.5, seg=3)
# ---- buffer tube, castle nut, collapsible stock with a rubber pad
G.cyl((0, -6, -165), (0, -6, -400), 14.5, A, seg=24, bevel=0.6)
G.cyl((0, -6, -165), (0, -6, -174), 18, S, seg=8, bevel=0.6)
st = G.prof([(-262, 4, 6), (-262, -22, 8), (-300, -36, 16), (-402, -96, 4), (-418, -98), (-418, 26, 4), (-300, 22, 10)], -19, 19, P, bevel=4, seg=3)
G.cut(st, [G.C("cyl", (-30, -60, -372), (30, -60, -372), 5, P, seg=12),     # sling hole
           G.C("cyl", (0, -6, -250), (0, -6, -395), 15.2, P, seg=20)])
G.prof([(-418, 26), (-430, 26, 3), (-430, -98, 3), (-418, -98)], -21, 21, R, bevel=2.5, seg=2)
G.box((0, -30, -279), (14, 12, 22), P, bevel=2)                              # adjustment lever housing

pts = {"Muzzle": [0, 0, 0.435], "Grip": [0, -0.068, -0.14], "Fore": [0, -0.02, 0.27], "Iron": [0, 0.0605, 0.34],
       "Scope": [0, 0.072, 0.03], "Eject": [0.016, 0.003, -0.066]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
