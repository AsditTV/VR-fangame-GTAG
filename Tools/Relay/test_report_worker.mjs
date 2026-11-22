// Tests du relais des signalements, sans Cloudflare ni Discord (Discord et le cache sont simulés).
// Lancer : node Tools/Relay/test_report_worker.mjs
import assert from "node:assert/strict";
import worker, { clean, parseReport } from "./report_worker.js";

const env = { DISCORD_WEBHOOK: "https://discord.example/webhook-test" };
const good = {
  game: "calm-faces", reportedUserId: "1d645c06aaaabbbbccccddddeeeeffff", reportedName: "Bob",
  reporterUserId: "9f00aa11bbbbccccddddeeeeffff0000", reason: "insults @everyone", utc: "2026-11-22T18:00:00Z",
  mode: "exploration", version: "0.1",
};

function makeCache() {
  const m = new Map();
  return {
    async match(req) { const v = m.get(req.url); return v === undefined ? undefined : new Response(v); },
    async put(req, res) { m.set(req.url, await res.text()); },
  };
}

function post(body, ip = "1.2.3.4") {
  return new Request("https://relay.test/report", {
    method: "POST", body: typeof body === "string" ? body : JSON.stringify(body),
    headers: { "CF-Connecting-IP": ip, "Content-Type": "application/json" },
  });
}

let sent = [];
const fakeDiscord = async (url, init) => { sent.push({ url, body: JSON.parse(init.body) }); return new Response(null, { status: 204 }); };

// 1. nettoyage
assert.equal(clean("a\u0000b`c@here", 50), "a b'c@​here");
assert.equal(clean("x".repeat(100), 10).length, 10);

// 2. refus des signalements invalides
assert.equal(parseReport("pas du json"), null);
assert.equal(parseReport(JSON.stringify({ ...good, game: "autre" })), null);
assert.equal(parseReport(JSON.stringify({ ...good, reportedUserId: "" })), null);
assert.equal(parseReport("x".repeat(5000)), null);

// 3. un bon signalement part vers Discord, sans mention possible
let cache = makeCache();
let res = await worker.fetch(post(good), env, null, { cache, fetch: fakeDiscord });
assert.equal(res.status, 200);
assert.equal(sent.length, 1);
assert.equal(sent[0].url, env.DISCORD_WEBHOOK);
assert.deepEqual(sent[0].body.allowed_mentions, { parse: [] });
const txt = JSON.stringify(sent[0].body);
assert.ok(!txt.includes("@everyone"), "la mention @everyone doit être neutralisée");
assert.ok(txt.includes("1D645C06"), "l'ID court du joueur signalé doit apparaître");
assert.ok(!txt.includes("1.2.3.4"), "l'adresse IP ne doit jamais partir vers Discord");

// 4. limite par IP : 5 passent, le 6e est refusé ; une autre IP passe
sent = []; cache = makeCache();
for (let i = 0; i < 5; i++) assert.equal((await worker.fetch(post(good), env, null, { cache, fetch: fakeDiscord })).status, 200);
assert.equal((await worker.fetch(post(good), env, null, { cache, fetch: fakeDiscord })).status, 429);
assert.equal((await worker.fetch(post(good, "5.6.7.8"), env, null, { cache, fetch: fakeDiscord })).status, 200);
assert.equal(sent.length, 6);

// 5. erreurs : mauvais chemin, mauvaise méthode, relais non configuré, Discord en panne
assert.equal((await worker.fetch(new Request("https://relay.test/autre", { method: "POST" }), env, null, { cache: null, fetch: fakeDiscord })).status, 404);
assert.equal((await worker.fetch(new Request("https://relay.test/report"), env, null, { cache: null, fetch: fakeDiscord })).status, 405);
assert.equal((await worker.fetch(post(good), {}, null, { cache: null, fetch: fakeDiscord })).status, 503);
const down = async () => new Response("", { status: 500 });
assert.equal((await worker.fetch(post(good), env, null, { cache: null, fetch: down })).status, 502);
assert.equal((await worker.fetch(new Request("https://relay.test/"), env, null, { cache: null })).status, 200);

console.log("relais des signalements : tous les tests passent");
