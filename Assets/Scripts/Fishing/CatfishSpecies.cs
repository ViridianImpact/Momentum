using System;
using System.Collections.Generic;
using UnityEngine;

namespace Momentum.Fishing
{
    /// <summary>
    /// Display-only identity for a caught catfish: a name + a swatch colour shown on the
    /// result panel. This carries NO fight stats — tension/HP/difficulty are still driven
    /// entirely by <see cref="FishData"/> in FishingTensionController, so every catfish
    /// fights identically. This is a content layer (who you caught), not a balance layer.
    ///
    /// Add a species = add one entry to <see cref="Catalogue"/>.
    /// </summary>
    [Serializable]
    public class CatfishSpecies
    {
        public string displayName;
        public Color swatchColor;

        public CatfishSpecies(string displayName, Color swatchColor)
        {
            this.displayName = displayName;
            this.swatchColor = swatchColor;
        }

        /// <summary>The catchable catfish, single biome. Identical stats — content only.</summary>
        public static readonly IReadOnlyList<CatfishSpecies> Catalogue = new List<CatfishSpecies>
        {
            new CatfishSpecies("Whiskers",  new Color(0.55f, 0.45f, 0.30f)), // muddy tan
            new CatfishSpecies("Old Tom",   new Color(0.35f, 0.38f, 0.42f)), // slate grey
            new CatfishSpecies("Spotmouth", new Color(0.80f, 0.70f, 0.25f)), // spotted yellow
        };

        /// <summary>Random catfish for a fresh fight. Uses UnityEngine.Random by design.</summary>
        public static CatfishSpecies PickRandom()
        {
            return Catalogue[UnityEngine.Random.Range(0, Catalogue.Count)];
        }
    }
}
