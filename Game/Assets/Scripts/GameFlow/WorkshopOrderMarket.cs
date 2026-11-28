public sealed partial class WorkshopOrder
{
    public const int GreenBambooSalePrice = 15;
    public const int SpottedBambooPurchasePrice = 100;
    public const int SpottedBambooSalePrice = 60;
    public const int TeaNoticeMinutes = 30;
    public const int TeaNoticeReward = 30;
    public const int SpottedBambooPenalty = 100;
    public const int ThreadBundle = 100;
    public const int ThreadBundlePrice = 40;
    public const int ScrapBundle = 25;
    public const int ScrapBundlePrice = 5;
    public const int BrightenerPrice = 20;
    public const int SabotagePenalty = 100;
    public const int CollectorBasePrice = 200;
    public const int KnifeUpgradePrice = 20;
    public const int ShuttleUpgradePrice = 30;
    public const int OfficialTipPrice = 50;
    public const int OfficialPaperPay = 30;
    public const int OfficialJobReward = OfficialPaperPay * PaperInspectionRound.SheetCount;
    public const int OfficialJobMinutes = 30;
    public const int OfficialOpeningHour = 12;
    public const int OfficialClosingHour = 18;

    int collectorVisitDay;
    int collectorSaleDay;
    int nightTraderNoticeDay;
    int lastTheftDay;
    int lastSabotageDay;
    bool spottedGroveHarvested;

    public int SpottedBamboo { get; private set; }
    public int Brightener { get; private set; }
    public bool TeaNoticeCompleted { get; private set; }
    public bool HasUpgradedKnife { get; private set; }
    public bool HasAutomaticShuttle { get; private set; }
    public bool HasOfficialTip { get; private set; }
    internal bool OfficialTipUnlocked => Days >= 2;
    internal bool CanBuyOfficialTip => OfficialTipUnlocked && CanVisitTeaStall && !HasOfficialTip &&
        Funds >= OfficialTipPrice;
    public bool OfficialJobCompleted { get; private set; }
    public PaperInspectionRound OfficialInspection { get; private set; }
    public int OfficialJobPay => OfficialJobCompleted ? OfficialInspection.Correct * OfficialPaperPay : 0;
    public bool OfficialStewardAvailable => CanDoSideQuests && HasOfficialTip &&
        Hour >= OfficialOpeningHour && Hour < OfficialClosingHour;
    public bool CanInspectOfficialPaper => OfficialStewardAvailable && !OfficialJobCompleted && !running.HasValue &&
        Hour * 60 + Minute + OfficialJobMinutes <= OfficialClosingHour * 60;
    public bool CanDoSideQuests => !DayEnded && (CurrentPhase == Phase.Village || CanExplore);
    public bool CanVisitTeaStall => CanDoSideQuests;
    public bool CanVisitSpottedGrove => CanDoSideQuests;
    public bool HasCollectorTip => collectorVisitDay == 2;
    public bool CollectorVisiting => CanExplore && Days == collectorVisitDay;
    public bool CollectorAvailable => CollectorVisiting && !CollectorBoughtToday;
    public bool CollectorBoughtToday => collectorSaleDay == Days;
    public bool CanStealSpottedBamboo => CanVisitSpottedGrove && !spottedGroveHarvested && lastTheftDay != Days;
    public bool SpottedGroveHarvested => spottedGroveHarvested;
    public bool XiaoPiSabotaged => lastSabotageDay == Days;
    public bool BlackMarketOpen => CanDoSideQuests && Hour >= 18;
    public bool CanSabotageXiaoPi => CanDoSideQuests && Brightener > 0 && !XiaoPiSabotaged;
    public QualityGrade? CollectorCurtainGrade => BestRibbedCurtain()?.Grade;

    internal bool TakeNightTraderArrivalNotice()
    {
        if (!BlackMarketOpen || nightTraderNoticeDay == Days)
            return false;
        nightTraderNoticeDay = Days;
        return true;
    }

    void ChangeFunds(int amount)
    {
        Funds += amount;
        TodayIncome += amount;
        Revision++;
    }

    public bool TrySellGreenBamboo()
    {
        if (!CanExplore || !GreenBambooBag.TryTakeOne())
            return false;
        ChangeFunds(GreenBambooSalePrice);
        return true;
    }

    public bool TryBuyKnifeUpgrade()
    {
        if (!CanExplore || HasUpgradedKnife || Funds < KnifeUpgradePrice)
            return false;
        HasUpgradedKnife = true;
        ChangeFunds(-KnifeUpgradePrice);
        return true;
    }

    public bool TryBuyShuttleUpgrade()
    {
        if (!CanExplore || HasAutomaticShuttle || Funds < ShuttleUpgradePrice)
            return false;
        HasAutomaticShuttle = true;
        ChangeFunds(-ShuttleUpgradePrice);
        return true;
    }

    public bool TryBuyOfficialTip()
    {
        if (!CanBuyOfficialTip)
            return false;
        HasOfficialTip = true;
        ChangeFunds(-OfficialTipPrice);
        return true;
    }

    public PaperInspectionRound OpenOfficialInspection()
    {
        if (!CanInspectOfficialPaper)
            return null;
        return OfficialInspection ??= new PaperInspectionRound();
    }

    public bool TryClassifyOfficialPaper(PaperInspectionRound.Condition choice)
    {
        if (!CanInspectOfficialPaper || OfficialInspection?.TryClassify(choice) != true)
            return false;
        if (OfficialInspection.Complete)
        {
            OfficialJobCompleted = true;
            ChangeFunds(OfficialJobPay);
            AdvanceTime(OfficialJobMinutes);
        }
        return true;
    }

    public bool TryBuySpottedBamboo()
    {
        if (!CanExplore || Funds < SpottedBambooPurchasePrice)
            return false;
        SpottedBamboo++;
        ChangeFunds(-SpottedBambooPurchasePrice);
        WorkshopInventory.NotifyChanged();
        return true;
    }

    public bool TrySellSpottedBamboo()
    {
        if (!CanExplore || !TryTakeSpottedBamboo())
            return false;
        ChangeFunds(SpottedBambooSalePrice);
        return true;
    }

    internal bool TryTakeSpottedBamboo()
    {
        if (DayEnded || SpottedBamboo <= 0)
            return false;
        SpottedBamboo--;
        Revision++;
        WorkshopInventory.NotifyChanged();
        return true;
    }

    public bool TryFinishTeaNotice()
    {
        if (!CanVisitTeaStall || TeaNoticeCompleted || MinutesUntilClose < TeaNoticeMinutes)
            return false;
        TeaNoticeCompleted = true;
        if (Days == 1)
            collectorVisitDay = 2;
        ChangeFunds(TeaNoticeReward);
        AdvanceTime(TeaNoticeMinutes);
        Journal.AddEvent($"{DayName} · 茶摊告示", "你帮茶摊写好告示，提醒乡亲们留心收古董的人。王老板付了茶水费。", announce: false);
        return true;
    }

    public bool TrySellToCollector()
    {
        if (!CollectorAvailable || running.HasValue)
            return false;
        Curtain curtain = BestRibbedCurtain();
        if (curtain == null || curtain.Grade > QualityGrade.A)
            return false;
        int reward = ValueCurtain(CollectorBasePrice, Coefficient(curtain.Grade), MaterialCoefficient(curtain));
        curtainStock.Remove(curtain);
        collectorSaleDay = Days;
        ChangeFunds(reward);
        StockChanged();
        return true;
    }

    Curtain BestRibbedCurtain()
    {
        Curtain best = null;
        foreach (Curtain curtain in curtainStock)
            if (curtain.State == CurtainState.Dried && curtain.Pattern == CurtainPatternKind.Ribbed &&
                curtain.Edge == CurtainEdgeStyle.None && (best == null || curtain.Grade < best.Grade))
                best = curtain;
        return best;
    }

    public bool TryResolveSpottedBambooTheft(bool caught)
    {
        if (!CanStealSpottedBamboo)
            return false;
        lastTheftDay = Days;
        if (caught)
            ChangeFunds(-SpottedBambooPenalty);
        else
        {
            spottedGroveHarvested = true;
            SpottedBamboo += 2;
            Revision++;
            WorkshopInventory.NotifyChanged();
        }
        return true;
    }

    public bool TrySellThreads(bool defective)
    {
        int count = defective ? ScrapBundle : ThreadBundle;
        if (!BlackMarketOpen || !WorkshopInventory.TradeThreads(count, defective))
            return false;
        ChangeFunds(defective ? ScrapBundlePrice : ThreadBundlePrice);
        return true;
    }

    public bool TryBuyBrightener()
    {
        if (!BlackMarketOpen || Funds < BrightenerPrice)
            return false;
        Brightener++;
        ChangeFunds(-BrightenerPrice);
        WorkshopInventory.NotifyChanged();
        return true;
    }

    public bool TrySabotageXiaoPi()
    {
        if (!CanSabotageXiaoPi)
            return false;
        Brightener--;
        lastSabotageDay = Days;
        Revision++;
        WorkshopInventory.NotifyChanged();
        return true;
    }
}
