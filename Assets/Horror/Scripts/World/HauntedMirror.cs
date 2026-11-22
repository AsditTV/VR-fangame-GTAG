using UnityEngine;
using UnityEngine.Rendering.Universal;
using Horror.Locomotion;

namespace Horror.World
{
    /// <summary>
    /// Le miroir de la loge. Quand tu t'en approches, il reflète la pièce (une petite caméra, seulement à ce
    /// moment-là, pour ne rien coûter au Quest le reste du temps)... et dans le reflet, une poupée se tient juste
    /// derrière toi. Elle n'existe QUE dans le miroir (calque que la caméra du joueur ne voit pas) : tu te
    /// retournes, il n'y a personne. Elle se rapproche un peu à chaque fois que tu détournes les yeux du miroir.
    /// Reflet volontairement simple (pas de vraie stéréo) : c'est un vieux miroir piqué, flou, sombre.
    /// </summary>
    public class HauntedMirror : MonoBehaviour
    {
        public const int MirrorOnlyLayer = 29;

        public Renderer surface;
        public Transform ghost;
        public float activeRange = 4.5f;
        public int resolution = 320;
        public AudioClip sting;

        private Camera _cam;
        private RenderTexture _rt;
        private float _ghostBack = 1.6f;
        private bool _wasLooking, _stung;

        private void Start()
        {
            _rt = new RenderTexture(resolution, Mathf.RoundToInt(resolution * 1.5f), 16) { name = "Reflet du miroir" };
            var go = new GameObject("Caméra du reflet");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            // pas de rendu XR pour cette caméra (en URP, stereoTargetEye ne marche pas et affiche un avertissement)
            _cam.GetUniversalAdditionalCameraData().allowXRRendering = false;
            _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 25f;
            _cam.enabled = false;
            if (surface != null)
            {
                surface.material.mainTexture = _rt;
                if (surface.material.HasProperty("_BaseMap")) surface.material.SetTexture("_BaseMap", _rt);
                surface.material.mainTextureScale = new Vector2(-1f, 1f);      // un miroir inverse gauche et droite
                surface.material.mainTextureOffset = new Vector2(1f, 0f);
            }
            if (ghost != null)
            {
                foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = MirrorOnlyLayer;
                ghost.gameObject.SetActive(false);
            }
            // aucune caméra de joueur ne voit le calque du reflet
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (c != _cam) c.cullingMask &= ~(1 << MirrorOnlyLayer);
        }

        private void LateUpdate()
        {
            ArmLocomotion viewer = null; float best = activeRange;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null || p.head == null) continue;
                Vector3 to = p.HeadPosition - transform.position;
                if (Vector3.Dot(to, transform.forward) <= 0f) continue;          // derrière le miroir
                float d = to.magnitude;
                if (d < best) { best = d; viewer = p; }
            }
            bool on = viewer != null;
            _cam.enabled = on;
            if (ghost != null) ghost.gameObject.SetActive(on);
            if (!on) { _stung = false; _ghostBack = 1.6f; return; }

            // la caméra se place au reflet de ta tête et regarde le miroir
            Vector3 n = transform.forward, head = viewer.HeadPosition;
            Vector3 mirrored = head - 2f * Vector3.Dot(head - transform.position, n) * n;
            _cam.transform.position = mirrored;
            _cam.transform.rotation = Quaternion.LookRotation(transform.position - mirrored, Vector3.up);
            Bounds b = surface != null ? surface.bounds : new Bounds(transform.position, Vector3.one);
            float dist = Vector3.Distance(mirrored, transform.position);
            _cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(b.extents.y * 1.15f / dist) * Mathf.Rad2Deg, 20f, 90f);
            _cam.aspect = 1f / 1.5f;
            // la caméra est « derrière » le miroir (dans le mur) : on coupe tout ce qui est derrière la glace
            _cam.ResetProjectionMatrix();
            Matrix4x4 w2c = _cam.worldToCameraMatrix;
            Vector3 cp = w2c.MultiplyPoint(transform.position + n * 0.01f);
            Vector3 cn = w2c.MultiplyVector(n).normalized;
            _cam.projectionMatrix = _cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));

            // la poupée : juste derrière toi (dans le reflet), elle avance quand tu ne regardes pas le miroir
            bool looking = Vector3.Angle(viewer.head.forward, transform.position - head) < 30f;
            if (_wasLooking && !looking) _ghostBack = Mathf.Max(0.55f, _ghostBack - 0.35f);
            _wasLooking = looking;
            if (ghost != null)
            {
                Vector3 away = head - transform.position; away.y = 0f; away.Normalize();
                Vector3 feet = new Vector3(head.x, head.y, head.z) + away * _ghostBack;
                feet.y = Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, out RaycastHit fh, 4f, ~0, QueryTriggerInteraction.Ignore)
                         ? fh.point.y : viewer.transform.position.y;            // debout sur le plancher, pas enfoncée
                ghost.SetPositionAndRotation(feet, Quaternion.LookRotation(-away, Vector3.up));
            }
            if (looking && !_stung && sting != null)
            {
                _stung = true;
                AudioSource.PlayClipAtPoint(sting, head + Vector3.up * 0.1f, 0.35f);
            }
        }

        private void OnDestroy() { if (_rt != null) _rt.Release(); }
    }
}
