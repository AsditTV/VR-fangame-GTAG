# Multijoueur : ce qui est prêt et ce qu'il te reste à faire

État (21/11/2026) : **comptes et apps Photon créés (« CALM FACES - Fusion » et « CALM FACES - Voice », 20 CCU gratuits chacune) ; le SDK n'est pas encore installé dans Unity.** Le jeu reste 100 % solo tant que tu n'as pas fait les étapes ci-dessous.
Niveau de confiance sur le plan : *probable* (Fusion change parfois ses API entre versions : on adaptera l'adaptateur à la version que tu installes).

## Ce qui existe déjà (`Assets/Horror/Scripts/Net/`)

| Fichier | Rôle |
|---|---|
| `INetworkSession.cs` | Ce dont le jeu a besoin du réseau (rejoindre une salle, envoyer pose/bruit/monstre/événements). Aucune référence à Photon. |
| `OfflineSession.cs` | La session du solo : on est l'hôte, rien ne part. C'est celle utilisée aujourd'hui. |
| `NetMessages.cs` | Messages compacts : `PlayerPose`, `MonsterState`, `NoiseMessage`, `GameEventMessage`. |
| `NetworkBootstrap.cs` | Choisit la session selon le mode du lobby (`ModeSelector.Current`), branche `NoiseSystem` : mes bruits partent vers l'hôte, ceux des autres sont ré-émis chez l'hôte (`NoiseSystem.ReceiveRemote`). `NetworkBootstrap.RunsAI` dit si les monstres doivent penser ici. |

Principe : **l'IA des monstres ne tourne que chez l'hôte.** Les invités envoient leurs bruits ; l'hôte renvoie la position et l'état des monstres. C'est pour ça que tout bruit passe déjà par `NoiseSystem.Emit`.

## Ce que TU fais (comptes et clés : jamais dans le code, jamais dans le chat)

1. Crée un compte sur le tableau de bord Photon, puis **deux applications** :
   - une **Fusion** (le jeu en ligne),
   - une **Voice** (le chat vocal de proximité).
2. Dans Unity : installe **Photon Fusion 2** (Asset Store ou le .unitypackage officiel), puis **Photon Voice 2**.
3. Colle les App ID **uniquement** dans les fichiers de configuration Photon :
   - Fusion : `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` → champ *App Id Fusion* ;
   - Voice : même fichier ou réglages Voice → champ *App Id Voice*.
   Ces fichiers ne doivent pas être poussés sur un dépôt public : ajoute-les au `.gitignore` si le dépôt devient public.
4. Dis-moi « Photon installé » : j'écris l'adaptateur.

## Ce que JE ferai après l'installation

1. `PhotonFusionSession : INetworkSession` (mode **Host** de Fusion : un joueur héberge, les autres rejoignent), déclaré au démarrage par
   `NetworkBootstrap.OnlineFactory = () => new PhotonFusionSession();`.
2. Avatars distants : la pose (`PlayerPose`) anime un Dessiné fantôme (tête + mains), interpolé.
3. Monstres : `if (!NetworkBootstrap.RunsAI) return;` en tête de l'IA, et application de `MonsterState` chez les invités.
4. Voix : Photon Voice branché sur `VoiceNoise` (ta voix fait du bruit pour le monstre **chez l'hôte**, et les autres t'entendent en 3D, étouffée par les murs).
5. Modes : INK TAG et HIDE & SEEK passent `available = true` au lobby.

## Ce qu'il faut savoir (vérités qui dérangent)

- **Le coût** : Photon est gratuit jusqu'à 20 joueurs simultanés (CCU) par application, payant au-delà. Pour un premier test entre amis, c'est suffisant.
- **Le Quest en Wi-Fi** : 20 envois de pose par seconde suffisent ; plus, ça ne sert à rien et ça vide la batterie.
- **La triche** : avec un hôte joueur, l'hôte peut tricher. Pour un jeu entre amis, c'est acceptable ; pour un jeu public classé, il faudrait un serveur dédié (plus tard, et payant).
- **Le Store Meta** exige une politique de confidentialité dès qu'il y a de la voix en ligne (le micro est une donnée personnelle).

## Fait le 21/11/2026
- Apps Photon créées sur le tableau de bord (Multiplayer Game) : **CALM FACES - Fusion** (Fusion 2) et
  **CALM FACES - Voice** (Voice). Leurs App ID se lisent sur https://dashboard.photonengine.com (Your Apps).
- Interface réseau prête pour la sécurité vocale : `INetworkSession.RemotePlayers`, `PlayerName`, `PlayerUserId`
  (identifiant STABLE du compte, pour retenir les blocages) ; la voix réseau doit lire `VoiceModeration.ShouldHear(userId)`.

## Installé le 21/11/2026 (sur ce PC uniquement)
- **Fusion 2.1.4 Stable 2500** et **Voice 2.64 (Realtime 5)**, depuis doc.photonengine.com > Fusion 2 > SDK & Download.
  Unity demande « Import Anyway » (paquets non signés : normal, ils viennent du site Photon) et propose de mettre à
  jour les DLL de Fusion (API Updater) : répondre Yes. Unity 6000.6 n'est pas encore dans la liste officielle.
- `Assets/Photon/` est **hors du dépôt** (`.gitignore`) : licence Photon et App ID. Sur un autre PC, réinstaller
  les mêmes versions. Sans le SDK, l'adaptateur ne compile pas (`#if FUSION2`) et le jeu reste solo ; il faut
  alors retirer les symboles FUSION* / PHOTON* des Scripting Define Symbols.
- `PhotonFusionSession` (Assets/Horror/Scripts/Net/Photon/) : mode **Shared**, messages en données fiables
  (pas d'objet réseau), « bonjour » avec pseudo + identifiant stable de l'appareil, master client = monstres.
  Reste : avatars des autres joueurs (poses), Photon Voice (volume par joueur = `VoiceModeration.ShouldHear`).

## Prochaine étape
1. ~~Installer Photon Fusion 2 puis Photon Voice 2~~ (fait).
2. ~~Coller les App ID dans `PhotonAppSettings`~~ (fait : format vérifié, fichier hors dépôt).
   **Test du 21/11/2026** : depuis l'éditeur, `PhotonFusionSession.Join("test")` rejoint la salle `calmfaces-test`
   (en ligne, master client = fait tourner les monstres, joueur n° 1), puis la quitte proprement. Un seul appareil :
   l'échange entre deux joueurs n'est PAS encore vérifié.
3. ~~`PhotonFusionSession`~~ (fait). Reste : avatars des autres joueurs, puis Photon Voice (volume = `ShouldHear`).
4. Test à 2 casques (ou 1 casque + l'éditeur) : rejoindre, se voir, s'entendre, couper/bloquer.

## Avatars, voix, monstres (21/11/2026)
**Jouer à plusieurs** : sur le Minitel, page ROOM, tapez le MÊME code (4 à 6 chiffres) sur chaque casque. 2,5 s après
le dernier chiffre, le jeu rejoint la salle `coop-CODE`. Effacer le code = retour en solo. Le code est gardé d'une
partie à l'autre. (Test PC : `CalmFaces.exe -online` rejoint la salle « test ».)

| Brique | Fichier | État |
|---|---|---|
| Avatars des autres | `Net/NetworkAvatars.cs` | Copie de ma poupée pilotée par la pose reçue (15/s, lissée), à SA couleur, pseudo au-dessus, bouche animée par sa voix. Bloqué = caché. |
| Voix de proximité | `Net/Photon/CalmVoiceClient.cs` | Photon Voice suit la salle Fusion (« _voice »). Ma voix porte mon identifiant ; celle des autres sort de la tête de leur avatar (3D, 1,5 à 25 m), étouffée derrière un mur. Coupé/bloqué = volume 0. Respecte le mode micro du Minitel. |
| Micro partagé | `Player/MicMouth.cs` | En ligne, le micro est à Photon Voice (un seul programme peut l'ouvrir sur Quest) : MicMouth lit son niveau (bouche + bruit pour les monstres). |
| Monstres | `Net/MonsterSync.cs` | L'IA tourne chez le master client ; il envoie position/orientation 10/s ; chez les autres l'IA est éteinte et le monstre suit (démarche animée). |

**Vérifié (éditeur, 2 clients Photon dans le même Unity)** : chacun voit l'autre (pseudo reçu, avatar affiché, part
quand il quitte) ; voix transmise de B vers A (son joué sur la tête de l'avatar) ; couper = volume 0 + « (MUTED) » ;
bloquer = avatar caché + muet ; débloquer = tout revient ; code de salle : rejoint puis quitte.

**Bug trouvé et corrigé** : en mode partagé, le joueur indiqué par Fusion à la réception d'un message est le
DESTINATAIRE, pas l'expéditeur : chaque message porte maintenant le numéro de son expéditeur.

**Pas encore fait / pas vérifié** :
- Le micro n'a pas été testé réellement (niveau 0 dans l'éditeur : deux enregistreurs sur le même micro).
  La voix a été testée avec un son du jeu à la place du micro.
- Aucun test entre deux vrais appareils (casque + PC) : c'est le prochain test.

## Tous les joueurs chassés + nouveau transport (21/11/2026)
**Mesure qui a tout changé** : avec `SendReliableDataToPlayer` (l'envoi « fiable » brut de Fusion), A envoyait
1 128 messages en 6 s, B en recevait 98 (plus de 90 % perdus : c'est fait pour de gros fichiers, ~16 messages/s
passent). Les invités auraient vu la plupart des monstres immobiles et jamais reçu « attrapé ».

**Nouveau transport (objets réseau Fusion)** — menu **Horror/11** crée les prefabs dans `Resources/Net/` :
- `NetRelay` (un par joueur, créé par lui) : pose, bouche, mort, pseudo, couleur, identifiant stable en
  propriétés synchronisées ; bruits et événements en RPC.
- `MonsterRelay` (objet du master client : repris par le suivant s'il part) : positions/orientations des monstres.
- `PhotonFusionSession` garde la même interface : le reste du jeu n'a pas changé.

**Monstres** : ils ne parlent plus qu'à `PlayerTarget` (Scripts/Player) : le joueur local OU l'avatar d'un joueur
distant. Chaque monstre décrit son screamer (`IScreamer`). Chez celui qui fait tourner l'IA, attraper un joueur
distant envoie « Capture » (RPC) ; chez la victime, le screamer se joue avec SA copie du monstre.

**Vérifié (éditeur, 2 clients)** : pseudo + identifiant reçus ; 17 monstres synchronisés ; le Docteur de A attrape
l'avatar de B dans le jeu -> B reçoit « Capture, monstre n° 3 » une fois par capture ; bruit reçu ; voix connectée ;
départ de B : avatar et proie retirés ; en solo, le Docteur attrape toujours le joueur local (screamer joué).
**Pas vérifié** : entre deux vrais appareils ; le screamer chez la victime (B de test n'a pas de joueur).

## Le Grenier (22/11/2026)
- La Veilleuse est dans `MonsterSync` (position envoyée par celui qui fait tourner l'IA) ; elle chasse tous les
  joueurs du grenier (`PlayerTargets`), la capture d'un joueur distant passe par l'événement `Capture`.
- Les ampoules : événement `Ampoule` (n° stable = rang par position, allumée/éteinte) envoyé à chaque changement.
  **Limite** : pas d'état complet à l'arrivée ; un joueur qui rejoint en cours voit les ampoules de départ
  jusqu'au prochain changement. Le grenier vide depuis 20 s revient à l'état de départ (chez tout le monde).
- Les cassettes et le projecteur sont locaux (chacun écoute/regarde de son côté).

## Âge du compte (22/11/2026)
- `AgeGate` (Scripts/Net) : tranche d'âge du compte Meta (enfant / ado / adulte / inconnu), gardée en cache.
  Compte **enfant** : chat vocal coupé dans les deux sens (micro jamais envoyé, voix des autres muettes), le
  tableau PLAYERS l'indique. Âge **inconnu** : rien n'est bloqué (règle de Meta).
- `MetaAgeProvider` lit l'âge via le SDK Platform de Meta (Get Age Category API). **Pas encore actif** : il faut
  installer le paquet « Meta XR Platform SDK », régler l'App ID dans ses paramètres (jamais dans le code), ajouter
  le symbole `CALM_META_PLATFORM`, et déclarer l'API dans le Contrôle de l'utilisation des données (Meta).
- Test : argument `-age child` ou PlayerPrefs `horror.age.override` = 1 (enfant), 2 (ado), 3 (adulte), 0 (aucun).
