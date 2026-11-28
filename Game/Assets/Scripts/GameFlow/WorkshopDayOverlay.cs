using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>World clock, end-of-day action, and daily earnings.</summary>
public sealed class WorkshopDayOverlay : MonoBehaviour
{
    static readonly WorkshopOrder.ClockSpeed[] Speeds =
    {
        WorkshopOrder.ClockSpeed.Paused, WorkshopOrder.ClockSpeed.Normal,
        WorkshopOrder.ClockSpeed.Double, WorkshopOrder.ClockSpeed.Quadruple
    };
    static readonly string[] SpeedLabels = { "暂停", "正常", "2×", "4×" };

    readonly Button[] speedButtons = new Button[Speeds.Length];
    WorkshopOrder order;
    GameObject clockBar;
    Button endDay;
    Text endDayLabel;
    WorkshopOrder.ClockSpeed renderedSpeed = (WorkshopOrder.ClockSpeed)(-1);
    GameObject morning;
    PlayerController morningPlayer;
    bool morningPlayerWasEnabled;
    GameObject report;
    Text reportTitle;
    Text reportRows;
    Text reportAction;
    Button alternateAction;
    int endingStep;
    bool shareNotes;
    SettlementExitController timeoutExit;

    public void Build(WorkshopOrder story)
    {
        order = story;
        var bar = WorkshopStoryUI.Panel(transform, "时间倍率", new Rect(1, 0, 0, 0),
            new Color32(45, 35, 27, 232));
        bar.rectTransform.pivot = new Vector2(1, 0);
        bar.rectTransform.anchoredPosition = new Vector2(-30, 26);
        bar.rectTransform.sizeDelta = new Vector2(405, 52);
        clockBar = bar.gameObject;
        WorkshopStoryUI.Label(bar.transform, "标题", "时间", new Rect(.015f, .09f, .16f, .82f),
            24, WorkshopStoryUI.Paper, bold: true);
        for (int i = 0; i < Speeds.Length; i++)
        {
            var speed = Speeds[i];
            speedButtons[i] = WorkshopStoryUI.Button(bar.transform, "时间 · " + SpeedLabels[i], SpeedLabels[i],
                new Rect(.18f + .202f * i, .10f, .19f, .80f), WorkshopStoryUI.Wood,
                24, () => SelectSpeed(speed));
        }
        DrawSpeed();
        endDay = WorkshopStoryUI.Button(transform, "进入下一天", "进入下一天", new Rect(0, 1, 0, 0),
            WorkshopStoryUI.Green, 24, EndDay);
        var endDayRect = (RectTransform)endDay.transform;
        endDayRect.pivot = new Vector2(0, 1);
        endDayRect.anchoredPosition = new Vector2(32, -333);
        endDayRect.sizeDelta = new Vector2(176, 44);
        endDayLabel = endDay.GetComponentInChildren<Text>();
    }

    public void Refresh(bool worldVisible, bool inventoryExpanded)
    {
        bool showClock = worldVisible && order.CurrentPhase != WorkshopOrder.Phase.Complete;
        if (clockBar.activeSelf != showClock)
            clockBar.SetActive(showClock);
        if (showClock)
            DrawSpeed();
        bool showEndDay = worldVisible && order.CanCloseDay && !order.TaughtLesson.HasValue;
        if (endDay.gameObject.activeSelf != showEndDay)
            endDay.gameObject.SetActive(showEndDay);
        if (showEndDay)
        {
            ((RectTransform)endDay.transform).anchoredPosition = new Vector2(32, inventoryExpanded ? -338 : -129);
            endDayLabel.text = order.Days < WorkshopOrder.LastStoryDay ? "进入下一天" : "结算比赛";
            endDay.interactable = !VillageSpottedGrove.IsSneaking;
        }
        if (worldVisible && !order.FinalSettlementAcknowledged && order.CurrentSettlement is { } record)
            ShowReport(record);
    }

    void EndDay()
    {
        if (GameFlowModalState.IsModalOpen || SceneTransitionState.IsLoading ||
            VillageSpottedGrove.IsSneaking || !order.CloseDay())
            return;
        if (order.CanAdvanceDay && WorkshopStoryUI.AdvanceDay(order))
            return;
        ShowReport(order.CurrentSettlement.Value);
    }

