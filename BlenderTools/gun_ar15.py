# AR-15 carbine, modern build like the reference photo: 16" barrel, 13" free-float M-LOK handguard (octagonal,
# slots at 3/6/9 o'clock and on the lower bevels, full-length top rail), flat-top upper with forward assist,
# brass deflector, ejection port and dust cover, folding back-up iron sights, A2 birdcage, 30-round curved
# magazine, A2-style grip, collapsible stock on a carbine buffer tube. About 840 mm long collapsed.
# Bore axis y = 0; upper receiver z -165 .. 20.
# The gunsmith: every part can be swapped (bpy.app.driver_namespace["ar_over"]) and each section is marked
# (bpy.app.driver_namespace["armark"]) so build_arparts.py can export the parts on their own.
import bpy, math
K = bpy.app.driver_namespace["gunkit3"]
bez = K.bez
v = dict(barrel=406, hg="mlok13", dev="a2", grip="a2", stock="ctr", mag="pmag30", ch="milspec")
v.update(bpy.app.driver_namespace.get("ar_over") or {})
MARK = bpy.app.driver_namespace.get("armark") or (lambda n: None)
G = K.Gun3("Gun_AR15", ["Anod", "Steel", "Polymer", "Rubber", "Metal", "Tan"])
bpy.app.driver_namespace["ar_G"] = G
A, S, P, R, M = "Anod", "Steel", "Polymer", "Rubber", "Metal"
zm = 20 + v["barrel"] - 45                     # the end of the barrel (16": z 381)

