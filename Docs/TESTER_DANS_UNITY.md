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

## Lobby, but du mode Exploration, screamers sur fond noir, décor (menu 8)
1. **Lobby** : dans la Salle de jeux, mur sud à droite de la porte, « CHOOSE YOUR GAME » : EXPLORATION
   (disponible), INK TAG et HIDE & SEEK (« ONLINE SOON », arriveront avec le multijoueur).
2. **Objectifs** (à gauche de la porte) : PAPERS x/17, PLACES x/14. Chaque nouveau lieu s'annonce devant
   tes yeux (ex. « - THE LAUNDRY - »). Tout trouvé -> la machine 23 de la Laverie s'éclaire, le masque
   blanc du 23e enfant y attend, +500 crayons (une fois).
3. **Screamers** : le décor disparaît (fond noir), seul le monstre entier fonce sur toi (bras compris),
   avec son cri à lui (L'Écoute : crissement métallique ; les Doux : rire de jouet qui déraille ;
   Le Drap : râle mouillé). Pour retester la progression : supprime les clés « horror.place.* » et
   « horror.fin23 » des PlayerPrefs (Edit > Clear All PlayerPrefs efface tout, crayons compris).
4. **Décor** : dessins d'enfants encadrés de travers, guirlandes de fanions, peluches et cubes oubliés,
   dans toutes les pièces. **Laverie** : l'eau est maintenant aux chevilles.

## Les 8 chantiers : ambiances, Envers, loge, téléphone, dessin, Quest, réseau, vidéo (menu 8)
1. **Appliques** : une lampe murale par pièce, lumière chaude qui grésille. Au casque : est-ce assez
   lisible sans être éclairé comme en plein jour ?
2. **Ambiances** (jamais de musique là où un monstre chasse) : dortoir = respirations d'enfants qui dorment,
   Standard = lignes qui grésillent, couloir = horloge, hall = courant d'air, Laverie = eau + gouttes.
3. **La loge** (derrière la toile de fond du théâtre) : un Doux sans tête face au miroir, sa tête posée sur
   la coiffeuse, et 8 « peaux » de Doux pendues au portant. Ampoules du miroir qui grésillent.