    void SelectSpeed(WorkshopOrder.ClockSpeed speed)
    {
        if (GameFlowModalState.IsModalOpen || SceneTransitionState.IsLoading ||
            order.CurrentPhase == WorkshopOrder.Phase.Complete)
            return;
        order.SetClockSpeed(speed);
        DrawSpeed();
    }

    void DrawSpeed()
    {
        if (renderedSpeed == order.Speed)
            return;
        renderedSpeed = order.Speed;
        for (int i = 0; i < Speeds.Length; i++)
        {
            bool active = Speeds[i] == renderedSpeed;
            speedButtons[i].image.color = active ? WorkshopStoryUI.Green : WorkshopStoryUI.Wood;
            speedButtons[i].GetComponentInChildren<Text>().text = active ? "· " + SpeedLabels[i] : SpeedLabels[i];
        }
    }

    public void ShowMorning()
    {
        if (!order.TakeMorningNotice())
            return;
        var cover = WorkshopStoryUI.Panel(transform, "换日字幕", new Rect(0, 0, 1, 1), WorkshopStoryUI.Ink, true);
        morning = cover.gameObject;
        WorkshopStoryUI.Label(cover.transform, "新一天", order.DayName,
            new Rect(.18f, .60f, .64f, .13f), 72, WorkshopStoryUI.Paper, bold: true);
        WorkshopStoryUI.Label(cover.transform, "清晨", "06:00   ·   院子",
            new Rect(.20f, .525f, .60f, .08f), 32, WorkshopStoryUI.Paper);
        if (order.PreviousSettlement is { } record)
        {
            WorkshopStoryUI.Label(cover.transform, "昨日账目", WorkshopStoryUI.DailyIncomeText(record, "昨日净收入"),
                new Rect(.19f, .38f, .62f, .10f), 34, WorkshopStoryUI.Paper, bold: true);
            WorkshopStoryUI.Label(cover.transform, "资金累计", WorkshopStoryUI.CumulativeFundsText(record),
                new Rect(.19f, .29f, .62f, .08f), 29, WorkshopStoryUI.Paper);
            if (record.XiaoPiPenalized)
                WorkshopStoryUI.Label(cover.transform, "扣款原因", WorkshopStoryUI.SabotageText,
                    new Rect(.19f, .21f, .62f, .07f), 26, WorkshopStoryUI.Paper);
        }
        GameFlowModalState.Acquire(this);
        morningPlayer = FindFirstObjectByType<PlayerController>();
        if (morningPlayer != null)
        {
            morningPlayerWasEnabled = morningPlayer.enabled;
            morningPlayer.enabled = false;
        }
        StartCoroutine(MorningCaption());
    }

    IEnumerator MorningCaption()
    {
        while (SceneTransitionState.IsLoading)
            yield return null;
        yield return new WaitForSecondsRealtime(3.2f);
        CloseMorning();
    }

    void CloseMorning()
    {
        if (morning == null)
            return;
        Destroy(morning);
        morning = null;
        if (morningPlayer != null)
            morningPlayer.enabled = morningPlayerWasEnabled;
        GameFlowModalState.Release(this);
    }

    void ShowReport(WorkshopOrder.DaySettlement record)
    {
        if (report == null)
            BuildReport();
        if (timeoutExit.IsSettlementActive)
            return;
        reportTitle.text = order.DayName + " · 收工";
        reportRows.text = WorkshopStoryUI.SettlementText(order, record);
        reportAction.text = order.CanAdvanceDay ? "收工，进入下一天" : "听舅舅说";
        timeoutExit.BeginSettlement(report);
    }

