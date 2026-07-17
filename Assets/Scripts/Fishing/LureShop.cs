using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Momentum.Player;

namespace Momentum.Fishing
{
    /// <summary>
    /// Dockside lure shop. Self-contained: builds its own stand (primitives) + trigger volume,
    /// its own screen-space hint and shop panel (all in code, in the same style as CoinHud /
    /// FishingTensionController), and holds the purchase/equip state. No manager classes.
    ///
    /// Flow:
    ///   - Player walks into the trigger volume while FREE (TopDownController.ControlEnabled) ->
    ///     a "Press E — Lure Shop" hint shows.
    ///   - E opens the shop: locks movement (SetControlEnabled(false)) and disables the fishing
    ///     interactor so clicks on the panel can't fall through and start a cast. E again / Close
    ///     unlocks both. Opening is refused mid-cast or during a fight (ControlEnabled == false).
    ///   - Buying deducts coins via PlayerWallet.TrySpend and auto-equips. Equipping sets the
    ///     lure colour in the world via FishingCastController.SetLureColor (persists through a
    ///     full cast -> fight -> return cycle). One lure equipped at a time.
    ///   - Ownership / equipped state is held here; SaveService persists it externally (restores
    ///     via RestoreLoadout on load, saves on the OnLoadoutChanged event). The shop does no I/O.
    ///
    /// References auto-wire at runtime (FindFirstObjectByType) but can be overridden in the
    /// Inspector. Prices and colours are Inspector-tunable.
    /// </summary>
    public class LureShop : MonoBehaviour
    {
        [Serializable]
        public class LureOption
        {
            public string displayName = "Lure";
            [Tooltip("Coin cost. 0 = free. Tunable without code changes.")]
            public int price = 0;
            public Color color = Color.white;
            [Tooltip("Owned from the start (the default Blue lure).")]
            public bool ownedByDefault = false;

            [Header("Species odds when equipped (weights, Catalogue order: Whiskers / Old Tom / Spotmouth)")]
            [Tooltip("Relative weight for Whiskers (Common). Normalized at pick time — any non-negative number.")]
            public float whiskersWeight = 70f;
            [Tooltip("Relative weight for Old Tom (Uncommon).")]
            public float oldTomWeight = 25f;
            [Tooltip("Relative weight for Spotmouth (Rare).")]
            public float spotmouthWeight = 5f;
        }

        [Header("Shop stock (name / price / colour / species odds — tunable)")]
        public LureOption[] lures =
        {
            new LureOption { displayName = "Blue",   price = 0,   color = new Color(0.25f, 0.55f, 0.95f), ownedByDefault = true,
                             whiskersWeight = 70f, oldTomWeight = 25f, spotmouthWeight =  5f },
            new LureOption { displayName = "Red",    price = 50,  color = new Color(0.90f, 0.25f, 0.25f),
                             whiskersWeight = 50f, oldTomWeight = 40f, spotmouthWeight = 10f },
            new LureOption { displayName = "Purple", price = 150, color = new Color(0.62f, 0.28f, 0.85f),
                             whiskersWeight = 30f, oldTomWeight = 45f, spotmouthWeight = 25f },
            new LureOption { displayName = "Gold",   price = 400, color = new Color(0.95f, 0.80f, 0.25f),
                             whiskersWeight = 15f, oldTomWeight = 40f, spotmouthWeight = 45f },
        };

        [Header("References (auto-wired if left empty)")]
        public PlayerWallet wallet;
        public TopDownController player;
        public FishingCastController castController;
        public FishingSpotInteractor interactor;

        [Header("Stand placement (world position of the stand + trigger)")]
        [Tooltip("Where the stand is planted. Default sits at the far (over-water) end of the Dock.")]
        public Vector3 standPosition = new Vector3(0f, 0.3f, 13f);
        [Tooltip("Half-extents of the trigger box the player must stand in to use the shop.")]
        public Vector3 triggerHalfExtents = new Vector3(2.2f, 1.5f, 2.2f);

        // ---- runtime state ----
        bool[] owned;
        int equippedIndex = -1;
        bool playerInRange;
        bool isOpen;

        /// <summary>Raised whenever ownership or the equipped lure changes (buy, equip, or a
        /// restore). Fired from <see cref="Equip"/>, which every ownership/equip change funnels
        /// through. Additive hook for SaveService (save-on-change); no play behaviour depends on it.</summary>
        public event Action OnLoadoutChanged;

