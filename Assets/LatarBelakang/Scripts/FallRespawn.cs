using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Jaring pengaman: kalau karakter jatuh melewati killY, kembalikan ke titik spawn.
    /// Pasang di root karakter.
    /// </summary>
    [DisallowMultipleComponent]
    public class FallRespawn : MonoBehaviour
    {
        [Tooltip("Kalau posisi Y di bawah nilai ini, karakter dianggap jatuh ke void.")]
        public float killY = -40f;

        [Tooltip("Titik tujuan respawn. Kalau kosong, pakai spawnPosition.")]
        public Transform spawnPoint;

        public Vector3 spawnPosition;

        CharacterController _cc;
        Rigidbody _rb;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _rb = GetComponent<Rigidbody>();
            if (spawnPoint == null && spawnPosition == Vector3.zero)
                spawnPosition = transform.position;
        }

        void Update()
        {
            if (transform.position.y < killY) Respawn();
        }

        public void Respawn()
        {
            Vector3 target = spawnPoint != null ? spawnPoint.position : spawnPosition;

            if (_cc != null) _cc.enabled = false;
            transform.position = target;
            if (_cc != null) _cc.enabled = true;

            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
