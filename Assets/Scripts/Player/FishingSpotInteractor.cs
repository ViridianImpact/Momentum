using System;
using UnityEngine;
using Momentum.Fishing;

namespace Momentum.Player
{
    /// <summary>
    /// Left-click a Water-layer collider to start the fishing minigame. With the top-down
    /// camera the mouse cursor is visible, so the ray is cast from the cursor position
    /// (Camera.main.ScreenPointToRay(Input.mousePosition)) — click directly on the water.
    ///
    /// On start: captures the player's position, turns the character to face the clicked
    /// point, locks player control, and calls FishingTensionController.BeginFight() (via the
    /// cast animation). When the player presses Done, the controller raises OnFightClosed and
    /// control is restored. Clicks on the dock/ground are ignored because only the Water layer
    /// is in <see cref="waterMask"/> and the closest hit must be water (dock/ground occlude it).
    /// </summary>
    public class FishingSpotInteractor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera to raycast from. Defaults to Camera.main if left empty.")]
        public Camera cam;
        public FishingTensionController fishing;
        [Tooltip("Player controller whose movement is locked while fishing, and which is asked " +
                 "to face the cast point. The top-down TopDownController exposes the same " +
                 "SetControlEnabled() lock the old FirstPersonController did.")]
        public TopDownController player;
        [Tooltip("First-person cast animation. If assigned (or found on this GameObject), the " +
                 "rod plays a windup+fling and BeginFight() fires when the lure lands. " +
                 "If null, BeginFight() is called instantly (old behaviour).")]
        public FishingCastController castController;
        [Tooltip("Bite-wait phase between the lure landing and the fight. If assigned (or found " +
                 "on this GameObject), the lure waits for a bite before the fight starts, with a " +
                 "chance of no catch. If null, the fight starts the instant the lure lands (old " +
                 "behaviour).")]
        public FishingBiteController bite;

        [Header("Water detection")]
        public LayerMask waterMask;
        public float maxCastDistance = 500f;

        [Header("Crosshair")]
        public bool drawCrosshair = true;
        public float crosshairSize = 6f;

        bool fishingActive;
        Vector3 capturedPosition;

        /// <summary>Additive telemetry hook: fired the instant a valid water click is confirmed and a
        /// cast is committed (before the cast animation / bite phase), carrying the target hit point.
        /// TelemetryService subscribes to this to log the 'cast' event. No gameplay behaviour depends
        /// on it — it is invoke-only.</summary>
        public event Action<Vector3> OnCastStarted;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (castController == null) castController = GetComponent<FishingCastController>();
            if (bite == null) bite = GetComponent<FishingBiteController>();
        }

        void OnEnable()
        {
            if (fishing != null) fishing.OnFightClosed += HandleFishingClosed;
        }

        void OnDisable()
        {
            if (fishing != null) fishing.OnFightClosed -= HandleFishingClosed;
        }

        void Update()
        {
            if (fishingActive || fishing == null || cam == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // Cast against ALL colliders and take the CLOSEST hit, then require that hit
            // to be on the Water layer. This way the dock/ground occlude the water — clicking
            // the dock hits the dock first (not water) and does nothing, even though the
            // water plane extends underneath it.
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxCastDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                bool hitWater = (waterMask.value & (1 << hit.collider.gameObject.layer)) != 0;
                if (hitWater)
                {
                    capturedPosition = transform.position; // where the player stood when they cast
                    fishingActive = true; // blocks re-trigger for the whole cast + fight
                    OnCastStarted?.Invoke(hit.point); // additive telemetry hook (no behaviour change)
                    if (player != null)
                    {
                        player.FaceTowards(hit.point);        // turn the character toward the clicked water
                        player.SetControlEnabled(false);      // lock movement now (through the cast)
                    }

                    // Play the cast animation, then run the bite-wait phase when the lure lands.
                    // The bite controller either starts the fight (bite) or returns the line with
                    // no catch (which routes to HandleNoCatch -> the normal unlock path). If no
                    // bite controller is present, the fight starts the instant the lure lands
                    // (old behaviour). Falls back to an instant BeginFight if there's no cast
                    // controller either.
                    if (castController != null)
                        castController.BeginCast(hit.point, StartBiteOrFight);
                    else
                        StartBiteOrFight();
                }
            }
        }

        // Runs when the lure lands: hand off to the bite-wait phase, or (no bite controller) start
        // the fight immediately as before.
        void StartBiteOrFight()
        {
            if (bite != null)
                bite.BeginWait(fishing.BeginFight, HandleNoCatch);
            else
                fishing.BeginFight();
        }

        // No-catch outcome (nothing / junk): no fight ever started, so reuse the exact same
        // unlock path the post-fight OnFightClosed handler uses.
        void HandleNoCatch()
        {
            ReleaseFishing();
        }

        void HandleFishingClosed()
        {
            ReleaseFishing();
        }

        // Shared unlock: clear the re-trigger guard, restore movement, and retract the lure. Used
        // by both the post-fight close and the no-catch outcome so casting again works immediately.
        void ReleaseFishing()
        {
            fishingActive = false;
            if (player != null) player.SetControlEnabled(true);
            if (castController != null) castController.ReturnToRest(); // retract lure to the rod tip
        }

        void OnGUI()
        {
            if (!drawCrosshair || fishingActive) return;
            float s = crosshairSize;
            var prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect((Screen.width - s) * 0.5f, (Screen.height - s) * 0.5f, s, s), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
