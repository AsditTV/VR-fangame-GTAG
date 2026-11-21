# Idées pour CALM FACES (horreur, cohérentes avec le lore)

Inspirées de mécaniques qu'on retrouve dans les fan games d'horreur en VR (locomotion aux bras),
mais réécrites pour notre monde : poupées de tissu, papier dessiné, encre, l'Institut Marivaux,
les 23 enfants. Rien n'est copié (ni nom, ni personnage, ni asset).

## Déjà dans le jeu (menu 8)
| Idée | Ce que ça fait | Pourquoi c'est cohérent |
|---|---|---|
| **Les enfants du dortoir** | 6 poupées assises sur les lits ; leur tête te suit quand tu ne regardes pas | Les 23 enfants de l'Institut ; « 1, 2, 3, soleil » comme les Doux |
| **Faire la poupée** (cachettes) | Sur un lit du dortoir, tes bruits portent 3x moins loin (`HideZone`) | Elle est aveugle : immobile parmi les poupées, tu es une poupée de plus |
| **Coupures de courant** | Toutes les 2,5 à 5 min, les lumières grésillent puis s'éteignent 6 à 11 s ; elle entend mieux dans le noir ; le retour du courant fait un « clac » qu'elle entend (`BlackoutDirector`) | Vieux bâtiment, Standard abandonné ; pas de lampe torche |
| **Traînées d'encre** | Flaques humides le long de sa ronde | Sa robe trempée d'encre goutte : le décor raconte où elle passe |
| **La Laverie + Le Drap** | Derrière la porte 23 : salle inondée, monstre qui SENT LE SOL ; on traverse par les machines et les fils à linge ; chaque monstre a son territoire (`Territory`) | Odette la lingère ; l'eau dissout l'encre de Céleste ; le lore se lit dans le décor (chemises, machine 23, panneaux) |

| **L'infirmerie + Le Docteur** | Sous la salle des machines (monte-charge) : ~50 x 90 m, couloirs en boucle ; le Docteur voit ce que sa lanterne éclaire, n'entend que les fracas, fredonne et se tait quand il t'a vu ; objets fragiles très bruyants | Le lieu des « tests » : les dossiers expliquent comment Céleste, Odette et les Doux sont devenus des monstres |
| **Mort chuchotée, boîte à musique, miroir, téléphone, dessin qui change** | Voir TESTER_DANS_UNITY.md | Le lore se découvre hors des journaux |

## Prochaines idées (par ordre d'intérêt / effort)
1. **Le jeu du chat-encre (multijoueur, Photon)** : un joueur attrapé par L'Écoute ne quitte pas la
   partie, il devient « taché » (poupée noircie d'encre) et ses bruits attirent le monstre pendant 30 s.
   Les autres peuvent le « laver » à la fontaine de la Salle de jeux. Mode de base des fan games
   (le chat), mais retourné : on ne chasse pas les autres, on les met en danger.
2. **La boîte à musique leurre** *(fait)* : un objet qu'on remonte (grip + tourner) puis qu'on lance ; elle joue
   20 s et L'Écoute y va. Coûte des crayons à la boutique (consommable), cohérent avec l'économie.
3. **Le Standard qui appelle** *(fait)* : de temps en temps, un téléphone sonne dans une pièce au hasard. Décrocher
   = un papier de lore lu à voix basse (enregistrement)… mais la sonnerie attire L'Écoute. Choix risqué.
4. **Les dessins qui changent** *(fait)* : les dessins d'enfants aux murs ont une version « après » (le monstre
   dessiné plus près, puis plus là). Changés quand aucun joueur ne regarde (même script que les Doux).
5. **Le miroir de la loge** *(fait)* : dans le reflet, une poupée de plus derrière toi (seulement dans le reflet).
6. **Nouvelles zones** *(Laverie, Envers et infirmerie faites ; reste le Parc et le plateau télé)* (déjà prévues dans MAP_design) : la **Laverie** (machines qui tournent = bruit
   de fond qui masque tes pas, mais aussi les siens), le **Parc** (extérieur, brouillard, manège qui
   grince), le **Plateau télé des Doux**, et **l'Envers** sous la piscine (la map vue « dessinée »,
   tout en traits de crayon, accessible seulement en finissant les papiers).
7. **Événements de saison** (comme les fan games qui retiennent leurs joueurs) : la nuit de la fête de
   l'Institut (guirlandes de papier, chapeaux pointus gratuits), un cosmétique par événement.
8. **Le classement « temps sans bruit »** (PlayFab) : temps passé dans la zone dangereuse sans être
   entendu. Plus intéressant qu'un simple temps de jeu, et ça récompense le style de jeu du jeu.

## À éviter (incohérent ou risqué)
- Lampe torche, armes, monstre qui voit : ça casse le principe (elle est aveugle, tu es dans le noir).
- Jumpscares gratuits à répétition : la peur vient de l'attente (silence, coupures, têtes qui tournent).
- Copier une mécanique ou un nom précis d'un autre jeu : on garde les idées générales seulement.

## Après l'infirmerie (idées pour la suite)
- **Le chariot du Docteur** : il pousse parfois un chariot qui grince (on l'entend de loin) et le laisse dans un couloir : un obstacle bruyant de plus.
- **La page arrachée de Mila** : cachée dans l'isolement, elle ouvre une fin alternative (qui était le 23e enfant ?).
- **Le Parc** (extérieur, brouillard) : un manège qui grince tout seul, les Doux y jouent à « 1, 2, 3, soleil » en plein air.
- **Coop** : un joueur fait le guet avec le miroir (il voit ce que les autres ne voient pas), l'autre fouille.
