# Preview renders of a gun being built: its parts coloured like the game's materials, from the side (both),
# above and a three-quarter view, into one sheet. bpy.app.driver_namespace["gunshot2"].sheet(prefix, path)
import bpy, types
src = r'''
import bpy, math, os
import numpy as np
from mathutils import Vector
COL = {"Polymer": (0.07, 0.07, 0.075), "Metal": (0.2, 0.21, 0.22), "Steel": (0.62, 0.63, 0.66), "Anod": (0.13, 0.13, 0.14),
       "Wood": (0.42, 0.2, 0.09), "Olive": (0.25, 0.28, 0.18), "Glass": (0.15, 0.25, 0.3), "Rubber": (0.03, 0.03, 0.03),
       "Brass": (0.75, 0.55, 0.2), "Lens": (0.2, 0.3, 0.4), "Tan": (0.55, 0.47, 0.33), "Yellow": (0.9, 0.75, 0.05)}
def mat(name):
    m = bpy.data.materials.get("pv_" + name) or bpy.data.materials.new("pv_" + name)
    c = COL.get(name, (0.3, 0.3, 0.3)); m.diffuse_color = (c[0], c[1], c[2], 1)
    return m
def sheet(gun, path, res=520):
    sc = bpy.context.scene
    objs = list(gun.parts)
    for n in gun.mats: mat(n)      # (the parts already carry these slots: clearing them would reset every face to slot 0)
    keep = set(objs)
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = o not in keep
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    ctr = (mn + mx) / 2; size = mx - mn
    cam = bpy.data.objects.get("GunCam") or bpy.data.objects.new("GunCam", bpy.data.cameras.new("GunCam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.show_shadows = False
    sh.background_type = "VIEWPORT"; sh.background_color = (0.55, 0.57, 0.6)
    pass
    sc.render.film_transparent = False
    views = [((1, 0, 0), 1.0), ((-1, 0, 0), 1.0), ((0.0001, 0, 1), 1.0), ((0.9, -0.8, 0.55), 1.0)]
    W, H = res * 2, int(res * 0.62)
    sc.render.resolution_x = W; sc.render.resolution_y = H; sc.render.resolution_percentage = 100
    tiles = []
    tmp = os.path.join(os.path.dirname(path), "_pv.png")
    for d, z in views:
        d = Vector(d).normalized()
        cam.location = ctr + d * size.length * 3
        up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y' if up.z == 1 else 'Y').to_euler()
        if abs(d.z) > 0.9:
            cam.rotation_euler = (0, 0, math.radians(90))
        persp = not (abs(d.x) > 0.99 or abs(d.z) > 0.9)
        cam.data.type = "PERSP" if persp else "ORTHO"; cam.data.lens = 50; cam.data.clip_start = 0.005
        if persp: cam.location = ctr + d * size.length * 1.9
        cam.data.ortho_scale = (max(size.y, size.x, size.z * W / H) * 1.06) if abs(d.x) > 0.99 or abs(d.z) > 0.9 else size.length * 1.15
        sc.render.filepath = tmp; sc.render.image_settings.file_format = 'PNG'
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(tmp, check_existing=False)
        a = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4); bpy.data.images.remove(im)
        tiles.append(a)
    out = np.zeros((H * 2, W * 2, 4), dtype=np.float32)
    for i, a in enumerate(tiles):
        r, c = 1 - i // 2, i % 2
        out[r * H:(r + 1) * H, c * W:(c + 1) * W] = a
    o = bpy.data.images.new("pvsheet", W * 2, H * 2)
    o.pixels.foreach_set(out.ravel()); o.filepath_raw = path; o.file_format = 'JPEG'; o.save(); bpy.data.images.remove(o)
'''
mod = types.ModuleType("gunshot2"); exec(src, mod.__dict__)
bpy.app.driver_namespace["gunshot2"] = mod
result = {"ok": True}
