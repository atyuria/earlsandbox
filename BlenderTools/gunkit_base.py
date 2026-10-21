
import bpy, bmesh, json, math, types
from mathutils import Vector, Matrix

src = r'''
import bpy, bmesh, json, math
from mathutils import Vector, Matrix

# All sizes in metres, given in UNITY axes: x right, y up, z forward (muzzle).
# In Blender that is (x, z, y): X right, Y forward, Z up.
def B(p): return Vector((p[0], p[2], p[1]))

class Gun:
    def __init__(self, name, mats):
        self.name = name; self.mats = list(mats); self.parts = []
        col = bpy.data.collections.get("GunWork") or bpy.data.collections.new("GunWork")
        if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
        self.col = col
        for o in list(col.objects):
            if o.name.startswith(name + "_"): bpy.data.objects.remove(o, do_unlink=True)

    def _obj(self, bm, mat, bevel):
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
        me = bpy.data.meshes.new(self.name + "_p")
        bm.to_mesh(me); bm.free()
        for p in me.polygons: p.material_index = self.mats.index(mat)
        o = bpy.data.objects.new(self.name + "_p", me)
        self.col.objects.link(o)
        self.parts.append(o)
        return o

    def box(self, c, size, mat, bevel=0.0015, rot=(0, 0, 0)):
        """Box centred at c (unity), size (unity x,y,z), rot = unity euler degrees (x pitch, y yaw, z roll)."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        S = Matrix.Diagonal((size[0], size[2], size[1], 1))
        R = Matrix.Rotation(math.radians(-rot[0]), 4, 'X') @ Matrix.Rotation(math.radians(-rot[1]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[2]), 4, 'Y')
        bmesh.ops.transform(bm, matrix=Matrix.Translation(B(c)) @ R @ S, verts=bm.verts)
        return self._obj(bm, mat, min(bevel, min(size) * 0.45))

    def prof(self, pts, x0, x1, mat, bevel=0.0015):
        """Side silhouette: unity (z, y) points, counter-clockwise or not, extruded from x=x0 to x=x1."""
        bm = bmesh.new()
        vs = [bm.verts.new(Vector((x0, z, y))) for (z, y) in pts]
        f = bm.faces.new(vs)
        r = bmesh.ops.extrude_face_region(bm, geom=[f])
        nv = [e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, vec=Vector((x1 - x0, 0, 0)), verts=nv)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self._obj(bm, mat, min(bevel, abs(x1 - x0) * 0.4))

    def cyl(self, a, b, r, mat, seg=16, r2=None, bevel=0.0):
        """Cylinder from unity point a to b, radius r (r2 at b end)."""
        A, Bp = B(a), B(b)
        d = Bp - A; L = d.length
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=(r if r2 is None else r2), depth=L)
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bmesh.ops.transform(bm, matrix=Matrix.Translation((A + Bp) / 2) @ q.to_matrix().to_4x4(), verts=bm.verts)
        return self._obj(bm, mat, bevel)

    def tube(self, pts, r, mat, seg=10):
        """Round bar along unity points (curved handles, trigger guards)."""
        for i in range(len(pts) - 1):
            self.cyl(pts[i], pts[i + 1], r, mat, seg)
            if i > 0:
                bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(4, seg // 2), radius=r)
                bmesh.ops.translate(bm, vec=B(pts[i]), verts=bm.verts)
                self._obj(bm, mat, 0)

    def ring(self, c, axis, r, thick, mat, seg=20):
        """Torus-ish ring (sight hoods, ring mounts): axis 'z' = facing forward."""
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=False, segments=seg, radius=r)
        o = self._obj(bm, mat, 0)
        return o

    def export(self, path, points):
        bpy.ops.object.select_all(action='DESELECT')
        for o in self.parts: o.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        o.name = self.name
        me = o.data
        bm = bmesh.new(); bm.from_mesh(me)
        # hard edges: split where faces meet at more than ~35 degrees so Unity's smooth normals stay crisp
        sharp = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(35)]
        sharp += [e for e in bm.edges if len(e.link_faces) != 2]
        bmesh.ops.split_edges(bm, edges=list(set(sharp)))
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bm.verts.ensure_lookup_table()
        verts = []
        for v in bm.verts:
            verts += [round(v.co.x, 5), round(v.co.z, 5), round(v.co.y, 5)]
        subs = {m: [] for m in self.mats}
        for f in bm.faces:
            i = [v.index for v in f.verts]
            # unity is (x, z, y) of blender: a mirror, so the winding flips
            subs[self.mats[f.material_index]] += [i[0], i[2], i[1]]
        bm.free()
        data = {"name": self.name, "verts": verts, "subs": [{"mat": m, "tris": t} for m, t in subs.items() if t],
                "points": {k: [round(x, 4) for x in v] for k, v in points.items()}}
        with open(path, "w") as fh: fh.write(json.dumps(data, separators=(",", ":")))
        return {"verts": len(verts) // 3, "tris": sum(len(t) for t in subs.values()) // 3}
'''
mod = types.ModuleType("gunkit")
exec(src, mod.__dict__)
bpy.app.driver_namespace["gunkit"] = mod
txt = bpy.data.texts.get("gunkit.py") or bpy.data.texts.new("gunkit.py")
txt.clear(); txt.write(src)
result = {"ok": True}
