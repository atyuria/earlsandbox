
import bpy, types
src = r'''
import bpy, math
from mathutils import Vector
def shoot(prefix, path, side=True):
    sc = bpy.context.scene
    objs = [o for o in bpy.data.collections["GunWork"].objects if o.name.startswith(prefix)]
    for o in bpy.data.objects: o.hide_render = o not in objs and o.type == 'MESH'
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    ctr = (mn + mx) / 2; size = mx - mn
    cam = bpy.data.objects.get("GunCam")
    if cam is None:
        cam = bpy.data.objects.new("GunCam", bpy.data.cameras.new("GunCam")); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL' if False else 'OBJECT'; sh.show_cavity = True; sh.show_shadows = False
    for o in objs: o.color = (0.35, 0.36, 0.38, 1)
    sc.render.resolution_x = 1000; sc.render.resolution_y = 560
    if side:
        cam.location = ctr + Vector((size.length * 2, 0, 0)); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
        cam.data.ortho_scale = max(size.y, size.z * 1.8) * 1.1
    else:
        d = Vector((1.2, -1.0, 0.7)).normalized()
        cam.location = ctr + d * size.length * 2
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        cam.data.ortho_scale = size.length * 0.95
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
'''
mod = types.ModuleType("gunshot"); exec(src, mod.__dict__)
bpy.app.driver_namespace["gunshot"] = mod
P = r"C:/Users/Binky/AppData/Local/Temp/claude/C--Users-Binky-OneDrive-Documents-GitHub-earlsandbox/216b7c61-9b36-44f2-a606-1ead5f3a19bb/scratchpad/"
result = {"ok": True}
