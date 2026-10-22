# Tiling textures for the TRENCHES map, painted with numpy: leaf litter over dark earth (the tree lines), the
# raw cut earth of the trench walls (strata, stones, roots, spade marks), and wet trench-floor mud.
import bpy, math, random, os
import numpy as np

OUT = r"C:\Users\Binky\OneDrive\Documents\GitHub\earlsandbox\Assets\CarlSandbox\Resources\Textures"
os.makedirs(OUT, exist_ok=True)
rs = np.random.RandomState(11)

def tnoise(n, cells):
    """Tiling value noise, n x n, with cells x cells lattice."""
    g = rs.rand(cells, cells)
    u = np.arange(n) * cells / n; i0 = np.floor(u).astype(int); f = u - i0; f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx, fy = f[None, :], f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
def tfbm(n, base, o=5):
    s, a, t = 0, 1.0, 0
    for k in range(o):
        c = base * 2 ** k
        if c > n: break
        s = s + a * tnoise(n, c); t += a; a *= 0.5
    return s / t

def stamp(img, cx, cy, w, h, fn):
    """Paint fn(local x, local y) -> (rgb, alpha) into a wrapped box."""
    n = img.shape[0]
    xs = (np.arange(int(cx - w), int(cx + w) + 1)); ys = (np.arange(int(cy - h), int(cy + h) + 1))
    X, Y = np.meshgrid(xs - cx, ys - cy)
    col, al = fn(X, Y)
    if al is None: return
    yi, xi = np.ix_(ys % n, xs % n)
    img[yi, xi] = img[yi, xi] * (1 - al[..., None]) + col * al[..., None]

def save(name, img):
    n = img.shape[0]
    rgba = np.concatenate([np.clip(img, 0, 1), np.ones((n, n, 1))], 2)[::-1]
    im = bpy.data.images.get(name) or bpy.data.images.new(name, n, n)
    if im.size[0] != n: im.scale(n, n)
    im.pixels.foreach_set(rgba.astype(np.float32).ravel())
    im.filepath_raw = os.path.join(OUT, name + ".png"); im.file_format = 'PNG'; im.save()

def leaf(img, cx, cy, L, ang, col, soggy=0.0):
    ca, sa = math.cos(ang), math.sin(ang); W = L * random.uniform(0.38, 0.55)
    def fn(X, Y):
        u = (X * ca + Y * sa) / L; v = (-X * sa + Y * ca) / W
        # pointed oval, a touch of curl, a stem
        shape = u * u + (v / (1 - 0.35 * u)) ** 2
        inside = shape < 1
        stem = (u < -1) & (u > -1.35) & (np.abs(v) < 0.08)
        a = np.clip((1 - shape) * 6, 0, 1) * inside + stem * 1.0
        if not a.any(): return None, None
        shade = 0.82 + 0.25 * (1 - np.abs(v)) - 0.25 * (np.abs(v) < 0.07) * inside
        shade = shade - 0.12 * (np.abs(np.abs(v) - 0.5 * (u + 1) * 0.6) < 0.04) * inside   # veins
        c = np.array(col)[None, None] * shade[..., None]
        c = c * (1 - soggy) + np.array([0.12, 0.09, 0.06]) * soggy
        return c, a * random.uniform(0.85, 1.0)
    stamp(img, cx, cy, L * 1.4, L * 1.4, fn)