    void BuildReport()
    {
        timeoutExit = gameObject.AddComponent<SettlementExitController>();
        var overlay = WorkshopStoryUI.Panel(transform, "每日结算", new Rect(0, 0, 1, 1),
            new Color32(31, 25, 20, 209), true);
        report = overlay.gameObject;
        var sheet = WorkshopStoryUI.Panel(overlay.transform, "日账", new Rect(.21f, .18f, .58f, .64f),
            WorkshopStoryUI.Paper).transform;
        reportTitle = WorkshopStoryUI.Label(sheet, "标题", "", new Rect(.06f, .76f, .88f, .17f),
            46, WorkshopStoryUI.Ink, bold: true);
        reportRows = WorkshopStoryUI.Label(sheet, "明细", "", new Rect(.08f, .25f, .84f, .49f),
            31, WorkshopStoryUI.Ink);
        var action = WorkshopStoryUI.Button(sheet, "收工", "确认收工", new Rect(.26f, .09f, .48f, .115f),
            WorkshopStoryUI.Green, 29, LeaveReport);
        reportAction = action.GetComponentInChildren<Text>();
        alternateAction = WorkshopStoryUI.Button(sheet, "另一个选择", "", new Rect(.53f, .09f, .41f, .115f),
            WorkshopStoryUI.Wood, 26, () => ChooseEnding(false));
        alternateAction.gameObject.SetActive(false);
        report.SetActive(false);
    }

    void LeaveReport()
    {
        if (order.CanAdvanceDay)
        {
            if (!WorkshopStoryUI.AdvanceDay(order))
            {
                reportTitle.text = "转场暂未就绪，请重试";
                return;
            }
        }
        else if (endingStep == 0)
        {
            endingStep = 1;
            reportTitle.text = "写给下一双手";
            var record = order.CurrentSettlement.Value;
            string result = record.RankOf(record.PlayerTotal) switch
            {
                1 when record.PlayerTotal > record.XiaoPiTotal && record.PlayerTotal > record.XiaoBaiTotal =>
                    "舅舅：你挣得最多。工坊钥匙在这儿，你想接单时就来拿。",
                1 => "舅舅：头名打了个平手。钥匙先挂回门口，谁要开工都用得着。",
                _ => "舅舅：榜首按约定先选工位。你这几天学到的，下回还用得上。"
            };
            reportRows.text = result + "\n你这几天留下的记录，愿意给来访的人看吗？";
            ((RectTransform)reportAction.transform.parent).anchorMin = new Vector2(.06f, .09f);
            ((RectTransform)reportAction.transform.parent).anchorMax = new Vector2(.47f, .205f);
            reportAction.text = "把记录留给工坊";
            alternateAction.GetComponentInChildren<Text>().text = "把记录带在身边";
            alternateAction.gameObject.SetActive(true);
            return;
        }
        else if (endingStep < 3)
        {
            ChooseEnding(true);
            return;
        }
        else
            order.AcknowledgeFinalSettlement();
        timeoutExit.CloseAndResume();
    }

    void ChooseEnding(bool firstOption)
    {
        if (endingStep == 1)
        {
            shareNotes = firstOption;
            endingStep = 2;
            reportTitle.text = "明天去哪儿";
            reportRows.text = shareNotes
                ? "你把愿意给人看的几页留在了工坊。舅舅找了个干净的木匣，把它们和旧样帘放在一起。"
                : "你把手册收进包里。舅舅说，哪天想添一页，回来找他就行。";
            reportAction.text = "留村，试着接下一单";
            alternateAction.GetComponentInChildren<Text>().text = "出村，看看还有什么想做";
            return;
        }

        if (endingStep != 2)
            return;
        endingStep = 3;
        string title = firstOption ? "结局 · 下一张帘" : "结局 · 路还长";
        string epilogue = firstOption
            ? "你在工坊门口挂上接单牌。第二天小皮拿着一根劈歪的竹片来问你，歪的那一边该怎么下刀。"
            : order.DeliveredCount > 0
                ? "你拖着来时的行李箱出了村。后来舅舅寄来一张扎花帘的样子，背面写着艾琳的第一张帘晾干了。"
                : "你拖着来时的行李箱出了村。后来舅舅寄来一张试纸，背面问你今天走到了哪里。";
        if (order.TaughtLesson.HasValue && !order.LessonSucceeded)
            epilogue += "\n艾琳把花心松了的那张帘夹进信里，说下次想再扎紧一点。";
        if (order.TaughtLesson.HasValue)
            epilogue += "\n" + order.PaperDestinationText;
        epilogue += shareNotes ? "\n工坊木匣里多了一页新来访者的批注。" : "\n手册里还夹着一根竹丝，两边都留了空白。";
        reportTitle.text = title;
        reportRows.text = epilogue;
        reportAction.text = "合上五天的账";
        alternateAction.gameObject.SetActive(false);
        order.Journal.AddEvent(title, epilogue, announce: true);
    }

    void OnDisable() => CloseMorning();
}