4. **Le téléphone du Standard** : sonne au hasard (toutes les 1,5 à 3,5 min, 12 s). La sonnerie est un
   BRUIT : L'Écoute vient. Appuie sur le combiné pendant qu'il sonne : une voix chuchote et une phrase de
   l'histoire s'écrit au-dessus (6 messages, dans l'ordre). Pour tester vite : clic droit sur
   « Téléphone du Standard » > StandardPhone > Faire sonner.
5. **Le dessin qui change** (dortoir) : regarde-le, détourne la tête 6 s en restant à moins de 9 m,
   regarde à nouveau : ce n'est plus le même dessin (petit grattement de crayon).
6. **L'Envers** (récompense de la fin) : à la machine 23 éclairée, bouton rose « CRAWL IN » -> pièce de
   papier blanc tout dessinée au crayon, dessins géants, message final. Pas de monstre. Bouton « BACK » ->
   retour Salle de jeux. Elle est très haut au-dessus de la carte (sous -30 m, le jeu ramène au spawn).
7. **Screamers** : nouveaux cris GRAVES (impact sub-basse + rugissement saturé, un par monstre) et
   lumière de face (le visage de L'Écoute se lit enfin). Au casque : sursaut ou pas ? Volume trop fort ?
   Anciens cris gardés dans VR-fangame-GTAG_sauvegardes/audio_jumpscare_v1. Refaire : Tools/Audio/cris_graves.sh.
8. **L'Écoute allégée** : 124 000 -> 62 500 triangles (robe, chignon, cornets moins découpés), même allure.
9. **Réseau** : rien de visible (le jeu reste solo). Voir Docs/MULTIJOUEUR.md pour Photon.

### Filmer une vidéo de vrai gameplay (sans casque)
En Play, un script crée `Horror.Tests.FrameRecorder.Begin(dossier, caméra de la tête)` puis enchaîne des plans
(`Glide`, `Hold`, `FollowHead` + `Wait` pour les screamers) ; chaque image est enregistrée en JPG 1080 x 1920 à
30 images/s de temps de jeu. Montage : `python Tools/Trailer/make_gameplay.py <dossier> <sortie.mp4>`
(textes, sons du jeu recalés, logo de fin). Résultat : Marketing/calm_faces_gameplay_tiktok.mp4.

## Mort, poursuite, vidéo POV (menu 8)
1. **Mourir** : après le screamer, ~2,5 à 3 s de noir (au lieu de 0,6) : une voix d'enfant chuchote une phrase
   de l'histoire, sous-titrée devant tes yeux (10 phrases, jamais la même deux fois de suite), puis la Salle de jeux.
   Au casque : le noir est-il trop long ? Le sous-titre se lit-il bien (taille, distance 1,6 m) ?
2. **La rature** : là où tu es mort, une giclée d'encre et une flaque noire au sol (25 s), avec un son
   d'éclaboussure. En ligne, c'est ce que les autres verront (événement réseau « Mort », déjà branché).
3. **Après une prise** : L'Écoute reste penchée 4 s sur l'endroit (sourde et immobile) avant de repartir ;
   un Doux reste planté 4 s puis regagne sa place (seulement quand personne ne le regarde).
4. **La poursuite** : L'Écoute court à 4,9 m/s (au lieu de 6,2). Si elle n'entend plus rien depuis 1 s, elle
   ralentit ; après 2,5 s sans un bruit, elle te perd et va fouiller le dernier endroit. Les bruits faibles et
   lointains ne la remettent pas sur ta piste. À tester : la fuir en ramant fort (dur), puis se figer (elle doit
   passer à côté). Réglages : BlindMonsterAI > chaseSpeed, unsureSpeedFactor, chaseLoseTime, chaseMinAudibility.
5. **Correctif** : elle se bloquait contre les meubles en pleine chasse (le pouf du hall) ; 19 points de passage
   étaient plantés dans des meubles. Elle longe maintenant les obstacles et ne grimpe plus dessus.

### Filmer la vidéo POV
En Play : `Horror.Tests.PovScareDirector.Begin("<dossier>")` (réglages : playerStart, monsterStart, strokes,
whisper), puis `python Tools/Trailer/make_pov.py <dossier> <sortie.mp4>`. Résultat : Marketing/calm_faces_pov_tiktok.mp4.
6. **La charge** : à moins de 7 m du dernier bruit, L'Écoute fonce à 7,5 m/s et ne lâche plus (si tu t'es éloigné
   sans bruit, elle charge l'endroit vide). Réglages : chargeSpeed, chargeDistance.
7. **Screamer** : son visage (le portrait aux yeux qui pleurent) est enfin visible : il est visé depuis le crâne
   (avant, câbles et cornets faussaient la visée et on voyait sa nuque) et éclairé de face.

## Boîte à musique leurre et miroir de la loge (menu 8)
1. **Boîte à musique** (Salle de jeux, grand dortoir, Standard) : prends-la, lance-la loin ou pose-la. Une fois
   immobile, elle joue ~18 s (la danseuse sans visage tourne) : c'est un leurre, L'Écoute fonce dessus au lieu
   de toi. Puis le ressort casse (« tchac ») et elle réapparaît à sa place ~2,5 min plus tard, si personne n'est
   à côté. Dans la Salle de jeux elle ne sert à rien (zone sûre) : il faut l'emporter. Test rapide : clic droit
   sur une boîte > MusicBoxLure > Jouer.
2. **Le miroir de la loge** (au-dessus de la coiffeuse) : approche-toi, il reflète la loge... avec une poupée
   juste derrière toi, qui n'existe que dans le reflet (retourne-toi : personne). Chaque fois que tu détournes
   les yeux du miroir, elle se rapproche. Un petit rire d'enfant la première fois. Au casque : le reflet n'est
   pas en stéréo (vieux miroir terne) : dis-moi si ça gêne. La caméra du reflet ne tourne que si tu es à moins
   de 4,5 m (rien ne coûte au Quest le reste du temps).

## Performances (menu 8 puis menu 9)
Après chaque menu 8, lance **Horror > 9 - Préparer pour le Quest** (occlusion culling : les pièces cachées
derrière les murs ne sont plus dessinées). Les poupées de décor utilisent une version allégée (18 k triangles au
lieu de 60 k), la Laverie est allégée, et le décor fixe est regroupé. Détails et chiffres : Docs/PERF_QUEST.md.
Au casque : rien ne doit « apparaître » en tournant la tête au coin d'une porte (sinon, dis-le moi).

## L'infirmerie du sous-sol et Le Docteur (menu 8, puis menu 9)
**Accès** : dans la salle des machines (sous-sol), une cage grillagée « INFIRMARY v » : bouton rose -> le
monte-charge grince et descend. Bouton « MACHINES ^ » pour remonter.
**La zone** (~50 x 90 m) : hall du monte-charge, long couloir, salle d'attente (petites chaises tournées vers
le mur), 3 cabinets, la salle des malades (12 lits à sangles, rideaux, deux enfants « en observation »), le bloc
opératoire, les archives (dossiers des enfants : comment Céleste, Odette et les Doux sont devenus des monstres),
la chambre froide (noir complet, poupées assises en rang) et l'isolement (griffonné). Des couloirs en boucle.
**Le Docteur** (masque de corbeau, lanterne verte) :
1. Il **voit** ce que sa lanterne éclaire (cône de 55°, 10 m, le faisceau vert est visible) ; tout près (2 m),
   il te sent même de dos. Murs et rideaux coupent sa vue ; allongé immobile dans un lit, tu es invisible.
2. Il est **sourd** aux petits bruits (tes pas, ta voix normale) : seuls les **fracas** et les **cris** l'attirent.
3. Il **fredonne** une comptine en patrouillant. Quand il **se tait** et claque du bec : il t'a vu.
4. En traque il COURT (5,6 m/s) ; caché 4 s, il te perd et fouille le dernier endroit.
5. Screamer unique : croassement + bec, il fonce de face (yeux rouges, bec cousu).
**Objets fragiles** (plateaux d'instruments, bocaux, pieds à perfusion, dans les salles ET dans les couloirs) :
les frôler les fait tomber avec un fracas énorme (34 m). Le Docteur l'entend même à travers un mur. Ils se
remettent en place 3 min plus tard, hors de vue. Test : clic droit sur un « Chute_* » > FragileNoisy > Faire tomber.
**Son chariot** : en ronde, il pousse un chariot d'instruments devant lui, une main sur la poignée, la lanterne
levée dans l'autre (bras placés par le script, mains à ~5 cm de la poignée). La roulette couine et les instruments
tintent : on l'entend venir de loin, même quand sa comptine est couverte. Dès qu'il te voit ou part voir un bruit,
il le **lâche** (« clang ») : le chariot reste planté au milieu du couloir, et son plateau s'écrase si tu le frôles
en fuyant (fracas de 34 m, il revient droit dessus). De temps en temps (40 à 75 s), il le **gare** et continue sans
lui : plus de couinement, il est silencieux... jusqu'à ce qu'il revienne le chercher (45 s plus tard). Il ne
reprend pas un chariot dont le plateau est par terre (il attend qu'il se remette en place).
Test sans casque : Play, sélectionne « Chariot du Docteur » > DoctorCart (Attached), et appelle GoTo sur le Docteur.
S'il reste coincé entre un meuble et un mur (moins de 25 cm en 2,5 s), il abandonne ce point et passe au suivant.
Au casque : la zone est-elle assez éclairée pour s'orienter ? Le faisceau du Docteur se voit-il assez tôt ?
**Sonnette de l'accueil** (comptoir du hall de l'infirmerie, « RING FOR THE DOCTOR ») : appuie, « ding ! », le
Docteur remonte vers l'accueil. Une diversion : tu fouilles ailleurs pendant ce temps... mais il faudra repasser
par l'accueil pour reprendre le monte-charge. **Page de Mila** : au sol de l'isolement, sous une lumière qui tremble.

## Démarches, vitesses, voix, screamers « double coup » (menu 8 puis 9)
1. **Démarches** : le Docteur marche (rebond, roulis) et COURT (penché en avant, bras tendus, lanterne brandie) ;
   le Doux marcheur se dandine puis trottine ; L'Écoute a gagné du poids (rebond, roulis). Pose « image par image ».
2. **Vitesses** : L'Écoute 5,8 m/s (charge 8,5 dans les 7 derniers mètres), Docteur 5,6, Doux 3,6, Drap 4 m/s.
   Il faut vraiment ramer vite. Trop dur ? Baisse chaseSpeed / huntSpeed / walkSpeed sur le monstre.
3. **Voix** : chuchoter = rien ; parler = ~5 m (L'Écoute vient si elle est près) ; CRIER = 30 m : L'Écoute
   te chasse, le Docteur accourt en courant. Ta bouche (FaceScribble) suit toujours ta voix.
4. **Screamers** : le monstre fonce, s'ARRÊTE à 60 cm et te fixe en silence 0,4 s, puis se jette sur toi avec le
   cri (Jumpscare > stareTime, 0 = l'ancien screamer). Visage éclairé quelle que soit la pose.
5. **Infirmerie habillée** : feuilles et dessins par terre, encre, peluches, dessins aux murs, griffonnages qui
   donnent des conseils (« HE CAN'T SEE IN THE DARK »), planning des tests, notes, fiches des lits, toise.

## Confort, screamers avec relief, poupées fusionnées (menu 8 puis 9)
1. **Panneau COMFORT** (Salle de jeux, sous le tableau des objectifs) : bouton FLASH = flashs rouges ON/OFF ;
   bouton SCARE = FULL (double coup, cri fort) ou SOFT (screamer court, sans arrêt, cri moins fort). Mémorisé.
2. **Screamers** : lumière par en dessous + lumière de côté en hauteur (des ombres sur le visage, plus effrayant
   qu'un visage blanc et plat). Au casque : assez lisible ? trop sombre ?
3. **Poupées fusionnées** : enfants du dortoir (la tête tourne toujours), peaux de la loge, poupées de
   l'infirmerie : un maillage par matière. Dortoir 747 -> 509 appels de dessin, loge 211 -> 157.
