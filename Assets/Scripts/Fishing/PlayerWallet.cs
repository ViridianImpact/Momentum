using System;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// Session-only coin wallet. Holds the player's coin balance (no saving / PlayerPrefs —
    /// resets to 0 every Play), and turns a won fight into a payout via the rarity->coins
    /// mapping below. Attach next to <see cref="FishingTensionController"/>; it auto-wires
    /// to that controller's OnFishLanded event in Awake().
    ///
    /// Payout amounts are public so they can be tuned in the Inspector without code changes.
    /// </summary>
    [RequireComponent(typeof(FishingTensionController))]
    public class PlayerWallet : MonoBehaviour
    {
        [Header("Payout per rarity (tunable — coins awarded when a fish of this rarity is landed)")]
        public int commonPayout = 10;
        public int uncommonPayout = 25;
        public int rarePayout = 60;
        public int epicPayout = 150;
        public int legendaryPayout = 400;

        /// <summary>Current coin balance for this Play session. Starts at 0.</summary>
        public int Coins { get; private set; }

        /// <summary>Raised whenever the balance changes. Args: (newTotal, deltaThisChange).</summary>
        public event Action<int, int> OnBalanceChanged;

        FishingTensionController fishing;

        void Awake()
        {
            fishing = GetComponent<FishingTensionController>();
        }

        void OnEnable()
        {
            if (fishing != null) fishing.OnFishLanded += HandleFishLanded;
        }

        void OnDisable()
        {
            if (fishing != null) fishing.OnFishLanded -= HandleFishLanded;
        }

        void HandleFishLanded(FishData fish)
        {
            if (fish == null) return;
            AddCoins(PayoutFor(fish.rarity));
        }

        /// <summary>Adds coins to the balance and notifies listeners. Ignores non-positive amounts.</summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            Debug.Log($"[PlayerWallet] +{amount} coins (total {Coins}).");
            OnBalanceChanged?.Invoke(Coins, amount);
        }

        /// <summary>Maps a fish rarity to its coin payout using the Inspector-tunable fields.</summary>
        public int PayoutFor(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Common:    return commonPayout;
                case FishRarity.Uncommon:  return uncommonPayout;
                case FishRarity.Rare:      return rarePayout;
                case FishRarity.Epic:      return epicPayout;
                case FishRarity.Legendary: return legendaryPayout;
                default:                   return commonPayout;
            }
        }
    }
}
