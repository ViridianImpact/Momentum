using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Momentum.Fishing
{
    /// <summary>
    /// Bite-wait phase inserted between the lure landing and the fight starting (Pokémon-encounter
    /// style). Lives on the Player GameObject. FishingSpotInteractor calls <see cref="BeginWait"/>
    /// as the cast's onLanded callback instead of FishingTensionController.BeginFight directly.
    ///
    /// On BeginWait the outcome is rolled ONCE up front:
    ///   - fish bite  -> after a random 1-5s wait, a "!" thought bubble pops above the character,
    ///                   holds for a short readable beat, then onBite() runs (starts the fight).
    ///   - nothing    -> after the FULL max wait, a brief screen message shows, then onNoCatch().
    ///   - junk       -> same as nothing but with flavor text (yarn / scratching post / hairball).
    ///                   Junk is flavor ONLY: no coins, no inventory, nothing spawned.
    ///
    /// Movement stays locked the whole time (the interactor locked it before the cast); onNoCatch
    /// reuses the interactor's normal post-fight unlock path so no fight ever has to start.
    ///
    /// Visuals (bubble + message) are built entirely in code. Coroutines + primitives only.
    /// </summary>
    public class FishingBiteController : MonoBehaviour
    {
        [Header("Outcome odds (normalized in code; need not sum to 100)")]
        [Tooltip("Chance a fish bites and the fight starts.")]
        public float fishBiteChance = 85f;
        [Tooltip("Chance nothing bites (line returns empty).")]
        public float nothingChance = 10f;
        [Tooltip("Chance a junk item is reeled in (flavor text only).")]
        public float junkChance = 5f;

        [Header("Wait timing (seconds)")]
        public float minWait = 1f;
        public float maxWait = 5f;
        [Tooltip("How long the '!' bubble holds after the bite before the fight opens.")]
        public float readBeat = 0.7f;

        [Header("Bubble")]
        [Tooltip("Local offset from CharacterVisual for the '!' bubble (above the head).")]
        public Vector3 bubbleLocalOffset = new Vector3(0f, 2.1f, 0f);
        [Tooltip("Uniform scale of the bubble root.")]
        public float bubbleScale = 1f;
        [Tooltip("CharacterVisual the bubble parents to. Auto-found on this GameObject if empty.")]
        public Transform characterVisual;

        [Header("No-catch message")]
        [Tooltip("How long the no-catch message stays on screen, in seconds. Control unlocks when it hides.")]
        public float noCatchMessageDuration = 2f;
        [Tooltip("Junk item flavor lines (sub-rolled uniformly on a junk outcome).")]
        public string[] junkMessages =
        {
            "You reeled in a ball of yarn.",
            "You reeled in a scratching post.",
            "You reeled in a hairball.",
        };
        [Tooltip("Shown when nothing bites.")]
        public string nothingMessage = "Nothing seems to be biting...";

        enum Outcome { Fish, Nothing, Junk }

        bool waiting;
        Camera cam;

        // ---- bubble refs (built lazily) ----
        Transform bubbleRoot;
        Material bubbleBgMat;

        // ---- message UI refs (built at Start) ----
        Font uiFont;
        GameObject messagePanel;
        Text messageLabel;

        void Awake()
        {
            if (characterVisual == null)
            {
                var cv = transform.Find("CharacterVisual");
                if (cv != null) characterVisual = cv;
            }
        }

        void Start()
        {
            cam = Camera.main;
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildBubble();
            BuildMessageUI();
        }

        // =====================================================================
        // Public API (called by FishingSpotInteractor as the cast onLanded callback)
        // =====================================================================

        /// <summary>Begin the bite-wait phase. Rolls the outcome once, waits, then either fires
        /// <paramref name="onBite"/> (fish -> start the fight) or <paramref name="onNoCatch"/>
        /// (nothing/junk -> return the line, unlock movement).</summary>
        public void BeginWait(Action onBite, Action onNoCatch)
        {
            if (waiting) return;
            StartCoroutine(WaitRoutine(onBite, onNoCatch));
        }

        IEnumerator WaitRoutine(Action onBite, Action onNoCatch)
        {
            waiting = true;
            Outcome outcome = RollOutcome();

            if (outcome == Outcome.Fish)
            {
                float wait = UnityEngine.Random.Range(minWait, maxWait);
                yield return new WaitForSeconds(wait);

                ShowBubble(true);              // "!" pops
                yield return new WaitForSeconds(Mathf.Max(0f, readBeat));
                ShowBubble(false);             // gone before the overlay opens

                waiting = false;
                onBite?.Invoke();              // -> FishingTensionController.BeginFight()
            }
            else
            {
                // Nothing/junk: let the FULL max wait elapse (not the fish-only roll), so a
                // no-catch feels like a long dead wait.
                yield return new WaitForSeconds(maxWait);

                string msg = outcome == Outcome.Junk ? PickJunkMessage() : nothingMessage;
                ShowMessage(msg, true);
                yield return new WaitForSeconds(Mathf.Max(0f, noCatchMessageDuration));
                ShowMessage(null, false);

                waiting = false;
                onNoCatch?.Invoke();           // interactor returns the line + unlocks movement
            }
        }

        Outcome RollOutcome()
        {
            float fish = Mathf.Max(0f, fishBiteChance);
            float nothing = Mathf.Max(0f, nothingChance);
            float junk = Mathf.Max(0f, junkChance);
            float total = fish + nothing + junk;
            if (total <= 0f) return Outcome.Fish; // degenerate config -> always bite

            float r = UnityEngine.Random.value * total;
            if (r < fish) return Outcome.Fish;
            if (r < fish + nothing) return Outcome.Nothing;
            return Outcome.Junk;
        }

        string PickJunkMessage()
        {
            if (junkMessages == null || junkMessages.Length == 0) return "You reeled in some junk.";
            int i = UnityEngine.Random.Range(0, junkMessages.Length);
            return junkMessages[i];
        }

        // =====================================================================
        // Bubble (world-space, parented to CharacterVisual, billboarded to Camera.main)
        // =====================================================================

        void BuildBubble()
        {
            if (characterVisual == null) return;

            bubbleRoot = new GameObject("BiteBubble").transform;
            bubbleRoot.SetParent(characterVisual, false);
            bubbleRoot.localPosition = bubbleLocalOffset;
            bubbleRoot.localScale = Vector3.one * bubbleScale;

            // White rounded-ish backdrop (a quad; kept behind the text). Sprites/Default is an
            // always-available unlit shader so the bubble stays bright regardless of lighting.
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "BubbleBg";
            StripCollider(bg);
            Shader unlit = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            bubbleBgMat = new Material(unlit) { color = Color.white };
            bg.GetComponent<Renderer>().sharedMaterial = bubbleBgMat;
            bg.transform.SetParent(bubbleRoot, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.02f); // slightly behind the text
            bg.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // "!" as a 3D TextMesh (built-in, no TMP dependency).
            var textGO = new GameObject("BubbleText");
            textGO.transform.SetParent(bubbleRoot, false);
            textGO.transform.localPosition = Vector3.zero;
            var tm = textGO.AddComponent<TextMesh>();
            tm.text = "!";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 96;
            tm.characterSize = 0.06f;
            tm.color = new Color(0.15f, 0.15f, 0.18f);
            tm.font = uiFont;
            var mr = textGO.GetComponent<MeshRenderer>();
            if (mr != null && uiFont != null) mr.sharedMaterial = uiFont.material;

            bubbleRoot.gameObject.SetActive(false);
        }

        void ShowBubble(bool on)
        {
            if (bubbleRoot != null) bubbleRoot.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (bubbleRoot == null || !bubbleRoot.gameObject.activeSelf) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            // Billboard: face the camera so the "!" is readable from the fixed top-down angle.
            bubbleRoot.rotation = Quaternion.LookRotation(
                bubbleRoot.position - cam.transform.position, cam.transform.up);
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // =====================================================================
        // No-catch message (screen-space overlay, sortingOrder below CoinHud's 100)
        // =====================================================================

        void BuildMessageUI()
        {
            var canvasGO = new GameObject("BiteMessageCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // below CoinHud (100), matches LureShop's panel layer
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            var panelGO = new GameObject("MessagePanel");
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.rectTransform.SetParent(root, false);
            panelImg.color = new Color(0.10f, 0.12f, 0.16f, 0.85f);
            var prt = panelImg.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 90f);
            prt.anchoredPosition = new Vector2(0f, -220f); // lower-center, clear of any bubble
            messagePanel = panelGO;

            messageLabel = MakeText("MessageLabel", panelGO.transform, "", 34,
                TextAnchor.MiddleCenter, new Color(0.92f, 0.92f, 0.95f));
            Stretch(messageLabel.rectTransform);

            messagePanel.SetActive(false);
        }

        void ShowMessage(string msg, bool on)
        {
            if (messagePanel == null) return;
            if (on && messageLabel != null) messageLabel.text = msg;
            messagePanel.SetActive(on);
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
