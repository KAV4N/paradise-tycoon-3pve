using System;
using UnityEngine;

// FR-030: data only; rolling and granting materials are handled separately.
[Serializable]
public sealed class WaveMaterialDropData
{
    public const int MinimumStars = 4;

    [Min(1)] public int waveNumber;
    [Range(0, 5)] public int ratingStars;
    [Min(0)] public int amount;

    public bool IsEligible => ratingStars >= MinimumStars && ratingStars <= 5;
}
