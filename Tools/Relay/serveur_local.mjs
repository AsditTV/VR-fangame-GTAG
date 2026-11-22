// Fait tourner le relais des signalements sur ce PC, avec un FAUX Discord : chaque message qui serait posté sur
// Discord est affiché ici et ajouté au fichier donné. Sert à tester le jeu (Play dans Unity) sans Cloudflare.
// Lancer : node Tools/Relay/serveur_local.mjs [port] [fichier]   (par défaut 8787, discord_local.jsonl)
// Puis mettre http://127.0.0.1:8787/report dans Assets/Horror/Resources/OnlineServices.asset (le temps du test).
import http from "node:http";
import fs from "node:fs";
import worker from "./report_worker.js";

const port = Number(process.argv[2] || 8787);
const out = process.argv[3] || "discord_local.jsonl";
const env = { DISCORD_WEBHOOK: "https://discord.invalid/faux-webhook" };
const fakeDiscord = async (url, init) => {
  fs.appendFileSync(out, init.body + "\n");
  console.log("[faux Discord] message reçu :", init.body.slice(0, 200));
  return new Response(null, { status: 204 });
};

http.createServer(async (req, res) => {
  const chunks = [];
  for await (const c of req) chunks.push(c);
  const body = Buffer.concat(chunks);
  const request = new Request(`http://127.0.0.1:${port}${req.url}`, {
    method: req.method,
    headers: { ...req.headers, "CF-Connecting-IP": req.socket.remoteAddress },
    body: req.method === "GET" || req.method === "HEAD" ? undefined : body,
  });
  const r = await worker.fetch(request, env, null, { cache: null, fetch: fakeDiscord });
  res.writeHead(r.status, { "Content-Type": "text/plain" });
  res.end(await r.text());
  console.log(req.method, req.url, "->", r.status);
}).listen(port, "127.0.0.1", () => console.log(`relais local : http://127.0.0.1:${port}/report`));
