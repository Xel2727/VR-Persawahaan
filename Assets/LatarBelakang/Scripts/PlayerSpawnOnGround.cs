using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Menempatkan player tepat di atas permukaan tanah saat start (biar tidak spawn di dalam tanah / melayang).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnOnGround : MonoBehaviour
    {
        public Transform spawnPoint;
        public float rayHeight = 200f;
        public LayerMask groundMask;

        void Start()
        {
            if (groundMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Ground");
                if (layer >= 0) groundMask = 1 << layer;
            }
            SnapToGround();
        }

        [ContextMenu("Snap To Ground")]
        public void SnapToGround()
        {
            Vector3 anchor = spawnPoint != null ? spawnPoint.position : transform.position;
            Vector3 origin = new Vector3(anchor.x, anchor.y + rayHeight * 0.5f, anchor.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayHeight, groundMask, QueryTriggerInteraction.Ignore))
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = hit.point + Vector3.up * 0.05f;
                if (cc != null) cc.enabled = true;
                Debug.Log(string.Format("[LatarBelakang] Player spawn di {0} (permukaan y={1:F2})", transform.position, hit.point.y));
            }
            else
            {
                Debug.LogWarning("[LatarBelakang] PlayerSpawnOnGround: tanah tidak ditemukan di bawah spawn point.");
            }
        }
    }
}
