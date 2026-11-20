// Rend une liste d'images : node render.js <dossier de travail> <jobs.json>
// Le dossier de travail contient www/ (render.html + modèles .glb + textures) et reçoit les images.
// THREE_DIR = dossier de three@0.160.0 (node_modules/three) ; RENDER_PORT = port du petit serveur web local.
// jobs.json = { "setup": "doll"|"map", "shots": [ { "out": "frames/x.png", ...paramètres de shot } ] }
const { chromium } = require("playwright");
const path = require("path");
const fs = require("fs");
const S = process.argv[2];
const jobs = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
const T = process.env.THREE_DIR || path.join(S, "node_modules/three");
const PORT = process.env.RENDER_PORT || "8769";
(async () => {
  const b = await chromium.launch({ args: ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"] });
  const p = await b.newPage({ viewport: { width: 1080, height: 1920 } });
  p.on("pageerror", (e) => console.log("pageerror:", e.message));
  p.on("console", (m) => { if (m.type() === "error") console.log("console:", m.text().slice(0, 200)); });
  await p.route("https://cdn.jsdelivr.net/npm/three@0.160.0/**", (r) => {
    const rel = r.request().url().split("three@0.160.0/")[1];
    r.fulfill({ path: path.join(T, rel), contentType: "application/javascript" });
  });
  await p.goto("http://localhost:" + PORT + "/render.html");
  await p.waitForFunction(() => typeof window.setup === "function", null, { timeout: 60000 });
  await p.evaluate((w) => window.setup(w), jobs.setup);
  let i = 0;
  for (const s of jobs.shots) {
    const url = await p.evaluate((sp) => window.shot(sp), s);
    fs.writeFileSync(path.join(S, s.out), Buffer.from(url.split(",")[1], "base64"));
    if (++i % 10 === 0) console.log(i + "/" + jobs.shots.length);
  }
  await b.close();
})();
