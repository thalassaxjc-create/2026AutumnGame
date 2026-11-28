using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VermilionTrimView : MonoBehaviour
{
    static readonly string[] Sides = { "上边", "右边", "下边", "左边" };
    static readonly Color32 SampleCard = new(219, 198, 158, 255);
    static readonly Color32 OrderCard = new(196, 207, 166, 255);
    readonly Dictionary<CurtainEdgeStyle, VermilionTrimDraft> practiceDrafts = new();
    readonly Button[] choices = new Button[CurtainEdgePalette.Choices.Length];
    readonly Text[] choiceStatus = new Text[CurtainEdgePalette.Choices.Length];
    WorkshopOrder order;
    VermilionTrimDraft draft;
    Action exit;
    GameObject catalog, work;
    CurtainPatternGraphic board;
    Text catalogNote, title, description, progress, instructions, feedback;
    Button confirm;
    int selectedOnFrame;

    public CurtainPatternGraphic Board => board;

    public void Open(WorkshopOrder activeOrder, Action returnToWorkshop)
    {
        order = activeOrder;
        exit = returnToWorkshop;
        if (catalog == null)
            Build();
        ShowCatalog();
    }

    void ShowCatalog()
    {
        draft?.Release();
        draft = null;
        work.SetActive(false);
        catalog.SetActive(true);
        catalogNote.text = order == null ? "开发试玩" : $"阴干素帘 ×{order.PlainCurtains}";
        for (int i = 0; i < choices.Length; i++)
        {
            var style = new WorkshopOrder.SheetStyle(CurtainPatternKind.None, CurtainEdgePalette.Choices[i]);
            bool requested = order?.NeedsStyle(style) == true;
            choices[i].image.color = requested ? OrderCard : SampleCard;
            choiceStatus[i].text = requested ? "今日待交" : "";
        }
    }

    void Select(CurtainEdgeStyle style)
    {
        if (order != null)
            draft = order.OpenTrimDraft(style);
        else if (!practiceDrafts.TryGetValue(style, out draft))
            practiceDrafts.Add(style, draft = new VermilionTrimDraft(style));
        if (draft == null)
            return;

        selectedOnFrame = Time.frameCount;
        catalog.SetActive(false);
        work.SetActive(true);
        CurtainEdgePalette palette = CurtainEdgePalette.Get(style);
        title.text = palette.Name;
        description.text = palette.Description;
        feedback.text = order == null ? "" : $"加工 {order.TrimMinutes} 分钟";
        board.Show(CurtainPatternKind.None, showGuide: false, edgeWork: draft);
        Refresh();
    }

    void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Exit();
            return;
        }
        Mouse mouse = Mouse.current;
        if (draft == null || draft.Complete || mouse == null || Time.frameCount == selectedOnFrame)
            return;
        if (!board.TryClothPointer(mouse.position.ReadValue(), out Vector2 point))
            return;

        if (mouse.leftButton.wasPressedThisFrame && draft.TryGrip(point))
            board.SetVerticesDirty();
        if (draft.Held && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame) && draft.Pointer != point)
        {
            if (draft.Drag(point))
                Refresh();
            board.SetVerticesDirty();
        }
        if (!mouse.leftButton.isPressed && draft.Held)
        {
            draft.Release();
            board.SetVerticesDirty();
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused && draft?.Held == true)
        {
            draft.Release();
            board.SetVerticesDirty();
        }
    }

    void OnDisable() => draft?.Release();

    void Exit() => exit?.Invoke();

    void Confirm()
    {
        if (draft?.Complete != true)
            return;
        if (order == null)
        {
            practiceDrafts.Remove(draft.Style);
            Exit();
        }
        else if (order.TryConfirmVermilionTrim())
            Exit();
        else
            feedback.text = $"今天剩余时间不足 {order.TrimMinutes} 分钟。";
    }

    void Refresh()
    {
        progress.text = $"已收边 {draft.SealedCorners} / {VermilionTrimDraft.SideCount} 角";
        instructions.text = draft.Complete ? "四边已收好，可以确认成品。"
            : draft.Turning ? "按住左键，顺着亮起的小弧线转过角。"
            : $"从金点抓住滚轮，沿{Sides[draft.ActiveSide]}推进。松手后可从金点续做。";
        confirm.interactable = draft.Complete;
    }

    void Build()
    {
        WorkshopStoryUI.ConfigureOverlay(gameObject, 240);
        var shade = WorkshopStoryUI.Panel(transform, "上漆边台画面", new Rect(0, 0, 1, 1),
            new Color32(41, 34, 28, 255), true);
        var plate = WorkshopStoryUI.Panel(shade.transform, "台面", new Rect(.045f, .07f, .91f, .86f),
            WorkshopStoryUI.Paper).transform;

        Transform page = WorkshopStoryUI.Region(plate, "上漆边选样页", new Rect(0, 0, 1, 1));
        catalog = page.gameObject;
        var ink = WorkshopStoryUI.Ink;
        WorkshopStoryUI.Label(page, "选样标题", "选一种帘边", new Rect(.045f, .81f, .5f, .105f),
            60, ink, TextAnchor.MiddleLeft, true);
        catalogNote = WorkshopStoryUI.Label(page, "可用素帘", "", new Rect(.60f, .827f, .355f, .072f),
            30, ink, TextAnchor.MiddleRight);
        for (int i = 0; i < choices.Length; i++)
        {
            CurtainEdgeStyle style = CurtainEdgePalette.Choices[i];
            CurtainEdgePalette palette = CurtainEdgePalette.Get(style);
            Button button = WorkshopStoryUI.Button(page, "选边_" + palette.Name, "",
                new Rect(.045f + .231f * i, .225f, .217f, .51f), SampleCard, 27, () => Select(style));
            choices[i] = button;
            CurtainPatternGraphic swatch = WorkshopStoryUI.Region(button.transform, "配色样片", new Rect(.08f, .31f, .84f, .57f))
                .gameObject.AddComponent<CurtainPatternGraphic>();
            swatch.Show(CurtainPatternKind.None, showGuide: false, finish: style);
            WorkshopStoryUI.Label(button.transform, "名称", palette.Name, new Rect(.07f, .17f, .86f, .12f),
                35, ink, bold: true);
            WorkshopStoryUI.Label(button.transform, "配色", palette.Description, new Rect(.04f, .04f, .92f, .105f),
                21, ink);
            choiceStatus[i] = WorkshopStoryUI.Label(button.transform, "订单", "", new Rect(.07f, .90f, .86f, .077f),
                21, WorkshopStoryUI.Green, TextAnchor.MiddleLeft, true);
        }
        WorkshopStoryUI.Button(page, "回工坊", "回工坊  Esc", new Rect(.778f, .055f, .177f, .082f),
            WorkshopStoryUI.Wood, 28, Exit);

        page = WorkshopStoryUI.Region(plate, "上漆边操作页", new Rect(0, 0, 1, 1));
        work = page.gameObject;
        title = WorkshopStoryUI.Label(page, "当前样式", "", new Rect(.045f, .817f, .55f, .111f),
            61, ink, TextAnchor.MiddleLeft, true);
        description = WorkshopStoryUI.Label(page, "配色说明", "", new Rect(.66f, .833f, .295f, .078f),
            26, ink, TextAnchor.MiddleRight);
        board = WorkshopStoryUI.Region(page, "帘边工作板", new Rect(.045f, .225f, .64f, .548f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        progress = WorkshopStoryUI.Label(page, "包边进度", "", new Rect(.72f, .64f, .235f, .105f),
            39, ink, TextAnchor.MiddleLeft, true);
        instructions = WorkshopStoryUI.Label(page, "包边操作", "", new Rect(.72f, .397f, .235f, .215f),
            28, ink, TextAnchor.MiddleLeft);
        feedback = WorkshopStoryUI.Label(page, "操作反馈", "", new Rect(.045f, .137f, .91f, .054f),
            27, ink, TextAnchor.MiddleLeft);
        WorkshopStoryUI.Button(page, "返回选样", "返回选样", new Rect(.045f, .044f, .159f, .078f),
            WorkshopStoryUI.Wood, 28, ShowCatalog);
        confirm = WorkshopStoryUI.Button(page, "确认包边", "确认成品", new Rect(.583f, .044f, .175f, .078f),
            WorkshopStoryUI.Green, 31, Confirm);
        WorkshopStoryUI.Button(page, "回工坊", "回工坊  Esc", new Rect(.778f, .044f, .177f, .078f),
            WorkshopStoryUI.Wood, 28, Exit);
    }
}
