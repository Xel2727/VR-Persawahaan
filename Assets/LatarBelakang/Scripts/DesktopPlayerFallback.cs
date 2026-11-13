using UnityEngine;
using UnityEngine.XR;

namespace LatarBelakang
{
    /// <summary>
    /// Kontrol keyboard + mouse untuk menguji scene tanpa headset.
    /// Otomatis nonaktif kalau HMD terdeteksi.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class DesktopPlayerFallback : MonoBehaviour
    {
        public Transform head;
        public float eyeHeight = 3.3f;
        public float walkSpeed = 8f;
        public float runSpeed = 15f;
        public float mouseSensitivity = 2.2f;
        public float pitchLimit = 85f;
        public KeyCode sprintKey = KeyCode.LeftShift;

        [Header("Lompat")]
        [Tooltip("Tinggi lompatan (m).")]
        public float jumpHeight = 1.8f;
        public KeyCode jumpKey = KeyCode.Space;
        [Tooltip("Masa tenggang masih dianggap menyentuh tanah setelah lepas (detik).")]
        public float coyoteTime = 0.08f;
        public float gravity = -24f;

        [Header("Guncangan Kamera")]
        public bool enableRunShake = true;
        [Tooltip("Amplitudo guncangan saat lari (meter).")]
        public float runShakePosition = 0.035f;
        public float runShakeFrequency = 11f;
        public float runShakeBlend = 8f;

        CharacterController _cc;
        float _pitch;
        float _verticalVelocity;
        float _coyoteTimer;
        bool _jumpQueued;
        bool _active;

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
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            _active = !hmd.isValid;

            if (_active && head != null)
            {
                // CameraOffset bisa punya tinggi sendiri (dipakai mode VR), jadi hitung selisihnya
                float rigOffset = head.parent != null ? head.parent.localPosition.y : 0f;
                head.localPosition = new Vector3(0f, eyeHeight - rigOffset, 0f);
                head.localRotation = Quaternion.identity;

                _cameraOffset = head.parent;
                if (_cameraOffset != null) _cameraBaseLocalPos = _cameraOffset.localPosition;
            }

            Cursor.lockState = _active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_active;
        }

        void Update()
        {
            if (!_active) return;

            // mouse look
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
            transform.Rotate(0f, mx, 0f, Space.World);

            _pitch = Mathf.Clamp(_pitch - my, -pitchLimit, pitchLimit);
            if (head != null) head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // gerak
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 wish = transform.forward * v + transform.right * h;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

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

            if (_coyoteTimer > 0f && (Input.GetKeyDown(jumpKey) || _jumpQueued))
            {
                _jumpQueued = false;
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                _coyoteTimer = 0f;
            }

            _verticalVelocity += gravity * Time.deltaTime;

            float speed = Input.GetKey(sprintKey) ? runSpeed : walkSpeed;
            Vector3 motion = wish * speed;
            motion.y = _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);

            bool running = enableRunShake && _active && _cc.isGrounded
                           && Input.GetKey(sprintKey) && wish.sqrMagnitude > 0.01f;
            UpdateRunShake(running, Time.deltaTime);
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

        /// <summary>Minta lompat dari luar Update (mis. tombol UI / event eksternal).</summary>
        public void Jump()
        {
            if (_active) _jumpQueued = true;
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_cameraOffset != null)
                _cameraOffset.localPosition = _cameraBaseLocalPos;
        }
    }
}
