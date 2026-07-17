using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// The single persistence layer for the fishing prototype: coins, owned lures, and the
    /// equipped lure survive stopping and restarting Play mode. Writes one JSON file under
    /// <see cref="Application.persistentDataPath"/> via JsonUtility. All persistence logic lives
    /// here — PlayerWallet and LureShop do no file I/O; they only expose the additive hooks this
    /// class drives (PlayerWallet.RestoreBalance / OnBalanceChanged; LureShop.RestoreLoadout /
    /// OwnedLureNames / EquippedLureName / OnLoadoutChanged).
    ///
    /// Ordering: marked [DefaultExecutionOrder] AFTER default components so this Start() runs
    /// after LureShop.Start() has applied its Blue default — the restore then overrides it. Coin
    /// restore is order-independent (event-driven; all OnEnable subscriptions exist before Start).
    ///
    /// Save() runs automatically on coin-balance changes and lure loadout changes. Restoring on
    /// load does NOT re-award coins (RestoreBalance fires a zero delta, so CoinHud shows no "+N")
    /// and does NOT re-save (guarded by <see cref="restoring"/>).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SaveService : MonoBehaviour
    {
        [Serializable]
        public class SaveData
        {
            public int schemaVersion = CurrentSchemaVersion;
            public int coins;
            public List<string> ownedLures = new List<string>();
            public string equippedLure;
        }

        /// <summary>Bump when the on-disk shape changes; Load() handles unknown versions defensively.</summary>
        public const int CurrentSchemaVersion = 1;

        [Tooltip("File name written under Application.persistentDataPath.")]
        public string fileName = "momentum_save.json";

        [Header("References (auto-wired if left empty)")]
        public PlayerWallet wallet;
        public LureShop lureShop;

        string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        bool restoring;      // true while applying a loaded save; suppresses save-on-change
        bool loggedPath;     // log the full save path exactly once (first save)

        void Awake()
        {
            if (wallet == null) wallet = FindFirstObjectByType<PlayerWallet>();
            if (lureShop == null) lureShop = FindFirstObjectByType<LureShop>();
        }

        void Start()
        {
            // Runs after LureShop.Start() (see DefaultExecutionOrder) so we override its Blue default.
            RestoreFromDisk();

            // Subscribe only AFTER restoring, so the restore itself never triggers a save.
            if (wallet != null) wallet.OnBalanceChanged += HandleBalanceChanged;
            if (lureShop != null) lureShop.OnLoadoutChanged += HandleLoadoutChanged;
        }

        void OnDestroy()
        {
            if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
            if (lureShop != null) lureShop.OnLoadoutChanged -= HandleLoadoutChanged;
        }

        // =====================================================================
        // Save-on-change
        // =====================================================================

        void HandleBalanceChanged(int newTotal, int delta)
        {
            if (restoring) return;
            Save();
        }

        void HandleLoadoutChanged()
        {
            if (restoring) return;
            Save();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>Writes the current coins + lure loadout to disk. Never throws; logs and returns
        /// on failure so a bad write can never interrupt play.</summary>
        public void Save()
        {
            try
            {
                var data = new SaveData
                {
                    schemaVersion = CurrentSchemaVersion,
                    coins = wallet != null ? wallet.Coins : 0,
                    ownedLures = lureShop != null ? lureShop.OwnedLureNames.ToList() : new List<string>(),
                    equippedLure = lureShop != null ? lureShop.EquippedLureName : null,
                };

                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));

                if (!loggedPath)
                {
                    Debug.Log($"[SaveService] Save file: {SavePath}");
                    loggedPath = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Save failed ({e.GetType().Name}: {e.Message}). Progress not written this time.");
            }
        }

        /// <summary>Reads the save file and returns its data. Returns fresh defaults (0 coins, the
        /// default lure owned + equipped) when the file is missing or unreadable — never throws,
        /// never blocks play.</summary>
        public SaveData Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return Defaults();

                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return Defaults();
                if (data.ownedLures == null) data.ownedLures = new List<string>();

                if (data.schemaVersion != CurrentSchemaVersion)
                    Debug.LogWarning($"[SaveService] Save schemaVersion {data.schemaVersion} != {CurrentSchemaVersion}; reading fields as-is.");

                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Load failed ({e.GetType().Name}: {e.Message}); starting from defaults.");
                return Defaults();
            }
        }

        // =====================================================================
        // Internals
        // =====================================================================

        void RestoreFromDisk()
        {
            SaveData data = Load();
            restoring = true;
            try
            {
                if (wallet != null) wallet.RestoreBalance(data.coins);
                if (lureShop != null) lureShop.RestoreLoadout(data.ownedLures, data.equippedLure);
            }
            finally
            {
                restoring = false;
            }
        }

        /// <summary>Fresh-start defaults. Owned/equipped are derived from the shop's ownedByDefault
        /// lures (the free Blue) so they stay correct even if that lure is renamed; falls back to a
        /// hard "Blue" only if the shop isn't wired.</summary>
        SaveData Defaults()
        {
            var data = new SaveData { schemaVersion = CurrentSchemaVersion, coins = 0 };

            if (lureShop != null && lureShop.lures != null && lureShop.lures.Length > 0)
            {
                foreach (var l in lureShop.lures)
                    if (l.ownedByDefault) data.ownedLures.Add(l.displayName);

                var def = lureShop.lures.FirstOrDefault(l => l.ownedByDefault) ?? lureShop.lures[0];
                data.equippedLure = def.displayName;
                if (!data.ownedLures.Contains(def.displayName)) data.ownedLures.Add(def.displayName);
            }
            else
            {
                data.ownedLures.Add("Blue");
                data.equippedLure = "Blue";
            }

            return data;
        }
    }
}
