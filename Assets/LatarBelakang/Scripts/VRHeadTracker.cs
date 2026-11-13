using UnityEngine;
using UnityEngine.XR;

// force recompile -> domain reload -> MCP bridge reconnect
namespace LatarBelakang
{
    /// <summary>
    /// Menempelkan pose headset (HMD) ke transform kamera.
    /// Hanya aktif kalau XR device terdeteksi, jadi aman dipakai di Editor tanpa headset.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRHeadTracker : MonoBehaviour
    {
        public bool applyPosition = true;
        public bool applyRotation = true;

        bool _xrActive;

        void Start()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            _xrActive = head.isValid;
        }

        void Update()
        {
            if (!_xrActive) return;

            if (applyPosition)
                transform.localPosition = InputTracking.GetLocalPosition(XRNode.CenterEye);

            if (applyRotation)
                transform.localRotation = InputTracking.GetLocalRotation(XRNode.CenterEye);
        }
    }
}
