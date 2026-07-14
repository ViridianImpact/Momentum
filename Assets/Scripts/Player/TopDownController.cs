using UnityEngine;

namespace Momentum.Player
{
    /// <summary>
    /// Top-down character controller (legacy Input Manager, to match the rest of the project).
    /// WASD moves on screen-relative world axes derived from the fixed camera (W = away from
    /// camera, S = toward, A/D = screen left/right). Uses the existing CharacterController and
    /// keeps gravity. A separate body visual is rotated to face the movement direction so the
    /// character's facing is readable from above.
    ///
    /// The mouse cursor is kept visible and unlocked at all times (aiming/casting is done by
    /// clicking the world with the cursor — see FishingSpotInteractor).
    ///
    /// Exposes the same <see cref="SetControlEnabled"/> lock the old FirstPersonController had,
    /// so FishingSpotInteractor can freeze movement during the cast/fight. Also exposes
    /// <see cref="FaceTowards"/> so the interactor can turn the character to face the cast point.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TopDownController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float gravity = -20f;

        [Header("Facing")]
        [Tooltip("Visual body transform that rotates to face movement/cast direction " +
                 "(the character capsule + nose). If empty, the Player root is rotated instead.")]
        public Transform bodyVisual;
        [Tooltip("Turn speed in degrees/second when facing the movement direction.")]
        public float turnSpeed = 720f;

        [Header("Camera frame")]
        [Tooltip("Camera used to map WASD onto screen-relative world axes. Defaults to Camera.main.")]
        public Camera cam;

        CharacterController controller;
        float verticalVelocity;
        bool controlEnabled = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cam == null) cam = Camera.main;
        }

        void Start()
        {
            SetCursorVisible();
        }

        /// <summary>Enable/disable WASD movement. Cursor stays visible either way. Same signature
        /// as the old FirstPersonController so existing lock/unlock callers keep working.</summary>
        public void SetControlEnabled(bool value)
        {
            controlEnabled = value;
        }

        /// <summary>True when WASD movement is enabled (i.e. the player is free — not mid-cast,
        /// not in a fight, shop not open). Read-only companion to SetControlEnabled.</summary>
        public bool ControlEnabled => controlEnabled;

        /// <summary>Turn the body to face a world point (used at cast start so the rod flings the
        /// right way). Yaw only — ignores height difference.</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (bodyVisual != null) bodyVisual.rotation = look;
            else transform.rotation = look;
        }

        static void SetCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            // Cursor visible at all times (in case something else grabbed it).
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None) SetCursorVisible();

            float h = controlEnabled ? Input.GetAxisRaw("Horizontal") : 0f;
            float v = controlEnabled ? Input.GetAxisRaw("Vertical") : 0f;

            // Map input onto world axes relative to the camera, flattened onto the ground plane.
            // With the fixed camera this makes W = away from camera, A/D = screen left/right.
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (cam != null)
            {
                camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
                camRight = cam.transform.right; camRight.y = 0f; camRight.Normalize();
            }

            Vector3 move = camRight * h + camForward * v;
            move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;

            // Turn the body to face the movement direction (smooth).
            if (bodyVisual != null && new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z), Vector3.up);
                bodyVisual.rotation = Quaternion.RotateTowards(bodyVisual.rotation, look, turnSpeed * Time.deltaTime);
            }

            // Gravity (grounded, no flying) — same model as FirstPersonController.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small stick-to-ground bias
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
