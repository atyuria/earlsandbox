# GLOCK 17 Gen5 (FS), from the factory dimensions: 202 mm long, slide 186 x 25.5 mm, 139 mm tall with the
# magazine, 114 mm barrel, 165 mm sight radius, grip raked about 22 degrees. Bore axis at y = 32 mm.
# Polymer frame with the Gen5 flared magwell and no finger grooves, steel slide with front and rear
# serrations, ejection port on the right with the barrel hood showing, recoil spring guide under the muzzle.
import bpy
K = bpy.app.driver_namespace["gunkit2"]
G = K.Gun2("Gun_Glock", ["Polymer", "Metal", "Steel"])
P, M, S = "Polymer", "Metal", "Steel"

# ---- slide (z -93 .. 93, y 16 .. 50)
sl = G.xsec([(-12.75, 16, 0.8), (12.75, 16, 0.8), (12.75, 45, 0.6), (8.6, 50, 1.2), (-8.6, 50, 1.2), (-12.75, 45, 0.6)], -93, 93, M, bevel=0.8)
cuts = [G.C("prof", [(70, 8), (104, 8), (104, 40)], -20, 20, M, bevel=0),        # the slanted nose under the muzzle
        G.C("prof", [(89, 60), (104, 60), (104, 46)], -20, 20, M, bevel=0),      # and a softened top front edge
        G.C("box", (0, 32, 96), (14.6, 14.6, 10), M, bevel=0),                   # barrel opening
        G.C("cyl", (0, 22.5, 88), (0, 22.5, 100), 3.6, M, seg=12),               # recoil spring guide hole
        G.C("box", (4.5, 45, 12), (17, 14, 38), M, bevel=0)]                      # ejection port (right side and top)
for side in (-1, 1):
    for i in range(8):     # rear serrations, raked forward like the factory's
        cuts.append(G.C("box", (side * 13.2, 31.5, -88 + i * 2.55), (2.4, 27, 1.15), M, bevel=0, rot=(-7, 0, 0)))
    for i in range(7):     # front serrations (Gen5 FS)
        cuts.append(G.C("box", (side * 13.2, 32.5, 58 + i * 2.55), (2.4, 25, 1.15), M, bevel=0, rot=(-7, 0, 0)))
G.cut(sl, cuts)
# what shows through the port: the barrel's chamber hood, flush with the slide top
G.box((1.5, 43, 12), (17.5, 13.5, 37), S, bevel=0.4)
G.box((11.4, 41, -4.5), (2.4, 5, 3.2), S, bevel=0.3)            # extractor claw edge at the back of the port
G.box((12.8, 40.5, -20), (0.8, 3.2, 17), S, bevel=0.2)          # extractor, flush in the slide side
# barrel crown and bore at the muzzle, recoil spring guide tip
br = G.cyl((0, 32, 70), (0, 32, 93.2), 6.9, S, seg=24, bevel=0.4)
G.cut(br, [G.C("cyl", (0, 32, 80), (0, 32, 100), 4.6, S, seg=16)])
G.cyl((0, 22.5, 82), (0, 22.5, 91.5), 2.6, P, seg=10)
G.box((0, 33, -93.3), (19, 26, 1.4), P, bevel=0.5)               # slide cover plate
G.cyl((0, 33, -94.2), (0, 33, -93.4), 2.2, S, seg=10)            # striker channel
# sights: polymer U-notch rear with a white outline, dot front
rs = G.box((0, 53.2, -82), (21, 6.4, 8.5), P, bevel=0.8)
G.cut(rs, [G.C("box", (0, 55.5, -82), (3.6, 5, 12), P, bevel=0)])
G.box((0, 53.2, 84.5), (3.4, 6.4, 4.6), P, bevel=0.5)
G.cyl((0, 54.5, 86.6), (0, 54.5, 86.9), 1.0, S, seg=10)
# ---- frame
# dust cover with the accessory rail (two side ledges and one cross slot)
dc = G.xsec([(-11.5, 16), (11.5, 16), (11.5, 7.5), (9.6, 7.5), (9.6, 5.2), (10.6, 5.2), (10.6, 2, 1.2), (-10.6, 2, 1.2), (-10.6, 5.2), (-9.6, 5.2), (-9.6, 7.5), (-11.5, 7.5)], -6, 86, P, bevel=0.5)
G.cut(dc, [G.C("prof", [(78, 0), (92, 0), (92, 12)], -20, 20, P, bevel=0), G.C("box", (0, 2.6, 67), (24, 3.4, 3.6), P, bevel=0)])
G.prof([(-93, 16), (0, 16), (0, 4), (-93, 4)], -11.5, 11.5, P, bevel=0.6)      # frame rails under the slide
# the grip: raked, no finger grooves, beavertail, Gen5 flared magwell with its front cutout
gp = G.prof([(-88, 8), (-18, 8), (-18, 3, 2), (-25, -4, 6), (-55, -79, 4), (-56, -86, 1.5), (-111, -86, 3),
             (-109, -74, 8), (-84, -6, 16), (-89, 5, 4), (-99, 11, 3), (-96, 16)], -14.6, 14.6, P, bevel=4.5, seg=3)
