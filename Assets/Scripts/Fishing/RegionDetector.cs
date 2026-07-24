using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// Utility that maps world-space positions to Region flags.
    /// Works by checking the player's position against scene-defined trigger volumes.
    /// Falls back to Region.All until explicit zones are placed in scene.
    /// </summary>
    public static class RegionDetector
    {
        /// <summary>
        /// Returns the Region at a world-space position.
        /// Stub: always returns Region.All until scene zones are configured.
        /// </summary>
        public static Region GetRegion(Vector3 worldPosition)
        {
            // M4+: check against trigger zones tagged "FishingRegion"
            // For now, return All so filtering doesn't exclude anything.
            return Region.All;
        }
    }
}