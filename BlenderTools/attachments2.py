
import bpy, math
def attachments(gk, zy, OUT):
    out = {}
    # Surefire X300 Ultra: origin = top of the rail it clamps to; hangs below (y negative), lens forward
    g = gk.Gun("Att_Light", ["Anod", "Lens", "Polymer", "Steel"])
    g.box((0, -0.006, -0.008), (0.026, 0.012, 0.034), "Anod", 0.002)                    # rail clamp
    g.box((0, -0.0005, -0.008), (0.016, 0.003, 0.03), "Anod", 0.0006)                    # rail lugs
    g.cyl((0.0145, -0.008, -0.012), (0.0175, -0.008, -0.012), 0.0035, "Steel", 10)       # clamp screw
    g.box((0, -0.022, 0.0), (0.03, 0.026, 0.058), "Anod", 0.006)                         # body
    g.cyl((0, -0.022, 0.026), (0, -0.022, 0.044), 0.0165, "Anod", 20)                    # bezel
    for k in range(3): g.cyl((0, -0.022, 0.03 + k * 0.004), (0, -0.022, 0.0315 + k * 0.004), 0.0172, "Anod", 20)
    g.cyl((0, -0.022, 0.044), (0, -0.022, 0.0445), 0.0138, "Lens", 20)
    for s in (-1, 1): g.box((s * 0.0125, -0.022, -0.033), (0.006, 0.02, 0.012), "Polymer", 0.0015)   # paddle switches
    out["light"] = g.export(OUT + "Att_Light.json", {"Lens": (0, -0.022, 0.045)})
    # Pistol suppressor (Omega 9K-ish): origin = rear face on the thread
    g = gk.Gun("Att_SuppPistol", ["Anod", "Polymer", "Steel"])
    g.cyl((0, 0, 0), (0, 0, 0.022), 0.0145, "Anod", 20)
    for k in range(5): g.cyl((0, 0, 0.003 + k * 0.004), (0, 0, 0.0045 + k * 0.004), 0.0152, "Anod", 20)   # knurled rear cap
    g.cyl((0, 0, 0.022), (0, 0, 0.165), 0.0175, "Anod", 24)
    g.cyl((0, 0, 0.165), (0, 0, 0.178), 0.0165, "Anod", 24, 0.014)
    g.cyl((0, 0, 0.1775), (0, 0, 0.1785), 0.0055, "Polymer", 12)
    out["sp"] = g.export(OUT + "Att_SuppPistol.json", {"Front": (0, 0, 0.178)})
    # Rifle suppressor on a QD mount
    g = gk.Gun("Att_SuppRifle", ["Anod", "Polymer", "Steel"])
    g.cyl((0, 0, 0), (0, 0, 0.032), 0.0165, "Steel", 20)
    for k in range(6): g.box((0, 0, 0.016), (0.035, 0.003, 0.03), "Steel", 0.0006, (0, 0, k * 30))   # mount wrench flats
    g.cyl((0, 0, 0.032), (0, 0, 0.185), 0.0215, "Anod", 26)
    g.cyl((0, 0, 0.185), (0, 0, 0.196), 0.0205, "Anod", 26, 0.017)
    g.cyl((0, 0, 0.1955), (0, 0, 0.1965), 0.006, "Polymer", 12)
    out["sr"] = g.export(OUT + "Att_SuppRifle.json", {"Front": (0, 0, 0.196)})
    # EOTech G33 magnifier on a flip-to-side mount; optical axis at y 0.032 (the holo's)
    g = gk.Gun("Att_Magnifier", ["Anod", "Glass", "Polymer", "Steel"])
    g.box((0, 0.004, 0.0), (0.026, 0.008, 0.04), "Anod", 0.0015)                         # base clamp
    g.box((-0.012, 0.012, 0.0), (0.008, 0.014, 0.03), "Anod", 0.0015)                    # flip pivot
    g.box((0, 0.014, 0.0), (0.03, 0.006, 0.032), "Anod", 0.0015)
    g.cyl((0, 0.032, -0.05), (0, 0.032, 0.05), 0.0178, "Anod", 22)
    g.cyl((0, 0.032, -0.05), (0, 0.032, -0.066), 0.0195, "Rubber" if False else "Polymer", 22)   # eyepiece
    g.cyl((0, 0.032, 0.05), (0, 0.032, 0.058), 0.0192, "Anod", 22)
    g.cyl((0, 0.032, -0.0655), (0, 0.032, -0.065), 0.015, "Glass", 20)
    g.cyl((0, 0.032, 0.0575), (0, 0.032, 0.058), 0.016, "Glass", 20)
    g.cyl((0.0178, 0.036, -0.01), (0.022, 0.036, -0.01), 0.0045, "Steel", 10)             # diopter knob
    out["mag"] = g.export(OUT + "Att_Magnifier.json", {"Eye": (0, 0.032, 0.0)})
    # PEQ-15 style laser box: origin = top of the rail, sits on it
    g = gk.Gun("Att_Laser", ["Tan", "Lens", "Polymer", "Steel"])
    g.box((0, 0.004, 0.0), (0.022, 0.008, 0.05), "Tan", 0.0015)
    g.box((0, 0.024, -0.004), (0.036, 0.032, 0.088), "Tan", 0.004)
    g.box((0, 0.024, 0.039), (0.032, 0.028, 0.004), "Polymer", 0.0008)
    for (x, y, r) in [(-0.009, 0.03, 0.0045), (0.005, 0.03, 0.0035), (0.005, 0.017, 0.0035), (-0.009, 0.017, 0.003)]:
        g.cyl((x, y, 0.041), (x, y, 0.042), r, "Lens", 12)
    g.box((0.0185, 0.03, -0.02), (0.003, 0.01, 0.02), "Polymer", 0.0008)                 # mode selector
    g.cyl((0, 0.041, -0.03), (0, 0.044, -0.03), 0.004, "Polymer", 10)                    # fire button
    g.cyl((0.0185, 0.018, 0.02), (0.022, 0.018, 0.02), 0.003, "Steel", 8)                 # adjusters
    g.cyl((0, 0.0405, 0.02), (0, 0.0435, 0.02), 0.003, "Steel", 8)
    out["laser"] = g.export(OUT + "Att_Laser.json", {"Beam": (-0.009, 0.03, 0.042)})
    return out
