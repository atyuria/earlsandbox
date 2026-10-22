# The optics, modelled on the real things (Tarkov's sights are the same ones): an enclosed reflex red dot on a
# low Picatinny mount (Holosun EPS / ACRO pattern), an EOTech EXPS3-style holographic sight (big hood over a
# 30 x 22 mm window, low electronics block behind, side adjusters, QD lever), a 1-8x LPVO in a cantilever mount
# (eyepiece with its throw lever, turrets, illumination dial, objective bell), and a G33-style 3x flip magnifier.
# Origin on the rail top, y up, z forward, sizes in mm. Each optic's sight line is its "Eye" point's height (the
# game puts the eye there: red dot 30, holo 32, scope 40, magnifier 32). The glass is the "Glass" material: its
# rearmost face is the eyepiece the game draws the sight picture on.
# blender --background --python load_gunkit.py --python gunkit2.py --python gunshot2.py --python optics.py -- [preview dir]
import bpy, sys, math
K = bpy.app.driver_namespace["gunkit2"]
OUT = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/Assets/CarlSandbox/Resources/Models/"
PV = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
A, P, Gl, R, S = "Anod", "Polymer", "Glass", "Rubber", "Steel"
res = {}

def clamp(G, z0, z1, w=22):
    """A Picatinny clamp: a flat base on the rail, a fixed jaw on the left, a moving jaw and a hex-nut crossbolt on the right."""
    G.box((0, 3, (z0 + z1) / 2), (w, 6, z1 - z0), A, bevel=1.0)
    for s in (-1, 1):
        G.prof([(z0 + 3, 6), (z1 - 3, 6), (z1 - 5, -3), (z0 + 5, -3)], s * 11.2, s * 14.2, A, bevel=0.6)
    zc = z0 + (z1 - z0) * 0.35
    G.cyl((14, 0.5, zc), (19, 0.5, zc), 5, S, seg=6)                     # crossbolt nut
    G.cyl((-14, 0.5, zc), (-16, 0.5, zc), 3.2, S, seg=10)                # bolt head
    G.box((0, 6.2, zc), (21, 1.0, 4), S, bevel=0.3)                      # recoil lug

def finish(G, name, eye):
    pts = {"Eye": [0, eye / 1000, 0]}
    res[name] = G.export(OUT + name + ".json", pts)
    if PV: bpy.app.driver_namespace["gunshot2"].sheet(G, PV + "/optic_" + name + ".png", 420)

# ============================================================ red dot: enclosed reflex, axis 30 mm
G = K.Gun2("Att_RedDot", [A, P, Gl, R, S])
clamp(G, -22, 22)
G.box((0, 10, 0), (26, 8, 40), A, bevel=1.4)                                  # riser
# the housing: a squared tube with rounded top corners, sloping down at the front like a hood
win = [(-11, 19.5, 3), (11, 19.5, 3), (11, 40.5, 6), (-11, 40.5, 6)]
hs = G.xsec([(-15, 14, 1), (15, 14, 1), (15, 41, 4), (9, 46.5, 5), (-9, 46.5, 5), (-15, 41, 4)], -21, 21, A, bevel=1.2, seg=2)
G.cut(hs, [G.C("xsec", win, -30, 30, A, bevel=0),
           G.C("box", (0, 52, 26), (40, 14, 20), A, bevel=0, rot=(-28, 0, 0))])   # front of the hood cut back at an angle
G.xsec(win, -19, -18.2, Gl, bevel=0)                                           # rear window
G.xsec(win, 14, 14.8, Gl, bevel=0)                                             # front window (the reflector)
G.box((0, 20.6, -14), (5, 1.6, 4), P, bevel=0.3)                               # LED emitter at the bottom rear
# controls: brightness buttons on the left, battery tray on the top right, adjuster screws, the sensor window
for z in (-9, 2): G.cyl((-15, 26, z), (-17, 26, z), 3.4, R, seg=14, bevel=0.5)
G.cyl((8, 46, -11), (8, 47.6, -11), 6.2, A, seg=20, bevel=0.5)
G.box((8, 47.8, -11), (8, 0.6, 1.2), S, bevel=0)
G.screw((15, 29, -6), 2.2, "x", S); G.screw((0, 46.6, -1), 2.2, "y", S)
G.cyl((-4, 46.2, 6), (-4, 46.8, 6), 2.2, Gl, seg=10)
finish(G, "Att_RedDot", 30)

