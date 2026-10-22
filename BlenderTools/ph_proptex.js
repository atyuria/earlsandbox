// For each prop in Resources/Props: copy its Poly Haven textures (colour, normal) into Resources/PropTex/<id>/ and
// write <id>.json mapping each glTF material name to them (and whether it is alpha-cut, for leaves and grass).
const fs = require("fs"), path = require("path");
const SRC = "C:/Users/Binky/PolyHaven/models/", RES = path.join(__dirname, "../Assets/CarlSandbox/Resources/");
for (const f of fs.readdirSync(RES + "Props").filter(f => f.endsWith(".fbx"))) {
  const id = f.slice(0, -4), dir = SRC + id, gl = fs.readdirSync(dir).find(x => x.endsWith(".gltf"));
  const j = JSON.parse(fs.readFileSync(path.join(dir, gl))), out = {}, od = RES + "PropTex/" + id;
  fs.mkdirSync(od, { recursive: true });
  const tex = i => { if (i == null) return null; const uri = decodeURIComponent(j.images[j.textures[i].source].uri);
    const name = path.basename(uri); fs.copyFileSync(path.join(dir, uri), path.join(od, name)); return name.replace(/\.[^.]+$/, ""); };
  for (const m of j.materials || []) out[m.name] = { diff: tex(m.pbrMetallicRoughness?.baseColorTexture?.index), nor: tex(m.normalTexture?.index),
    cut: m.alphaMode === "MASK" || m.alphaMode === "BLEND", two: !!m.doubleSided };
  fs.writeFileSync(od + ".json", JSON.stringify({ mats: Object.entries(out).map(([k, v]) => ({ name: k, ...v })) }));
}
console.log("done");
