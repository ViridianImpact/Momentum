using UnityEngine;

namespace Momentum.Player
{
    /// <summary>
    /// Fixed-angle, Stardew-style top-down camera. Follows the target's POSITION only —
    /// its rotation is authored in the scene (a fixed downward pitch) and is NEVER changed
    /// at runtime. Position is smoothed with SmoothDamp. Offset/smoothing are public for
    /// tuning in the Inspector.
    ///
    /// Replaces the old first-person setup where Main Camera was parented under Player and
    /// driven by FirstPersonController's mouse-look. This camera is unparented and standalone.
    /// </summary>
    public class TopDownCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to follow (the Player). Defaults to the object tagged 'Player' if left empty.")]
        public Transform target;

        [Header("Framing")]
        [Tooltip("World-space offset from the target to the camera. Y is height, -Z pulls the " +
                 "camera back behind the player so the character sits slightly below screen centre.")]
        public Vector3 offset = new Vector3(0f, 13f, -7f);

        [Tooltip("SmoothDamp time (seconds). Larger = laggier/smoother follow.")]
        public float smoothTime = 0.15f;

        Vector3 velocity;

        void Awake()
        {
            if (target == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform;
            }
        }

        void Start()
        {
            // Snap to the framed position immediately so there's no first-frame lerp from the origin.
            if (target != null) transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            // Rotation is intentionally left untouched — the fixed top-down angle is set in the scene.
        }
    }
}
