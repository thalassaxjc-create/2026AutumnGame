using System;
using System.Collections.Generic;

public sealed partial class WorkshopOrder
{
    enum CurtainState { Woven, Lacquered, Drying, Dried }

    sealed class Curtain
    {
        public QualityGrade Weaving { get; }
        public QualityGrade Lacquer { get; set; }
        public bool UsedDefective { get; }
        public bool UsedSpotted { get; }
        public CurtainState State { get; set; }
        public int DryAt { get; set; }
        public CurtainPatternKind Pattern { get; set; }
        public QualityGrade? PatternGrade { get; set; }
        public CurtainEdgeStyle Edge { get; set; }
        public Dictionary<CurtainPatternKind, CurtainPatternDraft> PatternDrafts { get; } = new();
        public Dictionary<CurtainEdgeStyle, VermilionTrimDraft> TrimDrafts { get; } = new();
        public SheetStyle Style => new(Pattern, Edge);
        public bool IsPlain => State == CurtainState.Dried && Pattern == CurtainPatternKind.None && Edge == CurtainEdgeStyle.None;
        public QualityGrade Grade => CombineGrades(Weaving, Lacquer, PatternGrade);

        public Curtain(QualityGrade weaving, bool defective, bool spotted)
        {
            Weaving = weaving;
            UsedDefective = defective;
            UsedSpotted = spotted;
        }
    }

    readonly List<Curtain> curtainStock = new();
    Stage? running;
    public CurtainPatternDraft PatternDraft { get; private set; }
    public VermilionTrimDraft TrimDraft { get; private set; }
    public int WovenCurtains => CountCurtains(CurtainState.Woven);
    public int LacqueredCurtains => CountCurtains(CurtainState.Lacquered);
    public int DryingCurtains => CountCurtains(CurtainState.Drying);
    public int PlainCurtains => CountCurtains(CurtainState.Dried, plainOnly: true);
    public int DriedCurtains => CountCurtains(CurtainState.Dried);
    public int FinishedCurtains => DriedCurtains - PlainCurtains;
    public int AvailableOrderCurtains => CountOrderCurtains();
    public int CurtainRevision { get; private set; }
    public bool IsDrying => FirstCurtain(CurtainState.Drying) != null;
    public bool CanUsePatternStation => !DayEnded && !running.HasValue && FirstPlainCurtain() != null;
    public bool CanUseTrimStation => CanUsePatternStation;
    public bool CanStartDrying => !DayEnded && !running.HasValue && FirstCurtain(CurtainState.Lacquered) != null;
    int CalendarMinute => (Days - 1) * FirstDayEndMinute + minuteOfDay;
    public int RemainingDryingMinutes
    {
        get
        {
            int soonest = int.MaxValue;
            foreach (Curtain curtain in curtainStock)
                if (curtain.State == CurtainState.Drying)
                    soonest = Math.Min(soonest, curtain.DryAt - CalendarMinute);
            return soonest == int.MaxValue ? 0 : Math.Max(0, soonest);
        }
    }

    Curtain FirstCurtain(CurtainState state)
    {
        foreach (Curtain curtain in curtainStock)
            if (curtain.State == state)
                return curtain;
        return null;
    }

    int CountCurtains(CurtainState state, bool plainOnly = false)
    {
        int count = 0;
        foreach (Curtain curtain in curtainStock)
            if (curtain.State == state && (!plainOnly || curtain.IsPlain))
                count++;
        return count;
    }

    Curtain FirstPlainCurtain() => curtainStock.Find(curtain => curtain.IsPlain);

    bool MatchesOrder(Curtain curtain) => curtain.State == CurtainState.Dried && NeedsStyle(curtain.Style);

    int CountOrderCurtains()
    {
        int count = 0;
        for (int index = 0; index < SheetsPerDay; index++)
            if (IsSheetReady(index))
                count++;
        return count;
    }

    public bool IsSheetReady(int index)
    {
        SheetStyle style = RequestedSheet(index);
        int needed = RequiredThrough(index, style) - DeliveredFor(style);
        if (needed <= 0)
            return false;
        foreach (Curtain curtain in curtainStock)
            if (curtain.State == CurtainState.Dried && curtain.Style.Matches(style) && --needed == 0)
                return true;
        return false;
    }

    Curtain FindDeliverable()
    {
        foreach (Curtain curtain in curtainStock)
            if (MatchesOrder(curtain))
                return curtain;
        return null;
    }

    void UpdateOrderPhase()
    {
        if (CurrentPhase is Phase.Production or Phase.ReadyToSubmit)
            CurrentPhase = FindDeliverable() == null ? Phase.Production : Phase.ReadyToSubmit;
    }

    void StockChanged(int workMinutes = 0)
    {
        CurtainRevision++;
        UpdateOrderPhase();
        Revision++;
        AdvanceTime(workMinutes);
    }

    public bool CanPlay(Stage stage)
    {
        if (DayEnded || running.HasValue || IsTeachingDay || AwaitingReturnedCurtain)
            return false;
        return stage switch
        {
            Stage.Weaving => WorkshopInventory.TotalThreads >= BambooCraftProgress.ThreadsPerBatch,
            Stage.Lacquer => FirstCurtain(CurtainState.Woven) != null,
            _ => false
        };
    }

