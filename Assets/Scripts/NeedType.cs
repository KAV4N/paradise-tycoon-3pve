using System;

// Shared MVP wave needs from the design documents.
// Keep these explicit IDs stable when extending the enum or preparing save data.
public enum NeedType
{
    None = 0,
    Rooms = 1,
    Food = 2,
    Drinks = 3,
    Relax = 4,
    OceanView = 5
}

public static class NeedTypes
{
    public static bool IsWaveNeed(this NeedType need) =>
        need == NeedType.Rooms || need == NeedType.Food || need == NeedType.Drinks ||
        need == NeedType.Relax || need == NeedType.OceanView;

    public static string GetDisplayName(this NeedType need)
    {
        switch (need)
        {
            case NeedType.None: return "None";
            case NeedType.Rooms: return "Rooms";
            case NeedType.Food: return "Food";
            case NeedType.Drinks: return "Drinks";
            case NeedType.Relax: return "Relax";
            case NeedType.OceanView: return "Ocean View";
            default: throw new ArgumentOutOfRangeException(nameof(need), need, "Unknown need type");
        }
    }

    // Migration of the original scene's string fields only; not a save/load system.
    public static bool TryFromLegacyName(string name, out NeedType need)
    {
        switch (name)
        {
            case "Rooms": need = NeedType.Rooms; return true;
            case "Food": need = NeedType.Food; return true;
            case "Drinks": need = NeedType.Drinks; return true;
            case "Relax": need = NeedType.Relax; return true;
            case "Ocean View": need = NeedType.OceanView; return true;
            default: need = NeedType.None; return false;
        }
    }
}
