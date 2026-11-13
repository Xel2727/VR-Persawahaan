using UnityEngine;
using UnityEngine.XR;

namespace LatarBelakang
{
    /// <summary>Menempelkan pose controller VR (kiri/kanan) ke transform.</summary>
    [DisallowMultipleComponent]
    public class VRHandTracker : MonoBehaviour
    {
        public XRNode node = XRNode.LeftHand;
        public bool applyPosition = true;
        public bool applyRotation = true;

        bool _xrActive;

        void Start()
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            _xrActive = device.isValid;

            if (!_xrActive)
            {
                // tanpa headset, controller disembunyikan
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!_xrActive) return;
            if (applyPosition) transform.localPosition = InputTracking.GetLocalPosition(node);
            if (applyRotation) transform.localRotation = InputTracking.GetLocalRotation(node);
        }
    }
}