        /// <summary>Display names of every currently-owned lure, in stock order. For SaveService.</summary>
        public IEnumerable<string> OwnedLureNames
        {
            get
            {
                for (int i = 0; i < lures.Length; i++)
                    if (owned != null && i < owned.Length && owned[i]) yield return lures[i].displayName;
            }
        }

        /// <summary>Display name of the equipped lure, or null if none. For SaveService.</summary>
        public string EquippedLureName =>
            (equippedIndex >= 0 && equippedIndex < lures.Length) ? lures[equippedIndex].displayName : null;

        // ---- UI refs ----
        Font uiFont;
        Canvas canvas;
        GameObject hintPanel;
        GameObject shopPanel;
        Text balanceLabel;
        Button[] rowButtons;
        Text[] rowButtonLabels;

        Material MakeStandMaterial(Color c)
        {
            Shader lit = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            return new Material(lit) { color = c };
        }

        void Awake()
        {
            if (wallet == null) wallet = FindFirstObjectByType<PlayerWallet>();
            if (player == null) player = FindFirstObjectByType<TopDownController>();
            if (castController == null) castController = FindFirstObjectByType<FishingCastController>();
            if (interactor == null) interactor = FindFirstObjectByType<FishingSpotInteractor>();

            owned = new bool[lures.Length];
            for (int i = 0; i < lures.Length; i++) owned[i] = lures[i].ownedByDefault;
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
            BuildStand();
            EnsureEventSystem();
            BuildUI();

            // Equip the default (first owned) lure so the world lure matches the shop's idea of
            // "equipped" from the start. The cast controller already defaults to blue; this keeps
            // them in sync regardless of colour tuning.
            int def = Array.FindIndex(owned, o => o);
            if (def >= 0) Equip(def);

            SetShopOpen(false);
            RefreshHint();
        }

        // =====================================================================
        // World stand + trigger
        // =====================================================================

