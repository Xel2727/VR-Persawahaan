using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Helper ground check untuk karakter (CharacterController / Rigidbody / XR Origin).
    /// Pasang di root karakter, bukan di kamera.
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundCheck : MonoBehaviour
    {
        [Tooltip("Layer yang dianggap tanah. Kosong = otomatis cari layer 'Ground'.")]
        public LayerMask groundMask;

        [Tooltip("Panjang raycast ke bawah (m).")]
        public float rayLength = 3f;

        [Tooltip("Titik awal raycast relatif terhadap pivot karakter.")]
        public Vector3 originOffset = new Vector3(0f, 0.1f, 0f);

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundDistance { get; private set; }
        public RaycastHit LastHit { get; private set; }

        void Awake()
        {
            if (groundMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Ground");
                groundMask = layer >= 0 ? 1 << layer : ~0;
            }
        }

        void Update()
        {
            Probe();
        }

        /// <summary>Raycast manual (bisa dipanggil kapan pun, mis. sebelum spawn).</summary>
        public bool Probe()
        {
            Vector3 origin = transform.position + originOffset;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundDistance = hit.distance;
                LastHit = hit;
                return true;
            }

            IsGrounded = false;
            GroundNormal = Vector3.up;
            GroundDistance = float.PositiveInfinity;
            return false;
        }

        /// <summary>Tempatkan karakter tepat di atas permukaan tanah (untuk spawn).</summary>
        public bool SnapToGround(float maxDistance = 200f)
        {
            Vector3 origin = transform.position + Vector3.up * maxDistance * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position + originOffset,
                transform.position + originOffset + Vector3.down * rayLength);
        }
    }
}
