# MOSSBERG 590A1, 20" heavy barrel, from the published figures: about 1040 mm long, 13.87" length of pull, 8+1.
# Aluminium receiver (ejection port right, loading port underneath), top-tang safety, ghost ring rear sight
# with protective ears, front blade, magazine tube under the barrel with its cap and the bayonet lug, barrel
# clamp ring, ribbed pump forend, polymer trigger guard, synthetic stock with a rubber pad. Bore axis y = 0.
import bpy
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_Shotgun", ["Anod", "Metal", "Polymer", "Rubber", "Steel"])
A, M, P, R, S = "Anod", "Metal", "Polymer", "Rubber", "Steel"

# ---- receiver (z -112 .. 92)
rc = G.prof([(-112, 18), (-80, 24, 8), (92, 24, 4), (92, -34), (-60, -34), (-112, -10)], -15, 15, A, bevel=2, seg=2)
G.cut(rc, [G.C("box", (13, 5, 40), (8, 22, 66), A, bevel=0),                       # ejection port
           G.C("prof", [(10, -36), (80, -36), (80, -24), (10, -24)], -11, 11, A, bevel=0)])   # loading port
G.box((10, 5, 40), (6, 20, 62), S, bevel=0.6)                                      # bolt in the port
G.box((0, 26, -104), (10, 5, 14), P, bevel=1.4)                                    # tang safety
G.box((0, 24.5, -104), (14, 2, 18), A, bevel=0.8)
# ghost ring rear sight with its protective ears, on the receiver
gr = G.prof([(-64, 24), (-40, 24), (-44, 44, 3), (-60, 44, 3)], -14, 14, M, bevel=1)
G.cut(gr, [G.C("prof", [(-62, 30), (-42, 30), (-42, 50), (-62, 50)], -9, 9, M, bevel=0)])
G.ring((0, 40, -52), 4.4, 1.3, S, axis="z", seg=16)
G.box((0, 34, -52), (3, 8, 3), S, bevel=0.3)
# ---- barrel, front sight, magazine tube, clamp, bayonet lug
G.cyl((0, 0, 92), (0, 0, 614), 11.2, M, seg=24, bevel=0.6)
G.cut(G.parts[-1], [G.C("cyl", (0, 0, 600), (0, 0, 620), 9.2, M, seg=20)])
G.prof([(566, 11), (596, 11), (594, 45, 2), (568, 45, 2)], -11, 11, M, bevel=1)
G.cut(G.parts[-1], [G.C("prof", [(560, 18), (600, 18), (600, 50), (560, 50)], -6, 6, M, bevel=0)])
G.prof([(574, 12), (588, 12), (586, 40, 1), (578, 40, 1)], -1.6, 1.6, S, bevel=0.3)  # front blade, level with the ghost ring
G.cyl((0, -26, 92), (0, -26, 594), 12, M, seg=24)                                    # magazine tube
G.cyl((0, -26, 594), (0, -26, 616), 13.5, M, seg=24, bevel=2)                         # tube cap
G.xsec([(-14, -40), (14, -40), (14, 13, 10), (-14, 13, 10)], 556, 574, M, bevel=1.2)  # barrel clamp
G.box((0, -41, 590), (8, 8, 20), M, bevel=1)                                          # bayonet lug
# ---- pump forend, ribbed, on its action bars
fe = G.xsec([(-21, -8), (21, -8), (24, -26, 10), (18, -45, 8), (-18, -45, 8), (-24, -26, 10)], 150, 352, P, bevel=2.5, seg=2)
G.cut(fe, [G.C("xsec", [(-30, -60), (30, -60), (30, 0), (-30, 0)], z, z + 6, P, bevel=0) for z in range(166, 340, 18)] +
          [G.C("cyl", (0, -26, 140), (0, -26, 360), 12.4, P, seg=24)])
G.xsec([(-24.5, -12), (24.5, -12), (24.5, -40, 6), (-24.5, -40, 6)], 166, 172, P, bevel=0.5) if False else None
for s in (-1, 1): G.box((s * 13, -18, 120), (2, 6, 60), S, bevel=0.4)                 # action bars
# ---- trigger guard and trigger, action release
tg = G.prof([(-8, -34), (-70, -34), (-70, -54, 8), (-8, -54, 8)], -6.5, 6.5, P, bevel=1.4, seg=2)
G.cut(tg, [G.C("prof", [(-14, -36), (-64, -36), (-64, -49, 5), (-14, -49, 5)], -10, 10, P, bevel=0)])
G.tube([(0, -32, -36), (0, -40, -34), (0, -46, -37)], 3, S, seg=10)
G.box((-7.5, -44, -72), (2, 7, 4), S, bevel=0.6)
for z in (-20, -88): G.screw((15, -12, z), 2.2, "x", S); G.screw((-15, -12, z), 2.2, "x", S)
# ---- synthetic stock: wrist, comb, rubber recoil pad
G.prof([(-108, 20), (-112, -10), (-150, -40, 20), (-392, -130), (-392, -16), (-200, 6, 40)], -19, 19, P, bevel=4.5, seg=3)
G.prof([(-392, -14), (-414, -14, 4), (-414, -134, 6), (-392, -132)], -21, 21, R, bevel=3, seg=2)
G.cyl((0, -112, -320), (0, -120, -320), 4, S, seg=10)

pts = {"Muzzle": [0, 0, 0.616], "Grip": [0, -0.046, -0.168], "Fore": [0, -0.03, 0.26], "Iron": [0, 0.04, 0.581],
       "Eject": [0.016, 0.006, 0.04]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
