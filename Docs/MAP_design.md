# Map : l'Institut Marivaux (design v1)

## Direction artistique

**Règle d'or : plus on descend, plus le pastel pourrit.**
- **En surface** (hall, couloirs, école) : pastel délavé, formes rondes de dessin animé,
  papier peint à motifs enfantins, meubles trop grands ou trop petits. C'est mignon, mais faux.
- **Au milieu** (buanderie, standard, parc) : couleurs ternies, taches d'humidité, papier peint
  qui se décolle et laisse voir des dessins d'enfants en dessous.
- **En profondeur** (salle des machines, piscine, l'Envers) : l'encre noire envahit tout,
  coule des plafonds, remplit les sols. Les rares couleurs restantes sont des lumières.

**Lumière.** Pas de néons. Uniquement de petites sources colorées dans le noir : veilleuses roses
en forme de lune, lampes de chevet vert d'eau, voyants des machines, reflets sur l'encre.
Le joueur voit des silhouettes, jamais tout. Pas de lampe torche.

**Les visages gribouillés (signature visuelle du jeu).**
- Les dessins d'enfants affichés aux murs ont des visages qui **réagissent à ta voix**, comme le
  tien : quand tu parles près d'eux, leur trait se met à trembler ; quand tu cries, ils se
  rayent les yeux. Même technique que le visage du joueur (script FaceScribble).
- Utilité en jeu : ça te montre que tu fais trop de bruit avant que le monstre arrive.
- Utilité pour le lore : les dessins sont "vivants", comme les 23 visages.

**Modèles 3D.** Packs d'assets stylisés (cartoon / low-poly), recolorés en pastel. Le code ne
fabrique que les prototypes et le greybox.

---

## Raconter par le décor : ce que le joueur VOIT, sans texte

Les papiers donnent les faits. Le décor fait **ressentir** l'histoire, et on comprend en
regardant autour de soi.

### Hall de l'hôtel (départ)
- Un grand portrait de Céleste au-dessus de l'accueil. **Ses yeux ont été grattés** au couteau.
- La sonnette de la réception. Elle fait du bruit : premier piège, premier test.
- Un panneau de l'époque de l'hôtel, « Ici les enfants rient ! », recouvert par un panneau de
  l'institut cloué par-dessus : « Ici, on se tait. »
- Le **Minitel** de l'accueil (voir plus bas) : c'est l'écran des réglages et des salles privées.

### Couloirs des chambres
- Des portes numérotées de 1 à 23. Chaque porte a un panneau à boutons (Protocole Verrou).
- Derrière certaines portes : des chambres d'hôtel transformées en cellules. Les lits sont collés au mur.
- Au sol, des marques de craie : le veilleur comptait les portes. Les chiffres deviennent de plus en plus tremblés.

### Aile école + dortoir aux 23 lits
- Un tableau noir couvert de la même phrase recopiée en punition : « Je ne crierai plus. »
- **23 lits. Un masque en papier posé sur chaque oreiller. Un lit n'a pas de masque : c'est le tien.**
- Une toise au mur avec les tailles des enfants. Toutes les mesures s'arrêtent en novembre 1985.
- Des pupitres avec des feuilles « Dessine ton visage calme ». Certaines sont ratées, rayées, déchirées.

### Salle des machines (sous-sol)
- La pompe de la piscine porte une plaque : « Remplissage spécial, 12/11/1985 ». Des tuyaux noirs d'encre partent vers le haut.
- Le carnet du mécanicien est accroché à un clou (papier de lore).
- Les machines s'arrêtent toutes les X minutes : **silence total**. C'est là que l'Écoute descend.

### Piscine d'encre
- Un plongeoir. Une bouée d'enfant en forme de canard, dégonflée, sur le bord : celle de Mila.
- Panneau : « Interdit de crier près du bassin » (le règlement, après sa noyade).
- Des gradins avec les restes de la fête de 1977 : guirlandes pastel, une enceinte renversée.
  **La musique qui a couvert l'appel de Mila.** L'enceinte grésille parfois.

### Le Standard (salle d'écoute)
- Un mur de prises et de câbles, un micro par pièce, chacun avec une étiquette :
  « Dortoir », « Piscine », « Chambre 7 »...
- Un casque d'écoute posé sur la chaise de Céleste, encore chaud (il grésille).
- **C'est ici qu'on comprend comment elle entend tout.** Parler dans un micro fait sortir ta
  voix ailleurs : un leurre.

### Buanderie
- Des draps qui sèchent, chacun brodé d'un numéro d'enfant.
- Une machine à laver qui tourne seule, avec un masque en papier collé contre le hublot.

### Parc fermé + plateau télé des Doux
- Le carrousel des Doux. Un des chevaux a été remplacé par une mascotte assise, immobile.
- Le plateau télé : décors en carton, une caméra tournée vers le public. Une télé passe en
  boucle le générique des *Doux*, mais le son est coupé.

### L'Envers (zone secrète, sous la piscine)
- Tout est dessiné au crayon : murs, sol, meubles. Le décor est un dessin d'enfant en 3D.
- Les murs se gribouillent et s'effacent quand on parle.
- Au bout : **la chambre de Mila**, la seule pièce en vraies couleurs du jeu, figée en 1977.
  Le dernier dessin de Mila est sur le lit.

---

## Plan général (à valider)

```
                         [Plateau télé]
                               |
 [Buanderie] -- [Couloirs] -- [HALL] -- [Parc fermé]
                     |          |
              [Aile école]  [Standard]
                     |
            [Salle des machines]
                     |
              [Piscine d'encre] --(secret)--> [L'Envers] --> [Chambre de Mila]
```

- Le Hall est le **hub** : on y revient souvent, c'est la zone de départ et celle du Minitel.
- Les **portes à boutons** coupent les chemins. Les ouvrir fait du bruit et prend du temps.
- Les papiers sont placés dans l'ordre du lore : on trouve 1 à 3 dans le hall, la noyade près de la piscine en dernier, etc.

## Son : la moitié de la peur
| Zone | Fond sonore | Son qui annonce un danger |
|---|---|---|
| Hall | Horloge, vent | La sonnette de la réception |
| Couloirs | Lampes qui grésillent | Quelqu'un qui compte les portes |
| École | Craie qui grince au loin | Un masque qui tombe d'un lit |
| Salle des machines | Machines très fortes | **Le silence** quand elles s'arrêtent |
| Piscine | Gouttes, eau qui clapote | Une éclaboussure qui n'est pas toi |
| Partout | — | Le trousseau de clés de l'Écoute |

---

## Le Minitel : l'ordinateur des réglages (façon "ordinateur de Gorilla Tag")

Un vieux terminal beige posé sur le comptoir de l'accueil (logique pour un lieu fermé en 1985).
On tape sur ses grosses touches **avec le bout du doigt** (la gâchette plie l'index : le doigt
tendu sert à appuyer).

| Écran | Ce qu'on peut faire |
|---|---|
| SALLE | Taper un code pour rejoindre ou créer une **salle privée** ; plus tard, salle publique. |
| NOM | Changer son pseudo (filtré). |
| COULEUR | Régler la couleur du tissu de la poupée (3 curseurs R/V/B, couleurs pastel). |
| MICRO | **Ouvert** / **Appuyer pour parler** (bouton de la manette) / **Muet**. Volume des autres. |
| VISAGE | Choisir son numéro d'enfant (les visages débloqués, cosmétiques). |
| AIDE | Rappel des commandes. |

À savoir : le mode micro change le jeu. En **Muet**, l'Écoute ne t'entend pas parler, mais tu ne
peux pas prévenir les autres. C'est un vrai choix stratégique.

**Quand le coder :** à l'étape 6 (multijoueur), parce que les salles privées et le micro
dépendent du réseau (Photon Fusion + Photon Voice). Les réglages locaux (nom, couleur, mode micro)
seront sauvegardés sur la machine du joueur.

## Mise à jour : sous-sol en boucle, chaises volantes, portes d'enfants

**Salle des machines agrandie (24 × 14 m, 7 m de haut)** : 4 chaudières, le grand moteur
(volant d'inertie), une passerelle basse et une passerelle haute (échelle, on y grimpe aux bras).
Elle ne s'arrête plus : elle continue en **boucle** :

```
Escalier -> SALLE DES MACHINES -> Galerie des portes -> Grand ventilateur
               ^                                            |
               |                                            v
            Piscine <------- Couloir des douches <---- Citerne d'encre
```

Une boucle change tout pour la poursuite : on peut fuir en rond au lieu de finir dans un cul-de-sac.
Une **deuxième Écoute** patrouille le sous-sol (la première reste en haut).

- **Galerie des portes** : couloir bas (3,2 m), tuyaux, 11 petites portes (dont une au plafond).
- **Grand ventilateur** : 6,6 m de diamètre, il tourne lentement (`SlowSpinner`), son souffle en boucle.
- **Citerne d'encre** : 4 cuves reliées à la piscine, l'une fuit.
- **Couloir des douches** : les pommeaux coulent noir ; ramène à la piscine.

**Chaises qui volent (10)** : dortoir, hall, couloir, salle du ventilateur. Elles flottent et tournent.
Si un joueur s'approche à moins de 1,8 m : elles tremblent, puis **tombent** -> bruit -> le monstre
vient. Elles remontent seules quand plus personne n'est à côté (`FloatingProp`).

**22 petites portes d'enfants** (+ la porte 23 du couloir = les « 23 friends » du dessin) : prénom
au feutre, dessin, trou de serrure. Certaines sont normales, d'autres en hauteur, de travers,
à moitié dans le sol, à l'envers, au plafond. Elles ne s'ouvrent pas. Parfois on y **frappe** de
l'autre côté, et une voix d'enfant chuchote (`ChildDoorKnock`) ; les coups attirent le monstre.
