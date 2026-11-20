# Prompt pour continuer CALM FACES dans une session Claude locale

Copie tout ce qui est dans le bloc ci-dessous dans ta nouvelle session (ouverte dans le dossier du dépôt).

```
Tu reprends mon projet de jeu VR Unity « CALM FACES ». Tout le travail précédent a été fait dans une
session cloud, SANS jamais lancer Unity : le code compile (vérifié avec dotnet + DLL Unity), mais rien
n'a encore tourné dans l'éditeur. J'ai maintenant Unity installé sur ce PC. Réponds-moi en français.

## Le dépôt
- GitHub : AsditTV/VR-fangame-GTAG, branche `claude/gallant-einstein-gspzcf` (travaille sur cette branche
  ou une branche dérivée ; ne pousse pas sur main sans me demander).
- Lis d'abord : `Docs/TESTER_DANS_UNITY.md` (menus Horror 1 à 8, ce qu'il faut tester),
  `Docs/MAP_design.md` (la map et toutes ses mises à jour), `Docs/LORE_proposition.md` et
  `Docs/LORE_papers_EN.md` (l'histoire), `Docs/ECONOMIE.md` (monnaie, boutique), `Marketing/` (logo, vidéos).

## Le jeu (ce que je veux)
- VR pour Meta Quest 3, surtout en PCVR via Link. Locomotion aux bras comme les fan games de Gorilla Tag,
  mais TOUT est original (aucun asset, nom ou personnage copié de Gorilla Tag ou d'un autre jeu).
- Horreur « cartoon pastel qui devient creepy », pas de néons criards, rien de parfaitement rond ou carré :
  tout doit avoir l'air fait main (dessins, lettres, objets un peu tordus).
- Nos personnages : des poupées de tissu SANS JAMBES (sac de tissu qui flotte), visage = masque en papier
  dessiné qui gribouille quand on parle / crie (micro), doigts animés par gâchette et grip, tête qui
  penche = le corps penche et glisse. Collision entre joueurs (on peut monter sur la tête d'un autre).
- Monstres : L'Écoute (aveugle, entend les pas, la voix, les objets lancés ; murmure quand elle n'entend
  rien, réagit et fonce quand elle entend) ; les Doux (mascottes du théâtre qui ne bougent que quand on
  ne les regarde pas, « 1, 2, 3, soleil »). Plusieurs monstres rapides par zone à terme.
- Pas de lampe torche, zones très sombres. Lore raconté seulement par des papiers / secrets / le décor.
- Zone sûre (spawn) : Salle de jeux avec boutique de cosmétiques, boîte de 100 crayons toutes les 7 h,
  Minitel des réglages (salle privée, push-to-talk / micro ouvert / muet, couleur). Le monstre n'y entre
  jamais ; on y réapparaît après un jumpscare.
- Map « l'Institut » : hall, couloir des chambres, dortoir aux 23 lits, Standard, sous-sol en boucle
  (salle des machines, galerie des portes, grand ventilateur, citerne d'encre, douches, piscine d'encre),
  théâtre des Doux (rideau, coulisses, loge), refuges en hauteur (elle ne grimpe pas). Pièces assez
  grandes pour des poupées d'1 m (le monde est agrandi ×1,4 : WORLD_SCALE).
- Pas de selon la surface, jumpscare, tension (coeur dans les manettes, silence quand elle rôde,
  lumières qui grésillent, fausses alertes), textes du jeu en anglais.
- Plus tard : multijoueur (salles privées + chat de proximité, puis lobby public) avec Photon
  Fusion + Voice, classement du temps de jeu (PlayFab), achats réels via le store Meta/Steam.

## Règles de travail (obligatoires)
- Ne jamais supprimer ou écraser des assets sans me le dire. Ne jamais mettre de clé API dans le code
  ni me demander de la coller dans le chat.
- Commentaires du code en français. Pas d'asmdef inutile. Petites étapes, dans l'ordre.
- Après chaque changement : vérifier la compilation ET l'Editor.log (erreurs, avertissements).
- À la fin de chaque étape : me dire exactement quoi tester à la main dans le casque.
- Sois mon conseiller, pas mon assistant : si je me trompe, dis-le (« Je ne suis pas d'accord parce
  que... ; je ferais plutôt... ; le risque est... »), donne ton niveau de confiance (certain / probable /
  supposition), la vérité qui dérange en premier, sans phrases creuses.

## Priorité 1 : faire tourner le projet dans Unity (AVANT tout le reste)
1. Ouvre le projet, installe ce qu'il faut (glTFast pour les .glb, OpenXR + Meta Quest, XR Plugin
   Management). Lis l'Editor.log et corrige toutes les erreurs.
2. Lance les menus Horror 1 à 8 dans l'ordre, comme dans `Docs/TESTER_DANS_UNITY.md`.
   Le menu 8 construit la scène complète `Assets/Horror/Scenes/Institut.unity`.
3. Vérifie en Play (et avec moi dans le casque) : locomotion aux bras, mains, glisse, monstres,
   portes, papiers, boutique, Minitel, jumpscare. Liste ce qui ne marche pas et corrige.

## Priorité 2 : de BEAUX modèles 3D
Les modèles actuels sont générés en Python (trimesh) : corrects mais simples. Je veux de vrais beaux
modèles, détaillés, pas lisses, style poupée de tissu / papier / pastel creepy :
- notre personnage (Le Dessiné, sans jambes), L'Écoute, les Doux, les cosmétiques, les meubles et
  objets de la map, puis la map elle-même (garder le plan, le JSON des portes / pièces / réseau de
  passage et WORLD_SCALE, sinon les menus Unity cassent).
- Utilise Blender si tu peux le piloter (MCP Blender ou script Python de Blender), garde les mêmes noms
  de noeuds que les modèles actuels (Tete, Masque, Masque_Papier, Bouche, Epaule_G/D, Coude_G/D, Main_G/D,
  doigts, Buste, Cornet_*, etc.) car les scripts Unity les cherchent par leur nom.
- Montre-moi un aperçu (capture) de chaque modèle avant de remplacer l'ancien, et ne l'écrase pas
  sans me le dire (garde une copie).

## Outils
Utilise TOUS les skills et outils disponibles dans cette session (liste-les au début) : le skill / MCP
Unity s'il existe (éditeur, scènes, console, tests), Blender, génération d'images, etc. S'il manque un
outil utile (MCP Unity, MCP Blender), dis-moi lequel et comment l'installer avant de t'en passer.

## Ensuite (dans l'ordre, en me demandant à chaque étape)
- Corriger / améliorer ce que les tests en casque montrent.
- Multijoueur Photon (je créerai les comptes et les App ID moi-même ; les clés vont dans la config
  Photon de Unity, jamais dans le code).
- Nouvelles zones et monstres (laverie, parc, plateau télé, l'Envers), plus de papiers de lore.
- Vidéos TikTok avec du VRAI gameplay en casque (les scripts de rendu sont dans `Tools/Trailer/`).

Commence par me dire ce que tu as trouvé dans le dépôt et les outils disponibles, puis attaque la
priorité 1.
```
