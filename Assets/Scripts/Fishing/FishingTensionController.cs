using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Momentum.Fishing
{
    /// <summary>
    /// Single-mechanic fishing prototype: keep a fish icon inside a green tension
    /// safe-zone while reeling it in.
    ///
    /// tension(icon height) = your reeling + the fish's current effort.
    ///   * Scroll the mouse wheel to reel  -> tension UP   + progress fills.
    ///   * Stop scrolling                  -> tension drifts DOWN (reelDecay).
    ///   * Fish effort rises/falls on a randomized timer and ALSO pushes tension up.
    ///
    /// Outside the green zone the (hidden) line HP drains faster the further out you
    /// are; inside it slowly regenerates. HP == 0 -> "Line snapped!".
    /// Progress bar full -> "Landed [fish]!". Both end states show a Done button;
        /// starting a new encounter is done by clicking the water again (BeginFight).
    ///
    /// The whole UI is built in code at Start() so the prototype is drop-in: just put
    /// this component on one GameObject in the scene and press Play.
    /// </summary>
    public class FishingTensionController : MonoBehaviour
    {
        // ----------------------------------------------------------------- Fish data
        [Header("Fish catalogue — add entries to add more fish")]
        public List<FishData> fishCatalogue = new List<FishData>
        {
            new FishData
            {
                displayName     = "River Bass",
                effortFrequency = 0.7f,
                effortIntensity = 0.40f,
                progressPerReel = 0.035f,
                rarity          = FishRarity.Common
            }
        };
        [Tooltip("Which fish from the catalogue this fight uses.")]
        public int startingFishIndex = 0;

        // ----------------------------------------------------------------- Reeling feel
        [Header("Reeling")]
        [Tooltip("Tension (0..1) added per mouse-wheel notch while reeling.")]
        public float reelTensionPerReel = 0.12f;
        [Tooltip("How fast your reeling tension bleeds back down per second when you stop — the 'drift down'.")]
        public float reelDecay = 0.35f;

        // ----------------------------------------------------------------- Tension / zone
        [Header("Tension & safe zone (0 = bottom, 1 = top of bar)")]
        [Range(0f, 1f)] public float safeZoneBottom = 0.3333f;
        [Range(0f, 1f)] public float safeZoneTop = 0.6667f;
        [Tooltip("How quickly the icon visually chases its target tension. Higher = snappier / twitchier.")]
        public float tensionSmoothing = 10f;

        // ----------------------------------------------------------------- Fish effort
        [Header("Fish effort")]
        [Tooltip("How quickly the fish's effort ramps toward each new struggle target.")]
        public float effortLerpSpeed = 2.5f;

        // ----------------------------------------------------------------- Line HP (hidden)
        [Header("Line HP (hidden value)")]
        public float maxLineHP = 100f;
        [Tooltip("HP lost per second at the FAR edge of a danger zone. Scales down to 0 at the green edge.")]
        public float hpDrainRate = 90f;
        [Tooltip("HP regained per second while inside the green safe zone.")]
        public float hpRegenRate = 6f;

        // ----------------------------------------------------------------- Visuals
        [Header("Fish icon visuals (effort feedback)")]
        public Color calmColor = new Color(0.40f, 0.80f, 1.00f);
        public Color fightColor = new Color(1.00f, 0.35f, 0.25f);
        public float wiggleMinFrequency = 2f;
        public float wiggleMaxFrequency = 22f;
        public float wiggleAmplitude = 10f;

        [Header("Debug")]
        [Tooltip("The line HP is meant to be hidden. Enable to show a small readout for tuning/testing.")]
        public bool showDebugHP = true;

        [Header("Layout (cosmetic, not game feel)")]
        public Vector2 barSize = new Vector2(74f, 460f);
        public float barLeftMargin = 100f;
        public Vector2 progressBarSize = new Vector2(640f, 44f);

        // ----------------------------------------------------------------- Integration
        [Header("Integration")]
        [Tooltip("If false, the fight does NOT auto-start on Play. Call BeginFight() to start it " +
                 "(e.g. from clicking the water). The overlay stays hidden until then.")]
        public bool autoStartOnPlay = false;

        /// <summary>Raised when the player closes the result panel via the Done button.</summary>
        public event System.Action OnFightClosed;

        // ----------------------------------------------------------------- Runtime state
        enum State { Fighting, Won, Lost }
        State state;
        FishData fish;
        float playerReel;     // tension from your reeling (0..1), decays on its own
        float currentEffort;  // fish's current effort (0..effortIntensity)
        float targetEffort;   // effort it is ramping toward
        float effortTimer;    // time until the fish picks a new struggle
        float displayedTension; // smoothed icon height (0..1) — the value that matters
        float lineHP;
        float progress;       // 0..1

        // ----------------------------------------------------------------- UI refs
        Font uiFont;
        GameObject uiRoot;   // the whole ScreenSpaceOverlay canvas; toggled by BeginFight/CloseFight
        RectTransform barBackRect;
        RectTransform shakesBandRect;
        RectTransform greenBandRect;
        RectTransform breakBandRect;
        RectTransform breakLabelRect;
        RectTransform safeLabelRect;
        RectTransform shakesLabelRect;
        RectTransform fishIconRect;
        Image fishIconImage;
        RectTransform progressFillRect;
        RectTransform hpFillRect;
        GameObject hpRoot;
        GameObject resultPanel;
        Text resultText;

        const float ScrollPerNotch = 0.1f; // Unity's "Mouse ScrollWheel" axis ~0.1 per notch

        // =====================================================================

        void Start()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();

            if (autoStartOnPlay)
                ResetFight();
            else if (uiRoot != null)
                uiRoot.SetActive(false); // stay hidden until BeginFight() is called
        }

        // =====================================================================
        // Public entry points (integration hooks — no fight logic changed)
        // =====================================================================

        /// <summary>Show the overlay and start a fresh fight. Call this on click-to-fish.</summary>
        public void BeginFight()
        {
            if (uiRoot != null) uiRoot.SetActive(true);
            ResetFight();
        }

        /// <summary>Hide the overlay and hand control back (fires OnFightClosed).</summary>
        public void CloseFight()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
            OnFightClosed?.Invoke();
        }

        void Update()
        {
            ApplyZoneLayout(); // bands + labels always follow safeZoneBottom / safeZoneTop

            // `state` defaults to Fighting (enum 0) but `fish` isn't assigned until ResetFight();
            // guard so Update() doesn't run the fight before BeginFight() has started one.
            if (state != State.Fighting || fish == null) return;
            float dt = Time.deltaTime;

            // --- Your reeling -------------------------------------------------
            // HOLD LEFT MOUSE BUTTON to reel: raises tension + fills progress.
            // Releasing it lets tension drift back down (reelDecay, below).
            // reelNotchesPerSecond converts hold-time into the same "reel notch"
            // unit the tunables (reelTensionPerReel / fish.progressPerReel) use, and
            // keeps the reel rate framerate-independent.
            const float reelNotchesPerSecond = 8f;
            if (Input.GetMouseButton(0))
            {
                float notches = reelNotchesPerSecond * dt;
                playerReel += notches * reelTensionPerReel;
                progress = Mathf.Clamp01(progress + notches * fish.progressPerReel);
            }

            // --- Alternative input: MOUSE SCROLL WHEEL (original) -------------
            // Comment out the LMB block above and uncomment this to compare feel:
            // float scroll = Input.GetAxis("Mouse ScrollWheel");
            // if (scroll > 0f)
            // {
            //     float notches = scroll / ScrollPerNotch;
            //     playerReel += notches * reelTensionPerReel;
            //     progress = Mathf.Clamp01(progress + notches * fish.progressPerReel);
            // }

            playerReel = Mathf.Clamp01(Mathf.MoveTowards(playerReel, 0f, reelDecay * dt));

            // --- Fish effort (randomized struggle timer) ----------------------
            effortTimer -= dt;
            if (effortTimer <= 0f)
            {
                targetEffort = Random.Range(0f, fish.effortIntensity);
                float interval = 1f / Mathf.Max(0.05f, fish.effortFrequency);
                effortTimer = Random.Range(0.5f, 1.5f) * interval;
            }
            currentEffort = Mathf.Lerp(currentEffort, targetEffort, Mathf.Clamp01(effortLerpSpeed * dt));

            // --- Combined tension --------------------------------------------
            float targetTension = Mathf.Clamp01(playerReel + currentEffort);
            displayedTension = Mathf.Lerp(displayedTension, targetTension, Mathf.Clamp01(tensionSmoothing * dt));

            // --- Line HP: drain outside green, regen inside -------------------
            float outside = 0f;
            if (displayedTension > safeZoneTop)
                outside = (displayedTension - safeZoneTop) / Mathf.Max(0.0001f, 1f - safeZoneTop);
            else if (displayedTension < safeZoneBottom)
                outside = (safeZoneBottom - displayedTension) / Mathf.Max(0.0001f, safeZoneBottom);

            if (outside > 0f)
                lineHP -= hpDrainRate * outside * dt;
            else
                lineHP = Mathf.Min(maxLineHP, lineHP + hpRegenRate * dt);

            // --- Resolve end states ------------------------------------------
            if (progress >= 1f) { Win(); }
            else if (lineHP <= 0f) { lineHP = 0f; Lose(); }

            UpdateVisuals();
        }

        // =====================================================================
        // Fight lifecycle
        // =====================================================================

        void ResetFight()
        {
            fish = (fishCatalogue != null && fishCatalogue.Count > 0)
                ? fishCatalogue[Mathf.Clamp(startingFishIndex, 0, fishCatalogue.Count - 1)]
                : new FishData();

            playerReel = (safeZoneBottom + safeZoneTop) * 0.5f; // start centered in the green
            currentEffort = 0f;
            targetEffort = 0f;
            effortTimer = 1f / Mathf.Max(0.05f, fish.effortFrequency); // brief grace before it fights
            displayedTension = playerReel;
            lineHP = maxLineHP;
            progress = 0f;
            state = State.Fighting;

            if (resultPanel != null) resultPanel.SetActive(false);
            UpdateVisuals();
        }

        void Win()
        {
            state = State.Won;
            ShowResult($"Landed {fish.displayName}!");
        }

        void Lose()
        {
            state = State.Lost;
            // Lost in the bottom (orange) danger zone -> the fish shook free and dove.
            // Lost in the top (red) danger zone -> too much tension, the line broke.
            float bottom = Mathf.Min(safeZoneBottom, safeZoneTop);
            ShowResult(displayedTension < bottom ? "The fish got away!" : "Line snapped!");
        }

        void ShowResult(string message)
        {
            resultText.text = message;
            resultPanel.SetActive(true);
        }

        // =====================================================================
        // Visuals
        // =====================================================================

        void UpdateVisuals()
        {
            float effortNorm = fish.effortIntensity > 0.0001f
                ? Mathf.Clamp01(currentEffort / fish.effortIntensity)
                : 0f;

            // Wiggle faster + harder, and tint toward "fighting" red, as effort rises.
            float freq = Mathf.Lerp(wiggleMinFrequency, wiggleMaxFrequency, effortNorm);
            float amp = wiggleAmplitude * (0.25f + 0.75f * effortNorm);
            float wiggleX = Mathf.Sin(Time.time * freq) * amp;

            fishIconRect.anchoredPosition = new Vector2(wiggleX, displayedTension * barSize.y);
            fishIconImage.color = Color.Lerp(calmColor, fightColor, effortNorm);

            SetFill(progressFillRect, progress);
            if (hpRoot != null && hpFillRect != null)
                SetFill(hpFillRect, maxLineHP > 0f ? lineHP / maxLineHP : 0f);
        }

        static void SetFill(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(t, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Keeps the coloured bands (and their side labels) locked to safeZoneBottom /
        /// safeZoneTop, so the green safe zone always sits exactly where the HP logic's
        /// safe zone is — even if those values are changed live in the Inspector.
        /// </summary>
        void ApplyZoneLayout()
        {
            float bottom = Mathf.Clamp01(Mathf.Min(safeZoneBottom, safeZoneTop));
            float top = Mathf.Clamp01(Mathf.Max(safeZoneBottom, safeZoneTop));

            SetBandRange(shakesBandRect, 0f, bottom);
            SetBandRange(greenBandRect, bottom, top);
            SetBandRange(breakBandRect, top, 1f);

            SetLabelFrac(breakLabelRect, (top + 1f) * 0.5f);
            SetLabelFrac(safeLabelRect, (bottom + top) * 0.5f);
            SetLabelFrac(shakesLabelRect, bottom * 0.5f);
        }

        static void SetBandRange(RectTransform rt, float from, float to)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, from);
            rt.anchorMax = new Vector2(1f, to);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetLabelFrac(RectTransform rt, float frac)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(1f, frac);
            rt.anchorMax = new Vector2(1f, frac);
        }

        // =====================================================================
        // UI construction (all in code)
        // =====================================================================

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("FishingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGO;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            // --- Tension bar (the water column) ------------------------------
            var barImg = MakeImage("TensionBar", root, new Color(0.10f, 0.12f, 0.16f, 0.95f));
            barBackRect = barImg.rectTransform;
            barBackRect.anchorMin = new Vector2(0f, 0.5f);
            barBackRect.anchorMax = new Vector2(0f, 0.5f);
            barBackRect.pivot = new Vector2(0.5f, 0.5f);
            barBackRect.sizeDelta = barSize;
            barBackRect.anchoredPosition = new Vector2(barLeftMargin, 0f);

            shakesBandRect = MakeBand(barBackRect, 0f, safeZoneBottom, new Color(0.85f, 0.50f, 0.15f, 0.55f)).rectTransform; // shakes free (bottom)
            greenBandRect  = MakeBand(barBackRect, safeZoneBottom, safeZoneTop, new Color(0.20f, 0.75f, 0.30f, 0.55f)).rectTransform; // safe (green)
            breakBandRect  = MakeBand(barBackRect, safeZoneTop, 1f, new Color(0.85f, 0.20f, 0.20f, 0.60f)).rectTransform; // line breaks (top)

            breakLabelRect  = MakeSideLabel("LINE BREAKS", (safeZoneTop + 1f) * 0.5f, new Color(1f, 0.6f, 0.6f));
            safeLabelRect   = MakeSideLabel("SAFE", (safeZoneBottom + safeZoneTop) * 0.5f, new Color(0.7f, 1f, 0.75f));
            shakesLabelRect = MakeSideLabel("SHAKES FREE", safeZoneBottom * 0.5f, new Color(1f, 0.85f, 0.6f));

            ApplyZoneLayout(); // lock the bands/labels to the current safe-zone values

            // --- Fish icon ---------------------------------------------------
            fishIconImage = MakeImage("FishIcon", barBackRect, calmColor);
            fishIconRect = fishIconImage.rectTransform;
            fishIconRect.anchorMin = new Vector2(0.5f, 0f);
            fishIconRect.anchorMax = new Vector2(0.5f, 0f);
            fishIconRect.pivot = new Vector2(0.5f, 0.5f);
            fishIconRect.sizeDelta = new Vector2(54f, 26f);

            // --- Progress bar (bottom) ---------------------------------------
            var progBack = MakeImage("ProgressBar", root, new Color(0.10f, 0.12f, 0.16f, 0.95f));
            var prt = progBack.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = progressBarSize;
            prt.anchoredPosition = new Vector2(0f, 64f);

            progressFillRect = MakeImage("ProgressFill", progBack.transform, new Color(0.25f, 0.60f, 0.95f, 1f)).rectTransform;
            SetFill(progressFillRect, 0f);
            Stretch(MakeText("ProgressLabel", progBack.transform, "REEL IN  (hold left mouse button)", 22, TextAnchor.MiddleCenter, Color.white).rectTransform);

            // --- Instructions (top) ------------------------------------------
            var info = MakeText("Instructions", root,
                "Hold the LEFT MOUSE BUTTON to REEL the fish in.\n" +
                "Keep the icon in the GREEN zone — ease off when it fights (turns red & wiggles fast).",
                26, TextAnchor.UpperCenter, Color.white);
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -24f);
            irt.sizeDelta = new Vector2(1100f, 90f);

            // --- Hidden HP debug readout -------------------------------------
            if (showDebugHP)
            {
                var hpBack = MakeImage("LineHP_Debug", root, new Color(0.10f, 0.12f, 0.16f, 0.95f));
                hpRoot = hpBack.gameObject;
                var hrt = hpBack.rectTransform;
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(0f, 1f);
                hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(24f, -24f);
                hrt.sizeDelta = new Vector2(220f, 26f);

                hpFillRect = MakeImage("HPFill", hpRoot.transform, new Color(0.90f, 0.85f, 0.20f, 1f)).rectTransform;
                SetFill(hpFillRect, 1f);
                Stretch(MakeText("HPLabel", hpRoot.transform, "Line HP (debug)", 16, TextAnchor.MiddleCenter, Color.black).rectTransform);
            }

            // --- Result panel (win/lose + Done) ------------------------------
            var panelImg = MakeImage("ResultPanel", root, new Color(0f, 0f, 0f, 0.80f));
            resultPanel = panelImg.gameObject;
            Stretch(panelImg.rectTransform);

            resultText = MakeText("ResultText", resultPanel.transform, "", 64, TextAnchor.MiddleCenter, Color.white);
            var trt = resultText.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 70f);
            trt.sizeDelta = new Vector2(1000f, 160f);

            // Done: hide the overlay and return control to the walk scene (integration hook).
            // Starting a new encounter is done by clicking the water again (FishingSpotInteractor -> BeginFight).
            var done = MakeButton("DoneButton", resultPanel.transform, "Done", new Color(0.30f, 0.35f, 0.40f, 1f), CloseFight);
            var drt = done.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(0f, -80f);
            drt.sizeDelta = new Vector2(240f, 72f);

            resultPanel.SetActive(false);
        }

        // ----------------------------------------------------------------- UI helpers

        Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            var img = go.AddComponent<Image>(); // auto-adds RectTransform + CanvasRenderer
            img.rectTransform.SetParent(parent, false);
            img.color = color;
            return img;
        }

        Image MakeBand(RectTransform parent, float from, float to, Color color)
        {
            var img = MakeImage("Band", parent, color);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, from);
            rt.anchorMax = new Vector2(1f, to);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        Text MakeText(string name, Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Text>();
            t.rectTransform.SetParent(parent, false);
            t.font = uiFont;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        RectTransform MakeSideLabel(string content, float frac, Color color)
        {
            var t = MakeText("Label_" + content, barBackRect, content, 18, TextAnchor.MiddleLeft, color);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(1f, frac);
            rt.anchorMax = new Vector2(1f, frac);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(12f, 0f);
            rt.sizeDelta = new Vector2(260f, 28f);
            return rt;
        }

        Button MakeButton(string name, Transform parent, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var img = MakeImage(name, parent, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            Stretch(MakeText(name + "_Label", img.transform, label, 30, TextAnchor.MiddleCenter, Color.white).rectTransform);
            return btn;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
