# AWP: Accuracy International AWM in .338 Lapua, from the published figures: about 1230 mm long, 686 mm fluted
# stainless barrel, five-round box magazine. Olive thumbhole stock on the aluminium chassis, raised cheek
# piece, rubber butt pad with spacers and a folding monopod, round action with the bolt knob on the right,
# Picatinny scope rail, fluted barrel and muzzle brake, folded bipod, 5-25x56 scope in rings. Bore axis y = 0.
import bpy, math
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_AWP", ["Olive", "Polymer", "Metal", "Steel", "Glass", "Rubber"])
O, P, M, S, GL, R = "Olive", "Polymer", "Metal", "Steel", "Glass", "Rubber"

# ---- action, bolt, rail
G.cyl((0, 0, -112), (0, 0, 92), 17, M, seg=28, bevel=1.0)
G.cut(G.parts[-1], [G.C("box", (14, 8, -40), (12, 14, 70), M, bevel=0)])      # ejection port
G.box((0, 6, -40), (20, 18, 66), S, bevel=0.8)                                 # the bolt body in the port
G.cyl((0, 0, -112), (0, 0, -142), 12.5, M, seg=20, bevel=1.4)                  # bolt shroud
G.tube([(16, 2, -100), (30, -4, -104), (40, -12, -110)], 3.4, S, seg=10)       # bolt handle
G.ball((43, -15, -112), 9, P, seg=16)                                          # oversized knob
xr = G.xsec([(-10.6, 18), (10.6, 18), (10.6, 22.5), (8.3, 25), (-8.3, 25), (-10.6, 22.5)], -150, 110, M, bevel=0.4)
G.cut(xr, [G.C("box", (0, 24.6, -146 + i * 10), (30, 4, 5.3), M, bevel=0) for i in range(25)])
# ---- fluted stainless barrel and muzzle brake
br = G.cyl((0, 0, 92), (0, 0, 768), 14.5, S, seg=24, r2=11.5)
G.cut(br, [G.C("box", (14 * math.cos(a), 14 * math.sin(a), 410), (5, 5, 460), S, bevel=0, rot=(0, 0, -math.degrees(a) + 45)) for a in [k * math.pi / 3 for k in range(6)]])
mb = G.cyl((0, 0, 768), (0, 0, 842), 16, S, seg=24, bevel=1.2)
G.cut(mb, [G.C("box", (0, 0, 785 + k * 16), (40, 9, 9), S, bevel=0) for k in range(4)] + [G.C("cyl", (0, 0, 830), (0, 0, 850), 5.5, S, seg=12)])
# ---- the chassis and thumbhole stock (olive), forend squared under the barrel
fe = G.prof([(92, -14), (430, -14), (434, -22, 4), (430, -58, 6), (100, -58), (80, -36)], -26, 26, O, bevel=3, seg=2)
st = G.prof([(92, -14), (-118, -14), (-150, 4, 10), (-250, 32, 6), (-370, 32, 6), (-392, 30), (-392, -118), (-376, -132, 8),
             (-296, -96, 20), (-176, -60), (-166, -134, 8), (-138, -138, 8), (-124, -60), (-112, -66), (80, -60)], -24, 24, O, bevel=4, seg=3)
G.cut(st, [G.C("prof", [(-176, -22), (-232, -12, 16), (-276, -10, 14), (-292, -40, 14), (-226, -56, 10), (-180, -56, 6)], -40, 40, O, bevel=0),   # thumbhole
           G.C("prof", [(40, -61), (-34, -61), (-34, -14), (40, -14)], -14, 14, O, bevel=0)])     # magazine well
for z in range(140, 420, 50):                                                  # forend vents
    G.cut(fe, [G.C("box", (s * 26, -36, z), (6, 14, 30), O, bevel=0) for s in (-1, 1)])
