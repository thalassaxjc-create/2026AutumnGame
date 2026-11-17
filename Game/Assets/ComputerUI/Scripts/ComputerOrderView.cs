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
    TMP_Text settlementText;

    public void Configure(ComputerUI computer, RectTransform orderTab, RectTransform page,
        RectTransform artboard, TMP_FontAsset labelFont)
    {
        screen = computer;
        font = labelFont;

        var badge = Label(orderTab, "订单提醒", "!", new Rect(195, 8, 60, 79), 70);
        badge.color = new Color32(226, 34, 34, 255);
        notice = badge.gameObject;
        submit = Button(page, "提交订单", new Rect(2900, 1475, 470, 155), Submit).gameObject;

        var dimmer = Panel(artboard, "订单结算", new Rect(0, 0, 3840, 2160), new Color(0, 0, 0, .5f));
        dimmer.raycastTarget = true;
        settlement = dimmer.gameObject;
        var card = Panel(dimmer.rectTransform, "结算面板", new Rect(1130, 540, 1580, 1020), Brown);
        Label(card.rectTransform, "结算标题", "订单结算", new Rect(100, 78, 1380, 140), 90);
        settlementText = Label(card.rectTransform, "结算明细", "", new Rect(100, 270, 1380, 440), 68);
        Button(card.rectTransform, "确认", new Rect(560, 780, 460, 145), () => settlement.SetActive(false));
        settlement.SetActive(false);
    }

    public void Refresh(WorkshopOrder order)
    {
        screen.SetProgress(order.Progress);
        screen.SetFunds(order.Funds);
        screen.SetTime(order.Hour, order.Minute);
        screen.SetDay(order.Days);
        for (int index = 0; index < 5; index++)
            screen.SetStepCompleted((ComputerUI.ProductionStep)index,
                order.GetStatus((WorkshopOrder.Stage)index) == WorkshopOrder.StageStatus.Complete);

        screen.SetStepCompleted(ComputerUI.ProductionStep.Delivery,
            order.CurrentPhase == WorkshopOrder.Phase.Complete);
        notice.SetActive(order.ComputerNotice);
        submit.SetActive(order.CurrentPhase == WorkshopOrder.Phase.ReadyToSubmit);
        settlement.SetActive(false);
    }

    void Submit()
    {
        var order = WorkshopOrder.Current;
        if (order == null || !order.Submit())
            return;
        Refresh(order);
        settlementText.text = $"编帘评级  {order.WeavingResult.Value.grade}    刷漆评级  {order.LacquerResult.Value.grade}\n" +
            $"综合评级  {order.FinalGrade.Value}    基础价  {order.BasePrice} 元\n" +
            $"品质系数  ×{order.QualityMultiplier:0.00}    材料系数  ×{order.MaterialMultiplier:0.00}\n" +
            $"本单收入  +{order.Reward} 元    主角资金  {order.FundsBeforeSubmission.Value} → {order.Funds}\n" +
            $"第一天对手资金    小皮 {order.XiaoPiFunds} 元    小白 {order.XiaoBaiFunds} 元";
        settlement.SetActive(true);
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
