using UnityEngine;

namespace Momentum.Player
{
    /// <summary>
    /// Minimal first-person controller using the legacy Input Manager (to match the rest
    /// of the project). WASD moves relative to the look direction with gravity (grounded,
    /// no flying); the mouse turns/looks. Control + cursor lock can be suspended while the
    /// fishing overlay is up via <see cref="SetControlEnabled"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float gravity = -20f;

        [Header("Look")]
        [Tooltip("Camera transform to pitch (usually the child Main Camera).")]
        public Transform cameraPivot;
        public float mouseSensitivity = 2f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;
        bool controlEnabled = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            SetCursorLocked(true);
        }

        /// <summary>Enable/disable movement + mouse-look, and lock/unlock the cursor to match.</summary>
        public void SetControlEnabled(bool value)
        {
            controlEnabled = value;
            SetCursorLocked(value);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (controlEnabled)
            {
                // --- Mouse look: yaw the body, pitch the camera pivot ---
                float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
                float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
                transform.Rotate(0f, mx, 0f);
                pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
                if (cameraPivot != null)
                    cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            // --- Movement relative to look (yaw only; body forward is horizontal) ---
            float h = controlEnabled ? Input.GetAxis("Horizontal") : 0f;
            float v = controlEnabled ? Input.GetAxis("Vertical") : 0f;
            Vector3 move = transform.right * h + transform.forward * v;
            move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;

            // --- Gravity ---
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small stick-to-ground bias
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
