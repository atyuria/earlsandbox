
import bpy, math
def mossberg(gk, zy, OUT):
    # Mossberg 590A1, 20" heavy-wall barrel, full-length tube, ghost ring sights, synthetic furniture (1.03 m)
    g = gk.Gun("Gun_Shotgun", ["Anod", "Metal", "Polymer", "Rubber", "Steel"])
    g.prof([(-0.115, -0.03), (-0.115, 0.012), (-0.105, 0.024), (0.09, 0.024), (0.1, 0.016), (0.1, -0.03)], -0.0185, 0.0185, "Anod", 0.002)
    g.box((0.0187, 0.004, 0.022), (0.0012, 0.022, 0.062), "Polymer", 0.0004)             # ejection port
    g.box((0.0192, -0.007, 0.022), (0.0016, 0.004, 0.06), "Steel", 0.0004)                # bolt face edge in the port
    g.box((0, -0.0305, 0.03), (0.026, 0.0015, 0.09), "Polymer", 0.0004)                   # loading port
    g.box((0, -0.03, 0.03), (0.0035, 0.0012, 0.07), "Steel", 0.0003)                      # shell elevator
    g.box((0, 0.026, -0.094), (0.011, 0.006, 0.017), "Polymer", 0.0015)                   # tang safety
    for k in range(4): g.box((0, 0.0295, -0.1 + k * 0.004), (0.0115, 0.0012, 0.0016), "Polymer", 0.0002)
    # ghost ring rear sight, protected by ears
    g.box((0, 0.029, -0.07), (0.022, 0.01, 0.04), "Anod", 0.0015)
    for s in (-1, 1): g.box((s * 0.0085, 0.042, -0.068), (0.004, 0.018, 0.022), "Anod", 0.001)
    g.ring((0, 0.044, -0.07), 0.0042, 0.0016, "Steel", "z", 12)
    g.box((0, 0.037, -0.07), (0.013, 0.004, 0.006), "Steel", 0.0005)
    g.cyl((0.0125, 0.034, -0.07), (0.0165, 0.034, -0.07), 0.0025, "Steel", 8)            # windage screw
    # heavy barrel, front ramp + blade (top at the ghost ring's centre: y 0.044)
    g.cyl((0, 0.0, 0.1), (0, 0.0, 0.615), 0.0108, "Metal", 22)
    g.cyl((0, 0.0, 0.6135), (0, 0.0, 0.6155), 0.009, "Polymer", 16)
    g.box((0, 0.017, 0.592), (0.012, 0.014, 0.034), "Metal", 0.002)
    g.box((0, 0.034, 0.594), (0.0035, 0.02, 0.012), "Steel", 0.0006)
    g.box((0, 0.0402, 0.594), (0.0036, 0.0016, 0.012), "Brass" if False else "Steel", 0.0003)
    # magazine tube to the muzzle, barrel clamp, bayonet lug end cap with sling swivel
    g.cyl((0, -0.029, 0.1), (0, -0.029, 0.6), 0.0125, "Metal", 18)
    g.box((0, -0.015, 0.565), (0.023, 0.05, 0.02), "Metal", 0.002)
    g.cyl((0, -0.029, 0.6), (0, -0.029, 0.625), 0.0138, "Metal", 16)
    g.box((0, -0.045, 0.607), (0.008, 0.012, 0.03), "Metal", 0.001)                      # bayonet lug
    g.ring((0, -0.055, 0.6), 0.008, 0.0016, "Steel", "z", 12)                             # sling swivel
    # forend: ribbed synthetic, riding the action bars
    g.box((0, -0.026, 0.3), (0.045, 0.046, 0.2), "Polymer", 0.012)
    for k in range(11): g.box((0, -0.026, 0.215 + k * 0.017), (0.0465, 0.0475, 0.006), "Rubber", 0.012)
    for s in (-1, 1): g.box((s * 0.0168, -0.022, 0.15), (0.0022, 0.006, 0.1), "Steel", 0.0006)
    # trigger group, guard, bolt release
    g.prof([(-0.08, -0.03), (-0.08, -0.04), (0.005, -0.04), (0.005, -0.03)], -0.012, 0.012, "Polymer", 0.0015)
    g.tube(zy([(0.003, -0.04), (0.0, -0.06), (-0.012, -0.064), (-0.06, -0.062), (-0.075, -0.045)]), 0.0032, "Polymer", 8)
    g.tube(zy([(-0.028, -0.04), (-0.024, -0.05), (-0.029, -0.058)]), 0.0026, "Steel", 8)
    g.box((-0.0125, -0.046, -0.073), (0.003, 0.006, 0.008), "Steel", 0.0008)
    # stock: wrist, comb dropping to the heel, ribbed recoil pad, sling stud
    g.prof([(-0.115, 0.016), (-0.2, 0.011), (-0.43, -0.012), (-0.433, -0.148), (-0.39, -0.143), (-0.24, -0.09), (-0.17, -0.064), (-0.115, -0.032)], -0.0195, 0.0195, "Polymer", 0.004)
    g.box((0, -0.08, -0.443), (0.042, 0.142, 0.02), "Rubber", 0.004)
    for k in range(7): g.box((0, -0.02 - k * 0.02, -0.4535), (0.043, 0.004, 0.003), "Rubber", 0.0008)
    g.cyl((0, -0.13, -0.35), (0, -0.136, -0.35), 0.004, "Steel", 10)
    return g.export(OUT + "Gun_Shotgun.json", {"Muzzle": (0, 0.0, 0.6155), "Grip": (0, -0.042, -0.15), "Fore": (0, -0.032, 0.29), "Iron": (0, 0.044, 0.594)})

