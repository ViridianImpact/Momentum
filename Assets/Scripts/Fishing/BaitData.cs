using System;

namespace Momentum.Fishing
{
    /// <summary>
    /// Runtime data for bait equipped by the player.
    /// The tags array is matched against SpeciesEntry.baitTags in SpeciesRegistry.Pick().
    /// </summary>
    [Serializable]
    public class BaitData
    {
        [Tooltip("Tags that describe this bait type (e.g. \"worm\", \"minnow\", \"insect\").")]
        public string[] tags = new string[0];
    }
}