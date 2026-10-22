// Downloads the whole Poly Haven library (CC0) at 1K into C:/Users/Binky/PolyHaven:
//   textures/<id>/   every map as 1K jpg (diffuse, normal GL, rough, AO, displacement, ...)
//   models/<id>/     1K glTF with its textures
//   hdris/<id>.hdr   1K HDR
// plus index.json (id, type, name, categories, tags) to search. Re-running skips what is already there.
// Usage: node polyhaven_download.js [res] [id,id,...]   (res defaults to 1k; ids: fetch only those)
const fs = require("fs"), path = require("path"), https = require("https");
const ROOT = "C:/Users/Binky/PolyHaven", RES = process.argv[2] || "1k";
const TYPES = { 0: "hdris", 1: "textures", 2: "models" };

function get(url, asJson) {
  return new Promise((ok, bad) => {
    https.get(url, { headers: { "User-Agent": "earlsandbox-asset-sync" } }, r => {
      if (r.statusCode >= 300 && r.statusCode < 400 && r.headers.location) return get(r.headers.location, asJson).then(ok, bad);
      if (r.statusCode !== 200) { r.resume(); return bad(new Error(r.statusCode + " " + url)); }
      const chunks = []; r.on("data", c => chunks.push(c)); r.on("end", () => {
        const b = Buffer.concat(chunks); ok(asJson ? JSON.parse(b.toString()) : b);
      }); r.on("error", bad);
    }).on("error", bad);
  });
}
async function save(url, file) {
  if (fs.existsSync(file) && fs.statSync(file).size > 0) return 0;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  for (let tries = 0; tries < 3; tries++) {
    try { const b = await get(url); fs.writeFileSync(file + ".part", b); fs.renameSync(file + ".part", file); return b.length; }
    catch (e) { if (tries === 2) throw e; await new Promise(r => setTimeout(r, 2000)); }
  }
}
function pick(o, res) { return o[res] || o["1k"] || o[Object.keys(o).sort()[0]]; }

async function one(id, a) {
  const kind = TYPES[a.type], files = await get(`https://api.polyhaven.com/files/${id}`, true);
  let bytes = 0;
  if (kind === "hdris") {
    const f = pick(files.hdri, RES).hdr; bytes += await save(f.url, `${ROOT}/hdris/${id}_${RES}.hdr`);
  } else if (kind === "textures") {
    for (const map in files) {
      if (["blend", "gltf", "mtlx"].includes(map)) continue;
      const r = files[map] && pick(files[map], RES); if (!r) continue;
      const f = r.jpg || r.png || r.exr; if (!f) continue;
      bytes += await save(f.url, `${ROOT}/textures/${id}/${path.basename(f.url)}`);
    }
  } else {
    const g = files.gltf && pick(files.gltf, RES); if (!g || !g.gltf) return 0;
    bytes += await save(g.gltf.url, `${ROOT}/models/${id}/${path.basename(g.gltf.url)}`);
    for (const rel in (g.gltf.include || {})) bytes += await save(g.gltf.include[rel].url, `${ROOT}/models/${id}/${rel}`);
  }
  return bytes;
}

(async () => {
  fs.mkdirSync(ROOT, { recursive: true });
  const assets = await get("https://api.polyhaven.com/assets", true);
  const only = process.argv[3] ? process.argv[3].split(",") : null;   // or just these ids
  const ids = Object.keys(assets).filter(id => !only || only.includes(id));
  if (!only) fs.writeFileSync(`${ROOT}/index.json`, JSON.stringify(ids.map(id => ({ id, type: TYPES[assets[id].type], name: assets[id].name,
    categories: assets[id].categories, tags: assets[id].tags })), null, 1));
  let done = 0, total = 0, failed = [];
  const queue = ids.slice();
  async function worker() {
    while (queue.length) {
      const id = queue.shift();
      try { total += await one(id, assets[id]); } catch (e) { failed.push(id + ": " + e.message); }
      if (++done % 25 === 0) console.log(`${done}/${ids.length}  ${(total / 1e9).toFixed(2)} GB  failed ${failed.length}`);
    }
  }
  await Promise.all([1, 2, 3, 4, 5, 6].map(worker));
  if (!only) fs.writeFileSync(`${ROOT}/failed.txt`, failed.join("\n"));
  console.log(`DONE ${done}/${ids.length}  ${(total / 1e9).toFixed(2)} GB  failed ${failed.length}`);
})();
