# The Kalashnikov family, one build with the parts each model actually has. Published figures:
#   AKM     880 mm, 415 mm barrel, 7.62x39: stamped receiver, ribbed top cover, wood furniture, slant brake
#   AK-74   940 mm, 415 mm barrel, 5.45x39: plum furniture, the big two-port AK-74 brake, plum magazine
#   AK-74M  943 mm (705 folded): black polymer, side-folding solid stock, side rail for optics
#   AK-105  824 mm, 314 mm barrel: the AK-74M shortened, small cylindrical brake
#   AKS-74U 730 mm (490 folded), 206 mm barrel: triangle wire folding stock, hinged top cover carrying the
#           rear sight, gas block and front sight at the muzzle, conical booster
#   AK-12   (2018) 945 mm, 415 mm barrel: full-length rail on a fixed top cover, railed handguard, telescoping
#           folding stock, new grip, three-port brake
#   RPK-74  1060 mm, 590 mm heavy barrel, bipod, club stock, 45-round magazine
# Variant chosen by bpy.app.driver_namespace["ak"] (a key of V). Receiver front face at z = 58, bore y = 0.
import bpy, math
K = bpy.app.driver_namespace["gunkit3"]
bez = K.bez

V = {
    "AKM":    dict(name="Gun_AK47",  barrel=415, cal=762, furn="Wood", gripm="Wood", hg="wood", stock="akm", mag="762", dev="slant", cover="ribbed", rail=False),
    "AK74":   dict(name="Gun_AK74",  barrel=415, cal=545, furn="Plum", gripm="Plum", hg="wood", stock="ak74", mag="545p", dev="ak74", cover="ribbed", rail=False),
    "AK74M":  dict(name="Gun_AK74M", barrel=415, cal=545, furn="Polymer", gripm="Polymer", hg="poly", stock="fold", mag="545b", dev="ak74", cover="ribbed", rail=True),
    "AK105":  dict(name="Gun_AK105", barrel=314, cal=545, furn="Polymer", gripm="Polymer", hg="poly", stock="fold", mag="545b", dev="short", cover="ribbed", rail=True),
    "AKS74U": dict(name="Gun_AKS74U", barrel=206, cal=545, furn="Plum", gripm="Plum", hg="short", stock="skel", mag="545p", dev="booster", cover="hinged", rail=False),
    "AK12":   dict(name="Gun_AK12",  barrel=415, cal=545, furn="Polymer", gripm="Polymer", hg="ak12", stock="ak12", mag="545b", dev="ak12", cover="railed", rail=False),
    "RPK74":  dict(name="Gun_RPK74", barrel=590, cal=545, furn="Plum", gripm="Plum", hg="wood", stock="rpk", mag="545x45", dev="slant", cover="ribbed", rail=False, bipod=True, heavy=True),
}
v = V[bpy.app.driver_namespace.get("ak", "AKM")]
F = v["furn"]
mats = ["Blued", "Steel", "Metal", F]
if v["gripm"] not in mats: mats.append(v["gripm"])
G = K.Gun3(v["name"], mats)
BL, S, M = "Blued", "Steel", "Metal"

L = v["barrel"]
zm = 58 + L                                    # the muzzle (end of the barrel itself)
short = v["stock"] == "skel"
# gas block: two thirds down a full barrel; at the muzzle on the AKS-74U
zg = zm - 22 if short else 58 + int(L * 0.66)

# ---------------------------------------------------------------- receiver (stamped, z -180 .. 58)
rc = G.xsec([(-15, -30, 1.4), (15, -30, 1.4), (15, 8), (13.5, 11.5), (-13.5, 11.5), (-15, 8)], -180, 58, BL, bevel=0.8)
G.cut(rc, [G.C("box", (14, 4, -30), (8, 13, 64), BL, bevel=0),                                    # ejection port
           G.C("prof", [(-40, -31), (22, -31), (22, -12), (-40, -12)], -11.5, 11.5, BL, bevel=0)] +      # magazine well
          [G.C("box", (s * 15.4, -9, -62), (1.4, 3.2, 196), BL, bevel=0) for s in (-1, 1)])               # side groove
for s in (-1, 1):
    for z, y in ((30, -21), (44, -21), (30, -6), (44, -6), (-160, -21), (-171, -21), (-160, -8), (-150, -21), (-60, -24)):
        G.screw((s * 15.1, y, z), 1.8, "x", S, depth=0.5)
