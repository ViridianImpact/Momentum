using System;

namespace Momentum.Fishing
{
    /// <summary>
    /// Runtime data for a lure equipped by the player.
    /// Used by SpeciesRegistry.Pick() to drive rarity-tier weighting.
    /// </summary>
    [Serializable]
    public class LureData
    {
        [Tooltip("Display name for UI.")]
        public string displayName = "Basic Lure";

        [Tooltip("Rarity-tier modifiers indexed by (int)FishRarity. " +
                 "Each value is a relative weight for that tier. " +
                 "Higher = more likely to roll that tier. Zero or negative = impossible.")]
        public float[] rarityTierModifiers = new float[6]
        {
            // Common  Uncommon  Rare  Epic  Legendary  Mythic
               1.0f,    0.8f,    0.3f, 0.0f, 0.0f,     0.0f
        };
    }
}