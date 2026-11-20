# Tester le jeu dans Unity (guide pas à pas)

Tout le code compile, mais rien n'a encore tourné dans Unity : ce guide sert à faire le premier
vrai test. Note chaque erreur rouge de la console et envoie-la.

## 0. Préparer le projet (une seule fois)
1. Unity Hub : projet **3D (URP ou Built-in)**, version **LTS récente** (Unity 6 ou 2022.3).
2. Copie le dossier `Assets/Horror` du dépôt dans ton projet.
3. **Window > Package Manager** → installe :
   - **XR Plugin Management** puis, dans *Project Settings > XR Plug-in Management*, coche **OpenXR**
     (onglet PC) et ajoute le profil **Oculus Touch Controller** ;
   - **glTFast** (`com.unity.cloud.gltfast`) : sans lui, les modèles `.glb` (poupée, monstre, map) ne s'importent pas.
4. Attends la fin de la compilation : **la console doit être sans erreur rouge**.

## 1. Le parcours de test (salle grise)
Lance les menus **dans l'ordre** (menu "Horror" en haut de Unity) :

| Menu | Ce qu'il ajoute | À vérifier dans le casque |
|---|---|---|
| **1** puis **2** | Le visage + la bouche (écran, sans casque) | Parle : la bouche bouge |
| **3** | Salle de test + joueur | Pousser le sol = avancer, sauter, grimper, se suspendre. Pencher la tête = glisser |
| **4** | L'Écoute + bruits du joueur | Taper fort / crier : elle tourne la tête puis fonce. Anneaux de bruit au sol |
| **5** | Salle de jeux (zone sûre), boutique, sons, jumpscare | Tu apparais sur le tapis rose, berceuse. Se faire attraper = jumpscare puis retour ici |
| **6** | Papiers de lore, attraper/lancer | Grip pour prendre un papier, l'approcher du visage pour le lire. Lancer une boîte |
| **7** | Le vrai personnage (poupée) + animation du monstre | Regarde tes mains, gâchette/grip plient les doigts |

## 2. L'Institut jouable (la vraie map)
**Menu 8** : crée la scène `Assets/Horror/Scenes/Institut.unity` avec tout : collisions, nuit,
ambiances par pièce, Salle de jeux (départ, zone sûre, boutique, journal), 9 portes à boutons,
17 papiers, balles à lancer, L'Écoute qui fait sa ronde de pièce en pièce, et toi.

À vérifier :
- tu apparais dans la Salle de jeux (lumière chaude), la porte du hall est devant toi ;
- appuyer sur un bouton rose à côté d'une porte la ferme / l'ouvre (et ça fait du bruit !) ;
- L'Écoute part du fond du dortoir, fait sa ronde, ouvre les portes avec ses clés ;
- les papiers sont lisibles quand tu les approches de ton visage ; le journal de la Salle de jeux compte ;
- on peut descendre l'escalier du Standard vers la salle des machines et la piscine ;
- l'encre de la piscine ne porte pas : on s'y enfonce (une échelle permet de ressortir) ;
- le **Minitel** du comptoir de l'accueil (4 touches : ◄ ► OK BACK) : change ton pseudo, la couleur de
  ta poupée, et le micro (OPEN / PUSH TO TALK avec A ou X / MUTED). En MUTED, L'Écoute ne t'entend plus parler.

## 3. Si ça ne marche pas
| Problème | Piste |
|---|---|
| Erreurs rouges à l'ouverture | Envoie-les : c'est le plus utile |
| "Le modèle n'est pas importé" | glTFast n'est pas installé |
| Image figée / rien dans le casque | OpenXR pas coché, ou Quest Link pas lancé |
| Mains qui tremblent | Vérifie que Time > Fixed Timestep suit le casque (fait par ArmLocomotion) |
| Le monstre traverse les murs | Il se déplace de point en point : vérifie la console, et envoie une capture de la vue Scène |
| Glisser donne mal au cœur | Décoche `LeanSlide` sur l'objet "Joueur" |
| Trop facile / trop dur | Sur L'Écoute : `hearingMultiplier` (ouïe), `chaseSpeed` (vitesse) |
