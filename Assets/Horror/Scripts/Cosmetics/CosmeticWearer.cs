using System.Collections.Generic;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Fait porter au personnage les cosmétiques équipés (un par emplacement).
    /// À mettre sur le joueur. Points d'attache : le haut du crâne, le cou, la poitrine, le dos.
    /// Multijoueur (étape 6) : on enverra la liste des identifiants portés aux autres joueurs, et leur
    /// CosmeticWearer (avec isLocal = faux) affichera les mêmes objets via ApplyRemote().
    /// </summary>
    public class CosmeticWearer : MonoBehaviour
    {
        [Tooltip("Tous les cosmétiques du jeu (pour retrouver un objet à partir de son identifiant).")]
        public CosmeticItem[] catalog;

        [Header("Points d'attache")]
        public Transform head;
        public Transform neck;
        public Transform chest;
        public Transform back;

        [Tooltip("Faux = personnage d'un autre joueur (les objets viennent du réseau).")]
        public bool isLocal = true;

        private readonly Dictionary<CosmeticSlot, GameObject> _worn = new Dictionary<CosmeticSlot, GameObject>();

        private void OnEnable()
        {
            if (!isLocal) return;
            PlayerWallet.Changed += RefreshLocal;
            RefreshLocal();
        }

        private void OnDisable()
        {
            if (isLocal) PlayerWallet.Changed -= RefreshLocal;
        }

        private void RefreshLocal()
        {
            foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
                Wear(slot, Find(PlayerWallet.EquippedId(slot)));
        }

        /// <summary>Pour un autre joueur : liste des identifiants portés reçue par le réseau.</summary>
        public void ApplyRemote(IEnumerable<string> ids)
        {
            var bySlot = new Dictionary<CosmeticSlot, CosmeticItem>();
            foreach (string id in ids)
            {
                CosmeticItem item = Find(id);
                if (item != null) bySlot[item.slot] = item;
            }
            foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
                Wear(slot, bySlot.TryGetValue(slot, out CosmeticItem it) ? it : null);
        }

        private void Wear(CosmeticSlot slot, CosmeticItem item)
        {
            if (_worn.TryGetValue(slot, out GameObject old) && old != null) Destroy(old);
            _worn.Remove(slot);
            Transform anchor = Anchor(slot);
            if (item == null || item.prefab == null || anchor == null) return;

            GameObject go = Instantiate(item.prefab, anchor);
            go.transform.localPosition = item.offset;
            go.transform.localRotation = Quaternion.Euler(item.rotation);
            go.transform.localScale = Vector3.one * item.scale;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            SetLayer(go, gameObject.layer); // ne gêne pas les mains (calque du joueur)
            _worn[slot] = go;
        }

        private CosmeticItem Find(string id)
        {
            if (string.IsNullOrEmpty(id) || catalog == null) return null;
            foreach (CosmeticItem item in catalog)
                if (item != null && item.id == id) return item;
            return null;
        }

        private Transform Anchor(CosmeticSlot slot) =>
            slot == CosmeticSlot.Tete ? head : slot == CosmeticSlot.Cou ? neck : slot == CosmeticSlot.Poitrine ? chest : back;

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }
    }
}
