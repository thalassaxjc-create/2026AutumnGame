using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

internal static class WorkshopStoryUI
{
    internal static readonly Color32 Ink = new(54, 42, 31, 255);
    internal static readonly Color32 Paper = new(245, 231, 199, 255);
    internal static readonly Color32 Green = new(77, 115, 97, 255);
    internal static readonly Color32 Wood = new(103, 76, 55, 255);

    internal static void ConfigureOverlay(GameObject root, int sortingOrder)
    {
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();
    }

    internal static void EnsureEventSystem(Transform owner)
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        var local = new GameObject("工坊交互事件", typeof(EventSystem), typeof(InputSystemUIInputModule));
        local.transform.SetParent(owner, false);
    }

    internal static RectTransform Region(Transform parent, string name, Rect anchors)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchors.min;
        rect.anchorMax = anchors.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    internal static Image Panel(Transform parent, string name, Rect anchors, Color color, bool block = false)
    {
        var image = Region(parent, name, anchors).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = block;
        return image;
    }

    internal static Text Label(Transform parent, string name, string content, Rect anchors, int size,
        Color color, TextAnchor alignment = TextAnchor.MiddleCenter, bool bold = false)
    {
        var label = Region(parent, name, anchors).gameObject.AddComponent<Text>();
        label.alignment = alignment;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.color = color;
        label.text = content;
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, size / 2);
        label.resizeTextMaxSize = size;
        InteractionPromptUI.EnsureChineseFont(label);
        return label;
    }

    internal static Button Button(Transform parent, string name, string caption, Rect anchors, Color color,
        int fontSize, UnityAction action)
    {
        var image = Panel(parent, name, anchors, color, true);
        Label(image.transform, "文字", caption, new Rect(.04f, .03f, .92f, .94f), fontSize, Paper, bold: true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .97f, .83f);
        colors.pressedColor = new Color(.78f, .76f, .71f);
        colors.disabledColor = new Color(.56f, .56f, .56f, .6f);
        button.colors = colors;
        button.onClick.AddListener(action);
        return button;
    }

    internal static bool AdvanceDay(WorkshopOrder order) => order != null && order.CanAdvanceDay &&
        SceneTransitionManager.TryRequest(SceneTransitionState.WorldSceneName, SceneTransitionState.SpawnHomeExteriorEast) &&
        order.TryStartNextDay();

    internal static string SheetDetails(WorkshopOrder.SheetDelivery delivery, int number)
    {
        string bonus = delivery.UsedSpottedBamboo ? "    斑竹订单 ×2" : "";
        string pattern = delivery.PatternGrade is { } grade ? $"    纹样 {grade}" : "";
        return $"第 {number} 张 · {delivery.Style.Name}    综合 {WorkshopOrder.GradeText(delivery.Grade)}    +{delivery.Reward} 元{bonus}\n" +
            $"编帘 {WorkshopOrder.GradeText(delivery.Weaving)}    刷漆 {delivery.Lacquer}{pattern}    材料 ×{delivery.MaterialMultiplier:0.00}";
    }

    internal static string SettlementText(WorkshopOrder order, WorkshopOrder.DaySettlement record)
    {
        var text = new StringBuilder();
        if (record.ReachedMidnight)
            text.Append("24:00 截止  ·  ");
        if (order.IsTeachingDay)
            text.AppendLine(order.TaughtLesson.HasValue ? order.LessonResult : "艾琳启程了，这次没能一起做帘。");
        else
            text.AppendLine($"已交付 {record.DeliveredCount}/{record.RequiredSheets} 张");
        for (int i = 0; i < order.Deliveries.Count; i++)
            text.AppendLine(SheetDetails(order.Deliveries[i], i + 1));
        if (record.BatchBonus > 0)
            text.AppendLine($"整批验收奖励 +{record.BatchBonus} 元");
        text.AppendLine(DailyIncomeText(record, "当日净收入"));
        if (record.XiaoPiPenalized)
            text.AppendLine(SabotageText);
        text.Append(CumulativeFundsText(record));
        if (record.Day == WorkshopOrder.LastStoryDay)
            text.Append($"\n五日名次    主角 第{record.RankOf(record.PlayerTotal)}    " +
                $"小皮 第{record.RankOf(record.XiaoPiTotal)}    小白 第{record.RankOf(record.XiaoBaiTotal)}");
        return text.ToString();
    }

    internal static string DailyIncomeText(WorkshopOrder.DaySettlement record, string title) =>
        $"{title}    主角 {record.PlayerIncome:+0;-0;0}    小皮 {record.XiaoPiIncome:+0;-0;0}    小白 {record.XiaoBaiIncome:+0;-0;0}";

    internal static string CumulativeFundsText(WorkshopOrder.DaySettlement record) =>
        $"累计资金    主角 ¥{record.PlayerTotal}    小皮 ¥{record.XiaoPiTotal}    小白 ¥{record.XiaoBaiTotal}";

    internal static string SabotageText =>
        $"舅舅验出小皮的帘子发白，扣款 {WorkshopOrder.SabotagePenalty} 元。";
}
