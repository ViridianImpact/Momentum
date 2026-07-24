using System;

namespace Momentum.Fishing
{
    /// <summary>
    /// Bitmask flag for fishing regions/biomes. A species can be catchable in one or more
    /// regions. The region detector supplies the active region at cast time.
    /// </summary>
    [Flags]
    public enum Region
    {
        None = 0,
        Lake  = 1 << 0,
        River = 1 << 1,
        Swamp = 1 << 2,
        All   = Lake | River | Swamp
    }
}