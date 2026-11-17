using System;
using UnityEngine;

/// <summary>第一天工坊材料背包。只保存数量，不持有场景对象或原生资源。</summary>
public static class WorkshopInventory
{
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
    public static int TotalDefective => DefectivePieces + DefectiveRods + DefectiveThreads;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        RoughPieces = FinePieces = DefectivePieces = 0;
        BambooRods = DefectiveRods = BambooThreads = DefectiveThreads = 0;
        Revision = 0;
        Changed = null;
    }

    public static void StartFirstDay()
    {
        RoughPieces = FinePieces = DefectivePieces = 0;
        BambooRods = DefectiveRods = BambooThreads = DefectiveThreads = 0;
        NotifyChanged();
    }

    public static void NotifyGreenBambooChanged() => NotifyChanged();

    public static void CaptureCraft(BambooCraftProgress craft)
    {
        if (craft == null)
            return;
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

    public static bool ConsumeThreads(int count, out bool usedDefective)
    {
        usedDefective = false;
        if (count <= 0 || TotalThreads < count)
            return false;
        int good = Mathf.Min(BambooThreads, count);
        BambooThreads -= good;
        int remaining = count - good;
        if (remaining > 0)
        {
            DefectiveThreads -= remaining;
            usedDefective = true;
        }
        NotifyChanged();
        return true;
    }

    static void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke();
    }
}
