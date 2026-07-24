namespace Momentum.Fishing
{
    /// <summary>
    /// Rod/reel tier used for gear-band filtering in SpeciesRegistry.Pick().
    /// Tier 6 (Mythic) is reserved — no species uses it yet.
    /// </summary>
    public enum GearTier
    {
        Tier1 = 0,
        Tier2 = 1,
        Tier3 = 2,
        Tier4 = 3,
        Tier5 = 4,
        Mythic = 5
    }
}