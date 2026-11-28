using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class ComputerOrderView : MonoBehaviour
{
    static readonly Color Green = new Color32(113, 166, 148, 255);
    static readonly Color Brown = new Color32(186, 166, 148, 255);
    ComputerUI screen;
    TMP_FontAsset font;
    GameObject notice;
    GameObject submit;
    GameObject settlement;
    TMP_Text settlementTitle;
    TMP_Text settlementText;
    TMP_Text settlementAction;
    readonly List<(GameObject child, bool visible)> plainOrderElements = new();
    GameObject patternCard;
    TMP_Text patternTitle;
    TMP_Text patternDescription;
    TMP_Text patternSteps;
    readonly CurtainPatternGraphic[] patternSamples = new CurtainPatternGraphic[WorkshopOrder.SheetsPerDay];
    readonly TMP_Text[] sampleLabels = new TMP_Text[WorkshopOrder.SheetsPerDay];
    TMP_Text sampleNotes;
    TMP_Text sampleHeading;

    public void Configure(ComputerUI computer, RectTransform orderTab, RectTransform page,
        RectTransform artboard, TMP_FontAsset labelFont)
    {
        screen = computer;
        font = labelFont;

        var badge = Label(orderTab, "订单提醒", "!", new Rect(195, 8, 60, 79), 70);
        badge.color = new Color32(226, 34, 34, 255);
        notice = badge.gameObject;
        foreach (Transform child in page)
            plainOrderElements.Add((child.gameObject, child.gameObject.activeSelf));
        BuildPatternCard(page);
        submit = Button(page, "提交订单", new Rect(2900, 1475, 470, 155), Submit).gameObject;

        var dimmer = Panel(artboard, "订单结算", new Rect(0, 0, 3840, 2160), new Color(0, 0, 0, .5f));
        dimmer.raycastTarget = true;
        settlement = dimmer.gameObject;
        var card = Panel(dimmer.rectTransform, "结算面板", new Rect(910, 480, 2020, 1200), Brown);
        settlementTitle = Label(card.rectTransform, "结算标题", "订单结算", new Rect(110, 90, 1800, 126), 96);
        settlementText = Label(card.rectTransform, "结算明细", "", new Rect(130, 261, 1760, 650), 72);
        settlementText.enableAutoSizing = true;
        settlementText.fontSizeMin = 45;
        settlementText.fontSizeMax = 72;
        settlementAction = Button(card.rectTransform, "确认", new Rect(620, 962, 780, 145), LeaveReport)
            .GetComponentInChildren<TMP_Text>();
        settlement.SetActive(false);
    }

    public void Refresh(WorkshopOrder order)
    {
        screen.SetProgress(order.Progress, order.DeliveredCount, order.RequiredSheets);
        screen.SetFunds(order.Funds);
        screen.SetTime(order.Hour, order.Minute);
        screen.SetDay(order.Days);
        for (int index = 0; index < 5; index++)
            screen.SetStepCompleted((ComputerUI.ProductionStep)index,
                order.GetStatus((WorkshopOrder.Stage)index) == WorkshopOrder.StageStatus.Complete);

        bool delivered = order.DeliveredCount == order.RequiredSheets;
        screen.SetStepCompleted(ComputerUI.ProductionStep.Delivery, delivered);
        notice.SetActive(order.ComputerNotice);
        submit.SetActive(order.CurrentPhase == WorkshopOrder.Phase.ReadyToSubmit && !order.PendingTrial);
        bool customOrder = order.IsPatternOrder || order.RequiresVermilionTrim || order.Days >= 4;
        if (patternCard.activeSelf != customOrder)
        {
            foreach (var entry in plainOrderElements)
                entry.child.SetActive(!customOrder && entry.visible);
            patternCard.SetActive(customOrder);
        }
        if (customOrder)
        {
            bool storyDay = order.Days >= 4;
            bool trim = order.RequiresVermilionTrim;
            patternTitle.text = order.OrderName;
            if (order.IsTeachingDay)
                patternDescription.text = order.DayName;
            else if (storyDay)
                patternDescription.text = $"{order.DayName}  ·  单张底价 {order.BasePrice} 元";
            else
            {
                string finishing = trim ? $"上漆边 {order.TrimMinutes} 分钟 / 张" : $"纹样 {order.PatternMinutes} 分钟 / 张";
                patternDescription.text = $"{order.DayName}  ·  单张底价 {order.BasePrice} 元  ·  {finishing}";
            }
            sampleHeading.gameObject.SetActive(!storyDay && !order.IsTeachingDay);
            sampleNotes.gameObject.SetActive(storyDay || order.IsTeachingDay);
            sampleNotes.text = order.PendingTrial ? "第一张已阴干，先回院子找老周试捞。" : order.CustomerNotes;
            for (int i = 0; i < patternSamples.Length; i++)
            {
                bool showSample = !storyDay && !order.IsTeachingDay;
                patternSamples[i].gameObject.SetActive(showSample);
                sampleLabels[i].gameObject.SetActive(showSample);
                if (!showSample)
                    continue;
                WorkshopOrder.SheetStyle style = order.RequestedSheet(i);
                patternSamples[i].Show(style.Pattern, isSample: true, showGuide: false, finish: style.Edge);
                string status = order.IsSheetDelivered(i) ? "已交付" : order.IsSheetReady(i) ? "可交付" : "待制作";
                sampleLabels[i].text = $"{style.Name} ×1\n{status}";
                sampleLabels[i].color = status == "待制作" ? WorkshopStoryUI.Ink : WorkshopStoryUI.Green;
            }
            if (order.IsTeachingDay)
                patternSteps.text = "";
            else if (storyDay)
                patternSteps.text = delivered ? "今日订单已全部交付。\n\n随时可以去村里做生意，或在左上角收工。"
                    : $"已交 {order.DeliveredCount}/{order.RequiredSheets} 张\n\n待交成品 ×{order.AvailableOrderCurtains}";
            else
            {
                string finishingStep = trim ? $"上漆边   {StepLabel(order, WorkshopOrder.Stage.VermilionTrim)}"
                    : $"纹样   {StepLabel(order, WorkshopOrder.Stage.Pattern)}";
                patternSteps.text = delivered ? "今日订单已全部交付。\n\n随时可以去村里做生意，或在左上角收工。"
                    : $"已交 {order.DeliveredCount}/{order.RequiredSheets} 张\n\n" +
                    $"待交成品 ×{order.AvailableOrderCurtains}\n" +
                    "推荐工序\n" +
                    $"篾竹与抽丝   {StepLabel(order, WorkshopOrder.Stage.BambooFinishing)}\n" +
                    $"编帘   {StepLabel(order, WorkshopOrder.Stage.Weaving)}\n" +
                    $"刷漆   {StepLabel(order, WorkshopOrder.Stage.Lacquer)}\n" +
                    $"阴干   {StepLabel(order, WorkshopOrder.Stage.Drying)}\n" + finishingStep;
            }
        }
        settlement.SetActive(false);
    }

    static string StepLabel(WorkshopOrder order, WorkshopOrder.Stage stage) => order.GetStatus(stage) switch
    {
        WorkshopOrder.StageStatus.Complete => "完成",
        WorkshopOrder.StageStatus.InProgress => "进行中",
        WorkshopOrder.StageStatus.Available => "可制作",
        _ => "待制作"
    };

    void Submit()
    {
        var order = WorkshopOrder.Current;
        if (order == null || !order.Submit())
            return;
        Refresh(order);
        bool allDelivered = order.CurrentPhase == WorkshopOrder.Phase.OrderComplete;
        settlementTitle.text = allDelivered ? "今日订单已全部交付" : $"第 {order.DeliveredCount} 张已交付";
        settlementText.text = WorkshopStoryUI.SheetDetails(order.LastDelivery.Value, order.DeliveredCount) +
            $"\n\n今日已交 {order.DeliveredCount}/{order.RequiredSheets} 张，当日净收入 {order.TodayIncome:+0;-0;0} 元" +
            (order.BatchBonus > 0 ? $"\n整批验收奖励 +{order.BatchBonus} 元" : "") +
            (allDelivered ? "\n还可以去集市交易，收工时点屏幕左上角。" : "");
        settlementAction.text = "返回工坊";
        settlement.SetActive(true);
    }

    void LeaveReport()
    {
        settlement.SetActive(false);
        screen.RequestClose();
    }

    void BuildPatternCard(RectTransform page)
    {
        var card = Panel(page, "纹样订单内容", new Rect(650, 294, 2740, 1420),
            new Color32(239, 223, 188, 255)).rectTransform;
        patternCard = card.gameObject;
        Panel(card, "订单色签", new Rect(0, 0, 2740, 18), WorkshopStoryUI.Green);
        var ink = WorkshopStoryUI.Ink;
        patternTitle = Label(card, "当日纹样", "", new Rect(120, 74, 2400, 128), 104);
        patternDescription = Label(card, "订单要求", "", new Rect(120, 220, 2500, 96), 61);
        sampleHeading = Label(card, "样稿标题", "交付样稿", new Rect(120, 365, 1300, 83), 59);
        sampleNotes = Label(card, "客户备注", "", new Rect(120, 1250, 2500, 115), 55);
        sampleNotes.color = ink;
        sampleNotes.alignment = TextAlignmentOptions.MidlineLeft;
        for (int i = 0; i < patternSamples.Length; i++)
        {
            float x = 120 + i * 740;
            patternSamples[i] = Place(card, i == 0 ? "订单样稿" : "第二张样稿", new Rect(x, 465, 688, 466))
                .gameObject.AddComponent<CurtainPatternGraphic>();
            patternSamples[i].raycastTarget = false;
            sampleLabels[i] = Label(card, $"样稿 {i + 1} 进度", "", new Rect(x, 948, 688, 138), 53);
            sampleLabels[i].color = ink;
        }
        patternSteps = Label(card, "纹样进度", "", new Rect(1680, 430, 946, 660), 58);
        foreach (TMP_Text line in new[] { patternTitle, patternDescription, sampleHeading, patternSteps })
        {
            line.color = ink;
            line.alignment = TextAlignmentOptions.MidlineLeft;
        }
        patternCard.SetActive(false);
    }

    Button Button(RectTransform parent, string caption, Rect bounds, UnityAction onClick)
    {
        var background = Panel(parent, caption, bounds, Green);
        background.raycastTarget = true;
        Label(background.rectTransform, "文字", caption, new Rect(0, 0, bounds.width, bounds.height), 68);
        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.highlightedColor = new Color(.88f, .88f, .88f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        return button;
    }

    TMP_Text Label(RectTransform parent, string name, string content, Rect bounds, float fontSize)
    {
        var label = Place(parent, name, bounds).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.text = content;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    static Image Panel(RectTransform parent, string name, Rect bounds, Color color)
    {
        var panel = Place(parent, name, bounds).gameObject.AddComponent<Image>();
        panel.color = color;
        panel.raycastTarget = false;
        return panel;
    }

    static RectTransform Place(RectTransform parent, string name, Rect bounds)
    {
        var element = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        element.SetParent(parent, false);
        element.anchorMin = element.anchorMax = element.pivot = new Vector2(0, 1);
        element.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        element.sizeDelta = bounds.size;
        return element;
    }
}