def twig(img, n_seg, x, y, col, width):
    a = random.uniform(0, math.tau)
    for k in range(n_seg):
        a += random.uniform(-0.35, 0.35); nx, ny = x + math.cos(a) * 5, y + math.sin(a) * 5
        def fn(X, Y, x=x, y=y, nx=nx, ny=ny):
            px, py = X + x, Y + y
            dx, dy = nx - x, ny - y; t = np.clip(((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy), 0, 1)
            d = np.hypot(px - x - t * dx, py - y - t * dy)
            al = np.clip(width - d, 0, 1)
            return np.array(col)[None, None] * (0.8 + 0.3 * (d < width * 0.4))[..., None], al
        stamp(img, (x + nx) / 2, (y + ny) / 2, 5, 5, fn)
        x, y = nx, ny

random.seed(5)
# ---------------------------------------------------------------- leaf litter (tree lines and field edges)
n = 1024
f = tfbm(n, 6)
img = np.stack([0.2 + 0.1 * f, 0.15 + 0.07 * f, 0.1 + 0.05 * f], 2)
pal = [(0.52, 0.33, 0.16), (0.6, 0.42, 0.2), (0.44, 0.28, 0.14), (0.34, 0.23, 0.14), (0.63, 0.52, 0.3),
       (0.28, 0.2, 0.13), (0.5, 0.4, 0.26), (0.4, 0.36, 0.22), (0.56, 0.3, 0.12)]
for k in range(3200):
    c = np.array(random.choice(pal)) * random.uniform(0.75, 1.1)
    leaf(img, random.uniform(0, n), random.uniform(0, n), random.uniform(9, 26), random.uniform(0, math.tau), c, random.uniform(0, 0.35))
for k in range(70):
    twig(img, random.randint(3, 12), random.uniform(0, n), random.uniform(0, n), (0.26, 0.21, 0.16), random.uniform(1.2, 2.6))
# patches of bare earth showing through, and of dead grass
bare = tfbm(n, 4, 3)
img = img * (1 - (np.clip((bare - 0.62) * 6, 0, 1))[..., None]) + np.array([0.24, 0.18, 0.12])[None, None] * (np.clip((bare - 0.62) * 6, 0, 1))[..., None]
save("trench_litter", img)

# ---------------------------------------------------------------- the cut earth of the walls
f = tfbm(n, 4); g = tfbm(n, 16)
y = np.arange(n)[:, None] / n
strata = np.sin((y * 9 + f * 0.6) * math.tau) * 0.5 + 0.5
streak = tnoise(n, 64)[:, :]
streak = np.repeat(tnoise(n, 96)[0:1], n, 0) * 0.5 + 0.5 * tnoise(n, 8)        # vertical spade scrapes
base = 0.27 + 0.1 * f + 0.07 * g + 0.035 * strata - 0.05 * streak + 0.07 * (tnoise(n, 256) - 0.5) + 0.05 * (rs.rand(n, n) - 0.5)
img = np.stack([base * 1.08, base * 0.84, base * 0.6], 2)
clay = np.clip((tfbm(n, 6, 3) - 0.6) * 5, 0, 1)[..., None]
img = img * (1 - 0.6 * clay) + np.array([0.46, 0.38, 0.27]) * 0.6 * clay
for k in range(500):       # stones and pebbles
    r = random.uniform(1.5, 6); c = np.array([0.36, 0.33, 0.28]) * random.uniform(0.6, 1.1)
    def fn(X, Y, r=r, c=c):
        d = np.hypot(X, Y * random.uniform(0.8, 1.3)) / r
        a = np.clip((1 - d) * 4, 0, 1)
        return c[None, None] * (1.15 - 0.4 * np.clip((X + Y) / (2 * r) + 0.5, 0, 1))[..., None], a
    stamp(img, random.uniform(0, n), random.uniform(0, n), r + 1, r + 1, fn)
for k in range(45):        # roots, cut where the spade went through
    twig(img, random.randint(6, 25), random.uniform(0, n), random.uniform(0, n), (0.2, 0.15, 0.1), random.uniform(1.0, 3.0))
for k in range(250):
    leaf(img, random.uniform(0, n), random.uniform(0, n), random.uniform(7, 15), random.uniform(0, math.tau),
         np.array(random.choice(pal)) * 0.75, random.uniform(0.3, 0.6))
save("trench_earth", img)

# ---------------------------------------------------------------- trench-floor mud
n = 512
f = tfbm(n, 5); g = tfbm(n, 24)
wet = np.clip((tfbm(n, 3, 3) - 0.45) * 3, 0, 1)
base = 0.17 + 0.07 * f + 0.03 * g - 0.06 * wet
img = np.stack([base * 1.1, base * 0.85, base * 0.62], 2)
for k in range(260):
    leaf(img, random.uniform(0, n), random.uniform(0, n), random.uniform(6, 13), random.uniform(0, math.tau),
         np.array(random.choice(pal)) * 0.6, random.uniform(0.4, 0.8))
for k in range(40):   # boot prints: dark heel and sole dents
    cx, cy, a = random.uniform(0, n), random.uniform(0, n), random.uniform(0, math.tau)
    def fn(X, Y, a=a):
        u = X * math.cos(a) + Y * math.sin(a); v = -X * math.sin(a) + Y * math.cos(a)
        d = (u / 16) ** 2 + (v / 6.5) ** 2
        return np.array([0.1, 0.075, 0.05])[None, None] * np.ones(X.shape)[..., None], np.clip((1 - d) * 2, 0, 1) * 0.55
    stamp(img, cx, cy, 18, 18, fn)
save("trench_mud", img)

# ---------------------------------------------------------------- camouflage netting (alpha in the red channel's partner file)
n = 512
net = np.zeros((n, n, 3)); alpha = np.zeros((n, n))
yy, xx = np.mgrid[0:n, 0:n]
mesh = ((xx % 16) < 2) | ((yy % 16) < 2)
scr = tfbm(n, 10, 3); scr2 = tfbm(n, 22, 2)
garn = (scr > 0.48) | (scr2 > 0.62)
alpha = np.where(mesh | garn, 1.0, 0.0)
cols = np.array([[0.22, 0.25, 0.15], [0.32, 0.3, 0.19], [0.17, 0.17, 0.12], [0.38, 0.33, 0.22]])
sel = (tnoise(n, 14) * 3.99).astype(int)
net = cols[sel] * (0.85 + 0.3 * scr2)[..., None]
net[mesh & ~garn] = [0.15, 0.15, 0.11]
rgba = np.concatenate([net, alpha[..., None]], 2)[::-1]
im = bpy.data.images.get("trench_net") or bpy.data.images.new("trench_net", n, n, alpha=True)
im.alpha_mode = 'STRAIGHT'
im.pixels.foreach_set(rgba.astype(np.float32).ravel())
im.filepath_raw = os.path.join(OUT, "trench_net.png"); im.file_format = 'PNG'; im.save()
result = {"ok": True}