G.prof([(-238, 34), (-342, 34), (-344, 44, 4), (-236, 44, 4)], -22, 22, P, bevel=2, seg=2)   # raised cheek piece
G.cyl((-14, 38, -255), (14, 38, -255), 3, S, seg=10); G.cyl((-14, 38, -325), (14, 38, -325), 3, S, seg=10)
for k in range(2): G.prof([(-392 - k * 6, 30), (-398 - k * 6, 30), (-398 - k * 6, -118), (-392 - k * 6, -118)], -24, 24, M, bevel=0.8)
G.prof([(-404, 32), (-420, 32, 4), (-420, -122, 6), (-404, -120)], -25, 25, R, bevel=3, seg=2)      # butt pad
G.tube([(0, -116, -376), (0, -150, -380), (0, -160, -382)], 5, M, seg=10)        # monopod, down
G.cyl((0, -160, -382), (0, -164, -382), 11, R, seg=14)
# ---- magazine, trigger guard and trigger
G.prof([(36, -60), (-30, -60), (-30, -86, 3), (36, -86, 3)], -13, 13, M, bevel=1.4, seg=2)
tg = G.prof([(-40, -60), (-112, -60), (-112, -78, 6), (-40, -78, 6)], -6, 6, M, bevel=1.2, seg=2)
G.cut(tg, [G.C("prof", [(-46, -62), (-106, -62), (-106, -73, 4), (-46, -73, 4)], -10, 10, M, bevel=0)])
G.tube([(0, -58, -78), (0, -66, -76), (0, -71, -79)], 2.6, S, seg=10)
G.box((15, -30, -100), (3, 8, 14), S, bevel=1)                                    # safety
# ---- bipod folded along the forend, sling studs
for s in (-1, 1):
    G.cyl((s * 18, -64, 400), (s * 18, -64, 190), 6, M, seg=12, bevel=0.8)
    G.cyl((s * 18, -64, 190), (s * 18, -64, 178), 9, R, seg=12)
G.box((0, -62, 410), (44, 10, 28), M, bevel=2)
G.cyl((0, -62, 300), (0, -70, 300), 4, S, seg=10); G.cyl((0, -132, -300), (0, -140, -300), 4, S, seg=10)
# ---- scope: 34 mm tube, 56 mm objective, turrets, in two rings
ax = 62
G.cyl((0, ax, -175), (0, ax, -130), 21, P, seg=28, bevel=1.5)                    # ocular
G.cyl((0, ax, -130), (0, ax, -110), 21, P, seg=28, r2=17, bevel=0.8)
G.cyl((0, ax, -110), (0, ax, 120), 17, P, seg=28)                                # tube
G.cyl((0, ax, 120), (0, ax, 160), 17, P, seg=28, r2=33)
G.cyl((0, ax, 160), (0, ax, 225), 33, P, seg=32, bevel=2)                        # objective bell
G.cyl((0, ax, 224), (0, ax, 226), 28, GL, seg=28); G.cyl((0, ax, -177), (0, ax, -175), 17, GL, seg=28)
G.cyl((0, ax + 17, 0), (0, ax + 40, 0), 16, M, seg=24, bevel=1.4)                # elevation turret
G.cyl((17, ax, 0), (40, ax, 0), 14, M, seg=24, bevel=1.4)                        # windage
G.cyl((-17, ax, 10), (-34, ax, 10), 13, M, seg=24, bevel=1.4)                    # parallax
G.box((0, ax, 5), (40, 40, 44), P, bevel=6, seg=2)                                # saddle
for z in (-70, 80):
    G.cyl((0, ax, z - 11), (0, ax, z + 11), 20.5, M, seg=24)
    G.box((0, 34, z), (30, 26, 22), M, bevel=2)

pts = {"Muzzle": [0, 0, 0.842], "Grip": [0, -0.086, -0.142], "Fore": [0, -0.05, 0.26], "Scope": [0, 0.062, 0.03],
       "Eject": [0.018, 0.01, -0.04]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
