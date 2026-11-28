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

    int collectorVisitDay;
    int collectorSaleDay;
    int nightTraderNoticeDay;
    int lastTheftDay;
    int lastSabotageDay;
    bool spottedGroveHarvested;

    public int SpottedBamboo { get; private set; }
    public int Brightener { get; private set; }
    public bool TeaNoticeCompleted { get; private set; }
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
                !curtain.HasTrim && (best == null || curtain.Grade < best.Grade))
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
