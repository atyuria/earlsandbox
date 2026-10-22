# Builds every gun and writes it to the game's Resources/Models as <name>.json (verts, material subsets, points).
import bpy
D = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/BlenderTools/"
OUT = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/Assets/CarlSandbox/Resources/Models/"
res = {}
for f in ("gun_glock.py", "gun_beretta.py", "gun_ar15.py", "gun_ak47.py", "gun_awp.py", "gun_barrett.py", "gun_mossberg.py", "gun_taser.py"):
    exec(open(D + f, encoding="utf-8").read(), {})
    G, pts = bpy.app.driver_namespace["lastgun"]
    res[G.name] = G.export(OUT + G.name + ".json", pts)
result = res
