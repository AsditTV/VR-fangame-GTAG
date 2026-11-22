# Alpha test : donner le jeu à des testeurs (canal ALPHA de Meta)

Plan de test à leur envoyer (en anglais) : `Docs/ALPHA_TEST_PLAN_EN.md`.

## 1. Une seule fois : la clé de publication (obligatoire pour Meta)
Meta refuse un APK signé avec la clé de debug d'Unity. Il faut ta propre clé :
1. Unity → Edit → Project Settings → Player → Android → **Publishing Settings** → **Keystore Manager** →
   Keystore… → **Create New** → **Anywhere** : enregistre-la **hors du dépôt** (ex. `C:\Dev\cles\calmfaces.keystore`).
2. Mot de passe solide, alias `calmfaces`, ton nom ou « AsditSociety ». Garde les mots de passe dans un gestionnaire
   de mots de passe : **ne les tape jamais dans un chat, ne les mets jamais dans le code.**
3. **Sauvegarde le fichier de clé** (clé USB, cloud privé). Clé perdue = plus jamais de mise à jour possible du jeu.
4. `*.keystore` et `*.jks` sont ignorés par git (le dépôt est public : la clé ne doit jamais y aller).
5. À chaque nouvelle session Unity, retape les deux mots de passe dans Publishing Settings avant le menu 10
   (Unity ne les garde pas, c'est voulu).

## 2. Construire l'APK
Menus **8**, puis **9**, puis **10**. Le menu 10 augmente tout seul le numéro de version (Meta exige qu'il monte à
chaque envoi) et avertit si la clé de debug est encore utilisée.

## 3. L'envoyer sur le canal Alpha
Tableau de bord Meta → CALM FACES → Distribution → **Canaux de sortie** → **Alpha** → **Importer un nouveau build** →
`Builds/CalmFaces.apk`. Notes de version : « Alpha 1 ».

## 4. Inviter les testeurs
Canal **Alpha** → onglet **Paramètres** :
- Accès à la chaîne : **Privé** (seulement les invités).
- **Par e-mail** : l'adresse de leur **compte Meta** (celle du casque), qu'ils t'envoient en privé ; ou
- **Par URL** : copie l'URL d'invitation, mode **Approbation manuelle** (tu acceptes chaque personne : si le lien
  fuit, personne d'autre n'entre). L'URL expire 90 jours après le dernier envoi d'APK.
- Coche « Envoyer les notifications par e-mail » (ils sont prévenus à chaque nouvel APK).
- Enregistrer les modifications.

## 5. Côté testeur
1. Accepter l'invitation (e-mail ou lien), connecté au même compte Meta que le casque.
2. Sur le casque : Bibliothèque → CALM FACES → Installer. Les mises à jour arrivent toutes seules.
3. Suivre le plan de test et poster chaque bug dans un salon Discord privé (ex. #alpha-bugs, visible seulement
   par toi et les testeurs).

## Avant de les inviter : ce qu'ils doivent savoir
- Les signalements REPORT ne partent pas encore aux modos (relais Cloudflare pas mis en route).
- Pas de vérification d'achat (SDK Platform pas encore installé) : ne pas partager l'APK, rien ne l'empêche.
- Jeu classé PEGI 16 / ESRB Teen, avec chat vocal : des testeurs de **16 ans ou plus**.