def rail(z0, z1, mat=A, y=19):
    """Picatinny rail along the top from z0 to z1, slots every 10 mm."""
    d = y - 19
    r = G.xsec([(-7.8, 19 + d), (7.8, 19 + d), (7.8, 23 + d), (10.6, 25.6 + d), (10.6, 26.6 + d), (8.3, 29 + d), (-8.3, 29 + d), (-10.6, 26.6 + d), (-10.6, 25.6 + d), (-7.8, 23 + d)], z0, z1, mat, bevel=0.35)
    n = int((z1 - z0 - 6) // 10)
    G.cut(r, [G.C("box", (0, 28.6 + d, z0 + 6 + i * 10), (30, 5, 5.3), mat, bevel=0) for i in range(n)])

# ---- upper receiver
MARK("core")
up = G.prof([(-165, 19), (20, 19), (20, -14), (-60, -14), (-62, -10), (-150, -10), (-165, -6)], -13.5, 13.5, A, bevel=1.4, seg=2)
G.cut(up, [G.C("box", (12, 2.5, -66), (10, 17, 58), A, bevel=0)])          # ejection port, right side
rail(-163, 18)
G.box((10.6, 2.5, -66), (1.4, 16, 57), A, bevel=0.3)                         # dust cover, closed
for i in range(5): G.box((11.6, -3 + i * 2.6, -66), (0.9, 0.9, 54), A, bevel=0.2)
G.prof([(-108, 6), (-92, 6), (-92, 19), (-104, 19)], 12.5, 17.5, A, bevel=1.6, seg=2)   # brass deflector
G.cyl((12, 10, -128), (21, 8, -104), 6.2, A, seg=16, bevel=0.6)              # forward assist housing
G.cyl((19, 8.6, -110), (23.5, 7.8, -100), 5.2, S, seg=16, bevel=0.8)         # and its plunger
MARK("cover")
if v["ch"] == "raptor":
    # Radian Raptor: an ambidextrous handle, a wide latch wing each side
    G.box((0, 15, -172), (30, 6.5, 9), A, bevel=1.5)
    for s in (-1, 1): G.prof([(-178, 12), (-166, 12), (-162, 18, 2), (-176, 18, 2)], s * 14, s * 26, A, bevel=1.2, seg=2)
else:
    G.box((0, 15, -172), (36, 6.5, 9), A, bevel=1.5)                            # charging handle T
    G.box((-14, 15.5, -170), (7, 4, 12), A, bevel=1.0)                          # its latch
MARK("core")
G.cyl((0, 0, 18), (0, 0, 36), 17, S, seg=24, bevel=1.0)                     # barrel nut
# ---- handguard
MARK("handguard")
hg = v["hg"]
oct = [(-14, 21), (14, 21), (23, 12), (23, -14), (14, -27), (-14, -27), (-23, -14), (-23, 12)]
if hg in ("mlok13", "mlok9"):
    hz = 356 if hg == "mlok13" else 262
    hgo = G.xsec(oct, 24, hz, A, bevel=1.2, seg=2)
    sl = []
    for k in range(int((hz - 66) // 42) + 1):
        z = 66 + k * 42
        if z + 16 > hz - 6: break
        sl.append(G.C("box", (23, -1, z), (6, 7.5, 32), A, bevel=0))
        sl.append(G.C("box", (-23, -1, z), (6, 7.5, 32), A, bevel=0))
        sl.append(G.C("box", (0, -27, z), (7.5, 6, 32), A, bevel=0))
        sl.append(G.C("box", (18.5, -20.5, z), (7.5, 6, 32), A, bevel=0, rot=(0, 0, -55)))
        sl.append(G.C("box", (-18.5, -20.5, z), (7.5, 6, 32), A, bevel=0, rot=(0, 0, 55)))
        sl.append(G.C("box", (18.5, 16.5, z), (12, 6, 20), A, bevel=0, rot=(0, 0, 45)))     # vent cut on the upper bevels
        sl.append(G.C("box", (-18.5, 16.5, z), (12, 6, 20), A, bevel=0, rot=(0, 0, -45)))
    G.cut(hgo, sl)
    rail(22, hz)
    # folding front sight (deployed) near the front of the rail
    zf = hz - 16
    G.box((0, 31.5, zf), (21, 5, 22), A, bevel=0.8)
    fs = G.prof([(zf - 10, 31), (zf + 10, 31), (zf + 7, 58, 2), (zf - 7, 58, 2)], -9, 9, A, bevel=0.8)
    G.cut(fs, [G.C("prof", [(zf - 6, 38), (zf + 6, 38), (zf + 5, 60), (zf - 5, 60)], -6, 6, A, bevel=0)])
    G.box((0, 54, zf), (2, 13, 2.2), S, bevel=0.3)                              # front post (top on the sight line, y 60.5)
    fore = 270 if hg == "mlok13" else 200
else:
    # M4 carbine handguard (ribbed oval halves with heat-shield holes) or the KAC RAS quad rail, both ending at the
    # A-frame front sight base on the barrel
    hz = 196
    if hg == "m4":
        h = G.xsec([(-17, -24, 8), (17, -24, 8), (21, -6, 8), (21, 10, 8), (14, 22, 6), (-14, 22, 6), (-21, 10, 8), (-21, -6, 8)], 34, hz, P, bevel=2.0, seg=2)
        G.cut(h, [G.C("box", (0, 22, z), (40, 4, 3), P, bevel=0) for z in range(46, hz - 6, 12)] +
                 [G.C("box", (0, -24, z), (40, 4, 3), P, bevel=0) for z in range(46, hz - 6, 12)])
        for z in (34, hz): G.lathe([(z - 4, 22.5), (z + 4, 22.5)], A, seg=24)          # delta ring and cap
    else:
        G.xsec([(-17, -21, 3), (17, -21, 3), (21, -17, 3), (21, 15, 3), (17, 19, 3), (-17, 19, 3), (-21, 15, 3), (-21, -17, 3)], 34, hz, A, bevel=1.2)
        rail(34, hz)
        for side in (-1, 1):
            r2 = G.prof([(34, -8), (hz, -8), (hz, 8), (34, 8)], side * 21, side * 27, A, bevel=0.3)
            G.cut(r2, [G.C("box", (side * 27, 0, 40 + k * 10), (5, 30, 5.3), A, bevel=0) for k in range(int((hz - 46) // 10))])
        r3 = G.xsec([(-8, -21), (8, -21), (8, -27), (-8, -27)], 34, hz, A, bevel=0.3)
        G.cut(r3, [G.C("box", (0, -27, 40 + k * 10), (30, 5, 5.3), A, bevel=0) for k in range(int((hz - 46) // 10))])
    zf = hz + 26
    G.lathe([(zf - 14, 13), (zf + 14, 13)], A, seg=20)                                  # gas block band
    fsb = G.prof([(zf - 12, 8), (zf + 12, 8), (zf + 5, 50, 2), (zf - 5, 50, 2)], -6, 6, A, bevel=1.0)
    G.cut(fsb, [G.C("box", (0, 42, zf), (20, 10, 6), A, bevel=0)])
    G.box((0, 47, zf), (8, 6, 6), A, bevel=0.8)
    G.box((0, 54, zf), (2, 13, 2.2), S, bevel=0.3)
    G.box((0, -12, zf), (12, 8, 18), A, bevel=1.0)                                      # bayonet lug
    fore = 120
bpy.app.driver_namespace["ar_fore"] = fore
iron_z = zf
# ---- barrel
MARK("core")
G.cyl((0, 0, 36), (0, 0, zm), 8.6, S, seg=20)
G.cyl((0, 0, zm - 4), (0, 0, zm + 0.5), 12.5, S, seg=6)                     # crush washer flats
# ---- muzzle device (its origin is the end of the barrel)
MARK("muzzle")
dev = v["dev"]
if dev == "a2":
    fh = G.cyl((0, 0, zm), (0, 0, zm + 54), 11, S, seg=24, bevel=0.8)
    fc = [G.C("cyl", (0, 0, zm + 14), (0, 0, zm + 59), 7, S, seg=16)]
    for k in range(5):
        a = (-60 + k * 30)
        x = 14 * math.sin(math.radians(a)); y = 14 * math.cos(math.radians(a))
        fc.append(G.C("box", (x, y, zm + 31), (3.2, 9, 30), S, bevel=0, rot=(0, 0, -a)))
    G.cut(fh, fc)
    dl = 54
elif dev == "sf3p":
    # SureFire SF3P: three long prongs
    G.lathe([(zm, 11), (zm + 22, 11)], S, seg=24)
    fh = G.cyl((0, 0, zm + 22), (0, 0, zm + 62), 11, S, seg=24)
    G.cut(fh, [G.C("cyl", (0, 0, zm + 18), (0, 0, zm + 70), 7.6, S, seg=16)] +
              [G.C("box", (8.6 * math.sin(math.radians(a)), 8.6 * math.cos(math.radians(a)), zm + 48), (3.6, 12, 34), S, bevel=0, rot=(0, 0, -a)) for a in (60, 180, 300)])
    dl = 62
elif dev == "pa":
    # Precision Armament M4-72: a squared-off brake, big side ports
    b = G.lathe([(zm, 11.5), (zm + 56, 11.5)], S, seg=6)
    G.cut(b, [G.C("cyl", (0, 0, zm - 4), (0, 0, zm + 60), 6.2, S, seg=16)] + [G.C("box", (s * 11, 0, zm + 16 + k * 13), (8, 12, 7), S, bevel=0) for s in (-1, 1) for k in range(3)])
    dl = 56
else:
    # SureFire SOCOM556-RC2: the mount, then the can
    G.lathe([(zm, 11), (zm + 30, 11), (zm + 30, 13)], S, seg=24)
    G.lathe([(zm + 26, 19.5), (zm + 170, 19.5), (zm + 178, 17), (zm + 180, 8)], A, seg=28)
    dl = 180
# ---- lower receiver
MARK("core")
# rear folding sight on the upper
G.box((0, 31.5, -150), (21, 5, 24), A, bevel=0.8)
rsf = G.prof([(-158, 31), (-142, 31), (-143, 67, 3), (-157, 67, 3)], -10, 10, A, bevel=0.8)
G.cut(rsf, [G.C("cyl", (0, 60.5, -165), (0, 60.5, -135), 2.2, A, seg=12)])   # aperture on the sight line
lo = G.prof([(-165, -10), (18, -10), (18, -24), (-5, -24), (-5, -32), (-108, -32), (-112, -22), (-150, -22), (-165, -18)], -12.8, 12.8, A, bevel=1.4, seg=2)
mw = G.prof([(-4, -10), (-4, -60, 2), (-1, -66, 2), (-80, -66, 2), (-77, -60, 2), (-77, -10)], -15, 15, A, bevel=2, seg=2)   # magwell, flared
G.cut(mw, [G.C("prof", [(-8, -40), (-73, -40), (-73, -70), (-8, -70)], -11.5, 11.5, A, bevel=0)])
G.box((15.5, -27, -16), (2.5, 9, 9), S, bevel=1.2)                           # mag release (right)
G.box((-14.5, -18, -86), (2.5, 14, 9), A, bevel=1.0)                         # bolt catch (left)
G.cyl((-13.5, -16, -116), (-16, -16, -116), 6.5, S, seg=16)                   # safety selector
G.box((-16.5, -12, -116), (2, 4, 14), S, bevel=0.6, rot=(-30, 0, 0))
for z in (10, -152): G.screw((13.4, -16, z), 2.3, "x", S); G.screw((-13.4, -16, z), 2.3, "x", S)
G.screw((13.4, -24, -96), 1.6, "x", S); G.screw((13.4, -24, -86), 1.6, "x", S)   # trigger and hammer pins
# trigger guard and trigger
G.prof([(-74, -32), (-112, -32), (-112, -46, 3), (-74, -46, 3)], -5.5, 5.5, A, bevel=1.2)
G.cut(G.parts[-1], [G.C("prof", [(-78, -34), (-108, -34), (-108, -42, 2), (-78, -42, 2)], -10, 10, A, bevel=0)])
G.tube([(0, -30, -92), (0, -36, -90.5), (0, -41, -93)], 2.4, S, seg=10)
# buffer tube and castle nut
G.cyl((0, -6, -165), (0, -6, -400), 14.5, A, seg=24, bevel=0.6)
G.cyl((0, -6, -165), (0, -6, -174), 18, S, seg=8, bevel=0.6)
# ---- the magazine
# (the STANAG's gentle forward curve, its stamped side ribs and the flared floorplate; it comes out on a reload)
MARK("mag")
mg = v["mag"]
if mg == "d60":
    # Magpul D-60: two drums side by side under a short feed tower
    tw = G.prof([(-8, -40), (-9, -88), (-72, -88), (-73, -40)], -11, 11, P, bevel=1.4, seg=2)
    dr = [G.cyl((s * 6, -150, -40), (s * 46, -150, -40), 62, P, seg=40, bevel=2.0) for s in (-1, 1)]
    G.as_mag(tw, *dr)
    mag_bottom = [0, -0.212, -0.04]
else:
    L = {"stanag20": 0.62, "pmag30": 1.0, "pmag40": 1.32, "sf60": 1.42}[mg]
    cv = 0.2 if mg == "stanag20" else 1.0
    b0 = -40 - 146 * L
    mout = [(-8, -40)] + bez((-8, -40), (-4, -40 - 70 * L), (-8 + 20 * cv, b0), 10) + [(-54 + 20 * cv - 20, b0 - 12)] + bez((-54 + 20 * cv - 20, b0 - 12), (-70, -40 - 72 * L), (-73, -40), 10)
    mmat = A if mg == "stanag20" else (P if mg != "sf60" else A)
    mgp = G.prof(mout, -11, 11, mmat, bevel=1.4, seg=2)
    if mg == "sf60": G.taper_y(mgp, -70, 1.0, -110, 1.7)
    else: G.cut(mgp, [G.C("box", (s * 11.6, -78 - k * 22 * L, -34 + k * 3), (2.4, 3.2, 52), mmat, bevel=0, rot=(-7 - k * 1.5, 0, 0)) for s in (-1, 1) for k in range(5 if L >= 1 else 3)])
    fb, bb = mout[11], mout[12]
    fpl = G.prof([(fb[0] + 2, fb[1] + 2), (bb[0] - 2, bb[1] + 2), (bb[0] - 3, bb[1] - 6, 3), (fb[0] + 3, fb[1] - 6, 3)], -13, 13, mmat, bevel=1.5, seg=2)
    if mg == "sf60": G.taper_y(fpl, -70, 1.0, -110, 1.7)
    G.as_mag(mgp, fpl)
    mag_bottom = [0, round((fb[1] + bb[1]) / 2000, 4), round((fb[0] + bb[0]) / 2000, 4)]
# ---- pistol grip
MARK("grip")
gk = v["grip"]
if gk == "a2":
    G.prof([(-108, -22), (-146, -22), (-150, -30, 4), (-176, -112, 6), (-166, -122, 6), (-140, -118, 4),
            (-131, -86, 8), (-124, -70, 6), (-122, -52, 8), (-110, -30, 3)], -12, 12, P, bevel=3.5, seg=3)
elif gk == "moe":
    # Magpul MOE+: a fuller, straight-backed grip, rubber over-moulding, no finger swell
    G.prof([(-108, -22), (-146, -22), (-152, -32, 4), (-178, -112, 8), (-168, -126, 6), (-138, -122, 6), (-124, -66, 10), (-112, -32, 3)], -13.5, 13.5, P, bevel=4, seg=3)
    G.prof([(-140, -50), (-160, -50), (-174, -106), (-146, -110)], -14.2, 14.2, R, bevel=1.5)
elif gk == "hogue":
    # Hogue OverMolded: rubber all over, three finger grooves
    G.prof([(-108, -22), (-146, -22), (-150, -30, 4), (-176, -112, 6), (-166, -124, 6), (-138, -120, 4), (-132, -104, 5), (-136, -96, 4), (-128, -86, 5), (-132, -76, 4), (-124, -64, 5), (-122, -50, 8), (-110, -30, 3)], -13, 13, R, bevel=3.5, seg=3)
else:
    # Magpul K2: more upright, for a squared-up stance
    G.prof([(-108, -22), (-146, -22), (-150, -30, 4), (-166, -118, 6), (-156, -128, 6), (-130, -124, 4), (-122, -70, 8), (-112, -32, 3)], -13, 13, P, bevel=3.8, seg=3)
# ---- stock
MARK("stock")
st = v["stock"]
if st == "ctr":
    sp = G.prof([(-262, 4, 6), (-262, -22, 8), (-300, -36, 16), (-402, -96, 4), (-418, -98), (-418, 26, 4), (-300, 22, 10)], -19, 19, P, bevel=4, seg=3)
    G.taper(sp, -262, 0.82, -418, 1.0)
    G.cut(sp, [G.C("cyl", (-30, -60, -372), (30, -60, -372), 5, P, seg=12), G.C("cyl", (0, -6, -250), (0, -6, -395), 15.2, P, seg=20)])
    G.prof([(-418, 26), (-430, 26, 3), (-430, -98, 3), (-418, -98)], -21, 21, R, bevel=2.5, seg=2)
    G.box((0, -30, -279), (14, 12, 22), P, bevel=2)
elif st == "moe":
    # Magpul MOE carbine stock: slimmer, an open slot under the tube
    sp = G.prof([(-270, 2, 6), (-270, -24, 8), (-410, -94, 4), (-420, -94), (-420, 22, 4), (-300, 20, 10)], -17, 17, P, bevel=3.5, seg=3)
    G.cut(sp, [G.C("prof", [(-300, -34), (-390, -80), (-390, -40), (-312, -24)], -30, 30, P, bevel=0), G.C("cyl", (0, -6, -260), (0, -6, -400), 15.2, P, seg=20)])
    G.prof([(-420, 22), (-430, 22, 3), (-430, -94, 3), (-420, -94)], -18, 18, R, bevel=2.2, seg=2)
elif st == "m4":
    # mil-spec M4 sliding stock: the ribbed fibreglass shell, the latch lever underneath
    sp = G.prof([(-275, 4, 4), (-275, -24, 4), (-410, -90, 3), (-418, -90), (-418, 20, 3), (-300, 20, 6)], -17, 17, P, bevel=2.5, seg=2)
    G.cut(sp, [G.C("box", (s * 17, -20 - k * 12, -350), (3, 3, 90), P, bevel=0, rot=(-25, 0, 0)) for s in (-1, 1) for k in range(3)] + [G.C("cyl", (0, -6, -270), (0, -6, -400), 15.2, P, seg=20)])
    G.box((0, -26, -286), (10, 8, 24), P, bevel=1.5)
    G.prof([(-418, 20), (-424, 20, 2), (-424, -90, 2), (-418, -90)], -18, 18, P, bevel=1.5, seg=2)
elif st == "prs":
    # Magpul PRS Gen3: a fixed precision stock, the adjustable cheek piece and butt with their knobs
    G.prof([(-230, 10, 6), (-230, -24, 6), (-300, -40, 12), (-430, -104, 4), (-438, -104), (-438, 18, 4), (-300, 22, 10)], -19, 19, P, bevel=3.5, seg=3)
    G.prof([(-300, 22), (-410, 22), (-410, 34, 4), (-300, 34, 4)], -15, 15, P, bevel=2.5, seg=2)         # cheek riser
    G.cyl((0, 6, -360), (0, -10, -360), 9, S, seg=16)                                                     # its knob
    G.cyl((22, -40, -400), (28, -40, -400), 10, P, seg=18)                                                # length-of-pull knob
    G.prof([(-438, 18), (-450, 18, 3), (-450, -106, 3), (-438, -106)], -21, 21, R, bevel=2.5, seg=2)
    G.box((0, -60, -336), (14, 50, 22), P, bevel=3)                                                       # monopod housing
# ("none": the bare buffer tube)
MARK("end")

stock_back = {"ctr": -430, "moe": -430, "m4": -424, "prs": -450, "none": -400}[st]
pts = {"Muzzle": [0, 0, round((zm + dl) / 1000, 4)], "Grip": [0, -0.068, -0.14], "Fore": [0, -0.02, round(fore / 1000, 4)], "Iron": [0, 0.0605, round(iron_z / 1000, 4)],
       "Scope": [0, 0.072, 0.03], "Eject": [0.016, 0.003, -0.066],
       "MagTop": [0, -0.04, -0.04], "MagBottom": mag_bottom, "Charge": [0, 0.015, -0.172]}
result = {"verts": G.stats()}
bpy.app.driver_namespace["lastgun"] = (G, pts)
