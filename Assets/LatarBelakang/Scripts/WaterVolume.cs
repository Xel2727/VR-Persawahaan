using System;
using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Penanda volume air (collider isTrigger). Kirim event saat objek masuk/keluar air.
    /// Contoh pakai: WaterVolume.WaterStateChanged += (col, inside) => ...
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaterVolume : MonoBehaviour
    {
        /// <summary>(collider objek lain, sedang di dalam air?)</summary>
        public static event Action<Collider, bool> WaterStateChanged;

        [Tooltip("Kedalaman (m) di bawah permukaan air, untuk efek nanti.")]
        public float surfaceY;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            surfaceY = transform.position.y;
        }

        void Awake()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            if (surfaceY == 0f) surfaceY = transform.position.y;
        }

        void OnTriggerEnter(Collider other)
        {
            WaterStateChanged?.Invoke(other, true);
        }

        void OnTriggerExit(Collider other)
        {
            WaterStateChanged?.Invoke(other, false);
        }
    }
}
