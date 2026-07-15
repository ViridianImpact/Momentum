using System;
using System.Collections.Generic;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// Identity + rarity for a caught catfish: a name, a swatch colour shown on the result
    /// panel, and a <see cref="FishRarity"/> that now drives the coin payout (see
    /// PlayerWallet). This still carries NO fight stats — tension/HP/difficulty are driven
    /// entirely by <see cref="FishData"/> in FishingTensionController, so every catfish
    /// fights identically. This is the content + reward-tier layer (who you caught and what
    /// it's worth), not the fight-difficulty layer.
    ///
    /// WHICH species you catch is a weighted roll (<see cref="PickRandom"/>) against an
    /// active weight table. The table defaults to heavily-Whiskers (see <see cref="ActiveWeights"/>)
    /// and is swapped by the equipped lure via <see cref="SetActiveWeights"/> so better lures
    /// shift the odds toward rarer species.
    ///
    /// Add a species = add one entry to <see cref="Catalogue"/> (and one matching weight
    /// wherever weight tables are authored, e.g. LureShop).
    /// </summary>
    [Serializable]
    public class CatfishSpecies
    {
        public string displayName;
        public Color swatchColor;
        public FishRarity rarity;

        public CatfishSpecies(string displayName, Color swatchColor, FishRarity rarity)
        {
            this.displayName = displayName;
            this.swatchColor = swatchColor;
            this.rarity = rarity;
        }

        /// <summary>The catchable catfish, single biome. Identical FIGHT stats; rarity differs
        /// (drives payout only). Catalogue ORDER is the canonical index order used by every
        /// weight table (0=Whiskers, 1=Old Tom, 2=Spotmouth).</summary>
        public static readonly IReadOnlyList<CatfishSpecies> Catalogue = new List<CatfishSpecies>
        {
            new CatfishSpecies("Whiskers",  new Color(0.55f, 0.45f, 0.30f), FishRarity.Common),   // muddy tan
            new CatfishSpecies("Old Tom",   new Color(0.35f, 0.38f, 0.42f), FishRarity.Uncommon), // slate grey
            new CatfishSpecies("Spotmouth", new Color(0.80f, 0.70f, 0.25f), FishRarity.Rare),     // spotted yellow
        };

        // The current pick odds, one weight per Catalogue entry (same index order). Weights are
        // relative and normalized at pick time, so any non-negative numbers work. Default
        // reproduces the old feel: heavily Whiskers, occasionally Old Tom, rarely Spotmouth.
        // Swapped by the equipped lure (LureShop.Equip -> SetActiveWeights).
        static float[] ActiveWeights = DefaultWeights();

        static float[] DefaultWeights()
        {
            // Length tracks the Catalogue; front-load 70/25/5, any extra species default to 1.
            var w = new float[Catalogue.Count];
            for (int i = 0; i < w.Length; i++) w[i] = 1f;
            if (w.Length > 0) w[0] = 70f;
            if (w.Length > 1) w[1] = 25f;
            if (w.Length > 2) w[2] = 5f;
            return w;
        }

        /// <summary>Sets the active pick odds. <paramref name="weights"/> must have one entry per
        /// Catalogue species, in Catalogue order (0=Whiskers, 1=Old Tom, 2=Spotmouth). Negative
        /// weights are clamped to 0. A null or wrong-length table is ignored (odds unchanged) so a
        /// mis-authored lure can't silently break picking. Applies to the NEXT fight — a fight
        /// already running read its odds at ResetFight and is unaffected.</summary>
        public static void SetActiveWeights(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count != Catalogue.Count) return;
            var w = new float[Catalogue.Count];
            for (int i = 0; i < w.Length; i++) w[i] = Mathf.Max(0f, weights[i]);
            ActiveWeights = w;
        }

        /// <summary>Weighted index into <see cref="Catalogue"/> using the active weights. Pure
        /// w.r.t. species state (only reads the table + rolls Random) so a statistical check can
        /// call it in a tight loop without perturbing anything. Falls back to a uniform roll if
        /// the weights sum to zero.</summary>
        public static int PickWeightedIndex()
        {
            int n = Catalogue.Count;
            float total = 0f;
            for (int i = 0; i < n; i++) total += WeightAt(i);
            if (total <= 0f) return UnityEngine.Random.Range(0, n);

            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < n; i++)
            {
                r -= WeightAt(i);
                if (r <= 0f) return i;
            }
            return n - 1; // floating-point guard: fall through lands on the last entry

            float WeightAt(int i) =>
                (ActiveWeights != null && i < ActiveWeights.Length) ? Mathf.Max(0f, ActiveWeights[i]) : 0f;
        }

        /// <summary>Random catfish for a fresh fight, weighted by the active odds. Uses
        /// UnityEngine.Random by design. Called once per fight from FishingTensionController.ResetFight().</summary>
        public static CatfishSpecies PickRandom()
        {
            return Catalogue[PickWeightedIndex()];
        }
    }
}
