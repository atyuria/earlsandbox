# Parked cars for the HOUSES street: a four-door saloon (4.6 m) and a five-door hatchback (4.05 m), built like the
# guns (gunkit2, sizes in mm, Unity axes, origin on the ground under the middle, nose toward +z). Materials: Paint
# (tinted per car by the game), Glass, Rubber, Steel (rims), Trim (black plastic), Light, Tail, Plate.
# blender --background --python load_gunkit.py --python gunkit2.py --python gunshot2.py --python car_build.py -- [preview dir]
import bpy, sys, math
K = bpy.app.driver_namespace["gunkit2"]
OUT = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/Assets/CarlSandbox/Resources/Models/"
PV = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
P, Gl, R, S, T, L, TL, PL = "Paint", "Glass", "Rubber", "Steel", "Trim", "Light", "Tail", "Plate"

def car(name, L2, wb, hatch):
    """L2: half the length; wb: wheelbase; hatch: a steep tailgate instead of a boot."""
    G = K.Gun2(name, [P, Gl, R, S, T, L, TL, PL])
    W = 890                                   # half the body's width
    zf, zr = wb / 2, -wb / 2                  # axles
    rear_top = -L2 + 60
    # ---- the body below the windows: bumpers rounded in plan by the bevel, bonnet sloping to the nose
    body = G.prof([(-L2 + 20, 250, 40), (-L2, 620, 100), (rear_top, 930, 120), (-L2 * 0.5, 965, 40), (L2 * 0.55, 945, 40),
                   (L2 - 60, 790, 160), (L2, 520, 120), (L2 - 10, 250, 40), (L2 - 500, 190), (-L2 + 500, 190)], -W, W, P, bevel=45, seg=3)
    G.cut(body, [G.C("cyl", (-W - 50, 330, z), (W + 50, 330, z), 395, P, seg=28) for z in (zf, zr)])
    for z in (zf, zr): G.cyl((-W + 235, 330, z), (W - 235, 330, z), 392, T, seg=28)        # the dark wheel wells
    # ---- the glasshouse: tumblehome (narrower than the body), a hatch drops nearly straight at the back
    if hatch:
        gh = [(-L2 + 80, 930), (-L2 + 200, 1360, 120), (wb * 0.12, 1420, 140), (zf - 120, 960)]
        roof = [(-L2 + 210, 1365), (wb * 0.12, 1418), (wb * 0.12, 1452, 20), (-L2 + 200, 1398, 20)]
    else:
        gh = [(-L2 + 760, 935), (-wb * 0.5 + 60, 1390, 120), (wb * 0.11, 1420, 140), (zf - 120, 960)]
        roof = [(-wb * 0.5 + 40, 1392), (wb * 0.11, 1418), (wb * 0.11, 1452, 20), (-wb * 0.5 + 40, 1425, 20)]
    G.prof(gh, -735, 735, Gl, bevel=60, seg=2)
    G.prof(roof, -715, 715, P, bevel=15)
    # pillars and the window frames round the glass
    rear_bot, rear_up = (gh[0][0], 930), (gh[1][0], gh[1][1])
    for s in (-1, 1):
        x = s * 738
        G.tube([(x, 950, zf - 130), (s * 712, 1405, wb * 0.11)], 34, P)                    # A pillar
        G.tube([(x, 935, rear_bot[0] + 20), (s * 712, rear_up[1] - 10, rear_up[0] + 30)], 60 if hatch else 46, P)   # C / D pillar
        G.box((s * 742, 1180, -wb * 0.08), (14, 470, 95), P, bevel=4)                    # B pillar
        G.tube([(s * 746, 952, rear_bot[0] + 30), (s * 746, 952, zf - 140)], 14, T)          # waist seal
        if not hatch: G.box((s * 742, 1180, -wb * 0.5 + 30), (14, 400, 60), T, bevel=4)     # rear quarter-light divider
        # door shut lines, handles, mirror
        xs = s * (W + 3)
        for z in (zf - 260, -wb * 0.08, (-wb * 0.5 - 180) if not hatch else (-wb * 0.5 - 60)):
            G.box((xs, 600, z), (4, 620, 6), T, bevel=0)
        for z in (zf - 560, -wb * 0.08 - 280):
            G.box((s * (W + 8), 860, z), (12, 26, 150), T, bevel=5)
        G.box((s * (W + 90), 1010, zf - 200), (150, 95, 90), P, bevel=20)
        G.box((s * (W + 40), 1000, zf - 230), (60, 40, 40), T, bevel=5)
        # side rubbing strip
        G.box((xs + s * 6, 560, 0), (10, 40, L2 * 1.3), T, bevel=4)
    # ---- wheels: tyre, five-spoke look via a dished rim and a hub cap
    for z in (zf, zr):
        for s in (-1, 1):
            x0, x1 = s * (W - 225), s * (W - 15)
            G.cyl((x0, 320, z), (x1, 320, z), 320, R, seg=32, bevel=25, bseg=2)
            G.cyl((x1, 320, z), (x1 + s * 4, 320, z), 215, S, seg=28)
            G.cyl((x1 + s * 4, 320, z), (x1 + s * 10, 320, z), 70, S, seg=16)
            for k in range(5):
                a = k / 5 * math.tau
                G.box((x1 + s * 6, 320 + math.sin(a) * 140, z + math.cos(a) * 140), (8, 46, 46), T, bevel=0)
    # ---- the nose and tail: lamps, grille, bumpers, plates
    for s in (-1, 1):
        G.box((s * 610, 720, L2 - 75), (400, 120, 110), L, bevel=25)
        G.box((s * 650, 780 if hatch else 760, -L2 + (40 if hatch else 25)), (360, 150, 60), TL, bevel=20)
        G.box((s * 700, 300, L2 - 10), (180, 60, 40), L, bevel=10)                       # fog lamps
    G.box((0, 560, L2 + 5), (760, 170, 40), T, bevel=15)                                 # grille
    G.box((0, 300, L2 + 10), (1760, 200, 70), T, bevel=40)                               # front bumper strip
    G.box((0, 300, -L2 - 10), (1760, 200, 70), T, bevel=40)                              # rear bumper strip
    G.box((0, 470, L2 + 32), (520, 115, 10), PL, bevel=3)
    G.box((0, 640 if hatch else 600, -L2 - 30), (520, 115, 10), PL, bevel=3)
    G.box((0, 230, 0), (1600, 110, L2 * 1.7), T, bevel=10)                               # underside
    pts = {"Centre": [0, 0.6, 0]}
    G.export(OUT + name + ".json", pts)
    if PV: bpy.app.driver_namespace["gunshot2"].sheet(G, PV + "/car_" + name + ".png", 420)

car("Car_Saloon", 2300, 2700, False)
car("Car_Hatch", 2025, 2550, True)
print("CARS OK")
