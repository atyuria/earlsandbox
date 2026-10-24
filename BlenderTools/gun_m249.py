# M249 SAW, 5.56 light machine gun. Published figures: 1038 mm long, 465 mm barrel. Tall rectangular receiver
# with the feed cover on top (rear sight at its back), the magazine well / belt feed on the left with the
# 200-round box hanging under it, heat shield and carrying handle on the barrel, gas cylinder under the barrel
# to the gas block and bipod, front sight post on its block, birdcage flash hider, lower handguard, pistol grip
# and the fixed ribbed polymer stock. Bore y = 0, receiver front face z = 120.
import bpy, math
K = bpy.app.driver_namespace["gunkit3"]
G = K.Gun3("Gun_M249", ["Blued", "Steel", "Polymer", "Tan"])
BL, S, P, T = "Blued", "Steel", "Polymer", "Tan"

# ---- receiver, feed cover, rear sight
rc = G.xsec([(-24, -44, 2), (24, -44, 2), (24, 16), (-24, 16)], -170, 120, BL, bevel=1.2)
G.cut(rc, [G.C("box", (24, -6, 0), (8, 18, 70), BL, bevel=0)] + [G.C("box", (s * 24.3, -28, -40), (1.4, 3, 200), BL, bevel=0) for s in (-1, 1)])
for s in (-1, 1):
    for z, y in ((-150, -36), (-150, 8), (100, -36), (100, 8), (-40, -36)): G.screw((s * 24.1, y, z), 2, "x", BL, depth=0.5)
G.xsec([(-23, 16), (23, 16), (23, 24, 4), (14, 32, 6), (-14, 32, 6), (-23, 24, 4)], -150, 110, BL, bevel=0.8)    # feed cover
G.xsec([(-10.5, 32), (10.5, 32), (10.5, 35), (8, 37.5), (-8, 37.5), (-10.5, 35)], -120, 100, BL, bevel=0.4)         # its rail
for z in range(-116, 100, 10): G.box((0, 36.4, z), (21.5, 3, 5.2), BL, bevel=0.3)
G.box((0, 42, -130), (22, 10, 26), BL, bevel=1.2)                                               # rear sight
lf = G.prof([(-140, 46), (-120, 46), (-120, 54), (-140, 54)], -7, 7, BL, bevel=0.4)
G.cut(lf, [G.C("cyl", (0, 50, -150), (0, 50, -110), 2.2, BL, seg=12)])
G.prof([(-10, -14), (30, -14), (34, -4, 3), (-10, -4)], -24, -40, S, bevel=1.4, seg=2)         # charging handle (left)
# the magazine well / feed tray on the left
G.box((-30, -30, 0), (14, 30, 70), BL, bevel=1.5)
# ---- barrel, heat shield, carrying handle, gas system, sights, flash hider
G.lathe([(120, 15), (136, 15), (136, 11), (200, 10), (585, 8.5)], S, seg=22)
G.xsec([(-14, 6), (14, 6), (14, 14, 6), (0, 20, 8), (-14, 14, 6)], 140, 300, BL, bevel=0.6)      # heat shield on top
hs = bpy.data.objects[G.parts[-1].name]
G.tube([(0, 16, 150), (0, 52, 170), (0, 58, 210), (0, 52, 250), (0, 16, 268)], 6, P, seg=12)   # carrying handle
G.cyl((0, -28, 122), (0, -28, 440), 9, BL, seg=16)
G.prof([(420, -42), (452, -42), (452, 12), (420, 12)], -14, 14, BL, bevel=1.5, seg=2)
G.prof([(548, -12), (574, -12), (574, 18), (548, 18)], -11, 11, BL, bevel=1.2, seg=2)
for s in (-1, 1): G.prof([(552, 16), (570, 16), (570, 40), (558, 40)], s * 10, s * 7, BL, bevel=0.5)
G.cyl((0, 18, 561), (0, 38, 561), 1.6, BL, seg=10)
fh = G.lathe([(585, 9), (592, 11), (636, 11), (640, 10.5)], BL, seg=24)
G.cut(fh, [G.C("cyl", (0, 0, 580), (0, 0, 646), 6.5, BL, seg=16)] +
          [G.C("box", (math.cos(a) * 11, math.sin(a) * 11, 620), (4, 4, 30), BL, bevel=0, rot=(0, 0, math.degrees(a))) for a in [k * math.tau / 6 + 0.3 for k in range(5)]])
# lower handguard, polymer, ribbed
lh = G.xsec([(-24, -18, 3), (24, -18, 3), (26, -36, 6), (18, -50, 8), (-18, -50, 8), (-26, -36, 6)], 120, 300, P, bevel=2, seg=2)
G.cut(lh, [G.C("box", (s * 26, y, 210), (3, 2.4, 150), P, bevel=0) for s in (-1, 1) for y in (-26, -34, -42)])
# ---- bipod folded under the barrel
for s in (-1, 1):
    G.tube([(s * 9, -42, 436), (s * 13, -46, 600)], 4.5, BL, seg=10)
    G.box((s * 13, -46, 604), (8, 7, 14), BL, bevel=1.5)
# ---- trigger group and grip
tg = G.prof([(-50, -44), (-134, -44), (-134, -58, 4), (-50, -58, 4)], -6, 6, BL, bevel=1.0)
G.cut(tg, [G.C("prof", [(-56, -46), (-128, -46), (-128, -54, 3), (-56, -54, 3)], -10, 10, BL, bevel=0)])
G.tube([(0, -42, -86), (0, -48, -84), (0, -54, -87)], 2.8, S, seg=10)
gp = G.prof([(-100, -44), (-144, -44), (-172, -138, 8), (-158, -148, 6), (-132, -144, 6), (-116, -78, 8), (-106, -54, 3)], -14, 14, P, bevel=3.5, seg=3)
G.cut(gp, [G.C("box", (s * 14, -84 - k * 12, -136 - k * 3.2), (2, 2.2, 32), P, bevel=0, rot=(-15, 0, 0)) for s in (-1, 1) for k in range(4)])
G.taper_y(gp, -44, 1.0, -148, 0.86)
# ---- fixed ribbed polymer stock with the buffer tube inside
sp = G.prof([(-168, 16), (-168, -44), (-200, -50, 10), (-470, -108, 6), (-480, -108), (-480, 6, 6), (-300, 16, 30)], -22, 22, P, bevel=3.5, seg=3)
G.cut(sp, [G.C("box", (s * 22, -30 - k * 14, -330), (3, 4, 220), P, bevel=0, rot=(-12, 0, 0)) for s in (-1, 1) for k in range(3)])
G.taper(sp, -170, 0.85, -480, 1.0)
G.prof([(-480, 8), (-490, 8, 3), (-490, -110, 3), (-480, -110)], -22, 22, "Polymer", bevel=1.6, seg=2)
# ---- the 200-round box under the left side, the belt up into the feed
bx = [G.box((-34, -92, 0), (54, 96, 120), T, bevel=5, seg=2), G.box((-34, -42, 0), (56, 6, 124), T, bevel=1.2)]
for k in range(4): bx.append(G.box((-34, -42 + k * 5, 26 - k * 6), (12, 6, 9), "Steel", bevel=0.8))
G.as_mag(*bx)

pts = {"Muzzle": [0, 0, 0.64], "Grip": [0, -0.088, -0.13], "Fore": [0, -0.034, 0.21], "Iron": [0, 0.038, 0.561],
       "Eject": [0.024, -0.04, -0.01], "MagTop": [-0.034, -0.044, 0], "MagBottom": [-0.034, -0.14, 0], "Charge": [-0.032, -0.009, 0.01]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
