using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Menggerakkan awan pelan-pelan menyusuri sumbu tertentu lalu mengulang (wrap),
    /// menggantikan animasi object dari Blender tanpa biaya Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public class CloudDrift : MonoBehaviour
    {
        public Vector3 direction = new Vector3(1f, 0f, 0.12f);
        public float speed = 2.5f;
        public float wrapRadius = 2200f;
        public bool randomizeStartPhase = true;

        Vector3 _start;
        Vector3 _dir;

        void Start()
        {
            _start = transform.position;
            _dir = direction.sqrMagnitude < 0.0001f ? Vector3.right : direction.normalized;

            if (randomizeStartPhase)
            {
                // geser posisi awal supaya awan tidak bergerak serempak
                float offset = Random.Range(-wrapRadius, wrapRadius);
                transform.position = _start + _dir * offset;
                _start = transform.position;
            }
        }

        void Update()
        {
            var pos = transform.position + _dir * (speed * Time.deltaTime);
            var delta = pos - _start;
            var along = Vector3.Dot(delta, _dir);
            if (along > wrapRadius)
                pos -= _dir * (wrapRadius * 2f);
            transform.position = pos;
        }
    }
}
