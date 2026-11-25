using System;

// S5: picks the recommended investment after a wave.
// Rule: the upgradeable building whose need had the LOWEST average satisfaction in the wave.
// Tie-break: the higher load (guests per seat). If nothing from the wave can be upgraded,
// falls back to the lowest-level upgradeable building. Returns false when everything is maxed.
public static class UpgradeRecommender
{
    public static bool TryRecommend(BuildingSystem buildings, WaveSnapshot wave,
        Func<NeedSnapshot, double> satisfactionOf, out UpgradeOffer offer, out string reason)
    {
        offer = null; reason = "";
        if (buildings == null) return false;

        double bestScore = double.MaxValue, bestLoad = -1;
        NeedType bestNeed = NeedType.None;

        if (wave != null)
        {
            foreach (var need in wave.Needs)
            {
                if (need.Total <= 0 || !need.Type.IsWaveNeed()) continue;
                var building = buildings.Find(WaveModelComponent.BuildingId(need.Type));
                if (!buildings.TryGetUpgradeOffer(building, out var candidate)) continue;

                double score = satisfactionOf != null ? satisfactionOf(need) : 100;
                double load = candidate.SeatsBefore > 0 ? (double)need.Total / candidate.SeatsBefore : 0;
                bool better = offer == null || score < bestScore - 1e-9 ||
                              (Math.Abs(score - bestScore) <= 1e-9 && load > bestLoad);
                if (!better) continue;
                offer = candidate; bestScore = score; bestLoad = load; bestNeed = need.Type;
            }
        }

        if (offer != null)
        {
            reason = bestScore < 99.5
                ? $"{bestNeed.GetDisplayName()} guests were the least satisfied this wave."
                : "All guests were happy - invest now to keep up with bigger waves.";
            return true;
        }

        // Fallback: cheapest next step among all upgradeable buildings.
        foreach (var building in buildings.buildings)
        {
            if (!buildings.TryGetUpgradeOffer(building, out var candidate)) continue;
            if (offer == null || candidate.LevelBefore < offer.LevelBefore) offer = candidate;
        }
        if (offer == null) return false;
        reason = "Invest in capacity for the next waves.";
        return true;
    }
}
