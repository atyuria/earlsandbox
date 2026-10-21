
import bpy, math
def physgun(gk, zy, OUT):
    # a handheld manipulator: pistol grip, armoured body, a glowing core and three claws round the emitter
    g = gk.Gun("Gun_PhysGun", ["Anod", "Tan", "Lens", "Polymer", "Steel"])
    g.prof([(-0.11, 0.0), (-0.11, 0.045), (-0.095, 0.06), (0.12, 0.06), (0.14, 0.04), (0.14, -0.01), (0.06, -0.02), (-0.03, -0.02), (-0.06, 0.0)], -0.03, 0.03, "Anod", 0.004)
    for s in (-1, 1):
        x0, x1 = (-0.034, -0.03) if s < 0 else (0.03, 0.034)
        g.prof([(-0.08, 0.008), (-0.08, 0.05), (0.1, 0.05), (0.12, 0.03), (0.12, 0.008)], x0, x1, "Tan", 0.002)       # side armour plates
        for k in range(5): g.box((s * 0.0345, 0.03, -0.05 + k * 0.03), (0.002, 0.03, 0.006), "Polymer", 0.0006)  # vents
    g.box((0, 0.068, 0.0), (0.02, 0.012, 0.16), "Anod", 0.002)                              # top spine
    g.cyl((0, 0.025, 0.02), (0, 0.025, 0.13), 0.016, "Lens", 16)                             # core (seen through the vents)
    g.cyl((0, 0.02, 0.14), (0, 0.02, 0.17), 0.026, "Anod", 20)                               # emitter collar
    g.cyl((0, 0.02, 0.17), (0, 0.02, 0.172), 0.018, "Lens", 20)                              # emitter lens
    for k in range(3):                                                                       # claws
        a = math.radians(90 + k * 120)
        x, y = math.cos(a) * 0.03, 0.02 + math.sin(a) * 0.03
        g.cyl((x, y, 0.15), (x * 1.5, 0.02 + (y - 0.02) * 1.5, 0.215), 0.0045, "Steel", 8)
        g.cyl((x * 1.5, 0.02 + (y - 0.02) * 1.5, 0.215), (x * 1.1, 0.02 + (y - 0.02) * 1.1, 0.245), 0.0038, "Steel", 8)
    g.prof([(-0.05, -0.015), (-0.072, -0.02), (-0.09, -0.115), (-0.08, -0.123), (-0.05, -0.123), (-0.045, -0.105), (-0.035, -0.02)], -0.0155, 0.0155, "Polymer", 0.003)   # grip
    g.tube(zy([(0.0, -0.02), (0.002, -0.05), (-0.01, -0.056), (-0.04, -0.052)]), 0.0032, "Anod", 8)     # guard
    g.box((0, -0.034, -0.012), (0.006, 0.02, 0.007), "Steel", 0.0008, (10, 0, 0))            # trigger
    g.tube(zy([(-0.09, -0.11), (-0.12, -0.09), (-0.13, -0.03), (-0.11, 0.02)]), 0.0045, "Polymer", 8)   # power cable to the back
    g.box((0, 0.025, -0.115), (0.03, 0.04, 0.012), "Tan", 0.002)                             # battery cap
    return g.export(OUT + "Gun_PhysGun.json", {"Muzzle": (0, 0.02, 0.245), "Grip": (0, -0.065, -0.062), "Fore": (0, -0.02, 0.08)})
