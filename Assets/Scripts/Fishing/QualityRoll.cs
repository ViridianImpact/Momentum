using System;

namespace Momentum.Fishing
{
    /// <summary>
    /// Per-cast randomisation values that scale the quality of a catch.
    /// Rolled once when the bite is locked in, applied to the species/tier stats
    /// to produce the final FishData.
    /// </summary>
    [Serializable]
    public struct QualityRoll
    {
        /// <summary>Normalised size multiplier, e.g. 0.5 = half size, 1.5 = 150 %.</summary>
        public float sizeFactor;

        /// <summary>Normalised weight multiplier applied on top of sizeFactor.</summary>
        public float weightFactor;

        /// <summary>
        /// Condition multiplier affecting tension at the start of the fight.
        /// Lower values = tired fish, higher values = aggressive fish.
        /// </summary>
        public float conditionFactor;

        /// <summary>Raw percentile used for roll tracking / telemetry.</summary>
        public float rawPercentile;

        public static QualityRoll Default => new QualityRoll
        {
            sizeFactor      = 1.0f,
            weightFactor    = 1.0f,
            conditionFactor = 1.0f,
            rawPercentile   = 0.5f
        };

        public override string ToString() =>
            $"Size:{sizeFactor:F2} Wt:{weightFactor:F2} Cond:{conditionFactor:F2} Pct:{rawPercentile:F2}";
    }
}