        void BuildStand()
        {
            transform.position = standPosition;

            Material counterMat = MakeStandMaterial(new Color(0.15f, 0.45f, 0.50f)); // teal, distinct
            Material signMat = MakeStandMaterial(new Color(0.95f, 0.65f, 0.15f));    // bright orange
            Material postMat = MakeStandMaterial(new Color(0.35f, 0.25f, 0.15f));    // dark wood

            // Counter — a solid cube resting on the dock. Its collider is fine (blocks the player);
            // it is NOT on the Water layer, so it never confuses the fishing raycast.
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "ShopCounter";
            counter.transform.SetParent(transform, false);
            counter.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            counter.transform.localScale = new Vector3(1.8f, 0.9f, 0.7f);
            counter.GetComponent<Renderer>().sharedMaterial = counterMat;

            // Sign post.
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "ShopPost";
            post.transform.SetParent(transform, false);
            post.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            post.transform.localScale = new Vector3(0.09f, 1.4f, 0.09f);
            post.GetComponent<Renderer>().sharedMaterial = postMat;

            // Sign board.
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "ShopSign";
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            sign.transform.localScale = new Vector3(1.3f, 0.5f, 0.08f);
            sign.GetComponent<Renderer>().sharedMaterial = signMat;

            // Trigger volume — on THIS GameObject so OnTriggerEnter/Exit fire here. Only the
            // player's CharacterController generates trigger events against a static trigger
            // (static stand colliders do not), so we still filter for the player below.
            var trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, triggerHalfExtents.y, 0f);
            trigger.size = triggerHalfExtents * 2f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other)) { playerInRange = true; RefreshHint(); }
        }

        void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                playerInRange = false;
                if (isOpen) SetShopOpen(false); // walking away closes the shop and unlocks
                RefreshHint();
            }
        }

        bool IsPlayer(Collider other)
        {
            if (player == null) return other.GetComponentInParent<TopDownController>() != null;
            return other.GetComponentInParent<TopDownController>() == player;
        }

        // =====================================================================
        // Input
        // =====================================================================

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (isOpen) SetShopOpen(false);
                else if (CanOpen()) SetShopOpen(true);
            }

            RefreshHint();
        }

        bool CanOpen()
        {
            // Free = in range and not mid-cast / not fighting (movement currently enabled).
            return playerInRange && player != null && player.ControlEnabled;
        }

        // =====================================================================
        // Open / close
        // =====================================================================

        void SetShopOpen(bool open)
        {
            isOpen = open;
            if (shopPanel != null) shopPanel.SetActive(open);

            if (player != null) player.SetControlEnabled(!open);
            // Block the fishing interactor while shopping so panel clicks can't fall through to a
            // water raycast and start a cast. Safe to toggle: no fight can be in flight while the
            // shop is open, and its OnEnable/OnDisable only balance the OnFightClosed subscription.
            if (interactor != null) interactor.enabled = !open;

            if (open) RefreshPanel();
            RefreshHint();
        }

        // =====================================================================
        // Purchase / equip
        // =====================================================================

        void TryBuy(int i)
        {
            if (i < 0 || i >= lures.Length || owned[i]) return;
            if (wallet == null) return;
            if (wallet.TrySpend(lures[i].price))
            {
                owned[i] = true;
                Equip(i);      // auto-equip on purchase
                RefreshPanel();
            }
        }

        void Equip(int i)
        {
            if (i < 0 || i >= lures.Length || !owned[i]) return;
            equippedIndex = i;
            if (castController != null) castController.SetLureColor(lures[i].color);
            ApplyLureOdds(lures[i]);
            RefreshPanel();
            OnLoadoutChanged?.Invoke();
        }

        /// <summary>Restores owned + equipped state on load (SaveService). Marks every lure whose
        /// name is in <paramref name="ownedNames"/> as owned (ownedByDefault lures are always kept
        /// owned so the free default can never be lost), then equips <paramref name="equippedName"/>
        /// — which re-applies that lure's colour AND species weight table via <see cref="Equip"/>.
        /// Falls back to the first owned lure if the equipped name is missing or unowned. Meant to
        /// run AFTER Start() has applied the Blue default, overriding it.</summary>
        public void RestoreLoadout(IReadOnlyList<string> ownedNames, string equippedName)
        {
            if (owned == null || owned.Length != lures.Length) return;

            for (int i = 0; i < lures.Length; i++)
            {
                bool inSave = ownedNames != null && ownedNames.Contains(lures[i].displayName);
                owned[i] = inSave || lures[i].ownedByDefault; // never un-own a default lure
            }

            int equip = -1;
            if (!string.IsNullOrEmpty(equippedName))
                equip = Array.FindIndex(lures, l => l.displayName == equippedName);
            if (equip < 0 || !owned[equip]) equip = Array.FindIndex(owned, o => o); // fall back to first owned
            if (equip >= 0) Equip(equip);
        }

        /// <summary>Pushes the equipped lure's species odds into CatfishSpecies. Order MUST match
        /// the Catalogue (0=Whiskers, 1=Old Tom, 2=Spotmouth). Takes effect on the NEXT fight; a
        /// fight already in progress read its odds at ResetFight and is unaffected.</summary>
        void ApplyLureOdds(LureOption lure)
        {
            if (lure == null) return;
            CatfishSpecies.SetActiveWeights(new[] { lure.whiskersWeight, lure.oldTomWeight, lure.spotmouthWeight });
        }

        void HandleBalanceChanged(int newTotal, int delta)
        {
            if (balanceLabel != null) balanceLabel.text = $"Coins: {newTotal}";
            if (isOpen) RefreshPanel(); // affordability of unbought rows may have changed
        }

        // =====================================================================
        // UI
        // =====================================================================

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("LureShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // below the CoinHud (100) so the balance stays visible on top
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            BuildHint(root);
            BuildShopPanel(root);
        }

        void BuildHint(Transform root)
        {
            hintPanel = MakeImage("ShopHint", root, new Color(0.10f, 0.12f, 0.16f, 0.85f)).gameObject;
            var rt = hintPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(360f, 64f);
            rt.anchoredPosition = new Vector2(0f, 120f);

            var label = MakeText("HintLabel", hintPanel.transform, "Press E — Lure Shop", 28,
                TextAnchor.MiddleCenter, new Color(1f, 0.87f, 0.35f));
            Stretch(label.rectTransform);

            hintPanel.SetActive(false);
        }

        void BuildShopPanel(Transform root)
        {
            shopPanel = MakeImage("ShopPanel", root, new Color(0.08f, 0.10f, 0.14f, 0.96f)).gameObject;
            var rt = shopPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(680f, 560f);
            rt.anchoredPosition = Vector2.zero;

            var title = MakeText("Title", shopPanel.transform, "Lure Shop", 40, TextAnchor.MiddleCenter, Color.white);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 60f); trt.anchoredPosition = new Vector2(0f, -18f);

            balanceLabel = MakeText("Balance", shopPanel.transform,
                $"Coins: {(wallet != null ? wallet.Coins : 0)}", 26, TextAnchor.MiddleCenter,
                new Color(1f, 0.87f, 0.35f));
            var brt = balanceLabel.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, 36f); brt.anchoredPosition = new Vector2(0f, -78f);

            // Rows.
            rowButtons = new Button[lures.Length];
            rowButtonLabels = new Text[lures.Length];
            const float rowH = 78f;
            float startY = -130f;
            for (int i = 0; i < lures.Length; i++)
            {
                BuildRow(i, startY - i * rowH, rowH - 10f);
            }

            // Close button (bottom).
            var close = MakeButton("Close", shopPanel.transform, "Close", new Color(0.30f, 0.16f, 0.16f));
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f); crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(200f, 52f); crt.anchoredPosition = new Vector2(0f, 22f);
            close.onClick.AddListener(() => SetShopOpen(false));

            shopPanel.SetActive(false);
        }

        void BuildRow(int i, float y, float h)
        {
            var rowGO = MakeImage($"Row{i}", shopPanel.transform, new Color(1f, 1f, 1f, 0.05f));
            var rr = rowGO.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(-40f, h); rr.anchoredPosition = new Vector2(0f, y);

            // Swatch (solid colour Image).
            var swatch = MakeImage("Swatch", rowGO.transform, lures[i].color);
            var srt = swatch.rectTransform;
            srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(0f, 0.5f); srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(48f, 48f); srt.anchoredPosition = new Vector2(20f, 0f);

            // Name + price.
            string priceText = lures[i].price > 0 ? $"{lures[i].price}" : "free";
            var name = MakeText($"Name{i}", rowGO.transform, $"{lures[i].displayName}  ({priceText})", 28,
                TextAnchor.MiddleLeft, Color.white);
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0f, 0.5f);
            nrt.offsetMin = new Vector2(84f, 0f); nrt.offsetMax = new Vector2(-190f, 0f);

            // Action button.
            var btn = MakeButton($"Action{i}", rowGO.transform, "Buy", new Color(0.16f, 0.30f, 0.20f));
            var art = btn.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(1f, 0.5f); art.anchorMax = new Vector2(1f, 0.5f); art.pivot = new Vector2(1f, 0.5f);
            art.sizeDelta = new Vector2(160f, 52f); art.anchoredPosition = new Vector2(-16f, 0f);
            int idx = i;
            btn.onClick.AddListener(() => OnRowButton(idx));

            rowButtons[i] = btn;
            rowButtonLabels[i] = btn.GetComponentInChildren<Text>();
        }

        void OnRowButton(int i)
        {
            if (owned[i]) Equip(i);
            else TryBuy(i);
        }

        void RefreshPanel()
        {
            if (rowButtons == null) return;
            if (balanceLabel != null) balanceLabel.text = $"Coins: {(wallet != null ? wallet.Coins : 0)}";

            for (int i = 0; i < lures.Length; i++)
            {
                var btn = rowButtons[i];
                var lbl = rowButtonLabels[i];
                if (btn == null) continue;

                bool isOwned = owned[i];
                bool isEquipped = i == equippedIndex;
                bool affordable = wallet != null && wallet.Coins >= lures[i].price;

                if (isEquipped)
                {
                    if (lbl != null) lbl.text = "Equipped";
                    btn.interactable = false;
                }
                else if (isOwned)
                {
                    if (lbl != null) lbl.text = "Equip";
                    btn.interactable = true;
                }
                else
                {
                    if (lbl != null) lbl.text = $"Buy {lures[i].price}";
                    btn.interactable = affordable; // visibly disabled when unaffordable
                }
            }
        }

        void RefreshHint()
        {
            if (hintPanel == null) return;
            bool show = playerInRange && !isOpen && player != null && player.ControlEnabled;
            if (hintPanel.activeSelf != show) hintPanel.SetActive(show);
        }

        // ---- UI construction helpers (mirror CoinHud / FishingTensionController) --------------

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

        Button MakeButton(string name, Transform parent, string label, Color bg)
        {
            var go = new GameObject(name);
            var img = go.AddComponent<Image>();
            img.rectTransform.SetParent(parent, false);
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var txt = MakeText(name + "Label", go.transform, label, 26, TextAnchor.MiddleCenter, Color.white);
            Stretch(txt.rectTransform);
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