# rear trunnion bulge and the front trunnion block
G.xsec([(-16, -24, 2), (16, -24, 2), (16, 6, 2), (-16, 6, 2)], -182, -168, BL, bevel=0.8)
G.xsec([(-16.5, -26, 2), (16.5, -26, 2), (16.5, 9, 2), (-16.5, 9, 2)], 40, 60, BL, bevel=0.8)

# ---- top cover
if v["cover"] == "ribbed":
    G.xsec([(-14, 10), (14, 10), (14, 17, 6), (6, 24, 6), (-6, 24, 6), (-14, 17, 6)], -174, 40, BL, bevel=0.8)
    for z in (-152, -122, -92): G.xsec([(-12.5, 21), (12.5, 21), (6, 25.5, 4), (-6, 25.5, 4)], z, z + 9, BL, bevel=0.6)
    G.cyl((0, 14, -184), (0, 14, -176), 4.6, S, seg=14, bevel=0.5)                       # cover release
elif v["cover"] == "hinged":
    # the AKS-74U: cover hinged at the front, the flip rear sight sits on its back end
    G.xsec([(-14, 10), (14, 10), (14, 17, 6), (6, 24, 6), (-6, 24, 6), (-14, 17, 6)], -174, 40, BL, bevel=0.8)
    G.cyl((-15, 15, 44), (15, 15, 44), 4, S, seg=12)
    G.box((0, 30, -160), (18, 12, 26), BL, bevel=1.5)
    lf = G.prof([(-170, 30), (-150, 30), (-150, 38), (-170, 38)], -6, 6, BL, bevel=0.4)
    G.cut(lf, [G.C("box", (0, 38, -160), (3.4, 6, 30), BL, bevel=0)])
else:
    # AK-12: a rigid cover, its full-length Picatinny rail running on onto the handguard
    G.xsec([(-14.5, 10), (14.5, 10), (14.5, 19, 4), (-14.5, 19, 4)], -176, 58, BL, bevel=0.8)
    G.xsec([(-10.5, 19), (10.5, 19), (10.5, 25), (8, 27.5), (-8, 27.5), (-10.5, 25)], -174, 56, BL, bevel=0.4)
    for z in range(-170, 56, 10): G.box((0, 26.4, z + 2), (21.5, 3, 5.2), BL, bevel=0.3)

# ---- selector lever (right side), charging handle, bolt carrier in the port
G.prof([(-170, 7), (-62, 3), (-56, -6, 2), (-62, -8), (-70, -2), (-170, 0)], 15.1, 16.8, S, bevel=0.5)
G.cyl((15.2, 3, -168), (17.4, 3, -168), 5, S, seg=14)
G.box((9.5, 5, -30), (8, 9, 60), S, bevel=0.5)
G.prof([(-4, 2), (6, 2), (8, 10, 3), (-2, 10, 3)], 15, 30, S, bevel=1.4, seg=2)
G.cyl((30, 6, 2), (36, 6, 2), 5, S, seg=12, bevel=1.0)
# side rail for optics (left side)
if v["rail"]:
    G.prof([(-118, -22), (-28, -22), (-28, -2), (-118, -2)], -19.5, -15, BL, bevel=0.6)
    G.prof([(-114, -16), (-32, -16), (-32, -8), (-114, -8)], -21, -19.4, BL, bevel=0.3)

# ---------------------------------------------------------------- barrel, gas system, sights
br = 10.5 if v.get("heavy") else 8.6
G.lathe([(58, 13), (72, 13), (72, br + 1.5), (110, br), (zm, br - 0.6)], S, seg=20)
G.box((0, 2, 76), (26, 26, 12), BL, bevel=1.2)                                                 # trunnion / retainer
if not short:
    # rear sight block and tangent leaf: its top level with the front post
    G.box((0, 14, 74), (24, 16, 34), BL, bevel=1.4)
    lf = G.prof([(58, 22), (90, 22), (88, 30), (60, 34.2)], -7, 7, BL, bevel=0.5)
    G.cut(lf, [G.C("cyl", (0, 31.6, 50), (0, 31.6, 66), 2.3, BL, seg=14), G.C("box", (0, 35, 60), (4.6, 7, 16), BL, bevel=0)])
    G.box((0, 26, 74), (16, 4, 6), S, bevel=0.8)