    public bool Begin(Stage stage)
    {
        if (!CanPlay(stage))
            return false;
        running = stage;
        Revision++;
        return true;
    }

    public void Leave(Stage stage)
    {
        if (running != stage)
            return;
        running = null;
        Revision++;
    }

    public void ObserveBamboo(BambooCraftProgress craft)
    {
        if (DayEnded)
            return;
        int inventoryRevision = WorkshopInventory.Revision;
        WorkshopInventory.CaptureCraft(craft);
        if (WorkshopInventory.Revision != inventoryRevision)
            Revision++;
        AdvanceTime(craft.TakeCompletedWorkMinutes(settings.preparationMinutes, settings.bambooFinishingMinutes));
    }

    public void ConfirmWeaving(WeavingMinigameController.WeavingResult result)
    {
        if (running != Stage.Weaving ||
            !WorkshopInventory.ConsumeThreads(BambooCraftProgress.ThreadsPerBatch, out bool defective, out bool spotted))
            return;
        QualityGrade grade = (QualityGrade)result.grade;
        if (grade == QualityGrade.SPlus && !HasAutomaticShuttle)
            grade = QualityGrade.S;
        curtainStock.Add(new Curtain(grade, defective, spotted));
        running = null;
        StockChanged(result.gameMinutes);
    }

    public void ConfirmLacquer(LacquerMinigameController.LacquerResult result)
    {
        Curtain curtain = FirstCurtain(CurtainState.Woven);
        if (running != Stage.Lacquer || curtain == null)
            return;
        curtain.Lacquer = (QualityGrade)result.grade;
        curtain.State = CurtainState.Lacquered;
        running = null;
        StockChanged(result.gameMinutes);
    }

    public bool BeginDrying()
    {
        if (!CanStartDrying)
            return false;
        Curtain curtain = FirstCurtain(CurtainState.Lacquered);
        curtain.State = CurtainState.Drying;
        curtain.DryAt = CalendarMinute + settings.dryingMinutes;
        StockChanged();
        return true;
    }

    void CompleteDryingIfReady()
    {
        foreach (Curtain curtain in curtainStock)
        {
            if (curtain.State != CurtainState.Drying || curtain.DryAt > CalendarMinute)
                continue;
            curtain.State = CurtainState.Dried;
            CurtainRevision++;
        }
        UpdateOrderPhase();
    }

    public bool TrySelectPattern(CurtainPatternKind selection)
    {
        Curtain curtain = FirstPlainCurtain();
        if (DayEnded || running.HasValue || curtain == null || CurtainPatternDesign.Get(selection) == null)
            return false;
        if (IsTeachingDay && (CurrentLesson != LessonBeat.Pattern || selection != AilinPattern))
            return false;
        if (!curtain.PatternDrafts.TryGetValue(selection, out CurtainPatternDraft draft))
        {
            draft = new CurtainPatternDraft(selection);
            curtain.PatternDrafts.Add(selection, draft);
        }
        PatternDraft = draft;
        return true;
    }

    public bool TryConfirmPattern()
    {
        Curtain curtain = FirstPlainCurtain();
        if (DayEnded || running.HasValue || curtain == null || PatternDraft?.CanConfirm != true ||
            !curtain.PatternDrafts.ContainsValue(PatternDraft) || MinutesUntilClose < PatternMinutes)
            return false;
        curtain.Pattern = PatternDraft.Kind;
        curtain.PatternGrade = PatternDraft.Grade;
        curtain.PatternDrafts.Clear();
        curtain.TrimDrafts.Clear();
        if (IsTeachingDay)
            RecordAilinWeave(curtain.PatternGrade, PatternDraft);
        PatternDraft = null;
        TrimDraft = null;
        StockChanged(PatternMinutes);
        return true;
    }

    public VermilionTrimDraft OpenTrimDraft(CurtainEdgeStyle style = CurtainEdgeStyle.VermilionJade)
    {
        Curtain curtain = FirstPlainCurtain();
        if (!CanUseTrimStation || curtain == null || !CurtainEdgePalette.IsFinish(style))
            return null;
        if (!curtain.TrimDrafts.TryGetValue(style, out var selected))
        {
            selected = new VermilionTrimDraft(style);
            curtain.TrimDrafts.Add(style, selected);
        }
        return TrimDraft = selected;
    }

    public bool TryConfirmVermilionTrim()
    {
        Curtain curtain = FirstPlainCurtain();
        if (DayEnded || running.HasValue || curtain == null || TrimDraft?.Complete != true ||
            !curtain.TrimDrafts.ContainsValue(TrimDraft) || MinutesUntilClose < TrimMinutes)
            return false;
        curtain.Edge = TrimDraft.Style;
        curtain.TrimDrafts.Clear();
        curtain.PatternDrafts.Clear();
        PatternDraft = null;
        TrimDraft = null;
        StockChanged(TrimMinutes);
        return true;
    }
}
