# TASER X26P, from the listed size: about 185 mm long, 81 mm tall, 33 mm wide. Yellow body (law-enforcement
# yellow), black rubber grip panels, ambidextrous safety, big trigger guard, the black cartridge in front with
# its two probe doors, LED/laser window under the cartridge, notch-and-blade sights on top, the rear display.
import bpy
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_Taser", ["Yellow", "Polymer", "Steel", "Lens", "Rubber"])
Y, P, S, L, R = "Yellow", "Polymer", "Steel", "Lens", "Rubber"

# body: long, rounded, the top sloping down to the back
G.prof([(-96, 22, 8), (-60, 34, 20), (40, 36, 6), (44, 30), (44, -16), (-30, -16), (-60, -10, 10), (-96, -4, 10)], -16.5, 16.5, Y, bevel=4, seg=3)
# grip, short and steep, with black rubber panels
G.prof([(-34, -14), (-74, -10), (-88, -46, 6), (-80, -52, 6), (-52, -50, 6), (-40, -24, 6)], -14.5, 14.5, Y, bevel=4, seg=3)
for s in (-1, 1):
    x0, x1 = (13, 15.6) if s > 0 else (-15.6, -13)
    G.prof([(-46, -18, 4), (-72, -16, 4), (-82, -44, 4), (-56, -46, 4)], x0, x1, R, bevel=1, seg=2)
# trigger guard and trigger
tg = G.prof([(-6, -16), (-40, -16), (-44, -32, 6), (-30, -40, 8), (6, -40, 8), (10, -26, 6)], -5, 5, Y, bevel=1.4, seg=2)
G.cut(tg, [G.C("prof", [(-2, -18), (-35, -18), (-36, -30, 4), (-28, -35, 5), (2, -35, 5), (4, -24, 4)], -10, 10, Y, bevel=0)])
G.tube([(0, -15, -18), (0, -24, -15), (0, -31, -19)], 3, P, seg=10)
# safety switch, both sides, and the rear display
for s in (-1, 1): G.box((s * 16.8, 14, -50), (2.6, 8, 14), P, bevel=1.2)
G.prof([(-97, 6), (-99, 6), (-99, 20, 3), (-97, 22)], -9, 9, L, bevel=0.6)
G.box((0, 2, -98), (12, 6, 3), P, bevel=1)
# sights
G.box((0, 37.5, -48), (12, 5, 8), P, bevel=1)
G.cut(G.parts[-1], [G.C("box", (0, 39.5, -48), (3, 4, 12), P, bevel=0)])
G.box((0, 38, 32), (2.4, 6, 6), P, bevel=0.6)
# the cartridge: black block with its two probe doors, release tabs on the sides
ct = G.prof([(44, 31), (86, 31, 3), (86, -12, 3), (44, -12)], -17, 17, P, bevel=2, seg=2)
G.cut(ct, [G.C("cyl", (0, 25, 80), (0, 25, 90), 5.4, P, seg=16), G.C("cyl", (0, -1, 80), (0, -1, 90), 5.4, P, seg=16)])
G.cyl((0, 25, 82), (0, 25, 84), 4.6, S, seg=16); G.cyl((0, -1, 82), (0, -1, 84), 4.6, S, seg=16)
for s in (-1, 1): G.box((s * 18, 10, 58), (2, 16, 12), Y, bevel=1)
# LED and laser window under the cartridge
G.box((0, -15, 46), (24, 8, 4), Y, bevel=1)
G.cyl((-6, -15, 47), (-6, -15, 48.6), 3.4, L, seg=14); G.cyl((7, -15, 47), (7, -15, 48.6), 2.4, L, seg=12)

pts = {"Muzzle": [0, 0.012, 0.086], "Grip": [0, -0.03, -0.06], "Fore": [-0.006, -0.05, -0.064], "Iron": [0, 0.041, 0.032],
       "Probe1": [0, 0.025, 0.086], "Probe2": [0, -0.001, 0.086]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
