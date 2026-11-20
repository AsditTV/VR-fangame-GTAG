# Économie de CALM FACES — les crayons

Nom du jeu : **CALM FACES** (le nom du programme de l'Institut dans le lore).
Monnaie : **les crayons** (`CRAYONS` en jeu). Tout est centralisé dans
`Assets/Horror/Scripts/Core/GameNames.cs` : changer le nom à un seul endroit suffit.

> ⚠️ Tant qu'il n'y a pas de serveur (PlayFab), **tout est sauvegardé sur l'appareil**
> (PlayerPrefs). Un joueur motivé peut tricher (avancer l'horloge, éditer la sauvegarde).
> C'est acceptable pour les tests, **pas pour une sortie avec de l'argent réel**.

---

## 1. Comment on gagne des crayons

| Source | Montant | Fréquence | Script |
|---|---|---|---|
| Départ | 100 | une fois | `PlayerWallet` |
| **Boîte de crayons** (Salle de jeux) | **100** | **toutes les 7 h** | `TimedReward` + `CrayonBoxReward` |
| Survivre hors de la zone sûre | 2 | toutes les 30 s | `CoinEarner` |
| Premier papier de lore trouvé | 25 | 13 papiers | `LorePaper` |
| Premier papier **secret** trouvé | 150 | 4 secrets | `LorePaper` |
| **Histoire complète** (13 papiers) | **1000** | une fois | `LoreJournal.CheckMilestones` |
| **Tous les secrets** (la vraie fin) | **2000** | une fois | `LoreJournal.CheckMilestones` |
| Achat réel (plus tard) | packs | — | store Meta / Steam + serveur |

**Total gagnable avec le lore :** 13 × 25 + 4 × 150 + 1000 + 2000 = **3925 crayons**, une seule fois.
C'est voulu : finir l'histoire paie presque un objet de prestige. Les joueurs qui explorent
sont récompensés, sans qu'on puisse « farmer » le lore.

### La boîte de crayons (100 toutes les 7 h)
- Première récolte disponible tout de suite.
- Le panneau affiche `FREE CRAYONS (+100)` puis `READY! PRESS THE BUTTON` ou `NEXT IN 06:42:13`.
- **Horloge reculée** → le compte à rebours repart de maintenant (pas de récolte gratuite).
- **Horloge avancée** → la triche marche encore. Solution : demander l'heure au serveur
  (PlayFab `GetTime` ou une CloudScript qui fait la récolte côté serveur). L'interface de
  `TimedReward` (`Remaining`, `CanClaim`, `TryClaim`) restera la même.

---

## 2. Les prix

| Objet | Emplacement | Prix |
|---|---|---|
| Moon Badge | poitrine | 300 |
| Ribbon Bow | tête | 600 |
| Party Hat | tête | 1000 |
| Yarn Scarf | cou | 1500 |
| **Paper Crown** | tête | **5000** (prestige) |

Source des prix : `Tools/Character/build_cosmetics.py` (maquette) et
`SafeZoneShopBuilder.Catalog` (Unity). ⚠️ Si les fichiers `CosmeticItem` existent déjà dans
ton projet, le menu ne les écrase pas : change le prix à la main dans l'Inspector
(ou supprime-les toi-même et relance le menu 5).

### Le calcul (sans payer)
- Boîte seule, en revenant 3 fois par jour : 300 crayons/jour → **5000 ≈ 17 jours**.
- Boîte 1 fois par jour : 100/jour → 50 jours.
- Une heure de jeu hors zone sûre ≈ 240 crayons (2 toutes les 30 s).
- Finir l'histoire + secrets : 3925 d'un coup.

Un joueur régulier a la couronne en **2 à 3 semaines**. Si les tests montrent que c'est trop
lent ou trop rapide, ne touche qu'à un réglage à la fois (le montant de la boîte en premier).

---

## 3. Argent réel (plus tard, pas encore codé)

Packs proposés (à ajuster) :

| Pack | Crayons | Idée de prix |
|---|---|---|
| Petit | 500 | ~0,99 € |
| Moyen | 1200 | ~1,99 € |
| Grand | 2600 | ~3,99 € |
| Énorme | 5500 | ~7,99 € |

Ce qu'il faut **avant** de vendre quoi que ce soit :
1. **Le paiement passe obligatoirement par le store** : Meta Horizon Store (Meta Platform SDK,
   achats « consumables ») ou Steam (Steamworks microtransactions). Pas de lien de paiement
   externe : c'est refusé par les stores.
2. **Un serveur qui vérifie l'achat** (PlayFab sait valider les reçus) et qui ajoute les crayons.
   Le jeu ne doit jamais se donner lui-même des crayons achetés.
3. **Le portefeuille sur le serveur**, plus dans PlayerPrefs, sinon un achat peut être perdu
   ou copié.
4. **Les joueurs mineurs** : beaucoup de joueurs de ce genre de jeu sont jeunes. Prix affichés
   clairement, pas de faux « compte à rebours d'offre », pas de boîtes surprises payantes
   (loot boxes) : c'est interdit ou encadré dans plusieurs pays.
5. **Équité** : uniquement du cosmétique. Rien qui aide contre les monstres.

Aucune clé (App ID, secret PlayFab, Steam) ne doit être écrite dans le code ou collée dans le
chat : elles iront dans la configuration du projet, hors du dépôt.

---

## 4. À tester dans Unity

1. Menu **Horror > 5** (ou **8** pour l'Institut) puis Play.
2. Appuie sur le bouton rose de la boîte : +100 crayons, son de pièce, le panneau passe à `NEXT IN 06:59:59`.
3. Rappuie : son « refusé », rien ne change.
4. Pour retester sans attendre 7 h : menu **Horror > Tests > Remettre la boîte de crayons à zéro**
   (Edit > Clear All PlayerPrefs marche aussi, mais remet TOUT à zéro : crayons, papiers, cosmétiques).
5. Lore : trouve tous les papiers normaux → le journal affiche `STORY COMPLETE +1000 CRAYONS`.
