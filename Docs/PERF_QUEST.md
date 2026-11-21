# Performances : où on en est pour le Quest

Aujourd'hui le jeu tourne en **PCVR (Quest 3 + Link)** : les chiffres ci-dessous sont largement tenables sur PC.
Pour un **Quest autonome** (Meta Quest Store), il reste du travail. Confiance sur le diagnostic : *certain*
(mesuré en jeu) ; sur les budgets cibles : *probable* (ordres de grandeur habituels Quest 3 à 72-90 Hz).

## Mesures (une vue, compteurs de rendu Unity, après les allègements du 21/11/2026)

| Endroit | Appels de dessin | SetPass | Triangles visibles |
|---|---|---|---|
| Hall | 520 (1 304 avant l'occlusion) | 142 | 240 k (496 k avant) |
| Loge | 211 (501 avant) | 59 | 248 k (409 k avant) |
| Dortoir | 747 | 107 | 341 k |
| Laverie | 545 | 41 | 322 k |

Scène entière : 1,30 M -> 0,87 M triangles. Cibles indicatives Quest 3 autonome : ~150 SetPass, ~300 appels,
~500 k triangles visibles en stéréo, peu de post-traitement.

## Déjà fait
- L'Écoute : 124 k -> 62 k triangles. Le Dessiné (joueur) : 61 k -> 51 k ; **version PNJ** (`LeDessine_PNJ.glb`,
  18 k, mêmes noeuds) pour les poupées du dortoir, le miroir et le mannequin (`Tools/Blender/make_light.py`).
- Laverie : hublots 3 800 -> ~400 triangles (x32), draps 4 200 -> 900 (x15).
- **Static batching** de 1 042 objets de décor (menu 8, automatique).
- **Occlusion culling** : menu **9**, à relancer après chaque menu 8.

## Ce qu'il reste pour un Quest autonome (par ordre de gain)
1. **Profil de qualité « Quest »** : HDR off, pas de grain ni d'aberration chromatique, bloom léger ou off,
   MSAA 4x, rendu à 1.0. Le post-traitement est le plus gros coût caché sur Quest.
2. **Poupées et Doux en un seul maillage** (sauf la tête qui tourne) : une poupée = 48 morceaux = 48 appels.
   Le dortoir (6 poupées) coûte ~290 appels à lui seul.
3. **Machines de la Laverie** : GPU instancing (43 machines identiques).
4. **60 lumières temps réel** : passer les appliques en lumière précalculée (lightmaps) et ne garder en temps réel
   que celles qui grésillent près du joueur.
5. Build Android + profilage dans le casque (OVR Metrics Tool) : seul juge réel.

## Profil graphique Quest (automatique)
`Horror.Core.QuestQuality` (objet « Profil graphique » posé par le menu 8) : sur Android (Quest autonome) il coupe
le grain, l'aberration chromatique et le HDR, allège le halo, met le MSAA 4x. Sur PC rien ne change. Pour le voir
sur PC : cocher `forceOnPc` (les réglages PC sont remis en sortant du Play).

## Poupées fusionnées (fait)
`InstitutBuilder.Fusion.cs` (menu 8, automatique) : les poupées et Doux immobiles sont fusionnés par matière
(la tête des enfants du dortoir reste à part : elle tourne). Dortoir 747 -> 509 appels, loge 211 -> 157.

## APK pour le Quest autonome (menu 10)
**Prérequis (fait le 21/11/2026)** : module Android de Unity 6000.6.10f1 (Android Build Support + OpenJDK + SDK/NDK).
Attention : si l'installateur affiche « Failed to delete old Unity Android Support installation files », choisir
« Ignorer » puis réinstaller OpenJDK et SDK/NDK (le module principal efface ce qui a été installé avant lui).

1. Menus **8** puis **9** (scène + occlusion).
2. Menu **10 - Construire l'APK Quest** : configure OpenXR pour Android (chargeur OpenXR, Meta Quest, manettes Touch
   Quest 2/Pro/3, rendu Single Pass Instanced, latence « Prioritize Input Polling »), vérifie les réglages
   (identifiant `com.asdittv.calmfaces`, ARM64, IL2CPP, Vulkan, Android 12L minimum, textures ASTC) et compile
   `Builds/CalmFaces.apk` (~150 Mo, ~6 min ; dossier ignoré par Git). Menu 10b = configurer sans compiler.
3. Installer sur le casque (mode développeur activé dans l'appli Meta Horizon du téléphone, câble USB-C) :
   - **Meta Quest Developer Hub** : glisser l'APK sur le casque ; ou
   - en ligne de commande : `adb install -r Builds/CalmFaces.apk` (adb est dans
     `...\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`).
   Le jeu apparaît dans la Bibliothèque > « Sources inconnues ».
4. Mesurer : OVR Metrics Tool (images/s, GPU). Cible : 72 images/s stables minimum.

Sur Android le profil Quest est appliqué tout seul (`QuestQuality`, niveau de qualité « Mobile » : pas de SSAO,
pas de HDR, MSAA 4x). Le profil PC (avec SSAO) est exclu d'Android : l'alerte SSAO d'OpenXR au build le concerne
lui, pas le Quest.
