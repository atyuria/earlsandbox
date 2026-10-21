
import bpy, types
txt = bpy.data.texts["gunkit.py"]
src = txt.as_string()
start = src.index("    def ring(")
src = src[:start] + r'''    def ring(self, c, r, thick, mat, axis="z", seg=20):
        """A round ring of bar, facing along unity 'z' (forward) or 'y' (up)."""
        pts = []
        for k in range(seg + 1):
            a = k / seg * math.tau
            if axis == "z": pts.append((c[0] + math.cos(a) * r, c[1] + math.sin(a) * r, c[2]))
            else: pts.append((c[0] + math.cos(a) * r, c[1], c[2] + math.sin(a) * r))
        for i in range(seg): self.cyl(pts[i], pts[i + 1], thick, mat, 6)

    def export(self, path, points):
        bm = bmesh.new()
        for o in self.parts: bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00002)
        # hard edges: split where faces meet at more than ~35 degrees so Unity's smooth normals stay crisp
        sharp = [e for e in bm.edges if len(e.link_faces) != 2 or e.calc_face_angle(0) > math.radians(35)]
        bmesh.ops.split_edges(bm, edges=sharp)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bm.verts.ensure_lookup_table(); bm.verts.index_update()
        verts = []
        for v in bm.verts: verts += [round(v.co.x, 5), round(v.co.z, 5), round(v.co.y, 5)]
        subs = {m: [] for m in self.mats}
        for f in bm.faces:
            i = [v.index for v in f.verts]
            # unity is (x, z, y) of blender: a mirror, so the winding flips
            subs[self.mats[f.material_index]] += [i[0], i[2], i[1]]
        bm.free()
        data = {"name": self.name, "verts": verts, "subs": [{"mat": m, "tris": t} for m, t in subs.items() if t],
                "points": {k: [round(x, 4) for x in v] for k, v in points.items()}}
        with open(path, "w") as fh: fh.write(json.dumps(data, separators=(",", ":")))
        return {"verts": len(verts) // 3, "tris": sum(len(t) for t in subs.values()) // 3, "subs": [m for m, t in subs.items() if t]}
'''
txt.clear(); txt.write(src)
mod = types.ModuleType("gunkit"); exec(src, mod.__dict__)
bpy.app.driver_namespace["gunkit"] = mod
result = {"ok": True}
