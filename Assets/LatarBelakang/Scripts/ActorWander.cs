using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Gerak sederhana untuk aktor (petani, hewan): jalan bolak-balik di sekitar titik awal,
    /// berhenti sejenak, lalu jalan lagi. Memakai klip legacy "Walk"/"Idle" dari model FBX.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActorWander : MonoBehaviour
    {
        [Header("Gerak")]
        public float walkSpeed = 1.1f;
        public float wanderRadius = 6f;
        public float minPause = 2f;
        public float maxPause = 6f;
        public float turnSpeed = 4f;

        [Header("Tanah")]
        public float groundOffset = 0.02f;
        public LayerMask groundMask;
        public float maxStep = 1f;

        [Header("Animasi")]
        public string walkStateName = "Walk";
        public string idleStateName = "Idle";

        Animation _anim;
        string _walk;
        string _idle;
        Vector3 _home;
        Vector3 _target;
        bool _paused = true;
        float _timer;

        void Start()
        {
            _home = transform.position;
            _anim = GetComponentInChildren<Animation>();
            if (_anim != null)
            {
                _anim.playAutomatically = false;
                _walk = FindState(_anim, walkStateName);
                _idle = FindState(_anim, idleStateName);
            }

            if (groundMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Ground");
                groundMask = layer >= 0 ? 1 << layer : ~0;
            }

            _paused = true;
            _timer = Random.Range(0.5f, 2f);
            Play(_idle);
        }

        static string FindState(Animation anim, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (AnimationState st in anim)
                if (st.name == key || st.name.EndsWith("|" + key) || st.name.EndsWith("_" + key))
                    return st.name;
            foreach (AnimationState st in anim)
                if (st.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return st.name;
            return null;
        }

        void Play(string state)
        {
            if (_anim == null || string.IsNullOrEmpty(state)) return;
            if (!_anim.IsPlaying(state)) _anim.CrossFade(state, 0.25f);
        }

        void PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            _target = _home + new Vector3(r.x, 0f, r.y);
            _target.y = transform.position.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_paused)
            {
                _timer -= dt;
                Play(_idle);
                if (_timer <= 0f)
                {
                    _paused = false;
                    PickTarget();
                }
                return;
            }

            Vector3 to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.5f)
            {
                StartPause();
                return;
            }

            Vector3 dir = to.normalized;
            Vector3 next = transform.position + dir * walkSpeed * dt;

            RaycastHit hit;
            if (Physics.Raycast(next + Vector3.up * 60f, Vector3.down, out hit, 250f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(hit.point.y - transform.position.y) > maxStep)
                {
                    StartPause();
                    return;
                }
                next.y = hit.point.y + groundOffset;
            }
            else
            {
                StartPause();
                return;
            }

            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(turnSpeed * dt));
            Play(_walk);
        }

        void StartPause()
        {
            _paused = true;
            _timer = Random.Range(minPause, maxPause);
            Play(_idle);
        }
    }
}