# gas block and tube
G.prof([(zg - 18, -11), (zg + 18, -11), (zg + 18, 27), (zg - 12, 27)], -12, 12, BL, bevel=1.4, seg=2)
G.cyl((0, 19, 92), (0, 19, zg - 10), 7.5, BL, seg=16)
# front sight: its own base near the muzzle (full length), or part of the gas block (AKS-74U)
zf = zg + 4 if short else zm - 14
fsb = G.prof([(zf - 12, -14), (zf + 12, -14), (zf + 12, 18), (zf + 7, 30), (zf - 6, 30), (zf - 12, 18)], -11, 11, BL, bevel=1.2, seg=2)
G.cut(fsb, [G.C("prof", [(zf - 8, 22), (zf + 8, 22), (zf + 8, 40), (zf - 8, 40)], -6.5, 6.5, BL, bevel=0)])
hood = G.cyl((0, 30.5, zf - 7), (0, 30.5, zf + 7), 10, BL, seg=20)
G.cut(hood, [G.C("cyl", (0, 30.5, zf - 14), (0, 30.5, zf + 14), 8.3, BL, seg=20), G.C("box", (0, 41, zf), (14, 8, 30), BL, bevel=0)])
G.cyl((0, 22, zf), (0, 34, zf), 1.4, BL, seg=10)
if not short:
    G.box((0, -17, zf - 14), (10, 7, 18), BL, bevel=1.0)                                     # bayonet lug
    G.cyl((0, -15, 110), (0, -15, zf - 12), 3, S, seg=10)                                    # cleaning rod
iron_z = zf

# ---- muzzle device
dev = v["dev"]
if dev == "slant":
    d = G.cyl((0, 0, zm), (0, 0, zm + 30), 10, BL, seg=20, bevel=0.6)
    G.cut(d, [G.C("cyl", (0, 0, zm - 3), (0, 0, zm + 37), 5.2, BL, seg=16), G.C("box", (-6, 0, zm + 36), (30, 30, 14), BL, bevel=0, rot=(0, -30, 0))])
    dl = 30
elif dev == "ak74":
    # the AK-74 brake: a chamber with two big side windows, a flat front with a notch, small vent holes on top
    d = G.lathe([(zm - 4, 10), (zm + 6, 11.5), (zm + 80, 11.5), (zm + 82, 10.5)], BL, seg=24)
    cuts = [G.C("cyl", (0, 0, zm - 8), (0, 0, zm + 90), 6.2, BL, seg=16)]
    cuts += [G.C("box", (s * 11, 0, zm + 52), (8, 13, 18), BL, bevel=0) for s in (-1, 1)]
    cuts += [G.C("box", (0, -10, zm + 78), (16, 8, 12), BL, bevel=0)]
    cuts += [G.C("cyl", (-3, 6, zm + z), (-6, 16, zm + z), 1.6, BL, seg=8) for z in (20, 30, 40)]
    G.cut(d, cuts)
    dl = 82
elif dev == "short":
    d = G.lathe([(zm - 4, 10), (zm + 4, 12), (zm + 48, 12), (zm + 52, 13.5), (zm + 60, 13.5)], BL, seg=24)
    G.cut(d, [G.C("cyl", (0, 0, zm - 8), (0, 0, zm + 66), 6.5, BL, seg=16)] + [G.C("box", (s * 12, 0, zm + 30), (6, 9, 12), BL, bevel=0) for s in (-1, 1)])
    dl = 60
elif dev == "booster":
    # the AKS-74U's flash hider / booster: a can that flares into a bell at the front
    d = G.lathe([(zm - 4, 10), (zm + 6, 12.5), (zm + 46, 12.5), (zm + 62, 16), (zm + 66, 16)], BL, seg=24)
    G.cut(d, [G.C("cyl", (0, 0, zm - 8), (0, 0, zm + 72), 6.5, BL, seg=16), G.C("cyl", (0, 0, zm + 50), (0, 0, zm + 72), 12.5, BL, seg=20, r2=14)])
    dl = 66
else:
    # AK-12: three slots each side
    d = G.lathe([(zm - 4, 10), (zm + 6, 11.8), (zm + 86, 11.8), (zm + 90, 10.5)], BL, seg=24)
    G.cut(d, [G.C("cyl", (0, 0, zm - 8), (0, 0, zm + 96), 6.2, BL, seg=16)] +
          [G.C("box", (s * 11, 2, zm + 30 + k * 16), (8, 16, 8), BL, bevel=0) for s in (-1, 1) for k in range(3)])
    dl = 90

