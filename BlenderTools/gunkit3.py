# Gun kit v3: on top of gunkit2 (Gun2: box/prof/xsec/top/cyl/tube/ball/screw + boolean cuts), what the realistic
# remodels need:
#   bez(p0, c, p2, n)         - points along a quadratic curve (curved magazines, stock lines, grips)
#   taper(obj, z0, w0, z1, w1) - squeeze a part's width (x) along the bore: stocks thin to the wrist, grips to the
#                               toe, handguards to the front
#   lathe(prof, mat)          - a turned part: (z, r) radius profile spun round the bore (barrels, brakes, flash hiders)
# Sizes in MILLIMETRES, Unity axes (x right, y up, z forward). Loaded after gunkit_base.py and gunkit2.py.
import bpy, bmesh, math, types
from mathutils import Vector, Matrix

src = r'''
import bpy, bmesh, math
from mathutils import Vector, Matrix
g2 = bpy.app.driver_namespace["gunkit2"]
MM = 0.001
def B(p): return Vector((p[0] * MM, p[2] * MM, p[1] * MM))

def bez(p0, c, p2, n=8):
    """Points from p0 to p2 bending towards c (2D tuples), p0 left out (so lists join end to end)."""
    out = []
    for k in range(1, n + 1):
        t = k / n
        out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p2[1]))
    return out

class Gun3(g2.Gun2):
    def __init__(self, name, mats):
        super().__init__(name, mats)
        self.magparts = []
        for o in list(bpy.data.objects):
            if o.name.startswith(name + "_Mag"): bpy.data.objects.remove(o, do_unlink=True)

    def as_mag(self, *objs):
        """These parts are the magazine (or belt box): exported on their own, so it can come out of the gun."""
        for o in objs:
            if o in self.parts: self.parts.remove(o)
            self.magparts.append(o)

    def taper(self, obj, z0, w0, z1, w1, y0=None, y1=None, yk=1.0):
        """Scale the part's x by w0 at z0 to w1 at z1 (linear, clamped); optionally only between heights y0..y1."""
        me = obj.data
        for v in me.vertices:
            z = v.co.y / MM; y = v.co.z / MM
            t = min(1.0, max(0.0, (z - z0) / (z1 - z0))) if z1 != z0 else 0.0
            k = w0 + (w1 - w0) * t
            if y0 is not None and not (y0 <= y <= y1): k = 1.0 + (k - 1.0) * yk
            v.co.x *= k
        me.update()
        return obj

    def taper_y(self, obj, y0, w0, y1, w1):
        """Scale x by height instead (a grip thinning toward its toe, a magazine's bottom)."""
        me = obj.data
        for v in me.vertices:
            y = v.co.z / MM
            t = min(1.0, max(0.0, (y - y0) / (y1 - y0))) if y1 != y0 else 0.0
            v.co.x *= w0 + (w1 - w0) * t
        me.update()
        return obj

    def lathe(self, prof, mat, seg=20, y=0.0, x=0.0, cap=True):
        """Turned part: prof = [(z, r), ...] front to back or back to front, spun round the line (x, y)."""
        bm = bmesh.new()
        rings = []
        for (z, r) in prof:
            ring = []
            for k in range(seg):
                a = k / seg * math.tau
                ring.append(bm.verts.new(B((x + math.cos(a) * r, y + math.sin(a) * r, z))))
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(seg):
                a, b2 = rings[i][k], rings[i][(k + 1) % seg]
                c, d = rings[i + 1][(k + 1) % seg], rings[i + 1][k]
                try: bm.faces.new((a, b2, c, d))
                except ValueError: pass
        if cap:
            for ring in (rings[0], rings[-1]):
                try: bm.faces.new(ring)
                except ValueError: pass
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self._obj(bm, mat, 0)
'''
mod = types.ModuleType("gunkit3")
exec(src, mod.__dict__)
bpy.app.driver_namespace["gunkit3"] = mod
