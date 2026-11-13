using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Gerak elang: berputar mengelilingi satu titik di langit + kepakan sayap
    /// prosedural. FBX BUR_Elang tidak punya animation clip, jadi bone sayap
    /// diputar langsung setiap frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElangFlight : MonoBehaviour
    {
        [Header("Orbit")]
        [Tooltip("Pusat lingkaran (x/z dipakai, y diabaikan).")]
        public Vector3 center = new Vector3(0f, 0f, -100f);
        public float radius = 50f;
        [Tooltip("Ketinggian di atas permukaan tanah di titik pusat.")]
        public float altitude = 45f;
        public float speed = 10f;
        public bool clockwise = true;
        public float startAngle = 0f;
        public float bankAngle = 18f;
        public float bobAmplitude = 0.8f;
        public float bobSpeed = 0.6f;

        [Header("Kepakan")]
        public float flapSpeed = 1.8f;
        [Range(0f, 1f)] public float flapIntensity = 1f;
        public float flapUpAngle = 40f;
        public float flapDownAngle = 24f;
        public float forearmAngle = 12f;
        public float handAngle = 10f;
        public float glideLift = 5f;
        public float flapPhase = 0f;
        [Tooltip("Balik arah kepakan kalau sayap kiri/kanan bergerak berlawanan.")]
        public bool mirrorFlap = false;

        [Header("Bone")]
        public string leftWingBone = "wing_L_humerus";
        public string rightWingBone = "wing_R_humerus";
        public string leftForearmBone = "wing_L_forearm";
        public string rightForearmBone = "wing_R_forearm";
        public string leftHandBone = "wing_L_hand";
        public string rightHandBone = "wing_R_hand";

        Transform _wingL, _wingR, _foreL, _foreR, _handL, _handR;
        Quaternion _wingLRest, _wingRRest, _foreLRest, _foreRRest, _handLRest, _handRRest;
        Vector3 _wingLAxis, _wingRAxis, _foreLAxis, _foreRAxis, _handLAxis, _handRAxis;

        float _angle;
        float _groundY;

        void Start()
        {
            _angle = startAngle;
            _groundY = GroundY(center);

            _wingL = FindDeep(transform, leftWingBone);
            _wingR = FindDeep(transform, rightWingBone);
            _foreL = FindDeep(transform, leftForearmBone);
            _foreR = FindDeep(transform, rightForearmBone);
            _handL = FindDeep(transform, leftHandBone);
            _handR = FindDeep(transform, rightHandBone);

            CacheBone(_wingL, out _wingLRest, out _wingLAxis);
            CacheBone(_wingR, out _wingRRest, out _wingRAxis);
            CacheBone(_foreL, out _foreLRest, out _foreLAxis);
            CacheBone(_foreR, out _foreRRest, out _foreRAxis);
            CacheBone(_handL, out _handLRest, out _handLAxis);
            CacheBone(_handR, out _handRRest, out _handRAxis);

            if (_wingL == null || _wingR == null)
                Debug.LogWarning("[Elang] Bone sayap tidak ditemukan. Cek nama bone hasil import FBX.", this);

            ApplyOrbit();
        }

        void Update()
        {
            float dir = clockwise ? 1f : -1f;
            _angle += dir * (speed / Mathf.Max(1f, radius)) * Time.deltaTime;
            ApplyOrbit();
        }

        void LateUpdate()
        {
            if (_wingL == null && _wingR == null) return;

            float t = Time.time * flapSpeed + flapPhase;
            float s = Mathf.Sin(t * Mathf.PI * 2f);

            float humerus = (s >= 0f ? s * flapUpAngle : s * flapDownAngle) * flapIntensity;
            humerus += (1f - Mathf.Clamp01(flapIntensity)) * glideLift;

            float fore = Mathf.Sin((t - 0.12f) * Mathf.PI * 2f) * forearmAngle * flapIntensity;
            float hand = Mathf.Sin((t - 0.24f) * Mathf.PI * 2f) * handAngle * flapIntensity;
            float mirror = mirrorFlap ? -1f : 1f;

            ApplyBone(_wingL, _wingLRest, _wingLAxis, humerus * mirror);
            ApplyBone(_wingR, _wingRRest, _wingRAxis, -humerus * mirror);
            ApplyBone(_foreL, _foreLRest, _foreLAxis, fore * mirror);
            ApplyBone(_foreR, _foreRRest, _foreRAxis, -fore * mirror);
            ApplyBone(_handL, _handLRest, _handLAxis, hand * mirror);
            ApplyBone(_handR, _handRRest, _handRAxis, -hand * mirror);
        }

        void ApplyOrbit()
        {
            float dir = clockwise ? 1f : -1f;

            Vector3 pos = center + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * radius;
            pos.y = _groundY + altitude + Mathf.Sin(Time.time * bobSpeed + flapPhase) * bobAmplitude;
            transform.position = pos;

            Vector3 tangent = new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * dir;
            if (tangent.sqrMagnitude < 0.0001f) tangent = transform.forward;

            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up) *
                                 Quaternion.Euler(0f, 0f, dir * bankAngle);
        }

        static void ApplyBone(Transform bone, Quaternion rest, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            // axis dihitung dalam ruang parent, jadi rotasi diterapkan sebelum rest.
            bone.localRotation = Quaternion.AngleAxis(degrees, axis) * rest;
        }

        void CacheBone(Transform bone, out Quaternion rest, out Vector3 axis)
        {
            if (bone == null)
            {
                rest = Quaternion.identity;
                axis = Vector3.forward;
                return;
            }

            rest = bone.localRotation;
            axis = bone.parent != null
                ? bone.parent.InverseTransformDirection(transform.forward)
                : Vector3.forward;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.forward;
            axis.Normalize();
        }

        float GroundY(Vector3 pos)
        {
            int layer = LayerMask.NameToLayer("Ground");
            int mask = layer >= 0 ? 1 << layer : ~0;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, 250f, pos.z), Vector3.down, out hit, 500f, mask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
                if (t.name == name) return t;

            foreach (var t in all)
                if (t.name.EndsWith("|" + name) || t.name.EndsWith("_" + name))
                    return t;

            foreach (var t in all)
                if (t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;

            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Vector3 c = new Vector3(center.x, transform.position.y, center.z);

            const int segments = 48;
            Vector3 prev = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
