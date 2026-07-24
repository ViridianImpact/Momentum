using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// Central species catalogue and catch-roll system (M2 refactor).
    /// Static C# class — NOT ScriptableObject, NOT JSON.
    /// Not yet wired into fight logic; FishingTensionController still uses the old path.
    /// </summary>
    public static class SpeciesRegistry
    {
        // =====================================================================
        // Tier-level Stat Templates (indexed by (int)FishRarity)
        // Plausible defaults — will be tuned later.
        // =====================================================================

        public static readonly StatTemplate[] TierTemplates = new StatTemplate[6]
        {
            // Common — low tension, short fight, no QTEs
            new StatTemplate
            {
                maxTension      = 0.30f,
                fightDuration   = 8f,
                qteFrequency    = 0f,
                progressPerReel = 0.08f,
                effortFrequency = 0.3f,
                effortIntensity = 0.30f
            },
            // Uncommon — moderate tension, moderate fight
            new StatTemplate
            {
                maxTension      = 0.50f,
                fightDuration   = 15f,
                qteFrequency    = 0.05f,
                progressPerReel = 0.07f,
                effortFrequency = 0.5f,
                effortIntensity = 0.50f
            },
            // Rare — high tension, long fight, occasional QTEs
            new StatTemplate
            {
                maxTension      = 0.70f,
                fightDuration   = 25f,
                qteFrequency    = 0.10f,
                progressPerReel = 0.06f,
                effortFrequency = 0.7f,
                effortIntensity = 0.70f
            },
            // Epic — very high tension, very long fight, frequent QTEs
            new StatTemplate
            {
                maxTension      = 0.85f,
                fightDuration   = 40f,
                qteFrequency    = 0.20f,
                progressPerReel = 0.05f,
                effortFrequency = 0.9f,
                effortIntensity = 0.85f
            },
            // Legendary — extreme tension, longest fight, constant QTEs
            new StatTemplate
            {
                maxTension      = 0.95f,
                fightDuration   = 60f,
                qteFrequency    = 0.35f,
                progressPerReel = 0.04f,
                effortFrequency = 1.2f,
                effortIntensity = 1.00f
            },
            // Mythic — same as Legendary for now (reserved slot, no species uses it)
            new StatTemplate
            {
                maxTension      = 0.95f,
                fightDuration   = 60f,
                qteFrequency    = 0.35f,
                progressPerReel = 0.04f,
                effortFrequency = 1.2f,
                effortIntensity = 1.00f
            }
        };

        // =====================================================================
        // Species Entry
        // =====================================================================

        public class SpeciesEntry
        {
            public string displayName;
            public Color swatchColor;
            public FishRarity rarity;
            public Region regions;           // bitmask
            public GearTier minRodTier;      // floor
            public GearTier maxRodTier;      // ceiling
            public string[] baitTags;        // e.g. ["worm"], ["minnow"], ["insect"]
            public float tierWeight = 1.0f;  // relative weight WITHIN rarity tier
            public QualityRoll qualityRoll;
            public StatTemplate statOffsets; // per-species offsets on top of tier template
        }

        // =====================================================================
        // Catalogue — 3 initial species matching Whiskers / Old Tom / Spotmouth
        // =====================================================================

        public static readonly IReadOnlyList<SpeciesEntry> Catalogue = new List<SpeciesEntry>
        {
            new SpeciesEntry
            {
                displayName = "Whiskers",
                swatchColor = new Color(0.60f, 0.70f, 0.25f),  // greenish catfish
                rarity      = FishRarity.Common,
                regions     = Region.All,
                minRodTier  = GearTier.T1,
                maxRodTier  = GearTier.T5,
                baitTags    = new[] { "worm" },
                tierWeight  = 1.0f,
                qualityRoll = new QualityRoll { minWeightKg = 0.5f, maxWeightKg = 3.0f,
                                                minHeightCm = 20f, maxHeightCm = 45f,
                                                qualityMultiplier = 1.0f },
                statOffsets = new StatTemplate()
            },
            new SpeciesEntry
            {
                displayName = "Old Tom",
                swatchColor = new Color(0.55f, 0.50f, 0.35f),  // brownish
                rarity      = FishRarity.Uncommon,
                regions     = Region.All,
                minRodTier  = GearTier.T1,
                maxRodTier  = GearTier.T5,
                baitTags    = new[] { "worm", "minnow" },
                tierWeight  = 1.0f,
                qualityRoll = new QualityRoll { minWeightKg = 2.0f, maxWeightKg = 8.0f,
                                                minHeightCm = 30f, maxHeightCm = 70f,
                                                qualityMultiplier = 1.5f },
                statOffsets = new StatTemplate()
            },
            new SpeciesEntry
            {
                displayName = "Spotmouth",
                swatchColor = new Color(0.75f, 0.40f, 0.20f),  // orange-spotted
                rarity      = FishRarity.Rare,
                regions     = Region.All,
                minRodTier  = GearTier.T2,
                maxRodTier  = GearTier.T5,
                baitTags    = new[] { "minnow" },
                tierWeight  = 1.0f,
                qualityRoll = new QualityRoll { minWeightKg = 4.0f, maxWeightKg = 15.0f,
                                                minHeightCm = 40f, maxHeightCm = 90f,
                                                qualityMultiplier = 2.0f },
                statOffsets = new StatTemplate()
            }
        };

        // =====================================================================
        // Pick() — full catch-roll algorithm
        // =====================================================================

        /// <summary>
        /// Roll a species from the catalogue given the current region, rod tier, lure, and optional bait.
        /// Returns null if no species matches the filters.
        /// </summary>
        public static SpeciesEntry Pick(Region region, GearTier rodTier, LureData lure, BaitData? bait = null)
        {
            // ---- Step 1: filter by region, rod tier band, and optionally bait ----
            List<SpeciesEntry> filtered = new List<SpeciesEntry>();
            foreach (var s in Catalogue)
            {
                if (!s.regions.HasFlag(region))
                    continue;
                if (rodTier < s.minRodTier)
                    continue;
                if (rodTier > s.maxRodTier)
                    continue;

                // Bait filter — skipped if bait is null (M4-ready)
                if (bait != null && bait.tags != null && bait.tags.Length > 0)
                {
                    bool hasMatchingTag = false;
                    if (s.baitTags != null)
                    {
                        foreach (var speciesTag in s.baitTags)
                        {
                            foreach (var baitTag in bait.tags)
                            {
                                if (speciesTag == baitTag)
                                {
                                    hasMatchingTag = true;
                                    break;
                                }
                            }
                            if (hasMatchingTag)
                                break;
                        }
                    }
                    if (!hasMatchingTag)
                        continue;
                }

                filtered.Add(s);
            }

            if (filtered.Count == 0)
                return null;

            // ---- Step 2: roll rarity tier ----
            float[] tierMods = lure?.rarityTierModifiers;
            float[] clamped = new float[6];
            if (tierMods != null && tierMods.Length > 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (i < tierMods.Length)
                        clamped[i] = Mathf.Max(0f, tierMods[i]);
                }
            }

            float sum = 0f;
            for (int i = 0; i < 6; i++)
                sum += clamped[i];

            FishRarity rolledTier;
            if (sum <= 0f)
            {
                // Uniform roll across all tiers
                rolledTier = (FishRarity)UnityEngine.Random.Range(0, 6);
            }
            else
            {
                // Weighted random
                float[] normalized = new float[6];
                for (int i = 0; i < 6; i++)
                    normalized[i] = clamped[i] / sum;
                rolledTier = (FishRarity)PickWeightedIndex(normalized);
            }

            // ---- Step 3: filter to rolled tier ----
            List<SpeciesEntry> tierPool = new List<SpeciesEntry>();
            foreach (var s in filtered)
            {
                if (s.rarity == rolledTier)
                    tierPool.Add(s);
            }

            // ---- Step 4: fallback if tier pool empty ----
            if (tierPool.Count == 0)
            {
                // Iterate tiers in ascending order, find first with species
                for (int t = 0; t < 6; t++)
                {
                    FishRarity fallbackTier = (FishRarity)t;
                    foreach (var s in filtered)
                    {
                        if (s.rarity == fallbackTier)
                            tierPool.Add(s);
                    }
                    if (tierPool.Count > 0)
                        break;
                }
            }

            if (tierPool.Count == 0)
                return null; // safety net

            // ---- Step 5: weighted pick within tier pool ----
            float[] tierWeights = new float[tierPool.Count];
            for (int i = 0; i < tierPool.Count; i++)
                tierWeights[i] = tierPool[i].tierWeight;

            int index = PickWeightedIndex(tierWeights);
            return tierPool[index];
        }

        // =====================================================================
        // Weighted Random Helper
        // =====================================================================

        /// <summary>
        /// Given an array of non-negative weights, returns a random index
        /// with probability proportional to weight.  0-weight items are never picked.
        /// If all weights are 0, returns a uniform random index.
        /// </summary>
        private static int PickWeightedIndex(float[] weights)
        {
            if (weights == null || weights.Length == 0)
                return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];

            if (total <= 0f)
                return UnityEngine.Random.Range(0, weights.Length);

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return i;
            }

            return weights.Length - 1; // fallback (floating-point safety)
        }
    }
}