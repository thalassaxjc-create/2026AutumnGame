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
        bool showEndDay = worldVisible && order.CanExplore;
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
        reportAction.text = order.CanAdvanceDay ? "收工，进入下一天" : "确认收工";
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
        else
            order.AcknowledgeFinalSettlement();
        timeoutExit.CloseAndResume();
    }

    void OnDisable() => CloseMorning();
}
