using System;
using UnityEngine;

/// <summary>工坊材料背包。工位与编帘共用同一批可消耗的材料。</summary>
public static class WorkshopInventory
{
    internal static BambooCraftProgress TrackedCraft { get; private set; }
    public static int RoughPieces { get; private set; }
    public static int FinePieces { get; private set; }
    public static int DefectivePieces { get; private set; }
    public static int BambooRods { get; private set; }
    public static int DefectiveRods { get; private set; }
    public static int BambooThreads { get; private set; }
    public static int DefectiveThreads { get; private set; }
    public static int Revision { get; private set; }
    public static int GreenBamboo => GreenBambooBag.Count;
    public static int TotalThreads => BambooThreads + DefectiveThreads;
    public static int SpottedThreads => TrackedCraft?.SpottedThreads ?? 0;
    public static int TotalDefective => DefectivePieces + DefectiveRods + DefectiveThreads;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        TrackedCraft = null;
        RoughPieces = FinePieces = DefectivePieces = 0;
        BambooRods = DefectiveRods = BambooThreads = DefectiveThreads = 0;
        Revision = 0;
        Changed = null;
    }

    public static void StartFirstDay()
    {
        TrackedCraft = null;
        RoughPieces = FinePieces = DefectivePieces = 0;
        BambooRods = DefectiveRods = BambooThreads = DefectiveThreads = 0;
        NotifyChanged();
    }

    public static void NotifyGreenBambooChanged() => NotifyChanged();

    public static void CaptureCraft(BambooCraftProgress craft)
    {
        if (craft == null)
            return;
        TrackedCraft = craft;
        bool changed = RoughPieces != craft.RoughCount || FinePieces != craft.FineCount ||
            DefectivePieces != craft.DefectiveCount || BambooRods != craft.RodCount ||
            DefectiveRods != craft.DefectiveRodCount || BambooThreads != craft.ThreadCount ||
            DefectiveThreads != craft.DefectiveThreadCount;
        if (!changed)
            return;

        RoughPieces = craft.RoughCount;
        FinePieces = craft.FineCount;
        DefectivePieces = craft.DefectiveCount;
        BambooRods = craft.RodCount;
        DefectiveRods = craft.DefectiveRodCount;
        BambooThreads = craft.ThreadCount;
        DefectiveThreads = craft.DefectiveThreadCount;
        NotifyChanged();
    }

    public static bool ConsumeThreads(int count, out bool usedDefective) =>
        ConsumeThreads(count, out usedDefective, out _);

    internal static bool ConsumeThreads(int count, out bool usedDefective, out bool usedSpotted)
    {
        usedDefective = false;
        usedSpotted = false;
        if (TrackedCraft == null || !TrackedCraft.ConsumeThreads(count, out usedDefective, out usedSpotted))
            return false;
        CaptureCraft(TrackedCraft);
        return true;
    }

    internal static bool TradeThreads(int count, bool defective)
    {
        if (TrackedCraft == null || !TrackedCraft.TradeThreads(count, defective))
            return false;
        CaptureCraft(TrackedCraft);
        return true;
    }

    internal static void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke();
    }
}
