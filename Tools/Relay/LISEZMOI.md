# Relais des signalements (bouton REPORT → salon Discord des modos)

Le jeu envoie chaque signalement à un petit programme gratuit chez Cloudflare (`report_worker.js`), qui le poste dans
un salon Discord privé des modérateurs. **L'adresse du webhook Discord est un secret** : elle ne va que dans
Cloudflare. Jamais dans le code, jamais dans le jeu, jamais dans un chat.

## Mise en route (une seule fois, environ 10 minutes)

### 1. Discord : le salon et le webhook
1. Crée un salon texte **#reports**, visible seulement par les modérateurs (paramètres du salon → Permissions).
2. Paramètres du salon → **Intégrations** → **Webhooks** → **Nouveau webhook**. Nom : « CALM FACES reports ».
3. **Copier l'URL du webhook**. Garde-la dans le presse-papiers pour l'étape 2 et ne la colle nulle part ailleurs.

### 2. Cloudflare : le relais
1. Crée un compte gratuit sur cloudflare.com (toi-même).
2. **Workers & Pages** → **Create** → **Create Worker** → nom : `calm-faces-reports` → **Deploy**.
3. **Edit code** : efface tout, colle le contenu de `Tools/Relay/report_worker.js`, puis **Deploy**.
4. Onglet **Settings** → **Variables and Secrets** → **Add** : type **Secret**, nom `DISCORD_WEBHOOK`,
   valeur = l'URL du webhook (étape 1.3) → **Deploy**.
5. Ouvre `https://calm-faces-reports.<ton-sous-domaine>.workers.dev/` : la page doit afficher
   « CALM FACES report relay: OK ».

### 3. Le jeu
Donne l'adresse du relais (elle n'est pas secrète) : on met `https://calm-faces-reports.<…>.workers.dev/report`
dans `Assets/Horror/Resources/OnlineServices.asset` (champ « Report Url »), puis on recompile l'APK.
Les signalements déjà faits dans un casque partent tout seuls à la mise à jour (ils attendaient dans le casque).

## Ce que fait le relais
- Refuse ce qui n'est pas un signalement de CALM FACES, tronque les champs, retire les caractères de contrôle.
- Aucune mention possible dans Discord (@everyone, @here, rôles).
- Au plus 5 signalements toutes les 10 minutes par adresse IP. L'IP n'est jamais envoyée à Discord ni conservée.
- Ce n'est pas une authentification : un client modifié peut envoyer de faux signalements. Les modos voient l'ID
  de celui qui signale, pour repérer les abus. Plus tard : vérification par le compte Meta (SDK Platform).

## Tester sans Cloudflare
- `node Tools/Relay/test_report_worker.mjs` : tests du relais (faux Discord).
- `node Tools/Relay/serveur_local.mjs` : relais sur le PC avec un faux Discord. Mettre
  `http://127.0.0.1:8787/report` dans `OnlineServices.asset` le temps du test (et le remettre ensuite).