def taser(gk, zy, OUT):
    # Taser X26P: yellow body, black cartridge with two probe bays, laser/LED, rear info display, battery pack
    g = gk.Gun("Gun_Taser", ["Yellow", "Polymer", "Steel", "Lens", "Rubber"])
    g.prof([(-0.066, 0.0), (-0.066, 0.027), (-0.052, 0.034), (0.046, 0.034), (0.05, 0.029), (0.05, -0.01), (0.04, -0.015), (-0.02, -0.015), (-0.04, -0.01)], -0.0158, 0.0158, "Yellow", 0.003)
    for s in (-1, 1): g.box((s * 0.016, 0.012, 0.0), (0.0012, 0.016, 0.07), "Polymer", 0.0006)   # black side inserts
    g.box((0, 0.012, 0.0665), (0.0335, 0.046, 0.034), "Polymer", 0.003)                 # cartridge
    for s in (-1, 1): g.box((s * 0.0168, 0.012, 0.066), (0.0012, 0.03, 0.02), "Yellow", 0.0004)   # cartridge release tabs
    g.box((0, 0.012, 0.0838), (0.028, 0.04, 0.0015), "Rubber", 0.0004)                    # blast doors
    for y in (0.025, -0.001):
        g.cyl((0, y, 0.083), (0, y, 0.0848), 0.0058, "Steel", 14)                          # probe bays
        g.cyl((0, y, 0.0846), (0, y, 0.0852), 0.0022, "Polymer", 10)
    g.box((0, -0.0225, 0.035), (0.022, 0.014, 0.024), "Polymer", 0.002)                  # laser / LED module
    g.cyl((-0.005, -0.0225, 0.047), (-0.005, -0.0225, 0.0478), 0.0035, "Lens", 10)
    g.cyl((0.005, -0.0225, 0.047), (0.005, -0.0225, 0.0478), 0.0022, "Lens", 8)
    g.prof([(-0.06, 0.0), (-0.068, -0.078), (-0.022, -0.078), (-0.02, -0.015), (-0.04, -0.01)], -0.0152, 0.0152, "Yellow", 0.003)
    for s in (-1, 1):
        x0, x1 = (-0.0162, -0.015) if s < 0 else (0.015, 0.0162)
        g.prof([(-0.058, -0.012), (-0.064, -0.07), (-0.03, -0.07), (-0.028, -0.018)], x0, x1, "Rubber", 0.0005)   # grip panels
    g.box((0, -0.085, -0.046), (0.033, 0.015, 0.05), "Polymer", 0.002)                  # power magazine
    g.box((0, -0.0925, -0.046), (0.03, 0.002, 0.044), "Rubber", 0.0005)
    g.tube(zy([(0.014, -0.015), (0.016, -0.038), (0.004, -0.044), (-0.018, -0.042), (-0.022, -0.03)]), 0.0034, "Yellow", 8)
    g.box((0, -0.026, -0.002), (0.006, 0.02, 0.006), "Polymer", 0.0008, (10, 0, 0))      # trigger
    for s in (-1, 1): g.box((s * 0.0168, 0.02, -0.044), (0.003, 0.008, 0.013), "Polymer", 0.0008)   # safety switch
    g.box((0, 0.016, -0.0665), (0.022, 0.014, 0.0018), "Lens", 0.0004)                   # central info display
    g.box((0, 0.0365, 0.04), (0.003, 0.005, 0.008), "Polymer", 0.0005)                   # front sight
    for s in (-1, 1): g.box((s * 0.004, 0.0365, -0.054), (0.0035, 0.005, 0.007), "Polymer", 0.0005)
    return g.export(OUT + "Gun_Taser.json", {"Muzzle": (0, 0.012, 0.085), "Grip": (0, -0.044, -0.043), "Fore": (-0.006, -0.06, -0.046), "Iron": (0, 0.039, 0.04), "Probe1": (0, 0.025, 0.085), "Probe2": (0, -0.001, 0.085)})
