# Gun kit v2: everything in gunkit (box/prof/cyl/tube/ring/export), plus what real guns need -
# rounded silhouettes (fillets), cross-sections run along the bore (slides, receivers, handguards),
# soft multi-segment bevels (grips, stocks) and boolean cuts (serrations, ejection ports, M-LOK slots,
# lightening holes, bores). Sizes are given in MILLIMETRES, Unity axes: x right, y up, z forward (muzzle).
# Load after load_gunkit.py; builders do  G = bpy.app.driver_namespace["gunkit2"].Gun2(name, mats).
import bpy, bmesh, math, types
from mathutils import Vector, Matrix

src = r'''
import bpy, bmesh, math
from mathutils import Vector, Matrix
gk = bpy.app.driver_namespace["gunkit"]
MM = 0.001
def B(p): return Vector((p[0] * MM, p[2] * MM, p[1] * MM))

def fillet(pts, seg=4):
    """Corner points (a, b) or (a, b, r): every corner with r > 0 is rounded off with that radius."""
    out = []; n = len(pts)
    for i, p in enumerate(pts):
        r = p[2] if len(p) > 2 else 0
        b = Vector((p[0], p[1]))
        if r <= 0: out.append((b.x, b.y)); continue
        a = Vector(pts[i - 1][:2]); c = Vector(pts[(i + 1) % n][:2])
        d1 = (a - b); d2 = (c - b)
        if d1.length < 1e-9 or d2.length < 1e-9: out.append((b.x, b.y)); continue
        t = min(r, d1.length * 0.49, d2.length * 0.49)
        p1 = b + d1.normalized() * t; p2 = b + d2.normalized() * t
        for k in range(seg + 1):
            s = k / seg
            q = (1 - s) ** 2 * p1 + 2 * (1 - s) * s * b + s * s * p2
            out.append((q.x, q.y))
    return out

def arc(cx, cy, r, a0, a1, n):
    """Points round a circle (degrees), for curved outlines."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * k / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * k / n))) for k in range(n + 1)]

class Gun2(gk.Gun):
    def __init__(self, name, mats):
        super().__init__(name, mats)
        self.cutmode = False
        for o in list(bpy.data.objects):
            if o.name.startswith("CUT_"): bpy.data.objects.remove(o, do_unlink=True)

    def _obj(self, bm, mat, bevel, seg=1):
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=seg, affect='EDGES', clamp_overlap=True, profile=0.5)
        me = bpy.data.meshes.new(("CUT_" if self.cutmode else self.name) + "_p")
        bm.to_mesh(me); bm.free()
        # (material slots first: with none, Blender clamps every face's material index to 0)
        for n in self.mats: me.materials.append(bpy.data.materials.get("pv_" + n) or bpy.data.materials.new("pv_" + n))
        mi = self.mats.index(mat)
        for p in me.polygons: p.material_index = mi
        o = bpy.data.objects.new(me.name, me)
        o["mi"] = mi
        self.col.objects.link(o)
        if self.cutmode: o.hide_render = True; o.display_type = 'WIRE'
        else: self.parts.append(o)
        return o

    # ---- shapes (mm) ----
    def box(self, c, size, mat, bevel=0.6, rot=(0, 0, 0), seg=1):
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        S = Matrix.Diagonal((size[0] * MM, size[2] * MM, size[1] * MM, 1))
        R = Matrix.Rotation(math.radians(-rot[0]), 4, 'X') @ Matrix.Rotation(math.radians(-rot[1]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[2]), 4, 'Y')
        bmesh.ops.transform(bm, matrix=Matrix.Translation(B(c)) @ R @ S, verts=bm.verts)
        return self._obj(bm, mat, min(bevel * MM, min(size) * MM * 0.45), seg)

    def _extrude(self, vs3, offset, mat, bevel, seg):
        bm = bmesh.new()
        vs = [bm.verts.new(v) for v in vs3]
        f = bm.faces.new(vs)
        r = bmesh.ops.extrude_face_region(bm, geom=[f])
        nv = [e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, vec=offset, verts=nv)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self._obj(bm, mat, bevel, seg)

    def prof(self, pts, x0, x1, mat, bevel=0.6, seg=1, fseg=4):
        """Side silhouette in (z, y[, r]) extruded across from x0 to x1."""
        P = fillet(pts, fseg)
        return self._extrude([Vector((x0 * MM, z * MM, y * MM)) for (z, y) in P], Vector(((x1 - x0) * MM, 0, 0)), mat,
                             min(bevel, abs(x1 - x0) * 0.45) * MM, seg)

    def xsec(self, pts, z0, z1, mat, bevel=0.5, seg=1, fseg=3):
        """Cross-section in (x, y[, r]) run along the bore from z0 to z1."""
        P = fillet(pts, fseg)
        return self._extrude([Vector((x * MM, z0 * MM, y * MM)) for (x, y) in P], Vector((0, (z1 - z0) * MM, 0)), mat,
                             min(bevel, abs(z1 - z0) * 0.45) * MM, seg)

    def top(self, pts, y0, y1, mat, bevel=0.5, seg=1, fseg=3):
        """Plan outline in (x, z[, r]) extruded upward from y0 to y1."""
        P = fillet(pts, fseg)
        return self._extrude([Vector((x * MM, z * MM, y0 * MM)) for (x, z) in P], Vector((0, 0, (y1 - y0) * MM)), mat,
                             min(bevel, abs(y1 - y0) * 0.45) * MM, seg)

    def cyl(self, a, b, r, mat, seg=16, r2=None, bevel=0.0, bseg=1):
        A, Bp = B(a), B(b)
        d = Bp - A; L = d.length
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r * MM, radius2=(r if r2 is None else r2) * MM, depth=L)
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bmesh.ops.transform(bm, matrix=Matrix.Translation((A + Bp) / 2) @ q.to_matrix().to_4x4(), verts=bm.verts)
        return self._obj(bm, mat, bevel * MM, bseg)

    def tube(self, pts, r, mat, seg=10):
        for i in range(len(pts) - 1):
            self.cyl(pts[i], pts[i + 1], r, mat, seg)
            if i > 0:
                bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(4, seg // 2), radius=r * MM)
                bmesh.ops.translate(bm, vec=B(pts[i]), verts=bm.verts)
                self._obj(bm, mat, 0)

    def ball(self, c, r, mat, seg=12, scale=(1, 1, 1)):
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=1.0)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(B(c)) @ Matrix.Diagonal((r * scale[0] * MM, r * scale[2] * MM, r * scale[1] * MM, 1)), verts=bm.verts)
        return self._obj(bm, mat, 0)

    def ring(self, c, r, thick, mat, axis="z", seg=20):
        pts = []
        for k in range(seg + 1):
            a = k / seg * math.tau
            if axis == "z": pts.append((c[0] + math.cos(a) * r, c[1] + math.sin(a) * r, c[2]))
            else: pts.append((c[0] + math.cos(a) * r, c[1], c[2] + math.sin(a) * r))
        for i in range(seg): self.cyl(pts[i], pts[i + 1], thick, mat, 6)

    def screw(self, c, r, axis, mat, depth=0.6):
        """A screw head / pin end flush on a face: axis 'x' (side), 'y' (top) or 'z' (front)."""
        d = {"x": (depth, 0, 0), "y": (0, depth, 0), "z": (0, 0, depth)}[axis]
        self.cyl((c[0] - d[0], c[1] - d[1], c[2] - d[2]), (c[0] + d[0], c[1] + d[1], c[2] + d[2]), r, mat, 10)

    # ---- boolean cuts ----
    def cutter(self, fn, *a, **k):
        self.cutmode = True
        try: return fn(*a, **k)
        finally: self.cutmode = False

    def cut(self, target, cutters):
        if not cutters: return target
        for c in cutters:
            m = target.modifiers.new("b", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = c
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(target.evaluated_get(dg))
        target.modifiers.clear()
        old = target.data; target.data = me; bpy.data.meshes.remove(old)
        for p in me.polygons: p.material_index = target["mi"]
        for c in cutters:
            cm = c.data; bpy.data.objects.remove(c, do_unlink=True); bpy.data.meshes.remove(cm)
        return target

    def C(self, kind, *a, **k):
        """One cutter of the given shape (box, cyl, prof, xsec, top), same arguments as the shape."""
        return self.cutter(getattr(self, kind), *a, **k)

    def stats(self):
        return sum(len(o.data.vertices) for o in self.parts)
'''
mod = types.ModuleType("gunkit2")
exec(src, mod.__dict__)
bpy.app.driver_namespace["gunkit2"] = mod
result = {"ok": True}
