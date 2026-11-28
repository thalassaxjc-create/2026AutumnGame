using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperInspectionView : MonoBehaviour
{
    static readonly string[] Bins = { "合格 · 纸面均匀", "折痕 · 一道斜线", "薄斑 · 一块透亮", "针孔 · 几个亮点" };
    WorkshopOrder order;
    PaperInspectionRound inspection;
    PaperInspectionGraphic board;
    GameObject table, report;
    Text progress, feedback, reportText;

    internal static PaperInspectionView Create(Transform parent, Action close)
    {
        var root = new GameObject("府上验纸界面", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var view = root.AddComponent<PaperInspectionView>();
        view.Build(close);
        return view;
    }

    internal void Open(WorkshopOrder activeOrder, PaperInspectionRound activeInspection)
    {
        order = activeOrder;
        inspection = activeInspection;
        gameObject.SetActive(true);
        Refresh();
    }

    internal void Hide() => gameObject.SetActive(false);

    void Update()
    {
        if (inspection != null && !inspection.Complete && Mouse.current is { } mouse)
            board.MoveLamp(mouse.position.ReadValue());
    }

    void Classify(PaperInspectionRound.Condition condition)
    {
        if (order.TryClassifyOfficialPaper(condition))
            Refresh();
    }

    void Refresh()
    {
        string result = inspection.Inspected == 0 ? ""
            : $"上一张 · {PaperInspectionRound.Label(inspection.LastCondition)}" +
              (inspection.LastCorrect ? $"，分对了 +{WorkshopOrder.OfficialPaperPay} 元" : "，归档有误");
        progress.text = inspection.Complete ? "已验完"
            : $"第 {inspection.Inspected + 1} / {PaperInspectionRound.SheetCount} 张    分对 {inspection.Correct} 张";
        table.SetActive(!inspection.Complete);
        report.SetActive(inspection.Complete);
        feedback.text = result;
        feedback.color = inspection.LastCorrect ? WorkshopStoryUI.Green : WorkshopStoryUI.Ink;
        if (inspection.Complete)
            reportText.text = $"分对 {inspection.Correct} / {PaperInspectionRound.SheetCount} 张\n\n收入  {order.OfficialJobPay} 元\n用时  {WorkshopOrder.OfficialJobMinutes} 分钟";
        else
            board.LayPaper(inspection.Current);
    }

    void Build(Action close)
    {
        WorkshopStoryUI.ConfigureOverlay(gameObject, 140);
        WorkshopStoryUI.EnsureEventSystem(transform);
        var background = WorkshopStoryUI.Panel(transform, "验纸背景", new Rect(0, 0, 1, 1),
            new Color32(37, 30, 24, 255), true);
        Transform plate = WorkshopStoryUI.Panel(background.transform, "验纸界面", new Rect(.045f, .075f, .91f, .85f),
            WorkshopStoryUI.Paper).transform;
        Color ink = WorkshopStoryUI.Ink;
        WorkshopStoryUI.Label(plate, "验纸标题", "高官府上 · 斜光验纸", new Rect(.045f, .838f, .60f, .105f),
            57, ink, TextAnchor.MiddleLeft, true);
        progress = WorkshopStoryUI.Label(plate, "验纸进度", "", new Rect(.66f, .844f, .295f, .078f),
            30, ink, TextAnchor.MiddleRight);
        feedback = WorkshopStoryUI.Label(plate, "验纸反馈", "", new Rect(.045f, .077f, .66f, .073f),
            30, ink, TextAnchor.MiddleLeft);
        WorkshopStoryUI.Button(plate, "离开验纸", "回村里  Esc", new Rect(.78f, .063f, .175f, .084f),
            WorkshopStoryUI.Wood, 28, () => close());

        table = WorkshopStoryUI.Region(plate, "验纸工作台", new Rect(0, 0, 1, 1)).gameObject;
        WorkshopStoryUI.Label(table.transform, "验纸手势", "把鼠标移到纸上，左右移动光带查看纸面，再选归档。",
            new Rect(.045f, .741f, .725f, .07f), 31, ink, TextAnchor.MiddleLeft);
        board = WorkshopStoryUI.Region(table.transform, "斜光纸样", new Rect(.045f, .205f, .624f, .502f))
            .gameObject.AddComponent<PaperInspectionGraphic>();
        for (int i = 0; i < Bins.Length; i++)
        {
            var condition = (PaperInspectionRound.Condition)i;
            WorkshopStoryUI.Button(table.transform, "归档_" + PaperInspectionRound.Label(condition), Bins[i],
                new Rect(.717f, .601f - i * .12f, .238f, .103f),
                i == 0 ? WorkshopStoryUI.Green : WorkshopStoryUI.Wood, 29, () => Classify(condition));
        }

        report = WorkshopStoryUI.Region(plate, "府上验纸结算", new Rect(.22f, .22f, .56f, .518f)).gameObject;
        WorkshopStoryUI.Panel(report.transform, "验纸结算纸", new Rect(0, 0, 1, 1), new Color32(229, 210, 172, 255));
        WorkshopStoryUI.Label(report.transform, "已结工钱", "工钱已结", new Rect(.07f, .70f, .86f, .22f),
            54, ink, bold: true);
        reportText = WorkshopStoryUI.Label(report.transform, "验纸明细", "", new Rect(.1f, .11f, .8f, .51f),
            37, ink);
    }
}
