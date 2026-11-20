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

## Boîte de crayons (100 toutes les 7 h) et bonus du lore
Après le menu 5 ou 8 : la boîte jaune de la Salle de jeux donne 100 crayons, puis affiche
`NEXT IN hh:mm:ss`. Pour retester : **Horror > Tests > Remettre la boîte de crayons à zéro**.
Détails, prix et calculs : `Docs/ECONOMIE.md`.

## Sous-sol en boucle, chaises volantes, petites portes (menu 8)
⚠️ Si la scène `Institut.unity` existe déjà, le menu 8 ne l'écrase pas : supprime-la toi-même
(ou renomme-la) pour la reconstruire avec la nouvelle maquette.
1. Descends au sous-sol : salle des machines -> galerie des portes -> ventilateur -> citerne ->
   douches -> piscine. Tu dois pouvoir faire le tour complet.
2. Approche-toi d'une chaise qui flotte : elle tremble, tombe (bruit), puis remonte ~10 s après ton départ.
3. Reste près des petites portes de la galerie : au bout d'un moment, toc toc toc (parfois une voix).
4. Le ventilateur tourne. Deux Écoutes : une en haut, une au sous-sol.

## Nouveautés à tester (menus 3, 5 et 8 ; supprime d'abord les anciennes scènes générées)
1. **Pas de jambes** : regarde-toi dans le miroir / sur le mannequin d'essai.
2. **Pas selon la surface** : tape sur le parquet, le carrelage de la piscine, le métal des
   passerelles, la moquette du hall. Le son change ET la portée du bruit (métal = on t'entend de loin,
   moquette = discret).
3. **Grimper sur un joueur** : le « Mannequin d'essai » (salle de test, et Salle de jeux de l'Institut)
   a la même coque qu'un joueur : pose tes mains sur sa tête et monte dessus.
4. **La peur** : approche-toi d'une Écoute sans qu'elle chasse : le coeur bat dans les manettes,
   ta respiration s'entend, l'ambiance se tait. Les lumières grésillent quand elle passe près.
   Reste 1 à 2 minutes au calme loin d'elle : une fausse alerte arrive (pas derrière toi, chuchotement...).
5. **Théâtre** : passe par la fente du rideau. Détourne les yeux du Doux du milieu, puis regarde-le :
   il a avancé. Laisse-le te toucher pour voir le jumpscare.
6. **Refuges** : monte au balcon du théâtre (caisses à gauche, ou colonnes) et fais du bruit : elle
   vient en dessous mais ne t'attrape pas, puis elle repart.

## Spawn agrandi + Minitel (menu 8, supprime l'ancienne scène Institut)
1. Tu apparais au centre de la Salle de jeux : boutique au fond, boîte de crayons à droite,
   **Minitel sur le petit bureau à gauche** (touches ◄ ► OK BACK).
2. Les pièces sont plus grandes (×1,4). Vérifie que les portes du Protocole Verrou couvrent bien
   les ouvertures et que le monstre passe partout.

## Nouveautés : peur dans l'Institut (menu 8, scène reconstruite automatiquement)
1. **Les enfants du dortoir** : 6 poupées assises sur les lits. Regarde-en une, détourne les yeux
   quelques secondes, regarde à nouveau : sa tête s'est tournée vers toi.
2. **Faire la poupée** : monte sur un lit du dortoir et tape doucement : L'Écoute t'entend beaucoup
   moins (tes bruits portent 3 fois moins loin). Dans l'allée, elle t'entend normalement.
3. **Coupures de courant** : toutes les 2,5 à 5 minutes, les lumières grésillent puis s'éteignent
   6 à 11 s (la Salle de jeux reste éclairée). Pendant le noir, elle entend mieux. Pour tester tout
   de suite : en Play, sélectionne « Coupures de courant » > clic droit sur le composant > Couper maintenant.
4. **Traînées d'encre** : des flaques humides sur sa ronde (hall, couloirs, dortoir...).
5. **Modèles** : nouveaux Dessiné (moufles), Écoute (griffes, épingles, vertèbres), Doux, cosmétiques.
6. **Sans casque** : `Assets/Horror/Scripts/Tests/PlaytestBot.cs` (ajouté au Joueur en Play) bouge
   tête et mains pour tester la logique (ramer, taper, appuyer sur un bouton, regarder).

## La Laverie et Le Drap (nouveau monstre, menu 8)
Au bout du couloir des chambres, la **porte 23** est maintenant ouverte : passage de service, puis
la Laverie inondée.
1. Monte sur le rebord d'entrée, puis traverse par les machines, la table, les tuyaux le long des
   murs et les fils à linge (on peut s'y suspendre). Ne touche pas l'eau.
2. Touche le carrelage (main dans l'eau, ou descends) : une bosse glisse sous l'eau vers toi puis
   Le Drap surgit -> jumpscare -> retour Salle de jeux. Debout sur une machine : il ne sent rien.
3. Lance une balle au fond de l'eau : il y va. Parle fort : il s'en moque.
4. Fais un bruit énorme dans la Laverie avec L'Écoute pas loin dans le couloir : elle n'entend rien
   et ne passe pas la porte 23 (territoire du Drap).
5. Lis les panneaux, les 23 chemises, la machine n°23.

## Screamers, Drap plus effrayant, Laverie habillée, étalonnage (menu 8)
1. **Jumpscare** (L'Écoute, Doux, Drap) : coupure au noir, le visage fonce sur toi, tremble par
   saccades sous des flashs rouges (jamais blancs), puis noir et Salle de jeux. Joueurs sensibles :
   sur « Joueur » > Jumpscare, décoche `strobe`.
2. **Le Drap** : en surgissant, son visage devient une bouche béante (trous noirs) et il se penche sur
   toi. Reste perché ~7 s sur une machine : sa tête sort de l'eau à côté de toi et te fixe, puis replonge.
3. **Laverie** : machines et sèche-linge pastel (Blender), table et linge plié, draps pendus, carrelage
   dessiné. Les collisions sont des boîtes (l'escalade reste fiable).
4. **Étalonnage** (toute la map) : ombres lilas, lumières pêche, vignette, grain, halo des lampes. Réglable
   dans Assets/Horror/Art/Map/Genere/Institut_Etalonnage.asset (s'il fait trop sombre au casque :
   Color Adjustments > Post Exposure).
