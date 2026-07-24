using System;

namespace Momentum.Fishing
{
    /// <summary>
    /// Stat values that define the fight behaviour for a rarity tier or a specific species.
    /// Species-level StatTemplate is applied as an OFFSET on top of the tier template.
    /// All fields are raw values used by FishingTensionController to drive the fight.
    /// </summary>
    [Serializable]
    public struct StatTemplate
    {
        /// <summary>Maximum tension the fish can exert (0-1 normalised).</summary>
        public float maxTension;

        /// <summary>Base fight duration in seconds before the fish tires.</summary>
        public float fightDuration;

        /// <summary>How often QTEs fire per second (0 = none).</summary>
        public float qteFrequency;

        /// <summary>Progress per reel notch (0-1 fraction of the bar).</summary>
        public float progressPerReel;

        /// <summary>How often the fish changes struggle direction (per second).</summary>
        public float effortFrequency;

        /// <summary>Peak effort intensity added on top of reeling (0-1).</summary>
        public float effortIntensity;

        /// <summary>Add two templates together (tier base + species offset).</summary>
        public static StatTemplate operator +(StatTemplate a, StatTemplate b) => new StatTemplate
        {
            maxTension      = a.maxTension      + b.maxTension,
            fightDuration   = a.fightDuration   + b.fightDuration,
            qteFrequency    = a.qteFrequency    + b.qteFrequency,
            progressPerReel = a.progressPerReel + b.progressPerReel,
            effortFrequency = a.effortFrequency + b.effortFrequency,
            effortIntensity = a.effortIntensity + b.effortIntensity
        };

        public override string ToString() =>
            $"Tension:{maxTension:F2} Dur:{fightDuration:F1}s QTE:{qteFrequency:F2} " +
            $"Prog:{progressPerReel:F3} EffFreq:{effortFrequency:F2} EffInt:{effortIntensity:F2}";
    }
}