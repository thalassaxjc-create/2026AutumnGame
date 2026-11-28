using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VermilionTrimView : MonoBehaviour
{
    static readonly string[] Sides = { "上边，向右", "右边，向下", "下边，向左", "左边，向上" };
    static readonly string[] CornerNames = { "右上角", "右下角", "左下角", "左上角" };
    WorkshopOrder order;
    VermilionTrimDraft draft;
    Action exit;
    CurtainPatternGraphic board;
    Text progress;
    Text instructions;
    Text feedback;
    Button confirm;

    public CurtainPatternGraphic Board => board;

    public void Open(WorkshopOrder activeOrder, Action returnToWorkshop)
    {
        order = activeOrder;
        draft = order == null ? new VermilionTrimDraft() : order.OpenTrimDraft();
        exit = returnToWorkshop;
        if (board == null)
            Build();
        feedback.text = "";
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
        if (draft == null || draft.Complete || mouse == null)
            return;
        if (!board.TryEdgePointer(mouse.position.ReadValue(), draft.ActiveSide, out float along, out float offset))
            return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (draft.NeedsCorner)
            {
                if (draft.TryPressCorner(along, offset))
                {
                    feedback.text = "折角已压牢。";
                    Refresh();
                }
                return;
            }
            if (draft.TryGrip(along, offset))
                feedback.text = "";
        }

        if (draft.Held && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame) && draft.Drag(along, offset))
            Refresh();
        if (!mouse.leftButton.isPressed)
            draft.Release();
    }

    void OnDisable() => draft?.Release();

    void Exit() => exit?.Invoke();

    void Confirm()
    {
        if (order == null || order.TryConfirmVermilionTrim())
            Exit();
        else if (draft.Complete)
            feedback.text = $"今天剩余时间不足 {order.TrimMinutes} 分钟。";
    }

    void Refresh()
    {
        board.Show(CurtainPatternKind.None, showGuide: false, edgeWork: draft, withJadeBamboo: true);
        progress.text = $"已压牢 {draft.SealedCorners} / {VermilionTrimDraft.SideCount} 角";
        instructions.text = draft.Complete ? "四边已贴好，可以确认包边。"
            : draft.NeedsCorner ? $"松开左键，再点金色标记，压牢{CornerNames[draft.ActiveSide]}。"
            : $"在金色标记处按住左键，沿{Sides[draft.ActiveSide]}拖到折角。";
        confirm.interactable = draft.Complete;
    }

    void Build()
    {
        WorkshopStoryUI.ConfigureOverlay(gameObject, 240);
        var shade = WorkshopStoryUI.Panel(transform, "朱边台画面", new Rect(0, 0, 1, 1),
            new Color32(41, 34, 28, 255), true);
        var plate = WorkshopStoryUI.Panel(shade.transform, "上朱边台面", new Rect(.045f, .07f, .91f, .86f),
            WorkshopStoryUI.Paper).transform;
        var ink = WorkshopStoryUI.Ink;
        WorkshopStoryUI.Label(plate, "工序", "上朱边", new Rect(.045f, .816f, .52f, .116f),
            62, ink, TextAnchor.MiddleLeft, true);
        WorkshopStoryUI.Label(plate, "包边用途", "红绸加固帘边，也方便在纸浆池里看清帘框",
            new Rect(.53f, .831f, .424f, .07f), 27, ink, TextAnchor.MiddleRight);
        board = WorkshopStoryUI.Region(plate, "朱边工作板", new Rect(.045f, .225f, .613f, .544f))
            .gameObject.AddComponent<CurtainPatternGraphic>();
        progress = WorkshopStoryUI.Label(plate, "包边进度", "", new Rect(.70f, .631f, .255f, .123f),
            44, ink, TextAnchor.MiddleLeft, true);
        instructions = WorkshopStoryUI.Label(plate, "包边操作", "", new Rect(.70f, .429f, .255f, .18f),
            30, ink, TextAnchor.MiddleLeft);
        feedback = WorkshopStoryUI.Label(plate, "操作反馈", "", new Rect(.045f, .137f, .91f, .054f),
            28, ink, TextAnchor.MiddleLeft);
        confirm = WorkshopStoryUI.Button(plate, "确认朱边", "确认包边", new Rect(.583f, .044f, .175f, .078f),
            CurtainPatternGraphic.Vermilion, 31, Confirm);
        WorkshopStoryUI.Button(plate, "回工坊", "回工坊  Esc", new Rect(.778f, .044f, .177f, .078f),
            WorkshopStoryUI.Wood, 28, Exit);
    }
}