# ---------------------------------------------------------------- handguards
hg = v["hg"]
hz1 = zg - 26                                   # the handguard runs from the trunnion to short of the gas block
if hg in ("wood", "short"):
    hm = F
    G.xsec([(-12, 12, 4), (12, 12, 4), (12, 22, 8), (-12, 22, 8)], 95, hz1, hm, bevel=2, seg=2)
    G.xsec([(-15, 22), (15, 22), (12, 30, 6), (-12, 30, 6)], 95, hz1, hm, bevel=1.5, seg=2)
    lh = G.xsec([(-21, 10), (21, 10), (23, -6, 6), (16, -24, 8), (-16, -24, 8), (-23, -6, 6)], 90, hz1 + 10, hm, bevel=3, seg=2)
    # finger grooves along each side of the lower
    G.cut(lh, [G.C("box", (s * 23, -9, (90 + hz1) / 2), (4, 7, (hz1 - 90) * 0.75), hm, bevel=0) for s in (-1, 1)] +
              [G.C("box", (s * 21.6, -18, (90 + hz1) / 2), (4, 4, (hz1 - 90) * 0.75), hm, bevel=0, rot=(0, 0, s * 35)) for s in (-1, 1)])
    G.box((0, -6, hz1 + 14), (34, 30, 10), BL, bevel=1.4)
elif hg == "poly":
    # AK-74M: black polymer, ribbed lower, the upper over the gas tube with its heat shield
    G.xsec([(-13, 12, 4), (13, 12, 4), (13, 23, 8), (-13, 23, 8)], 95, hz1, F, bevel=2, seg=2)
    lh = G.xsec([(-21.5, 10), (21.5, 10), (23.5, -6, 6), (16, -25, 8), (-16, -25, 8), (-23.5, -6, 6)], 90, hz1 + 10, F, bevel=3, seg=2)
    G.cut(lh, [G.C("box", (s * 23.5, y, (90 + hz1) / 2), (3, 2.4, (hz1 - 90) * 0.8), F, bevel=0) for s in (-1, 1) for y in (-3, -9, -15)])
    G.box((0, -6, hz1 + 14), (34, 30, 10), BL, bevel=1.4)
else:
    # AK-12: an octagonal railed handguard to the gas block, rail on top in line with the cover's
    hgo = G.xsec([(-20, -22, 3), (20, -22, 3), (24, -6), (24, 14), (16, 22), (-16, 22), (-24, 14), (-24, -6)], 62, hz1 + 14, F, bevel=1.2)
    G.cut(hgo, [G.C("box", (s * 24, 2, z), (5, 7, 22), F, bevel=0) for s in (-1, 1) for z in range(110, hz1, 40)])
    G.xsec([(-10.5, 22), (10.5, 22), (10.5, 25), (8, 27.5), (-8, 27.5), (-10.5, 25)], 62, hz1 + 12, BL, bevel=0.4)
    for z in range(66, hz1 + 8, 10): G.box((0, 26.4, z), (21.5, 3, 5.2), BL, bevel=0.3)
    G.xsec([(-6, -22), (6, -22), (6, -26), (-6, -26)], 150, hz1 + 4, BL, bevel=0.4)   # bottom rail strip

# ---------------------------------------------------------------- trigger group
tg = G.prof([(-44, -30), (-124, -30), (-124, -44, 4), (-44, -44, 4)], -5.5, 5.5, S, bevel=1.0)
G.cut(tg, [G.C("prof", [(-50, -32), (-118, -32), (-118, -40, 3), (-50, -40, 3)], -10, 10, S, bevel=0)])
G.tube([(0, -28, -78), (0, -34, -76), (0, -40, -79)], 2.6, S, seg=10)
G.prof([(-40, -30), (-48, -30), (-50, -46, 2), (-44, -46, 2)], -6, 6, S, bevel=0.8)

# ---------------------------------------------------------------- magazine
mg = v["mag"]
if mg == "762":
    out = [(22, -31)] + bez((22, -31), (32, -118), (98, -200), 12) + [(50, -220)] + bez((50, -220), (-26, -124), (-38, -31), 12)
    mw, mm_ = 13.2, M
elif mg == "545x45":
    out = [(22, -31)] + bez((22, -31), (30, -150), (90, -268), 14) + [(46, -288)] + bez((46, -288), (-24, -162), (-38, -31), 14)
    mw, mm_ = 12.6, F
