using UnityEngine;

// S5: read-only description of the next upgrade of a building (before/after, price, Resort Value gain).
public sealed class UpgradeOffer
{
    public BuildingSystem.Building Building;
    public int LevelBefore, LevelAfter;
    public int SeatsBefore, SeatsAfter;
    public int WorkerSlotsBefore, WorkerSlotsAfter;
    public int CoinCost, MaterialCost;
    public int ResortValueGain;
    // False while the design documents leave the price open (e.g. Lv4/5 Materials = -1).
    public bool IsConfigured;
    public bool CanAfford;
    public int MissingCoins, MissingMaterials;
    public bool IsAvailable => IsConfigured && CanAfford;
}

// Partial of the existing BuildingSystem: reuses its private tables, changes no existing behaviour.
public partial class BuildingSystem
{
    // Returns false when the building cannot be upgraded any further (locked, Spa, max level).
    public bool TryGetUpgradeOffer(Building b, out UpgradeOffer offer)
    {
        offer = null;
        if (b == null || !b.unlocked || b.IsSpa || b.level < 1 || b.level >= MaxLevel(b))
            return false;

        int cost = Value(upgradeCoinsByLevel, b.level);
        int mats = Value(upgradeMaterialsByLevel, b.level);
        // Same rule as TryUpgrade / the S9 panel.
        bool configured = cost >= 0 && mats >= 0 && (b.level < 3 || mats > 0);

        offer = new UpgradeOffer
        {
            Building = b,
            LevelBefore = b.level,
            LevelAfter = b.level + 1,
            SeatsBefore = Capacity(b),
            SeatsAfter = Value(capacityByLevel, b.level),
            WorkerSlotsBefore = WorkerSlots(b),
            WorkerSlotsAfter = Value(workerSlotsByLevel, b.level),
            CoinCost = Mathf.Max(0, cost),
            MaterialCost = Mathf.Max(0, mats),
            ResortValueGain = Mathf.Max(0, resortValueSystem.UpgradeGain(resortValueByLevel, b.level)),
            IsConfigured = configured,
            CanAfford = configured && coins >= cost && materials >= mats,
            MissingCoins = Mathf.Max(0, cost - coins),
            MissingMaterials = Mathf.Max(0, mats - materials)
        };
        return true;
    }
}
