# BERETTA 92FS / M9, from the published figures: 217 mm long, 137 mm tall, 38 mm wide, 125 mm barrel,
# 155 mm sight radius. Bore axis at y = 32 mm. The open-top slide with the barrel showing down to its middle,
# the slide-mounted ambidextrous safety / decocker, exposed ring hammer, the squared hooked trigger guard,
# separate checkered grip panels with medallions, a lanyard loop.
import bpy
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_Beretta", ["Metal", "Steel", "Rubber", "Polymer"])
M, S, R, P = "Metal", "Steel", "Rubber", "Polymer"

# ---- slide (z -82 .. 117): rounded top, open from just ahead of the breech to the front sight bridge
sl = G.xsec([(-12, 15, 0.8), (12, 15, 0.8), (12, 39, 5), (7, 47, 4), (-7, 47, 4), (-12, 39, 5)], -82, 117, M, bevel=0.8)
cuts = [G.C("prof", [(-1, 62), (0, 41, 5), (18, 31, 14), (92, 31, 14), (104, 41, 5), (105, 62)], -14, 14, M, bevel=0),
        G.C("cyl", (0, 32, 100), (0, 32, 125), 8.2, M, seg=20),                    # barrel bore through the front
        G.C("cyl", (0, 21.5, 108), (0, 21.5, 125), 3.4, M, seg=12),                 # recoil guide
        G.C("prof", [(108, 6), (125, 6), (125, 26)], -20, 20, M, bevel=0)]          # front lower bevel
for side in (-1, 1):
    for i in range(10):
        cuts.append(G.C("box", (side * 12.4, 27.5, -77 + i * 2.4), (1.8, 20, 1.1), M, bevel=0))
G.cut(sl, cuts)
# the barrel, exposed in the open top: chamber block, then the round barrel out past the slide
G.box((0, 37.5, 8), (17, 11, 22), S, bevel=0.6)
br = G.cyl((0, 32, 0), (0, 32, 123.5), 7.4, S, seg=28, bevel=0.5)
G.cut(br, [G.C("cyl", (0, 32, 110), (0, 32, 130), 4.6, S, seg=16)])
G.cyl((0, 21.5, 104), (0, 21.5, 116.5), 2.4, S, seg=10)
G.box((11.6, 39, -4), (1.6, 3.6, 18), S, bevel=0.3)                # extractor
# slide-mounted safety / decocker levers, both sides
for side in (-1, 1):
    G.cyl((side * 12, 38, -70), (side * 13.6, 38, -70), 4.2, S, seg=16)
    G.box((side * 14, 41.5, -73), (2.4, 6.5, 9), S, bevel=0.8, rot=(-25, 0, 0))
# sights: dovetailed rear with a square notch and white dots, front blade on the bridge
rs = G.box((0, 50.5, -74), (19, 7.5, 7.5), M, bevel=0.8)
G.cut(rs, [G.C("box", (0, 53.5, -74), (3.3, 5, 12), M, bevel=0)])
G.prof([(108, 47), (115, 47), (114, 53, 1), (110, 53, 1)], -1.6, 1.6, M, bevel=0.3)
# exposed ring hammer at the back
hm = G.prof([(-81, 22), (-84, 22), (-93, 41, 3), (-90, 46, 3), (-83, 42), (-81, 34)], -3.1, 3.1, S, bevel=0.6)
G.cut(hm, [G.C("cyl", (-10, 41, -89), (10, 41, -89), 2.2, S, seg=12)])
# ---- frame (alloy)
dc = G.xsec([(-11, 15), (11, 15), (11, 7, 4), (7, 3, 3), (-7, 3, 3), (-11, 7, 4)], -10, 110, M, bevel=0.6)
G.cut(dc, [G.C("prof", [(98, -2), (118, -2), (118, 18)], -20, 20, M, bevel=0)])
G.prof([(-82, 15), (0, 15), (0, 4), (-82, 4)], -11, 11, M, bevel=0.6)
# grip frame: hardly raked, a short beavertail under the hammer, serrated front strap
gp = G.prof([(-84, 9), (-18, 9), (-20, 2, 3), (-25, -6, 6), (-38, -86, 3), (-40, -90), (-90, -90, 3),
             (-87, -74, 8), (-79, -14, 14), (-82, 4, 4), (-91, 10, 3), (-88, 15)], -12.2, 12.2, M, bevel=3, seg=2)
