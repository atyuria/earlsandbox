# Run inside Blender (MCP execute or the Text editor): loads the gun-building toolkit into
# bpy.app.driver_namespace["gunkit"] / ["gunshot"]. Units are metres in Unity axes (x right, y up, z forward).
import os
D = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/BlenderTools"
for f in ("gunkit_base.py", "gunkit_patch.py", "gunshot.py"):
    exec(open(os.path.join(D, f), encoding="utf-8").read(), {"__name__": "gk_" + f})
