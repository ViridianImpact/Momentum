using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Momentum.Player;

namespace Momentum.Fishing
{
    /// <summary>
    /// Local, fire-and-forget gameplay telemetry. Appends one JSON object per line (JSONL) to a file
    /// under <see cref="Application.persistentDataPath"/> so play sessions can be analysed later for
    /// balancing. Has ZERO effect on gameplay: it only subscribes to existing events and two small
    /// additive hooks, and writes to disk in a try/catch that never throws and never blocks.
    ///
    /// Every line carries an ISO-8601 UTC timestamp, a per-Play session id (a fresh GUID each time
    /// Play starts), an event <c>type</c>, and an event-specific payload. A single <c>session_start</c>
    /// line is written when Play begins.
    ///
    /// Event sources (all subscribed in OnEnable, released in OnDisable — no polling):
    ///   cast          <- FishingSpotInteractor.OnCastStarted (additive hook)
    ///   bite_outcome  <- FishingBiteController.OnBiteResolved (additive hook): fish / nothing / junk
    ///   fight_start   <- DERIVED: emitted right after a "fish" bite (onBite == BeginFight), so no hook
    ///                    is added to the protected FishingTensionController.
    ///   fight_end     <- win: FishingTensionController.OnFishLanded (carries species + rarity);
    ///                    loss: FishingTensionController.OnFightClosed firing with no preceding
    ///                    OnFishLanded for that fight (tracked by <see cref="wonThisFight"/>).
    ///   coins         <- PlayerWallet.OnBalanceChanged (zero-delta restore events are skipped).
    ///   loadout       <- LureShop.OnLoadoutChanged (equipped lure name read from EquippedLureName).
    ///
    /// Ordering: [DefaultExecutionOrder(-100)] so this component's OnEnable subscribes to OnFishLanded
    /// BEFORE PlayerWallet (which awards coins in its own OnFishLanded handler). That guarantees a win
    /// logs fight_end BEFORE the coins line, matching the intended event order.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TelemetryService : MonoBehaviour
    {
        [Tooltip("File name written (append-only) under Application.persistentDataPath.")]
        public string fileName = "momentum_telemetry.jsonl";

        [Header("References (auto-wired if left empty)")]
        public FishingTensionController fishing;
        public PlayerWallet wallet;
        public LureShop lureShop;
        public FishingSpotInteractor interactor;
        public FishingBiteController bite;

        string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        string sessionId;
        bool wonThisFight;   // set when OnFishLanded fires; distinguishes a win from a loss at close
        bool loggedPath;     // log the full telemetry path exactly once (first write)

        void Awake()
        {
            sessionId = Guid.NewGuid().ToString();

            if (fishing == null) fishing = FindFirstObjectByType<FishingTensionController>();
            if (wallet == null) wallet = FindFirstObjectByType<PlayerWallet>();
            if (lureShop == null) lureShop = FindFirstObjectByType<LureShop>();
            if (interactor == null) interactor = FindFirstObjectByType<FishingSpotInteractor>();
            if (bite == null) bite = FindFirstObjectByType<FishingBiteController>();
        }

        void OnEnable()
        {
            if (interactor != null) interactor.OnCastStarted += HandleCastStarted;
            if (bite != null) bite.OnBiteResolved += HandleBiteResolved;
            if (fishing != null)
            {
                fishing.OnFishLanded += HandleFishLanded;   // subscribed before PlayerWallet (exec order)
                fishing.OnFightClosed += HandleFightClosed;
            }
            if (wallet != null) wallet.OnBalanceChanged += HandleBalanceChanged;
            if (lureShop != null) lureShop.OnLoadoutChanged += HandleLoadoutChanged;
        }

        void OnDisable()
        {
            if (interactor != null) interactor.OnCastStarted -= HandleCastStarted;
            if (bite != null) bite.OnBiteResolved -= HandleBiteResolved;
            if (fishing != null)
            {
                fishing.OnFishLanded -= HandleFishLanded;
                fishing.OnFightClosed -= HandleFightClosed;
            }
            if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
            if (lureShop != null) lureShop.OnLoadoutChanged -= HandleLoadoutChanged;
        }

        void Start()
        {
            // Runs before LureShop.Start()/SaveService.Start() (exec order -100), so session_start is
            // the first line of the session, ahead of any startup loadout line.
            WriteEvent("session_start", null);
        }

        // =====================================================================
        // Event handlers -> one telemetry line each
        // =====================================================================

        void HandleCastStarted(Vector3 target)
        {
            WriteEvent("cast",
                Field("x", target.x) + "," + Field("y", target.y) + "," + Field("z", target.z));
        }

        void HandleBiteResolved(string outcome, string junkMessage)
        {
            string payload = Field("outcome", outcome);
            if (!string.IsNullOrEmpty(junkMessage)) payload += "," + Field("junk", junkMessage);
            WriteEvent("bite_outcome", payload);

            // A fish bite deterministically starts the fight (onBite == BeginFight), so derive
            // fight_start here — no hook on the protected controller. Reset the win flag for this fight.
            if (outcome == "fish")
            {
                wonThisFight = false;
                WriteEvent("fight_start", null);
            }
        }

        void HandleFishLanded(FishData fish, CatfishSpecies species)
        {
            wonThisFight = true; // consumed by HandleFightClosed so the win isn't double-logged as a loss

            string name = species != null ? species.displayName
                        : fish != null ? fish.displayName : "Unknown";
            FishRarity rarity = species != null ? species.rarity
                              : fish != null ? fish.rarity : FishRarity.Common;

            WriteEvent("fight_end",
                Field("result", "win") + "," + Field("species", name) + "," +
                Field("rarity", rarity.ToString()));
            // The coins line follows from PlayerWallet's own OnFishLanded handler (OnBalanceChanged),
            // which runs after this one thanks to the execution-order subscription.
        }

        void HandleFightClosed()
        {
            if (!wonThisFight)
                WriteEvent("fight_end", Field("result", "loss")); // no species, and no coins were awarded
            wonThisFight = false;
        }

        void HandleBalanceChanged(int newTotal, int delta)
        {
            if (delta == 0) return; // skip zero-delta restore/refresh events
            WriteEvent("coins", Field("delta", delta) + "," + Field("total", newTotal));
        }

        void HandleLoadoutChanged()
        {
            string equipped = lureShop != null ? lureShop.EquippedLureName : null;
            WriteEvent("loadout", Field("equipped", equipped ?? ""));
        }

        // Dev utility: append a hand-fired test line (right-click the component > this menu item)
        // to confirm the file writes without needing a full gameplay loop.
        [ContextMenu("Telemetry: Fire Test Event")]
        public void FireTestEvent()
        {
            WriteEvent("test_event", Field("note", "hand-fired"));
        }

        // =====================================================================
        // Writer (single append point; never throws, never blocks)
        // =====================================================================

        void WriteEvent(string type, string payload)
        {
            if (string.IsNullOrEmpty(sessionId)) sessionId = Guid.NewGuid().ToString();

            string line = "{" +
                Field("ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)) + "," +
                Field("session", sessionId) + "," +
                Field("type", type) +
                (string.IsNullOrEmpty(payload) ? "" : "," + payload) +
                "}";

            try
            {
                File.AppendAllText(SavePath, line + "\n");
                if (!loggedPath)
                {
                    Debug.Log($"[TelemetryService] Telemetry log: {SavePath}");
                    loggedPath = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TelemetryService] Write failed ({e.GetType().Name}: {e.Message}). Event dropped this time.");
            }
        }

        // ---- JSON field builders (compact, single-line, invariant-culture numbers) ----

        static string Field(string key, string value) => Quote(key) + ":" + Quote(value);
        static string Field(string key, int value) => Quote(key) + ":" + value.ToString(CultureInfo.InvariantCulture);
        static string Field(string key, float value) => Quote(key) + ":" + value.ToString("0.###", CultureInfo.InvariantCulture);

        static string Quote(string s) => "\"" + Escape(s) + "\"";

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
