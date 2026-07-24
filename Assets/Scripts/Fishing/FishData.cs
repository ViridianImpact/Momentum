using System;
using UnityEngine;

namespace Momentum.Fishing
{
    public enum FishRarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

    public enum GearTier { Basic, Advanced, Pro, Master }

    /// <summary>
    /// Pure data describing one catchable fish. ALL fight behaviour is driven from
    /// these fields, never from hardcoded constants in the controller. Adding a new
    /// fish = adding one more entry to FishingTensionController.fishCatalogue.
    /// </summary>
    [Serializable]
    public class FishData
    {
        [Tooltip("Name shown on the 'Landed ...!' panel.")]
        public string displayName = "River Bass";

        [Tooltip("How often the fish picks a new struggle (roughly struggles per second). " +
                 "Higher = it changes its effort more frequently / less predictably.")]
        public float effortFrequency = 0.7f;

        [Range(0f, 1f)]
        [Tooltip("How hard the fish can fight. This is the max effort (0..1) added on top of " +
                 "your reeling when it peaks. Higher = harder to keep in the green.")]
        public float effortIntensity = 0.40f;

        [Tooltip("Fraction of the progress bar (0..1) filled per mouse-wheel reel notch. " +
                 "Higher = lands faster for the same amount of reeling.")]
        public float progressPerReel = 0.035f;

        [Tooltip("Catalogue rarity tag. Cosmetic for this prototype.")]
        public FishRarity rarity = FishRarity.Common;
    }
}
