# BARRETT M82A1 .50 BMG, from the published figures: about 1448 mm long with the 29" (737 mm) fluted barrel,
# 10-round box magazine. Long stamped upper receiver with its row of cooling slots and holes, folding carry
# handle, top rail, the big dual-chamber arrowhead muzzle brake, lower receiver with grip, magazine and butt
# (rubber pad, monopod), bipod folded forward, scope in rings. Bore axis y = 0.
import bpy, math
K = bpy.app.driver_namespace["gunkit3"]
G = K.Gun3("Gun_Barrett", ["Metal", "Polymer", "Steel", "Glass", "Rubber"])
M, P, S, GL, R = "Metal", "Polymer", "Steel", "Glass", "Rubber"

# ---- upper receiver (z -300 .. 340): tall stamped box, rounded top, cooling slots and holes along the front half
up = G.xsec([(-24, -24), (24, -24), (24, 34, 6), (18, 44, 6), (-18, 44, 6), (-24, 34, 6)], -300, 340, M, bevel=1.4)
uc = [G.C("box", (14, 10, -150), (24, 22, 90), M, bevel=0)]                     # ejection port, right
for k in range(6):
    z = 70 + k * 44
    for s in (-1, 1):
        uc.append(G.C("box", (s * 24, 18, z), (8, 22, 26), M, bevel=0))        # cooling slots high up
        uc.append(G.C("cyl", (s * 30, -10, z), (s * 18, -10, z), 7, M, seg=14))   # and the round holes below
G.cut(up, uc)
G.box((0, 10, -150), (40, 20, 86), S, bevel=0.8)                                # bolt carrier in the port
G.cyl((0, 0, -300), (0, 0, 330), 15, S, seg=20)                                  # barrel back inside
rl = G.xsec([(-10.6, 43), (10.6, 43), (10.6, 47.5), (8.3, 50), (-8.3, 50), (-10.6, 47.5)], -260, 150, M, bevel=0.4)
G.cut(rl, [G.C("box", (0, 49.6, -256 + i * 10), (30, 4, 5.3), M, bevel=0) for i in range(40)])
G.tube([(-20, 44, 180), (-20, 70, 196), (20, 70, 196), (20, 44, 180)], 5, M, seg=10)   # carry handle, up
G.box((26, 6, -40), (6, 12, 26), S, bevel=2)                                     # charging handle
# ---- barrel (fluted), arrowhead brake
br = G.cyl((0, 0, 330), (0, 0, 830), 18, S, seg=24, r2=15)
G.cut(br, [G.C("box", (16 * math.cos(a), 16 * math.sin(a), 560), (6, 6, 400), S, bevel=0, rot=(0, 0, -math.degrees(a) + 45)) for a in [k * math.pi / 3 for k in range(6)]])
G.cyl((0, 0, 330), (0, 0, 360), 26, M, seg=20, bevel=1.5)                       # barrel spring housing front
mb = G.top([(-38, 830), (38, 830), (38, 860, 6), (8, 892, 4), (-8, 892, 4), (-38, 860, 6)], -22, 22, S, bevel=2, seg=2)
G.cut(mb, [G.C("box", (s * 30, 0, 852), (30, 30, 16), S, bevel=0) for s in (-1, 1)] +
          [G.C("box", (s * 30, 0, 874), (30, 30, 10), S, bevel=0) for s in (-1, 1)] + [G.C("cyl", (0, 0, 820), (0, 0, 900), 8, S, seg=14)])
# ---- lower receiver, magazine well, grip, butt
# (the lower: deep under the magazine and grip, narrowing back along the butt to the pad, its underside
# rising to the monopod; tapered so the butt is slimmer than the receiver)
lo = G.prof([(80, -24), (-300, -24), (-560, -24), (-560, -100, 6), (-520, -102, 10), (-440, -80, 30), (-300, -62, 20), (-200, -62), (60, -54, 10), (80, -40, 6)], -22, 22, M, bevel=2.4, seg=2)
G.taper(lo, -260, 1.0, -560, 0.8)
G.cut(lo, [G.C("prof", [(-40, -62), (-130, -62), (-130, -20), (-40, -20)], -16, 16, M, bevel=0)])
mgz = [G.prof([(-36, -50), (-134, -50), (-136, -168, 4), (-34, -168, 4)], -17, 17, M, bevel=2, seg=2)]   # magazine
for k in range(4): mgz.append(G.box((0, -80 - k * 22, -85), (36, 2, 92), M, bevel=0.6))
mgz.append(G.prof([(-30, -166), (-140, -166), (-140, -176, 3), (-30, -176, 3)], -19, 19, M, bevel=1.5, seg=2))   # floorplate
G.as_mag(*mgz)
tg = G.prof([(-140, -60), (-200, -60), (-200, -82, 6), (-140, -82, 6)], -6, 6, M, bevel=1.2)
G.cut(tg, [G.C("prof", [(-146, -62), (-194, -62), (-194, -76, 4), (-146, -76, 4)], -10, 10, M, bevel=0)])
G.tube([(0, -58, -168), (0, -66, -166), (0, -72, -169)], 3, S, seg=10)
G.prof([(-200, -60), (-232, -60), (-262, -170, 8), (-232, -176, 8), (-208, -90, 6)], -15, 15, P, bevel=4, seg=3)   # grip
G.prof([(-560, 50), (-584, 50, 6), (-584, -98, 6), (-560, -98)], -26, 26, R, bevel=4, seg=2)              # butt pad
ck = G.prof([(-300, 44, 10), (-420, 52, 30), (-560, 50, 8), (-560, -24), (-300, -24)], -20, 20, M, bevel=3, seg=2)  # butt top, the cheek rise
G.taper(ck, -300, 1.0, -560, 0.82)
G.tube([(0, -96, -490), (0, -150, -500)], 7, M, seg=12); G.cyl((0, -150, -500), (0, -156, -500), 16, R, seg=14)
# ---- bipod folded forward, scope
G.box((0, -30, 300), (54, 14, 30), M, bevel=2)
for s in (-1, 1):
    G.cyl((s * 22, -34, 300), (s * 22, -38, 640), 8, M, seg=12, bevel=1)
    G.box((s * 22, -42, 650), (22, 10, 40), R, bevel=3)
ax = 88
G.cyl((0, ax, -330), (0, ax, -280), 20, P, seg=28, bevel=1.5)
G.cyl((0, ax, -280), (0, ax, 20), 15, P, seg=28)
G.cyl((0, ax, 20), (0, ax, 50), 15, P, seg=28, r2=26)
G.cyl((0, ax, 50), (0, ax, 110), 26, P, seg=32, bevel=2)
G.cyl((0, ax, 109), (0, ax, 111), 22, GL, seg=28); G.cyl((0, ax, -332), (0, ax, -330), 15, GL, seg=28)
G.cyl((0, ax + 15, -130), (0, ax + 34, -130), 13, M, seg=24, bevel=1.2)
G.cyl((15, ax, -130), (32, ax, -130), 12, M, seg=24, bevel=1.2)
for z in (-210, -40):
    G.cyl((0, ax, z - 11), (0, ax, z + 11), 18.5, M, seg=24)
    G.box((0, 60, z), (30, 26, 22), M, bevel=2)

pts = {"Muzzle": [0, 0, 0.892], "Grip": [0, -0.108, -0.226], "Fore": [0, -0.04, 0.12], "Scope": [0, 0.088, -0.1],
       "Eject": [0.026, 0.012, -0.15], "MagTop": [0, -0.05, -0.085], "MagBottom": [0, -0.172, -0.085], "Charge": [0.03, 0.006, -0.04]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