G.cut(gp, [G.C("cyl", (-20, -86, -55), (20, -86, -55), 7.5, P, seg=16),        # magwell cutout
           G.C("prof", [(-59, -86), (-106, -86), (-104, -62), (-62, -62)], -9.6, 9.6, P, bevel=0)])   # mag well
# the magazine in the well: body and floorplate
G.prof([(-62, -62), (-103, -62), (-105, -86), (-60, -86)], -9.4, 9.4, P, bevel=0.6)
G.prof([(-55, -86), (-111, -86, 1), (-111, -90.5, 2), (-57, -90.5, 2), (-54, -88)], -12.6, 12.6, P, bevel=1.2, seg=2)
# trigger guard: squared, flat-fronted, a little hook
tg = G.prof([(-22, 6), (28, 6), (33, -9, 4), (27, -17.5, 6), (-16, -17.5, 6), (-24, -8, 4)], -4.6, 4.6, P, bevel=1.4, seg=2)
G.cut(tg, [G.C("prof", [(-15, 3), (25, 3), (27.5, -9, 3), (23, -13, 4), (-12, -13, 4), (-17, -4, 3)], -10, 10, P, bevel=0)])
# trigger with its safety blade
G.tube([(0, 6.5, 9), (0, 1, 10.2), (0, -5, 8.5), (0, -10, 5.5)], 2.7, P, seg=10)
G.box((0, -1.5, 11.2), (1.4, 7, 1.6), P, bevel=0.3)
# controls: slide stop (left), takedown lever (both sides), mag release (both sides), pins
G.box((-13.4, 15.2, 3), (1.8, 3.4, 13), S, bevel=0.5)
G.box((-11.9, 10.6, 13), (1.6, 3.2, 5.5), S, bevel=0.4); G.box((11.9, 10.6, 13), (1.6, 3.2, 5.5), S, bevel=0.4)
G.box((-14.8, -1, -24), (2.4, 7.5, 6.5), P, bevel=0.8); G.box((14.8, -1, -24), (2.4, 7.5, 6.5), P, bevel=0.8)
for x in (-11.7, 11.7):
    G.screw((x, 10.2, 1.5), 1.25, "x", S); G.screw((x, 12.2, -28), 1.25, "x", S); G.screw((x, 5.5, -81), 1.25, "x", S)

pts = {"Muzzle": [0, 0.032, 0.0935], "Grip": [0, -0.03, -0.058], "Fore": [-0.006, -0.07, -0.07], "Iron": [0, 0.0564, 0.0845],
       "MagBase": [0, -0.086, -0.08], "SlideBack": [0, 0.034, -0.094], "Eject": [0.012, 0.047, 0.012]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