# ============================================================ holographic: EXPS3 pattern, axis 32 mm
G = K.Gun2("Att_Holo", [A, P, Gl, R, S])
G.box((0, 3, 0), (24, 6, 82), A, bevel=1.0)                                    # base along the rail
for s in (-1, 1): G.prof([(-36, 6), (36, 6), (33, -3), (-33, -3)], s * 11.2, s * 14.2, A, bevel=0.6)
# QD lever on the left: a plate along the side with the paddle at the front
G.prof([(-28, -2), (24, -2), (30, 6, 3), (-24, 8, 3)], -14.2, -16.4, P, bevel=0.6)
G.screw((-16.4, 2, -18), 2.6, "x", S); G.screw((16, 1, -18), 3.2, "x", S)
# lower body under the hood, the low electronics block behind it with the buttons on its back face
G.xsec([(-19.5, 5, 1), (19.5, 5, 1), (19.5, 19, 2), (-19.5, 19, 2)], -14, 47, A, bevel=1.2)
G.prof([(-43, 5, 2), (-14, 5), (-14, 21), (-36, 21, 4), (-43, 15, 3)], -17, 17, A, bevel=1.4, seg=2)
for x in (-7, 7): G.cyl((x, 13, -43.2), (x, 13, -45), 3.6, R, seg=14, bevel=0.6)    # up / down
G.cyl((0, 13, -43.2), (0, 13, -44.4), 2.4, R, seg=12)                           # night vision
# the hood: a thick rectangular frame round the window, open front and back, the front face squared off
w = [(-15, 21, 2), (15, 21, 2), (15, 43, 4), (-15, 43, 4)]
hd = G.xsec([(-21.5, 14, 1), (21.5, 14, 1), (21.5, 46, 6), (15, 52, 7), (-15, 52, 7), (-21.5, 46, 6)], -14, 46, A, bevel=1.4, seg=2)
G.cut(hd, [G.C("xsec", w, -20, 52, A, bevel=0),
           G.C("prof", [(-20, 52.5), (-6, 52.5), (-20, 47)], -30, 30, A, bevel=0)])     # the top rounds down at the back
G.xsec(w, -12, -11.2, Gl, bevel=0)                                              # rear window (the hologram)
G.xsec(w, 41, 41.8, Gl, bevel=0)                                                # front window
# the front panel under the window: the battery door, four screws
G.box((0, 11.5, 46.6), (32, 11, 1.2), P, bevel=0.4)
for x in (-12, 12):
    for y in (8, 15): G.screw((x, y, 47.2), 1.5, "z", S)
# side adjusters on the right (elevation and windage dials with their slots) and the battery cap behind them
for z in (8, 26):
    G.cyl((21.5, 30, z), (23.2, 30, z), 6.4, A, seg=22, bevel=0.5)
    G.box((23.4, 30, z), (0.5, 7, 1.2), S, bevel=0)
G.cyl((17, 13, -26), (24, 13, -26), 7.5, R, seg=24, bevel=0.8)                  # knurled battery cap
for k in range(12):
    a = k / 12 * math.tau
    G.box((24.2, 13 + math.sin(a) * 7, -26 + math.cos(a) * 7), (1.2, 1.2, 1.2), R, bevel=0.2)
finish(G, "Att_Holo", 32)

# ============================================================ scope: 1-8x LPVO on a cantilever mount, axis 40 mm
G = K.Gun2("Att_Scope", [A, P, Gl, R, S])
clamp(G, -62, 20, 22)
for zr in (-52, 6):
    G.box((0, 15, zr), (20, 20, 13), A, bevel=1.2)                              # ring stanchion
    rg = G.cyl((0, 40, zr - 7), (0, 40, zr + 7), 19, A, seg=28, bevel=0.8)       # ring round the tube
    G.cut(rg, [G.C("cyl", (0, 40, zr - 9), (0, 40, zr + 9), 15.2, A, seg=28)])
    G.box((0, 40, zr), (42, 1.0, 14.4), S, bevel=0)                              # split line of the cap
    for s in (-1, 1):
        for dz in (-3.5, 3.5): G.screw((s * 18.6, 44, zr + dz), 1.6, "x", S)