else:
    out = [(22, -31)] + bez((22, -31), (27, -112), (74, -198), 12) + [(30, -216)] + bez((30, -216), (-30, -120), (-38, -31), 12)
    mw, mm_ = 12.6, ("Plum" if mg == "545p" else "Polymer")
if mm_ not in G.mats: G.mats.append(mm_)
mag = G.prof(out, -mw, mw, mm_, bevel=1.4, seg=2)
# the ribs pressed into each side of the steel one, or the moulded ribs of the polymer ones
n = 7 if mg == "762" else 9
ribs = []
for k in range(n):
    t = (k + 1.0) / (n + 1)
    # a point inside the body, along its middle, and the local slope there
    ia = int(t * 12) if mg != "545x45" else int(t * 14)
    p_front = out[1 + ia]; p_back = out[-1 - ia]
    cz, cy = (p_front[0] + p_back[0]) / 2, (p_front[1] + p_back[1]) / 2
    ang = math.degrees(math.atan2(p_front[1] - p_back[1], p_front[0] - p_back[0]))
    ln = math.hypot(p_front[0] - p_back[0], p_front[1] - p_back[1]) * 0.6
    ribs += [G.C("box", (s * mw, cy, cz), (1.6, 2.6, ln), mm_, bevel=0, rot=(-ang, 0, 0)) for s in (-1, 1)]
G.cut(mag, ribs)
# the floorplate across the bottom, and the magazine kept apart (it comes out on a reload)
ns = 12 if mg != "545x45" else 14
fb, bb = out[ns], out[ns + 1]
fp = G.prof([(fb[0] + 3, fb[1] + 3), (fb[0] - 1, fb[1] - 6), (bb[0] - 3, bb[1] - 5), (bb[0] + 1, bb[1] + 4)], -mw - 1.2, mw + 1.2, mm_, bevel=1.2, seg=2)
G.as_mag(mag, fp)
mag_bottom = [0, round((fb[1] + bb[1]) / 2000, 4), round((fb[0] + bb[0]) / 2000, 4)]

# ---------------------------------------------------------------- pistol grip
gm = v["gripm"]
if v["stock"] == "ak12":
    gp = G.prof([(-94, -30), (-136, -30), (-160, -78, 10), (-170, -126, 8), (-150, -134, 6), (-128, -118, 8), (-124, -86, 10), (-106, -54, 8), (-98, -40, 3)], -14, 14, gm, bevel=3.5, seg=3)
    G.cut(gp, [G.C("box", (s * 14, -86 - k * 9, -140 + k * 2.6), (2, 2.4, 30), gm, bevel=0, rot=(-16, 0, 0)) for s in (-1, 1) for k in range(4)])
else:
    gp = G.prof([(-94, -30), (-134, -30), (-162, -122, 8), (-150, -132, 6), (-124, -128, 6), (-108, -64, 8), (-98, -40, 3)], -13, 13, gm, bevel=3.5, seg=3)
    if gm != "Wood": G.cut(gp, [G.C("box", (s * 13, -70 - k * 12, -128 - k * 3.2), (2, 2.2, 32), gm, bevel=0, rot=(-15, 0, 0)) for s in (-1, 1) for k in range(4)])
G.taper_y(gp, -30, 1.0, -132, 0.86)

# ---------------------------------------------------------------- stocks
st = v["stock"]
if st in ("akm", "ak74"):
    sp = G.prof([(-178, 12), (-178, -30), (-210, -44, 14), (-392, -128), (-392, -14), (-262, 4, 50)], -19, 19, F, bevel=4, seg=3)
    G.taper(sp, -180, 0.7, -392, 1.0)
    if st == "ak74":
        # the lightening slots down each side of the AK-74's stock
        G.cut(sp, [G.C("box", (s * 19.5 * 0.86, -40, -300), (5, 10, 140), F, bevel=0, rot=(-17, 0, 0)) for s in (-1, 1)])
    G.prof([(-392, -12), (-400, -12, 2), (-400, -130, 2), (-392, -130)], -19.5, 19.5, S, bevel=1.6, seg=2)
    G.screw((19.5, -68, -396), 2.4, "x", S)
