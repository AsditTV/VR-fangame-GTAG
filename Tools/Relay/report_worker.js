/**
 * Relais des signalements de CALM FACES (Cloudflare Worker, gratuit).
 * Le jeu envoie POST /report (JSON) ; le relais le poste dans le salon Discord des modérateurs.
 *
 * Pourquoi un relais : l'adresse du webhook Discord donne le droit d'écrire dans le salon. Mise dans l'APK,
 * n'importe qui pourrait la lire et spammer le Discord. Ici elle reste un SECRET de Cloudflare (DISCORD_WEBHOOK),
 * jamais dans le code ni dans le jeu.
 *
 * Protections (ce n'est pas une authentification : un client modifié peut quand même envoyer de faux signalements) :
 *  - taille et champs limités, caractères de contrôle retirés ;
 *  - aucune mention possible (@everyone, @here, rôles) : allowed_mentions vide + « @ » neutralisé ;
 *  - au plus RATE_MAX signalements par adresse IP toutes les RATE_WINDOW secondes ;
 *  - l'adresse IP n'est jamais transmise à Discord ni conservée (seulement un compteur éphémère).
 * Plus tard : vérifier l'identité du joueur avec le SDK Platform de Meta (« user proof »).
 */

const RATE_MAX = 5;
const RATE_WINDOW = 600; // secondes

/** Nettoie un champ texte : coupe, retire les caractères de contrôle, empêche les mentions et le formatage de code. */
export function clean(value, max) {
  return String(value ?? "")
    .replace(/[\u0000-\u001f\u007f]/g, " ")
    .replace(/@/g, "@​")
    .replace(/`/g, "'")
    .trim()
    .slice(0, max);
}

/** Vérifie et met en forme le signalement reçu. Renvoie null s'il n'est pas valable. */
export function parseReport(text) {
  if (typeof text !== "string" || text.length === 0 || text.length > 4000) return null;
  let r;
  try { r = JSON.parse(text); } catch { return null; }
  if (!r || typeof r !== "object" || r.game !== "calm-faces") return null;
  const report = {
    reportedId: clean(r.reportedUserId, 64),
    reportedName: clean(r.reportedName, 40) || "PLAYER",
    reporterId: clean(r.reporterUserId, 64),
    reason: clean(r.reason, 300) || "(no reason)",
    when: clean(r.utc, 40),
    mode: clean(r.mode, 30),
    version: clean(r.version, 20),
  };
  if (!report.reportedId || !report.reporterId) return null;
  return report;
}

/** Message Discord (embed), sans aucune mention autorisée. */
export function discordMessage(rep) {
  const short = (id) => id.slice(0, 8).toUpperCase();
  return {
    username: "CALM FACES reports",
    allowed_mentions: { parse: [] },
    embeds: [{
      title: "Player report",
      color: 0xd94a4a,
      fields: [
        { name: "Reported", value: `${rep.reportedName}\nID ${short(rep.reportedId)} (${rep.reportedId})`, inline: true },
        { name: "By", value: `ID ${short(rep.reporterId)}`, inline: true },
        { name: "Reason", value: rep.reason },
        { name: "Mode / version", value: `${rep.mode || "?"} / ${rep.version || "?"}`, inline: true },
        { name: "Time (UTC)", value: rep.when || "?", inline: true },
      ],
    }],
  };
}

/** Compteur par IP (approximatif, par centre de données Cloudflare) dans le cache du Worker. Vrai = autorisé. */
async function allowed(ip, cache) {
  if (!cache || !ip) return true;
  const key = new Request(`https://rate.calm-faces.invalid/${encodeURIComponent(ip)}`);
  const hit = await cache.match(key);
  const n = hit ? parseInt(await hit.text(), 10) || 0 : 0;
  if (n >= RATE_MAX) return false;
  await cache.put(key, new Response(String(n + 1), { headers: { "Cache-Control": `max-age=${RATE_WINDOW}` } }));
  return true;
}

export default {
  async fetch(request, env, ctx, deps = {}) {
    const cache = deps.cache !== undefined ? deps.cache : (typeof caches !== "undefined" ? caches.default : null);
    const send = deps.fetch || fetch;
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/") return new Response("CALM FACES report relay: OK", { status: 200 });
    if (url.pathname !== "/report") return new Response("not found", { status: 404 });
    if (request.method !== "POST") return new Response("method not allowed", { status: 405 });
    if (!env || !env.DISCORD_WEBHOOK) return new Response("relay not configured", { status: 503 });

    const report = parseReport(await request.text());
    if (!report) return new Response("bad report", { status: 400 });
    if (!(await allowed(request.headers.get("CF-Connecting-IP"), cache))) return new Response("too many reports", { status: 429 });

    const res = await send(env.DISCORD_WEBHOOK, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(discordMessage(report)),
    });
    // 2xx : le jeu marque le signalement comme envoyé ; sinon il réessaiera plus tard
    return new Response(res.ok ? "ok" : "discord error", { status: res.ok ? 200 : 502 });
  },
};