# tube, saddle and turrets
G.cyl((0, 40, -72), (0, 40, 66), 15, A, seg=32)
G.xsec([(-17, 24, 3), (17, 24, 3), (17, 56, 5), (-17, 56, 5)], -14, 22, A, bevel=1.5, seg=2)
G.cyl((0, 56, 4), (0, 66, 4), 12.5, A, seg=28, bevel=0.6)                      # elevation turret
G.cyl((0, 66, 4), (0, 70, 4), 11.2, A, seg=28, bevel=1.0)
G.cyl((17, 40, 4), (27, 40, 4), 12.5, A, seg=28, bevel=0.6)                    # windage turret
G.cyl((27, 40, 4), (31, 40, 4), 11.2, A, seg=28, bevel=1.0)
for k in range(16):                                                              # turret grip flutes
    a = k / 16 * math.tau
    G.box((math.cos(a) * 12.4, 61, 4 + math.sin(a) * 12.4), (1.2, 8, 1.2), A, bevel=0)
G.cyl((-17, 40, 4), (-26, 40, 4), 11.5, A, seg=28, bevel=0.6)                  # illumination dial
G.cyl((-26, 40, 4), (-28, 40, 4), 7, P, seg=20)
# eyepiece: magnification ring with its throw lever, the ocular bell, a rubber eyecup
G.cyl((0, 40, -88), (0, 40, -72), 19.5, A, seg=32, bevel=0.6)
for k in range(20):
    a = k / 20 * math.tau
    G.box((math.cos(a) * 19.4, 40 + math.sin(a) * 19.4, -80), (1.6, 1.6, 12), A, bevel=0)
G.prof([(-84, 56), (-76, 56), (-76, 59, 1), (-84, 59, 1)], 1, 5, A, bevel=0.4)   # throw lever fin
G.box((3, 57, -80), (4, 3, 8), A, bevel=0.5)
G.cyl((0, 40, -132), (0, 40, -88), 22, A, seg=32, r2=19.5, bevel=0.8)          # ocular bell
oc = G.cyl((0, 40, -142), (0, 40, -130), 23, R, seg=32, bevel=1.0)              # eyecup
G.cut(oc, [G.C("cyl", (0, 40, -150), (0, 40, -128), 19, R, seg=32)])
G.cyl((0, 40, -133), (0, 40, -132.2), 19, Gl, seg=32)                           # ocular lens
# objective: the bell, a short sunshade lip, the front lens
G.cyl((0, 40, 66), (0, 40, 90), 15, A, seg=32, r2=20)
ob = G.cyl((0, 40, 90), (0, 40, 132), 20, A, seg=32, bevel=0.8)
G.cut(ob, [G.C("cyl", (0, 40, 120), (0, 40, 140), 17.6, A, seg=32)])
G.cyl((0, 40, 120), (0, 40, 120.8), 17.6, Gl, seg=32)                           # objective lens
G.cyl((0, 40, 104), (0, 40, 108), 20.6, R, seg=32)                              # grip band
finish(G, "Att_Scope", 40)

# ============================================================ magnifier: G33-style 3x on a flip-to-side mount, axis 32 mm
G = K.Gun2("Att_Magnifier", [A, Gl, P, S])
clamp(G, -20, 20)
G.prof([(-16, 6), (16, 6), (14, 18), (-14, 18)], 9, 18, A, bevel=1)             # riser on the right with the hinge
G.cyl((15, 18, -17), (15, 18, 17), 4.2, S, seg=14)                              # hinge pin
G.prof([(-15, 16), (15, 16), (15, 22), (-15, 22)], 6, 18, A, bevel=1)           # arm up to the ring
rg = G.cyl((0, 32, -12), (0, 32, 12), 19, A, seg=28, bevel=0.8)
G.cut(rg, [G.C("cyl", (0, 32, -14), (0, 32, 14), 15.6, A, seg=28)])
G.box((0, 51.5, 0), (8, 3, 16), S, bevel=0.5)                                   # ring clamp screw block
G.cyl((0, 32, -52), (0, 32, 54), 15.5, A, seg=32)                               # body
G.cyl((0, 32, -62), (0, 32, -46), 18, A, seg=32, r2=15.5, bevel=0.6)            # eyepiece
ec = G.cyl((0, 32, -66), (0, 32, -58), 19, P, seg=32, bevel=0.8)                # eyecup
G.cut(ec, [G.C("cyl", (0, 32, -70), (0, 32, -56), 15.5, P, seg=32)])
G.cyl((0, 32, -59), (0, 32, -58.4), 14.5, Gl, seg=32)                           # ocular lens
G.cyl((0, 32, 46), (0, 32, 58), 17.5, A, seg=32, bevel=0.6)                     # objective
G.cyl((0, 32, 57.4), (0, 32, 58.2), 14.5, Gl, seg=32)
G.cyl((-15.5, 38, -4), (-19, 38, -4), 3, P, seg=12)                             # diopter / release button
finish(G, "Att_Magnifier", 32)
result = res
print("OPTICS", res)
