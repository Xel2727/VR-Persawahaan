using UnityEngine;
using UnityEngine.XR;

namespace LatarBelakang
{
    /// <summary>
    /// Lokomosi VR paling ringan: smooth move (stick kiri) + snap turn (stick kanan) + gravitasi + lompat.
    /// Tanpa paket XR Interaction Toolkit, jadi tidak menambah beban di Built-in RP.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class VRPlayerLocomotion : MonoBehaviour
    {
        [Header("Referensi")]
        [Tooltip("Transform kamera/headset. Kalau kosong pakai Camera.main.")]
        public Transform head;

        [Header("Gerak")]
        public float walkSpeed = 4.8f;
        public float sprintSpeed = 8.4f;
        public float gravity = -24f;
        public float snapTurnAngle = 30f;
        [Range(0.5f, 0.95f)] public float snapTurnThreshold = 0.75f;
        public float snapTurnCooldown = 0.25f;

        [Tooltip("Balik kalau kontrol terasa terbalik.")]
        public bool invertTurn = false;

        [Header("Lompat")]
        [Tooltip("Tinggi lompatan (m). Tombol: A (primaryButton) controller kanan.")]
        public float jumpHeight = 1.8f;
        [Tooltip("Masa tenggang masih dianggap menyentuh tanah setelah lepas (detik).")]
        public float coyoteTime = 0.08f;

        [Header("Guncangan Kamera")]
        public bool enableRunShake = true;
        [Tooltip("Amplitudo guncangan saat sprint (meter). VR dibuat sangat halus.")]
        public float runShakePosition = 0.014f;
        public float runShakeFrequency = 10f;
        public float runShakeBlend = 6f;

        CharacterController _cc;
        float _verticalVelocity;
        float _turnCooldown;
        float _coyoteTimer;
        bool _jumpHeld;
        bool _jumpQueued;
        bool _xrActive;

        Transform _cameraOffset;
        Vector3 _cameraBaseLocalPos;
        float _shakeWeight;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        void Start()
        {
            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            _xrActive = headDevice.isValid;

            _cameraOffset = head != null ? head.parent : null;
            if (_cameraOffset != null) _cameraBaseLocalPos = _cameraOffset.localPosition;
        }

        void Update()
        {
            if (!_xrActive) return;

            _turnCooldown -= Time.deltaTime;

            // vertikal: gravitasi + lompat
            if (_cc.isGrounded)
            {
                _coyoteTimer = coyoteTime;
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
            }

            bool jumpHeld = ReadButton(XRNode.RightHand, CommonUsages.primaryButton);
            if (_coyoteTimer > 0f && ((jumpHeld && !_jumpHeld) || _jumpQueued))
            {
                _jumpQueued = false;
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                _coyoteTimer = 0f;
            }
            _jumpHeld = jumpHeld;

            _verticalVelocity += gravity * Time.deltaTime;

            Vector2 stick = ReadAxis(XRNode.LeftHand);
            Vector2 turnStick = ReadAxis(XRNode.RightHand);
            bool sprint = ReadButton(XRNode.LeftHand, CommonUsages.gripButton)
                       || ReadButton(XRNode.LeftHand, CommonUsages.triggerButton);

            Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : transform.forward;
            if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 wish = forward * stick.y + right * stick.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 motion = wish * speed;
            motion.y = _verticalVelocity;

            _cc.Move(motion * Time.deltaTime);

            bool running = enableRunShake && _xrActive && _cc.isGrounded
                           && sprint && wish.sqrMagnitude > 0.01f;
            UpdateRunShake(running, Time.deltaTime);

            if (_turnCooldown <= 0f && Mathf.Abs(turnStick.x) > snapTurnThreshold)
            {
                float dir = Mathf.Sign(turnStick.x) * (invertTurn ? -1f : 1f);
                transform.Rotate(0f, dir * snapTurnAngle, 0f, Space.World);
                _turnCooldown = snapTurnCooldown;
            }
        }

        void UpdateRunShake(bool running, float dt)
        {
            if (_cameraOffset == null) return;

            float target = running ? 1f : 0f;
            _shakeWeight = Mathf.MoveTowards(_shakeWeight, target, Mathf.Max(0.01f, runShakeBlend) * dt);

            float t = Time.time * Mathf.Max(0.01f, runShakeFrequency);
            float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.73f, t) - 0.5f) * 2f;

            Vector3 offset = new Vector3(x, y, 0f) * (runShakePosition * _shakeWeight);
            _cameraOffset.localPosition = _cameraBaseLocalPos + offset;
        }

        void OnDisable()
        {
            if (_cameraOffset != null)
                _cameraOffset.localPosition = _cameraBaseLocalPos;
        }

        /// <summary>Minta lompat dari luar Update (mis. tombol UI / event eksternal).</summary>
        public void Jump()
        {
            if (_xrActive) _jumpQueued = true;
        }

        static Vector2 ReadAxis(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value))
                return value;
            return Vector2.zero;
        }

        static bool ReadButton(XRNode node, InputFeatureUsage<bool> usage)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }
    }
}
