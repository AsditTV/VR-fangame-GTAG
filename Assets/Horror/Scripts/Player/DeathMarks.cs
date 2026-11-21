using System.Collections;
using UnityEngine;

namespace Horror.Player
{
    /// <summary>
    /// Ce que les AUTRES voient quand quelqu'un meurt : le Dessiné est « raturé ». Une giclée d'encre noire jaillit
    /// (gouttes qui retombent), une flaque d'encre irrégulière s'étale au sol puis s'efface lentement, avec un son
    /// d'éclaboussure et de papier froissé. Le mort, lui, est déjà dans le noir (voir Jumpscare).
    /// En solo, la flaque reste visible à ton retour : on sait où on est mort. En ligne : déclenché par
    /// l'événement réseau « Mort » (Horror.Net.NetworkBootstrap).
    /// Un seul objet dans la scène (posé par le builder) ; DeathMarks.Spawn(position) depuis n'importe où.
    /// </summary>
    public class DeathMarks : MonoBehaviour
    {
        public Material ink;
        public AudioClip sound;
        [Tooltip("Durée de vie de la flaque (s) avant qu'elle ne s'efface.")]
        public float lifetime = 25f;

        private static DeathMarks _instance;

        private void Awake() { _instance = this; }
        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>Raturer quelqu'un à cet endroit (position des pieds ou de la tête : on cherche le sol).</summary>
        public static void Spawn(Vector3 position)
        {
            if (_instance != null) _instance.StartCoroutine(_instance.Mark(position));
        }

        private IEnumerator Mark(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point;
            if (sound != null) AudioSource.PlayClipAtPoint(sound, at + Vector3.up * 0.5f, 1f);

            var root = new GameObject("Rature (mort)");
            root.transform.position = at;
            // la flaque : quelques disques aplatis qui se chevauchent (bord irrégulier, comme une tache d'encre)
            var blobs = new Transform[6];
            for (int i = 0; i < blobs.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(b.GetComponent<Collider>());
                b.name = "Encre";
                b.transform.SetParent(root.transform, false);
                Vector2 o = i == 0 ? Vector2.zero : Random.insideUnitCircle * 0.45f;
                b.transform.localPosition = new Vector3(o.x, 0.004f + i * 0.0015f, o.y);
                b.transform.localScale = Vector3.zero;
                if (ink != null) b.GetComponent<Renderer>().sharedMaterial = ink;
                blobs[i] = b.transform;
            }
            // la giclée : des gouttes lancées vers le haut qui retombent
            var ps = new GameObject("Giclée d'encre").AddComponent<ParticleSystem>();
            ps.transform.SetParent(root.transform, false);
            ps.transform.localPosition = Vector3.up * 0.9f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f; main.loop = false; main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = new Color(0.05f, 0.03f, 0.06f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 80;
            var emission = ps.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.25f;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            if (ink != null) pr.sharedMaterial = ink;
            ps.Play();

            // la flaque s'étale vite...
            var sizes = new float[blobs.Length];
            for (int i = 0; i < sizes.Length; i++) sizes[i] = i == 0 ? 1.1f : Random.Range(0.3f, 0.65f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.5f, 3f);
                for (int i = 0; i < blobs.Length; i++) blobs[i].localScale = new Vector3(sizes[i] * k, 0.002f, sizes[i] * k);
                yield return null;
            }
            // ... reste, puis rétrécit doucement et disparaît
            yield return new WaitForSeconds(Mathf.Max(0f, lifetime - 3f));
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                float k = 1f - t / 3f;
                for (int i = 0; i < blobs.Length; i++) blobs[i].localScale = new Vector3(sizes[i] * k, 0.002f, sizes[i] * k);
                yield return null;
            }
            Destroy(root);
        }
    }
}