fs = [G.C("box", (0, -20 - i * 4.2, -26.2 - i * 0.6), (30, 1.5, 3), M, bevel=0, rot=(-8, 0, 0)) for i in range(15)]
fs.append(G.C("prof", [(-40, -90), (-89, -90), (-88, -66), (-41, -66)], -9, 9, M, bevel=0))
G.cut(gp, fs)
# grip panels: checkered polymer slabs with the round medallions and two screws each
for side in (-1, 1):
    x0, x1 = (12, 15.4) if side > 0 else (-15.4, -12)
    pn = G.prof([(-28, -6, 4), (-38, -84, 4), (-84, -84, 6), (-77, -16, 10), (-72, -6, 4)], x0, x1, P, bevel=1.4, seg=2)
    ch = []
    for i in range(12):
        ch.append(G.C("box", (side * 15.6, -22 - i * 5.4, -55), (1.4, 0.9, 60), P, bevel=0, rot=(35, 0, 0)))
        ch.append(G.C("box", (side * 15.6, -22 - i * 5.4, -55), (1.4, 0.9, 60), P, bevel=0, rot=(-35, 0, 0)))
    G.cut(pn, ch)
    G.cyl((side * 15, -46, -58), (side * 16.2, -46, -58), 5.2, S, seg=18)
    G.screw((side * 15.6, -13, -64), 2.2, "x", S); G.screw((side * 15.6, -78, -66), 2.2, "x", S)
# magazine floorplate and the lanyard loop
G.prof([(-40, -90), (-89, -90, 1), (-89, -95, 2), (-41, -95, 2)], -11.5, 11.5, P, bevel=1, seg=2)
G.ring((0, -91, -89), 3.4, 0.9, S, axis="y", seg=12)
# trigger guard: squared front with the hook, big enough for a gloved finger
tg = G.prof([(-24, 6), (36, 6), (44, -8, 4), (43, -14, 2), (39, -16), (39, -21, 4), (31, -24, 7), (-18, -24, 7), (-26, -10, 4)], -4.4, 4.4, M, bevel=1.2, seg=2)
G.cut(tg, [G.C("prof", [(-16, 3), (33, 3), (38, -10, 4), (33, -19, 6), (-12, -19, 6), (-19, -6, 4)], -10, 10, M, bevel=0)])
G.tube([(0, 7, 10), (0, 0, 13), (0, -8, 11), (0, -15, 6)], 2.8, S, seg=10)
G.box((11.6, 7.5, 6), (1.2, 2.6, 24), S, bevel=0.3)            # trigger bar along the right side
# controls: slide catch (left), takedown lever (left) and button (right), reversible mag release
G.box((-12.4, 14.5, 8), (1.8, 3.6, 16), S, bevel=0.6)
G.box((-11.7, 10, 22), (1.6, 3, 9), S, bevel=0.5, rot=(0, 0, 0)); G.cyl((11, 10, 22), (12.2, 10, 22), 2.4, S, seg=12)
G.box((-13, -8, -24), (3, 8, 8), S, bevel=1)
for x in (-11, 11): G.screw((x, 9.5, -2), 1.3, "x", S); G.screw((x, 9, -35), 1.3, "x", S)

pts = {"Muzzle": [0, 0.032, 0.124], "Grip": [0, -0.032, -0.055], "Fore": [-0.006, -0.068, -0.078], "Iron": [0, 0.053, 0.112],
       "MagBase": [0, -0.09, -0.064], "SlideBack": [0, 0.034, -0.082], "Eject": [0.004, 0.044, 0.006]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
