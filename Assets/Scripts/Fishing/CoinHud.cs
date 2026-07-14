using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Momentum.Fishing
{
    /// <summary>
    /// Always-visible screen-space coin counter (top-left corner), built entirely in code in
    /// the same style as FishingTensionController.BuildUI() — its own ScreenSpaceOverlay canvas,
    /// LegacyRuntime font, anchored RectTransforms. Updates whenever the wallet's balance changes.
    ///
    /// On an award it also floats a brief "+N" above the counter (coroutine + AnimationCurve).
    /// The win result panel itself is protected code, so the "+N" is shown here next to the HUD
    /// rather than on that panel.
    /// </summary>
    [RequireComponent(typeof(PlayerWallet))]
    public class CoinHud : MonoBehaviour
    {
        [Header("Layout (cosmetic)")]
        public Vector2 panelSize = new Vector2(200f, 52f);
        public Vector2 cornerMargin = new Vector2(24f, -24f);

        [Header("+N popup")]
        [Tooltip("How long the '+N' float lasts, in seconds.")]
        public float popupDuration = 1.1f;
        [Tooltip("How far (px) the '+N' rises over its lifetime.")]
        public float popupRise = 40f;
        [Tooltip("Alpha over normalized popup lifetime (0..1). Fades the '+N' out.")]
        public AnimationCurve popupAlpha = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

        PlayerWallet wallet;
        Font uiFont;
        Text coinLabel;
        Text popupLabel;
        RectTransform popupRect;
        Coroutine popupRoutine;
        Vector2 popupHome;

        void Awake()
        {
            wallet = GetComponent<PlayerWallet>();
        }

        void OnEnable()
        {
            if (wallet != null) wallet.OnBalanceChanged += HandleBalanceChanged;
        }

        void OnDisable()
        {
            if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
        }

        void Start()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
            RefreshLabel(wallet != null ? wallet.Coins : 0);
        }

        void HandleBalanceChanged(int newTotal, int delta)
        {
            RefreshLabel(newTotal);
            if (delta > 0) ShowPopup(delta);
        }

        void RefreshLabel(int total)
        {
            if (coinLabel != null) coinLabel.text = $"Coins: {total}";
        }

        void ShowPopup(int amount)
        {
            if (popupLabel == null) return;
            popupLabel.text = $"+{amount}";
            if (popupRoutine != null) StopCoroutine(popupRoutine);
            popupRoutine = StartCoroutine(PopupRoutine());
        }

        IEnumerator PopupRoutine()
        {
            popupLabel.gameObject.SetActive(true);
            float t = 0f;
            while (t < popupDuration)
            {
                float u = popupDuration > 0f ? t / popupDuration : 1f;
                var c = popupLabel.color;
                c.a = Mathf.Clamp01(popupAlpha.Evaluate(u));
                popupLabel.color = c;
                popupRect.anchoredPosition = popupHome + new Vector2(0f, popupRise * u);
                t += Time.deltaTime;
                yield return null;
            }
            var end = popupLabel.color;
            end.a = 0f;
            popupLabel.color = end;
            popupLabel.gameObject.SetActive(false);
            popupRoutine = null;
        }

        // --- UI construction (mirrors the FishingTensionController.BuildUI pattern) ----------

        void BuildUI()
        {
            var canvasGO = new GameObject("CoinHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Sit above the fight overlay so the counter stays readable during a fight too.
            canvas.sortingOrder = 100;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            // --- Counter panel (top-left) ---------------------------------------------------
            var panel = MakeImage("CoinPanel", root, new Color(0.10f, 0.12f, 0.16f, 0.85f));
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = panelSize;
            prt.anchoredPosition = cornerMargin;

            coinLabel = MakeText("CoinLabel", panel.transform, "Coins: 0", 28, TextAnchor.MiddleCenter,
                new Color(1f, 0.87f, 0.35f));
            Stretch(coinLabel.rectTransform);

            // --- "+N" popup (just below the panel, floats up) -------------------------------
            popupLabel = MakeText("CoinPopup", root, "", 30, TextAnchor.MiddleLeft,
                new Color(1f, 0.87f, 0.35f));
            popupRect = popupLabel.rectTransform;
            popupRect.anchorMin = new Vector2(0f, 1f);
            popupRect.anchorMax = new Vector2(0f, 1f);
            popupRect.pivot = new Vector2(0f, 1f);
            popupRect.sizeDelta = new Vector2(200f, 40f);
            popupHome = cornerMargin + new Vector2(12f, -panelSize.y - 6f);
            popupRect.anchoredPosition = popupHome;
            popupLabel.gameObject.SetActive(false);
        }

        Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            var img = go.AddComponent<Image>();
            img.rectTransform.SetParent(parent, false);
            img.color = color;
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
