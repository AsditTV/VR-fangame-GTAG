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

## Prochaine étape
1. Installer **Photon Fusion 2** puis **Photon Voice 2** dans Unity (paquets officiels Photon / Asset Store).
2. Coller les App ID dans `PhotonAppSettings` (toi, depuis le tableau de bord : jamais dans le code ni le chat).
3. J'écris `PhotonFusionSession` et le branchement de Voice (volume de chaque voix = `ShouldHear`).
4. Test à 2 casques (ou 1 casque + l'éditeur) : rejoindre, se voir, s'entendre, couper/bloquer.