elif st == "rpk":
    # the RPK's club stock: a deep drop behind the grip for the support hand
    sp = G.prof([(-178, 12), (-178, -30), (-212, -76, 24), (-252, -64), (-412, -132), (-412, -14), (-262, 4, 50)], -19, 19, F, bevel=4, seg=3)
    G.taper(sp, -180, 0.72, -412, 1.0)
    G.prof([(-412, -12), (-420, -12, 2), (-420, -134, 2), (-412, -134)], -19.5, 19.5, S, bevel=1.6, seg=2)
elif st == "fold":
    # AK-74M: a solid polymer stock that folds to the left, hinge block at the receiver
    G.xsec([(-17, -26, 3), (17, -26, 3), (17, 8, 3), (-17, 8, 3)], -200, -180, BL, bevel=1.0)
    sp = G.prof([(-198, 10), (-198, -30), (-226, -46, 14), (-402, -126), (-402, -16), (-266, 4, 50)], -18, 18, F, bevel=3.5, seg=3)
    G.taper(sp, -198, 0.72, -402, 1.0)
    G.cut(sp, [G.C("box", (s * 18 * 0.86, -44, -310), (4, 10, 130), F, bevel=0, rot=(-17, 0, 0)) for s in (-1, 1)])
    G.prof([(-402, -14), (-410, -14, 2), (-410, -128, 2), (-402, -128)], -19, 19, "Polymer" if "Polymer" in G.mats else F, bevel=1.6, seg=2)
elif st == "skel":
    # AKS-74U: the triangle of steel strap, folded out, and its butt plate
    G.xsec([(-15, -24, 2), (15, -24, 2), (15, 6, 2), (-15, 6, 2)], -196, -180, BL, bevel=0.8)
    for s in (-1, 1):
        G.tube([(s * 9, 0, -192), (s * 9, -6, -420)], 4.2, BL, seg=10)
    G.tube([(0, -22, -192), (0, -96, -420)], 4.5, BL, seg=10)
    G.prof([(-416, 2), (-428, 2, 3), (-428, -112, 3), (-416, -112)], -17, 17, BL, bevel=1.4, seg=2)
    bp = G.prof([(-418, -30), (-418, -84), (-426, -84), (-426, -30)], -20, 20, BL, bevel=0)
else:
    # AK-12: hinge, telescoping tube and a skeletal polymer stock with a raised cheek rest
    G.xsec([(-17, -26, 3), (17, -26, 3), (17, 8, 3), (-17, 8, 3)], -200, -180, BL, bevel=1.0)
    G.xsec([(-14, -18, 4), (14, -18, 4), (14, 6, 4), (-14, 6, 4)], -290, -198, BL, bevel=1.0)
    sp = G.prof([(-246, 14), (-246, -22), (-300, -38, 10), (-412, -122, 6), (-424, -122, 4), (-424, 10, 4), (-330, 22, 10), (-262, 22, 6)], -17, 17, F, bevel=3, seg=3)
    G.cut(sp, [G.C("prof", [(-306, -18), (-392, -84), (-392, -6), (-320, 0)], -30, 30, F, bevel=0)])
    G.prof([(-424, 12), (-432, 12, 2), (-432, -124, 2), (-424, -124)], -18, 18, "Polymer", bevel=1.6, seg=2)

# ---------------------------------------------------------------- RPK bipod (folded forward under the barrel)
if v.get("bipod"):
    G.cyl((0, -2, zm - 64), (0, -2, zm - 52), 15, BL, seg=18)
    for s in (-1, 1):
        G.tube([(s * 9, -12, zm - 58), (s * 12, -20, zm - 330)], 4, BL, seg=10)
        G.box((s * 12, -21, zm - 334), (9, 6, 14), BL, bevel=1.5)

stock_back = {"akm": -400, "ak74": -400, "rpk": -420, "fold": -410, "skel": -428, "ak12": -432}[st]
pts = {"Muzzle": [0, 0, round((zm + dl) / 1000, 4)], "Grip": [0, -0.074, -0.122],
       "Fore": [0, -0.008, round(((90 + hz1) / 2 if hg != "ak12" else (70 + hz1) / 2) / 1000, 4)],
       "Iron": [0, 0.034, round(iron_z / 1000, 4)], "Eject": [0.016, 0.006, -0.03],
       "MagTop": [0, -0.031, -0.008], "MagBottom": mag_bottom, "Charge": [0.033, 0.006, 0.002]}
if v["rail"] or v["cover"] == "railed": pts["Rail"] = [0, 0.0275 if v["cover"] == "railed" else 0.024, -0.06]
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
