using System;
using System.Collections;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// First-person rod/arm view-model + animated cast. Built entirely in code (no manual
    /// scene wiring) and parented to the camera so it tracks the player's look direction.
    ///
    /// Flow (driven by FishingSpotInteractor on a water click):
    ///   BeginCast(target, onLanded)
    ///     -> WINDUP  : rod pitches back
    ///     -> RELEASE : rod snaps forward; lure detaches from the tip and flies a parabola
    ///                  to <paramref name="target"/> (the exact raycast hit point)
    ///     -> on landing: onLanded() fires (FishingSpotInteractor uses it to call BeginFight)
    ///   ReturnToRest() (called after the fight closes) retracts the lure to the rod tip.
    ///
    /// The string is a LineRenderer drawn in world space from the rod tip to the lure every
    /// LateUpdate, so it stays synced whether the lure is docked at the tip or out on the water.
    /// Uses only Coroutines + AnimationCurve — no tweening package.
    /// </summary>
    public class FishingCastController : MonoBehaviour
    {
        [Header("Mounting")]
        [Tooltip("Parent for the view-model. Defaults to Camera.main's transform if left empty.")]
        public Transform viewModelParent;

        [Header("Cast timing (seconds)")]
        public float windupDuration = 0.20f;
        public float releaseDuration = 0.12f;
        public float flightDuration = 0.50f;
        [Tooltip("Peak extra height of the lure's parabolic arc, in metres.")]
        public float arcHeight = 3.0f;

        [Header("Rod pitch poses (local X euler, degrees)")]
        public float idlePitch = -8f;     // resting, angled slightly up
        public float windupPitch = -60f;  // cocked back
        public float releasePitch = 45f;  // snapped forward
        public float holdPitch = 20f;     // pointing out at the water during the fight

        // ---- view-model refs (created at Start) ----
        Transform rodPivot;   // what we rotate to animate the cast
        Transform rodTip;     // world anchor for the string + lure launch point
        Transform lure;
        LineRenderer line;
        Material bodyMat;      // arm + rod
        Material lureMat;

        bool casting;

        // ---- curves ----
        readonly AnimationCurve windupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        readonly AnimationCurve snapCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)); // fast out, ease in

        void Awake()
        {
            if (viewModelParent == null && Camera.main != null)
                viewModelParent = Camera.main.transform;
        }

        void Start()
        {
            BuildViewModel();
            SetRodPitch(idlePitch);
            DockLureToTip();
        }

        // =====================================================================
        // View-model construction (all in code)
        // =====================================================================

        void BuildViewModel()
        {
            // Resolve a lit shader that exists in THIS project. The live editor renders with the
            // Built-in pipeline (scene materials are "Standard"), but fall back gracefully to URP
            // in case this runs in the URP variant of the project.
            Shader lit = Shader.Find("Standard")
                         ?? Shader.Find("Universal Render Pipeline/Lit");
            bodyMat = new Material(lit) { color = new Color(0.55f, 0.40f, 0.28f) }; // wood/skin tone
            lureMat = new Material(lit) { color = new Color(0.25f, 0.55f, 0.95f) }; // blue

            // Root that holds the whole view-model, offset to the lower-right of the view.
            var root = new GameObject("FishingViewModel").transform;
            root.SetParent(viewModelParent, false);
            root.localPosition = new Vector3(0.28f, -0.30f, 0.55f);
            root.localRotation = Quaternion.identity;

            // Arm (forearm) — a cube receding toward the camera from the hand.
            var arm = MakePrimitive(PrimitiveType.Cube, "Arm", root, bodyMat);
            arm.localPosition = new Vector3(0f, 0f, -0.18f);
            arm.localRotation = Quaternion.Euler(8f, -6f, 0f);
            arm.localScale = new Vector3(0.10f, 0.10f, 0.42f);

            // Rod pivot at the hand — this is the transform we animate.
            rodPivot = new GameObject("RodPivot").transform;
            rodPivot.SetParent(root, false);
            rodPivot.localPosition = new Vector3(0f, 0.03f, 0.02f);

            // Rod — a long thin cube extending forward from the pivot.
            var rod = MakePrimitive(PrimitiveType.Cube, "Rod", rodPivot, bodyMat);
            rod.localScale = new Vector3(0.022f, 0.022f, 1.3f);
            rod.localPosition = new Vector3(0f, 0f, 0.65f); // half its length forward of the pivot

            // Rod tip anchor (empty) at the far end of the rod.
            rodTip = new GameObject("RodTip").transform;
            rodTip.SetParent(rodPivot, false);
            rodTip.localPosition = new Vector3(0f, 0f, 1.3f);

            // Lure — a small blue sphere, scaled into an oval (non-uniform on Z).
            lure = MakePrimitive(PrimitiveType.Sphere, "Lure", null, lureMat);
            lure.localScale = new Vector3(0.06f, 0.06f, 0.10f);

            // String — LineRenderer in world space, tip -> lure.
            var lineGO = new GameObject("FishingLine");
            lineGO.transform.SetParent(root, false);
            line = lineGO.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.006f;
            line.endWidth = 0.006f;
            line.numCapVertices = 2;
            // Sprites/Default is an always-available unlit shader (renders the line at full
            // brightness regardless of pipeline); fall back to the body shader if absent.
            Shader lineShader = Shader.Find("Sprites/Default") ?? lit;
            line.material = new Material(lineShader) { color = new Color(0.9f, 0.9f, 0.85f) };
            line.startColor = line.endColor = new Color(0.9f, 0.9f, 0.85f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        /// <summary>Create a primitive, strip its collider (so it never blocks the water raycast),
        /// and assign the given material.</summary>
        static Transform MakePrimitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        // =====================================================================
        // Public API (called by FishingSpotInteractor)
        // =====================================================================

        /// <summary>Play windup -> fling -> parabolic flight to <paramref name="target"/>,
        /// then invoke <paramref name="onLanded"/> when the lure touches down.</summary>
        public void BeginCast(Vector3 target, Action onLanded)
        {
            if (casting) return;
            StartCoroutine(CastRoutine(target, onLanded));
        }

        /// <summary>Retract the lure back to the rod tip and return the rod to its idle pose.
        /// Call after the fight closes (OnFightClosed).</summary>
        public void ReturnToRest()
        {
            StopAllCoroutines();
            casting = false;
            SetRodPitch(idlePitch);
            DockLureToTip();
        }

        // =====================================================================
        // Cast animation
        // =====================================================================

        IEnumerator CastRoutine(Vector3 target, Action onLanded)
        {
            casting = true;

            // 1) Windup — rod pitches back.
            yield return PitchRod(idlePitch, windupPitch, windupDuration, windupCurve);

            // 2) Release — rod snaps forward AND the lure flies at the same time.
            Vector3 launch = rodTip.position;
            lure.SetParent(null, true); // detach to world space, keep current pose
            StartCoroutine(PitchRod(windupPitch, releasePitch, releaseDuration, snapCurve));
            yield return Flight(launch, target);

            // 3) Landed.
            onLanded?.Invoke();

            // 4) Ease the rod to a natural "holding the fight" pose (lure stays out on the water).
            yield return PitchRod(releasePitch, holdPitch, 0.20f, windupCurve);
            casting = false;
        }

        IEnumerator Flight(Vector3 start, Vector3 target)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, flightDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(start, target, u);
                p.y += arcHeight * 4f * u * (1f - u); // parabola: 0 at ends, peak at u=0.5
                lure.position = p;
                yield return null;
            }
            lure.position = target; // land exactly on the clicked point
        }

        IEnumerator PitchRod(float from, float to, float duration, AnimationCurve curve)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = curve.Evaluate(Mathf.Clamp01(t / dur));
                SetRodPitch(Mathf.LerpUnclamped(from, to, u));
                yield return null;
            }
            SetRodPitch(to);
        }

        void SetRodPitch(float pitch)
        {
            if (rodPivot != null) rodPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void DockLureToTip()
        {
            if (lure == null || rodTip == null) return;
            lure.SetParent(rodTip, false);
            lure.localPosition = new Vector3(0f, -0.06f, 0f); // hangs just below the tip
            lure.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            if (line == null || rodTip == null || lure == null) return;
            line.SetPosition(0, rodTip.position);
            line.SetPosition(1, lure.position);
        }
    }
}
