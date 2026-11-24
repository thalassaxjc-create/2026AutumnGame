using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CurtainPatternView : MonoBehaviour
{
    WorkshopOrder order;
    CurtainPatternDraft draft;
    Action returnToWorkshop;
    CurtainPatternGraphic board;
    CurtainPatternGraphic sample;
    Text dayLabel;
    Text title;
    Text caption;
    Text instructions;
    Text score;
    Text remark;
    Text actionLabel;
    Button undo;
    Button swap;
    Button press;
    Button review;
    Button continueWork;
    bool inspecting;

    public CurtainPatternGraphic Board => board;

    public void Open(WorkshopOrder activeOrder, Action exit)
    {
        order = activeOrder;
        draft = order.PatternDraft;
        returnToWorkshop = exit;
        inspecting = false;
        if (board == null)
            Build();
        gameObject.SetActive(true);
        remark.text = string.Empty;
        sample.Show(draft.Kind, isSample: true);
        dayLabel.text = order.DayName + "   /   工坊手作";
        Refresh();
    }

    public void Close()
    {
        order = null;
        draft = null;
        returnToWorkshop = null;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (draft == null)
            return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Exit();
            return;
        }
        Mouse mouse = Mouse.current;
        if (inspecting || mouse == null)
            return;
        if (mouse.rightButton.wasPressedThisFrame)
            Undo();
        if (mouse.leftButton.wasPressedThisFrame &&
            board.TryHitPin(mouse.position.ReadValue(), out int loop, out int pin))
        {
            if (draft.TryStitch(loop, pin))
                ApplyChange(draft.IsClosed(loop) ? "收结完成" : string.Empty);
            else
                remark.text = "从亮起的起针孔开始，沿相邻孔走线。";
        }
    }

    void Exit() => returnToWorkshop?.Invoke();

    void Undo()
    {
        if (draft?.Undo() == true)
            ApplyChange("已退回上一步");
    }

    void SwitchThread()
    {
        if (draft.TrySwitchThread())
            ApplyChange("已换朱线。绕外框时，记得压好两处青线尾。");
    }

    void PressThread()
    {
        if (draft.TryPress())
            ApplyChange("青线尾已压入朱边");
    }

    void ReviewOrConfirm()
    {
        if (!inspecting)
        {
            inspecting = true;
            ApplyChange("");
            return;
        }
        if (order.MinutesUntilClose <= order.PatternMinutes)
        {
            remark.text = $"需要 {order.PatternMinutes} 分钟加工并留出交单时间，今天还剩 {order.MinutesUntilClose} 分钟。";
            return;
        }
        if (order.TryConfirmPattern())
            Exit();
    }

    void ReturnToStitching()
    {
        inspecting = false;
        ApplyChange("");
    }

    void ApplyChange(string message)
    {
        remark.text = message;
        Refresh();
    }

    void Refresh()
    {
        bool colored = draft.Kind == CurtainPatternKind.VermilionJade;
        title.text = inspecting ? "验样 · " + order.OrderName : order.OrderName;
        caption.text = inspecting ? "完成品" : "台面 · " + (!colored ? "本色线" : draft.UsesVermilion ? "朱线" : "青线");
        instructions.text = inspecting ? "对照样稿查看针路、边口和收结。"
            : !colored ? "照左侧示范补右侧两格。\n右上格随金色针孔走线，右下格看中线找对称。"
            : draft.UsesVermilion ? "从外框一角起针，再沿朱边走一圈。\n到左右接边孔时，点「压线」。"
            : "用青线走出四格龟纹。\n收结后换朱线，从外边开始包边。";
        var result = draft.Evaluate();
        score.text = $"对样 {result.Correct} / {result.Total}    ·    {result.Grade} 级\n加工 {order.PatternMinutes} 分钟";
        board.Show(draft.Kind, draft, showPins: !inspecting);

        undo.interactable = !inspecting && draft.CanUndo;
        swap.gameObject.SetActive(colored && !inspecting);
        swap.interactable = draft.CanSwitchThread;
        press.gameObject.SetActive(colored && !inspecting && draft.UsesVermilion);
        press.interactable = draft.CanPress;
        continueWork.gameObject.SetActive(inspecting);
        actionLabel.text = inspecting ? "确认成品" : "对样";
        review.interactable = !inspecting || draft.CanConfirm;
        if (inspecting && !draft.CanConfirm)
            remark.text = colored ? "至少收好一格青纹，并闭合朱色外框。" : "至少收好右侧一格龟纹。";
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        var scale = gameObject.AddComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1920, 1080);
        scale.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var back = WorkshopStoryUI.Panel(transform, "纹样台画面", new Rect(0, 0, 1, 1),
            new Color32(41, 34, 28, 255), true);
        var plate = WorkshopStoryUI.Panel(back.transform, "纸本", new Rect(.045f, .07f, .91f, .86f),
            WorkshopStoryUI.Paper).transform;
        var ink = WorkshopStoryUI.Ink;
        dayLabel = WorkshopStoryUI.Label(plate, "天数", "", new Rect(.045f, .9f, .45f, .06f),
            26, ink, TextAnchor.MiddleLeft);
        title = WorkshopStoryUI.Label(plate, "纹样订单", "", new Rect(.045f, .82f, .6f, .09f),
            54, ink, TextAnchor.MiddleLeft, true);
        WorkshopStoryUI.Label(plate, "操作提示", "左键落针   ·   右键退针   ·   Esc 回工坊",
            new Rect(.57f, .85f, .39f, .075f), 27, ink, TextAnchor.MiddleRight);
        caption = WorkshopStoryUI.Label(plate, "台面说明", "", new Rect(.045f, .752f, .59f, .055f),
            30, ink, TextAnchor.MiddleLeft, true);
        board = WorkshopStoryUI.Region(plate, "落针板", new Rect(.045f, .205f, .60f, .54f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        WorkshopStoryUI.Label(plate, "样稿标题", "订单样稿", new Rect(.695f, .752f, .26f, .055f),
            30, ink, TextAnchor.MiddleLeft, true);
        sample = WorkshopStoryUI.Region(plate, "样稿", new Rect(.695f, .49f, .26f, .255f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        instructions = WorkshopStoryUI.Label(plate, "当天技法", "", new Rect(.695f, .325f, .26f, .151f),
            27, ink, TextAnchor.MiddleLeft);
        score = WorkshopStoryUI.Label(plate, "计分", "", new Rect(.695f, .209f, .26f, .11f),
            27, ink, TextAnchor.MiddleLeft, true);
        remark = WorkshopStoryUI.Label(plate, "针路反馈", "", new Rect(.045f, .137f, .91f, .058f),
            25, ink, TextAnchor.MiddleLeft);

        undo = WorkshopStoryUI.Button(plate, "撤一步", "撤一步", new Rect(.045f, .044f, .13f, .078f),
            WorkshopStoryUI.Wood, 28, Undo);
        swap = WorkshopStoryUI.Button(plate, "换朱线", "换朱线", new Rect(.194f, .044f, .13f, .078f),
            CurtainPatternGraphic.Vermilion, 28, SwitchThread);
        press = WorkshopStoryUI.Button(plate, "压线", "压线", new Rect(.343f, .044f, .13f, .078f),
            CurtainPatternGraphic.Vermilion, 28, PressThread);
        continueWork = WorkshopStoryUI.Button(plate, "返回修补", "返回修补", new Rect(.194f, .044f, .18f, .078f),
            WorkshopStoryUI.Wood, 28, ReturnToStitching);
        review = WorkshopStoryUI.Button(plate, "对样", "对样", new Rect(.583f, .044f, .175f, .078f),
            WorkshopStoryUI.Green, 31, ReviewOrConfirm);
        actionLabel = review.GetComponentInChildren<Text>();
        WorkshopStoryUI.Button(plate, "回工坊", "回工坊", new Rect(.778f, .044f, .177f, .078f),
            WorkshopStoryUI.Wood, 28, Exit);
    }
}
