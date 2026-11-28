using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CurtainPatternView : MonoBehaviour
{
    static readonly Color32 Card = new(233, 218, 181, 255);
    static readonly Color32 OrderCard = new(210, 213, 169, 255);
    readonly Button[] choices = new Button[CurtainPatternDesign.Choices.Count];
    readonly Text[] tutorialHints = new Text[2];
    readonly Dictionary<CurtainPatternKind, CurtainPatternDraft> practiceDrafts = new();
    readonly StringBuilder crossingRecipe = new(160);
    WorkshopOrder order;
    CurtainPatternDraft draft;
    Action returnToWorkshop;
    GameObject catalog;
    GameObject bench;
    Text catalogNote;
    CurtainPatternGraphic board;
    CurtainPatternGraphic sample;
    Text mode;
    Text title;
    Text caption;
    Text recipe;
    Text instructions;
    Text score;
    Text processingTime;
    Text remark;
    Text actionLabel;
    Button undo;
    Button review;
    Button continueWork;
    int selectionFrame;
    bool inspecting;
    bool shuttleUnder;

    public CurtainPatternGraphic Board => board;

    public void Open(WorkshopOrder activeOrder, Action exit)
    {
        order = activeOrder;
        returnToWorkshop = exit;
        if (catalog == null)
            Build();
        gameObject.SetActive(true);
        ShowCatalog();
    }

    public void Close()
    {
        order = null;
        draft?.Release();
        draft = null;
        returnToWorkshop = null;
        gameObject.SetActive(false);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true)
        {
            Exit();
            return;
        }
        Mouse mouse = Mouse.current;
        if (draft == null || inspecting || Time.frameCount == selectionFrame)
            return;
        if (mouse == null)
        {
            draft.Release();
            return;
        }
        if (mouse.rightButton.wasPressedThisFrame)
        {
            Redo();
            return;
        }
        if (draft.CanConfirm)
            return;

        int row = draft.CompletedRows;
        int crossings = draft.CheckedCrossings(row);
        bool under = keyboard?.spaceKey.isPressed == true;
        bool moved = false;
        if ((mouse.leftButton.isPressed || (draft.IsGripped && mouse.leftButton.wasReleasedThisFrame)) &&
            board.TryShuttlePointer(mouse.position.ReadValue(), row, out Vector2 travel))
        {
            if (mouse.leftButton.wasPressedThisFrame)
                draft.TryGrip(travel);
            moved = draft.Drag(travel, under);
            if (!mouse.leftButton.isPressed)
                draft.Release();
        }
        else
            draft.Release();

        bool layerChanged = shuttleUnder != under;
        shuttleUnder = under;
        if (draft.CompletedRows != row)
            ApplyChange(draft.CanConfirm ? "三道已收线，可以对样。" : "松开左键，在新起点抓住木梭。");
        else if (draft.CheckedCrossings(row) != crossings)
        {
            int last = draft.CheckedCrossings(row) - 1;
            ApplyChange(draft.ResultAt(row, last) == CurtainPatternDraft.CrossingResult.Wrong
                ? "朱色线结标出了错压的位置。右键可重织这一道。" : "");
        }
        else
        {
            if (layerChanged)
                ShowLayer();
            if (moved || layerChanged)
                board.RefreshShuttle(shuttleUnder);
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
            draft?.Release();
    }

    void ShowCatalog()
    {
        draft?.Release();
        draft = null;
        bool needsTurtle = order?.IsPatternOrder == true && order.DeliveredCount < WorkshopOrder.SheetsPerDay;
        catalogNote.text = order == null ? "开发试玩"
            : $"阴干素帘 ×{order.PlainCurtains}" + (needsTurtle ? "　今日订单需要龟纹" : "");
        for (int i = 0; i < choices.Length; i++)
        {
            CurtainPatternKind kind = CurtainPatternDesign.Choices[i];
            choices[i].image.color = needsTurtle && kind == CurtainPatternKind.Turtle ? OrderCard : Card;
        }
        bench.SetActive(false);
        catalog.SetActive(true);
    }

    void Select(CurtainPatternKind kind)
    {
        if (order != null)
        {
            if (!order.TrySelectPattern(kind))
                return;
            draft = order.PatternDraft;
        }
        else if (!practiceDrafts.TryGetValue(kind, out draft))
        {
            draft = new CurtainPatternDraft(kind);
            practiceDrafts.Add(kind, draft);
        }

        selectionFrame = Time.frameCount;
        shuttleUnder = Keyboard.current?.spaceKey.isPressed == true;
        inspecting = false;
        catalog.SetActive(false);
        bench.SetActive(true);
        mode.text = order == null ? "开发试玩" : "使用一张阴干素帘";
        processingTime.text = order == null ? "" : $"加工 {order.PatternMinutes} 分钟";
        sample.Show(kind, isSample: true, showGuide: false);
        for (int i = 0; i < tutorialHints.Length; i++)
            tutorialHints[i].text = draft.GoesUnder(0, i) ? "按住空格" : "松开空格";
        ApplyChange("");
    }

    void Exit() => returnToWorkshop?.Invoke();

    void Redo()
    {
        if (!inspecting && draft?.RedoRow() == true)
            ApplyChange("这一道已退回起点。");
    }

    void ReviewOrConfirm()
    {
        if (draft?.CanConfirm != true)
            return;
        if (!inspecting)
        {
            draft.Release();
            inspecting = true;
            ApplyChange(draft.ThreadingScore < 60 ? "朱色线结标出了错压的位置。" : "");
            return;
        }
        if (order == null)
        {
            practiceDrafts.Remove(draft.Kind);
            ShowCatalog();
            return;
        }
        if (order.TryConfirmPattern())
            Exit();
        else
            remark.text = $"今天剩余时间不足 {order.PatternMinutes} 分钟。";
    }

    void ReturnToStitching()
    {
        inspecting = false;
        ApplyChange("");
    }

    void ApplyChange(string message)
    {
        remark.text = message;
        string name = CurtainPatternDesign.Get(draft.Kind).Name;
        title.text = inspecting ? "对样 · " + name : name;
        string direction = CurtainPatternDesign.IsReverse(draft.CompletedRows) ? "向左拖梭" : "向右拖梭";
        caption.text = $"帘面    {draft.CompletedRows} / {CurtainPatternDraft.RowCount} 道已收线" +
            (draft.CanConfirm ? "" : $"    ·    {direction}");
        board.Show(draft.Kind, draft, showGuide: !inspecting);
        if (!inspecting)
            board.RefreshShuttle(shuttleUnder);
        for (int i = 0; i < tutorialHints.Length; i++)
            tutorialHints[i].gameObject.SetActive(!inspecting && draft.CompletedRows == 0 && draft.CheckedCrossings(0) <= i);

        instructions.gameObject.SetActive(!inspecting);
        ShowLayer();
        ShowRecipe();
        score.gameObject.SetActive(inspecting);
        if (inspecting)
            score.text = $"穿线 {draft.ThreadingScore}/60    走线 {draft.AlignmentScore}/40\n纹样 {draft.Grade}  ·  {draft.Score}/100";
        undo.interactable = !inspecting && draft.CanRedo;
        continueWork.gameObject.SetActive(inspecting);
        actionLabel.text = inspecting ? (order == null ? "完成试玩" : "确认成品") : "对样";
        review.interactable = draft.CanConfirm;
    }

    void ShowLayer()
    {
        instructions.text = draft.CanConfirm ? "对照样稿查看成品。" :
            shuttleUnder ? "空格按住    从下穿" : "空格松开    从上跨";
    }

    void ShowRecipe()
    {
        crossingRecipe.Clear();
        for (int row = 0; row < CurtainPatternDraft.RowCount; row++)
        {
            if (row > 0)
                crossingRecipe.AppendLine();
            bool reverse = CurtainPatternDesign.IsReverse(row);
            crossingRecipe.Append("第 ").Append(row + 1).Append(reverse ? " 道 ←" : " 道 →");
            for (int column = 0; column < CurtainPatternDraft.CrossingsPerRow; column++)
            {
                int crossing = reverse ? CurtainPatternDraft.CrossingsPerRow - 1 - column : column;
                bool next = row == draft.CompletedRows && crossing == draft.CheckedCrossings(row);
                crossingRecipe.Append('　');
                if (next)
                    crossingRecipe.Append("<color=#356752>");
                crossingRecipe.Append(draft.GoesUnder(row, crossing) ? '下' : '上');
                if (next)
                    crossingRecipe.Append("</color>");
            }
        }
        recipe.text = crossingRecipe.ToString();
    }

    void Build()
    {
        WorkshopStoryUI.ConfigureOverlay(gameObject, 240);
        var back = WorkshopStoryUI.Panel(transform, "纹样台画面", new Rect(0, 0, 1, 1),
            new Color32(41, 34, 28, 255), true);
        var plate = WorkshopStoryUI.Panel(back.transform, "纸本", new Rect(.045f, .07f, .91f, .86f),
            WorkshopStoryUI.Paper).transform;
        BuildCatalog(plate);
        BuildBench(plate);
    }

    void BuildCatalog(Transform plate)
    {
        Transform page = WorkshopStoryUI.Region(plate, "纹样选样页", new Rect(0, 0, 1, 1));
        catalog = page.gameObject;
        var ink = WorkshopStoryUI.Ink;
        WorkshopStoryUI.Label(page, "选样标题", "选一种帘纹", new Rect(.045f, .805f, .51f, .112f),
            62, ink, TextAnchor.MiddleLeft, true);
        catalogNote = WorkshopStoryUI.Label(page, "订单所需", "", new Rect(.535f, .817f, .421f, .089f),
            29, ink, TextAnchor.MiddleRight);

        for (int i = 0; i < CurtainPatternDesign.Choices.Count; i++)
        {
            CurtainPatternKind kind = CurtainPatternDesign.Choices[i];
            string name = CurtainPatternDesign.Get(kind).Name;
            var swatch = WorkshopStoryUI.Button(page, "选样_" + name, "", new Rect(.045f + i * .184f, .23f, .174f, .51f),
                Card, 26, () => Select(kind));
            choices[i] = swatch;
            var graphic = WorkshopStoryUI.Region(swatch.transform, "帘纹样片", new Rect(.07f, .23f, .86f, .69f))
                .gameObject.AddComponent<CurtainPatternGraphic>();
            graphic.Show(kind, isSample: true, showGuide: false);
            WorkshopStoryUI.Label(swatch.transform, "名称", name, new Rect(.07f, .047f, .86f, .135f),
                35, ink, bold: true);
        }
        WorkshopStoryUI.Button(page, "回工坊", "回工坊  Esc", new Rect(.778f, .064f, .177f, .082f),
            WorkshopStoryUI.Wood, 28, Exit);
    }

    void BuildBench(Transform plate)
    {
        Transform page = WorkshopStoryUI.Region(plate, "纹样加工页", new Rect(0, 0, 1, 1));
        bench = page.gameObject;
        var ink = WorkshopStoryUI.Ink;
        mode = WorkshopStoryUI.Label(page, "加工类型", "", new Rect(.045f, .908f, .52f, .06f),
            26, ink, TextAnchor.MiddleLeft);
        title = WorkshopStoryUI.Label(page, "当前纹样", "", new Rect(.045f, .814f, .55f, .095f),
            56, ink, TextAnchor.MiddleLeft, true);
        WorkshopStoryUI.Label(page, "操作提示", "左键拖梭    空格提线    右键重织    Esc 回工坊",
            new Rect(.53f, .85f, .427f, .075f), 27, ink, TextAnchor.MiddleRight);
        caption = WorkshopStoryUI.Label(page, "帘面进度", "", new Rect(.045f, .752f, .59f, .055f),
            30, ink, TextAnchor.MiddleLeft, true);
        board = WorkshopStoryUI.Region(page, "走梭帘面", new Rect(.045f, .205f, .60f, .54f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        for (int i = 0; i < tutorialHints.Length; i++)
        {
            Vector2 anchor = CurtainPatternDesign.ClothPosition(0, new Vector2(CurtainPatternDraft.CrossingProgress(i), .91f));
            tutorialHints[i] = WorkshopStoryUI.Label(board.transform, "提线提示", "", new Rect(anchor.x, anchor.y, 0, 0),
                25, ink, bold: true);
            tutorialHints[i].rectTransform.sizeDelta = new Vector2(118f, 28f);
        }
        WorkshopStoryUI.Label(page, "样稿标题", "走线样稿", new Rect(.695f, .752f, .26f, .055f),
            30, ink, TextAnchor.MiddleLeft, true);
        sample = WorkshopStoryUI.Region(page, "样稿", new Rect(.695f, .513f, .26f, .232f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        recipe = WorkshopStoryUI.Label(page, "穿线顺序", "", new Rect(.695f, .355f, .26f, .145f),
            26, ink, TextAnchor.MiddleLeft, true);
        instructions = WorkshopStoryUI.Label(page, "织法", "", new Rect(.695f, .254f, .26f, .079f),
            27, WorkshopStoryUI.Green, TextAnchor.MiddleLeft, true);
        score = WorkshopStoryUI.Label(page, "计分", "", new Rect(.695f, .246f, .26f, .097f),
            27, ink, TextAnchor.MiddleLeft, true);
        processingTime = WorkshopStoryUI.Label(page, "加工工时", "", new Rect(.695f, .205f, .26f, .042f),
            24, ink, TextAnchor.MiddleLeft);
        remark = WorkshopStoryUI.Label(page, "针路反馈", "", new Rect(.045f, .137f, .91f, .058f),
            25, ink, TextAnchor.MiddleLeft);

        undo = WorkshopStoryUI.Button(page, "重织一道", "重织一道", new Rect(.045f, .044f, .128f, .078f),
            WorkshopStoryUI.Wood, 28, Redo);
        WorkshopStoryUI.Button(page, "返回选样", "返回选样", new Rect(.187f, .044f, .159f, .078f),
            WorkshopStoryUI.Wood, 28, ShowCatalog);
        continueWork = WorkshopStoryUI.Button(page, "返回修补", "返回修补", new Rect(.36f, .044f, .159f, .078f),
            WorkshopStoryUI.Wood, 28, ReturnToStitching);
        review = WorkshopStoryUI.Button(page, "预览纹样", "对样", new Rect(.583f, .044f, .175f, .078f),
            WorkshopStoryUI.Green, 31, ReviewOrConfirm);
        actionLabel = review.GetComponentInChildren<Text>();
        WorkshopStoryUI.Button(page, "回工坊", "回工坊", new Rect(.778f, .044f, .177f, .078f),
            WorkshopStoryUI.Wood, 28, Exit);
    }
}
