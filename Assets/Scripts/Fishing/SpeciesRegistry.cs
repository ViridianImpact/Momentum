using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// Central species catalogue and catch-table engine.
    /// Replaces the hard-coded per-species weights in CatfishSpecies with a
    /// data-driven tier → weight → pick pipeline.
    /// NOT yet wired into the fight — FishingTensionController still calls the
    /// old path. Wiring happens in Prompt 3.
    /// </summary>
    public static class SpeciesRegistry
    {
        // =====================================================================
        // SpeciesEntry
        // =====================================================================

        [Serializable]
        public class SpeciesEntry
        {
            public string displayName;
            public Color swatchColor;
            public FishRarity rarity;
            public Region regions;          // bitmask
            public GearTier minRodTier;     // floor
            public GearTier maxRodTier;     // ceiling
            public string[] baitTags;       // e.g. ["worm"], ["minnow"], ["insect"]
            public float tierWeight = 1.0f; // relative weight WITHIN rarity tier
            public QualityRoll qualityRoll;
            public StatTemplate statOffsets; // per-species offsets on top of tier template
        }

        // =====================================================================
        // Tier Templates (indexed by (int)FishRarity)
        // =====================================================================

        /// <summary>
        /// Plausible defaults for all 6 tiers. These will be tuned later.
        /// Indexed by (int)FishRarity: Common=0, Uncommon=1, Rare=2,
        /// Epic=3, Legendary=4, Mythic=5.
        /// </summary>
        public static StatTemplate[] TierTemplates = new StatTemplate[6]
        {
            // Common — low tension, short fight, no QTEs
            new StatTemplate
            {
                baseTension = 40f,
                tensionGrowthRate = 8f,
                maxTension = 100f,
                fightDurationSeconds = 12f,
                qteFrequency = 0f,
                qteDifficulty = 0f,
                escapeChance = 0.05f,
                thrashIntensity = 0.3f,
                thrashFrequency = 0.2f,
            },
            // Uncommon — moderate tension, moderate fight
            new StatTemplate
            {
                baseTension = 55f,
                tensionGrowthRate = 12f,
                maxTension = 120f,
                fightDurationSeconds = 20f,
                qteFrequency = 0.15f,
                qteDifficulty = 0.2f,
                escapeChance = 0.08f,
                thrashIntensity = 0.5f,
                thrashFrequency = 0.35f,
            },
            // Rare — high tension, long fight, occasional QTEs
            new StatTemplate
            {
                baseTension = 70f,
                tensionGrowthRate = 16f,
                maxTension = 140f,
                fightDurationSeconds = 30f,
                qteFrequency = 0.3f,
                qteDifficulty = 0.4f,
                escapeChance = 0.12f,
                thrashIntensity = 0.7f,
                thrashFrequency = 0.5f,
            },
            // Epic — very high tension, very long fight, frequent QTEs
            new StatTemplate
            {
                baseTension = 85f,
                tensionGrowthRate = 20f,
                maxTension = 160f,
                fightDurationSeconds = 42f,
                qteFrequency = 0.5f,
                qteDifficulty = 0.6f,
                escapeChance = 0.16f,
                thrashIntensity = 0.85f,
                thrashFrequency = 0.65f,
            },
            // Legendary — extreme tension, longest fight, constant QTEs
            new StatTemplate
            {
                baseTension = 100f,
                tensionGrowthRate = 25f,
                maxTension = 180f,
                fightDurationSeconds = 55f,
                qteFrequency = 0.7f,
                qteDifficulty = 0.8f,
                escapeChance = 0.20f,
                thrashIntensity = 1.0f,
                thrashFrequency = 0.8f,
            },
            // Mythic — same as Legendary for now (reserved)
            new StatTemplate
            {
                baseTension = 100f,
                tensionGrowthRate = 25f,
                maxTension = 180f,
                fightDurationSeconds = 55f,
                qteFrequency = 0.7f,
                qteDifficulty = 0.8f,
                escapeChance = 0.20f,
                thrashIntensity = 1.0f,
                thrashFrequency = 0.8f,
            },
        };

        // =====================================================================
        // Catalogue
        // =====================================================================

        /// <summary>
        /// All registered species. Add new species here as they are designed.
        /// </summary>
        public static IReadOnlyList<SpeciesEntry> Catalogue { get; } = new List<SpeciesEntry>
        {
            // ---- Whiskers (Common) ----
            new SpeciesEntry
            {
                displayName = "Whiskers",
                swatchColor = new Color(0.55f, 0.70f, 0.55f), // muted green
                rarity = FishRarity.Common,
                regions = Region.All,
                minRodTier = GearTier.T1,
                maxRodTier = GearTier.T5,
                baitTags = new[] { "worm" },
                tierWeight = 1.0f,
                qualityRoll = new QualityRoll
                {
                    sizeMultiplierMin = 0.8f,
                    sizeMultiplierMax = 1.2f,
                    weightMultiplierMin = 0.8f,
                    weightMultiplierMax = 1.2f,
                    fightModifierMin = 0.9f,
                    fightModifierMax = 1.1f,
                },
                statOffsets = new StatTemplate(), // zero offsets
            },
            // ---- Old Tom (Uncommon) ----
            new SpeciesEntry
            {
                displayName = "Old Tom",
                swatchColor = new Color(0.65f, 0.50f, 0.35f), // brown
                rarity = FishRarity.Uncommon,
                regions = Region.All,
                minRodTier = GearTier.T1,
                maxRodTier = GearTier.T5,
                baitTags = new[] { "worm", "minnow" },
                tierWeight = 1.0f,
                qualityRoll = new QualityRoll
                {
                    sizeMultiplierMin = 0.9f,
                    sizeMultiplierMax = 1.3f,
                    weightMultiplierMin = 0.9f,
                    weightMultiplierMax = 1.3f,
                    fightModifierMin = 0.95f,
                    fightModifierMax = 1.15f,
                },
                statOffsets = new StatTemplate
                {
                    baseTension = 5f,
                    fightDurationSeconds = 3f,
                },
            },
            // ---- Spotmouth (Rare) ----
            new SpeciesEntry
            {
                displayName = "Spotmouth",
                swatchColor = new Color(0.85f, 0.35f, 0.35f), // reddish
                rarity = FishRarity.Rare,
                regions = Region.All,
                minRodTier = GearTier.T2,
                maxRodTier = GearTier.T5,
                baitTags = new[] { "minnow" },
                tierWeight = 1.0f,
                qualityRoll = new QualityRoll
                {
                    sizeMultiplierMin = 0.95f,
                    sizeMultiplierMax = 1.4f,
                    weightMultiplierMin = 0.95f,
                    weightMultiplierMax = 1.4f,
                    fightModifierMin = 0.9f,
                    fightModifierMax = 1.2f,
                },
                statOffsets = new StatTemplate
                {
                    baseTension = 10f,
                    tensionGrowthRate = 2f,
                    fightDurationSeconds = 5f,
                    qteFrequency = 0.05f,
                },
            },
        };

        // =====================================================================
        // Pick() — the catch-table engine
        // =====================================================================

        /// <summary>
        /// Select a species given the current region, rod tier, lure, and
        /// optional bait. Returns null if no species matches the filters.
        /// </summary>
        /// <param name="region">The region the player is fishing in.</param>
        /// <param name="rodTier">The tier of the equipped rod.</param>
        /// <param name="lure">The equipped lure (provides rarityTierModifiers).</param>
        /// <param name="bait">Optional bait. If null, bait filtering is skipped (M4-ready).</param>
        public static SpeciesEntry Pick(Region region, GearTier rodTier, LureData lure, BaitData? bait = null)
        {
            // 1. Filter by region, rod tier band, and optional bait
            List<SpeciesEntry> filtered = new List<SpeciesEntry>();
            foreach (var species in Catalogue)
            {
                if (!species.regions.HasFlag(region))
                    continue;
                if (rodTier < species.minRodTier)
                    continue;
                if (rodTier > species.maxRodTier)
                    continue;
                if (bait.HasValue && bait.Value.tags != null && bait.Value.tags.Length > 0)
                {
                    if (!species.baitTags.Intersect(bait.Value.tags).Any())
                        continue;
                }
                filtered.Add(species);
            }

            // 2. If filtered pool is empty, return null
            if (filtered.Count == 0)
                return null;

            // 3. Roll rarity tier using lure.rarityTierModifiers
            float[] tierMods = lure.rarityTierModifiers;
            int tierCount = tierMods != null ? tierMods.Length : 0;
            float[] tierProbs = new float[6]; // one per FishRarity value

            if (tierCount > 0)
            {
                // Clamp negatives to 0
                for (int i = 0; i < 6; i++)
                {
                    float val = i < tierCount ? tierMods[i] : 0f;
                    tierProbs[i] = Mathf.Max(0f, val);
                }
            }
            else
            {
                // No modifiers — uniform
                for (int i = 0; i < 6; i++)
                    tierProbs[i] = 1f;
            }

            float tierSum = 0f;
            for (int i = 0; i < 6; i++)
                tierSum += tierProbs[i];

            FishRarity rolledTier;
            if (tierSum <= 0f)
            {
                // Uniform roll across all tiers
                rolledTier = (FishRarity)UnityEngine.Random.Range(0, 6);
            }
            else
            {
                // Weighted random
                float roll = UnityEngine.Random.Range(0f, tierSum);
                float accum = 0f;
                int chosen = 0;
                for (int i = 0; i < 6; i++)
                {
                    accum += tierProbs[i];
                    if (roll <= accum)
                    {
                        chosen = i;
                        break;
                    }
                }
                rolledTier = (FishRarity)chosen;
            }

            // 4. Filter pool to species where species.rarity == rolledTier
            List<SpeciesEntry> tierPool = new List<SpeciesEntry>();
            foreach (var species in filtered)
            {
                if (species.rarity == rolledTier)
                    tierPool.Add(species);
            }

            // 5. If tier pool is empty, fall back through tiers
            if (tierPool.Count == 0)
            {
                FishRarity[] fallbackOrder =
                {
                    FishRarity.Common,
                    FishRarity.Uncommon,
                    FishRarity.Rare,
                    FishRarity.Epic,
                    FishRarity.Legendary,
                };
                foreach (var fallbackTier in fallbackOrder)
                {
                    foreach (var species in filtered)
                    {
                        if (species.rarity == fallbackTier)
                            tierPool.Add(species);
                    }
                    if (tierPool.Count > 0)
                        break;
                }
            }

            // If still empty (shouldn't happen since filtered was non-empty), return null
            if (tierPool.Count == 0)
                return null;

            // 6. Weight each species by tierWeight
            float[] weights = new float[tierPool.Count];
            for (int i = 0; i < tierPool.Count; i++)
                weights[i] = tierPool[i].tierWeight;

            // 7. Weighted random pick
            int pickedIndex = PickWeightedIndex(weights);
            return tierPool[pickedIndex];
        }

        // =====================================================================
        // PickWeightedIndex helper
        // =====================================================================

        /// <summary>
        /// Given an array of non-negative weights, returns a random index
        /// selected with probability proportional to each weight.
        /// If all weights are zero, returns a uniform random index.
        /// </summary>
        private static int PickWeightedIndex(float[] weights)
        {
            if (weights == null || weights.Length == 0)
                return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += Mathf.Max(0f, weights[i]);

            if (total <= 0f)
                return UnityEngine.Random.Range(0, weights.Length);

            float roll = UnityEngine.Random.Range(0f, total);
            float accum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                accum += Mathf.Max(0f, weights[i]);
                if (roll <= accum)
                    return i;
            }

            return weights.Length - 1; // fallback
        }
    }
}