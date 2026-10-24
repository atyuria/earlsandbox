import bpy, os
bpy.app.driver_namespace["ak"] = "AKS74U"
exec(open(os.path.join(bpy.app.driver_namespace["gun_dir"], "gun_akfamily.py"), encoding="utf-8").read(), {})
