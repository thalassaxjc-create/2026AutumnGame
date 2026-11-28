using System;
using UnityEngine;
using UnityEngine.InputSystem;

internal sealed class WorkshopCustomerDialogue : MonoBehaviour, IDialogueEntryPoint, IDialogueAvailability
{
    static readonly int[] StitchRoute = { 0, 1, 3, 2 };
    static readonly string[] Corners = { "左上", "右上", "左下", "右下" };

    VillageActivityView view;
    int stitchCount;
    int clamps;
    bool frameTurned;
    Action closed;
    CursorLockMode previousLock;
    bool previousVisibility;

    public bool IsDialogueAvailable => WorkshopOrder.Current is { } order &&
        (order.IsTeachingDay ? order.CanTeachLesson : order.CanExplore && order.CurrentPhase != WorkshopOrder.Phase.Home);

    public bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed)
    {
        if (!IsDialogueAvailable || closed != null)
            return false;
        view ??= new VillageActivityView(transform, Close);
        closed = onDialogueClosed;
        previousLock = Cursor.lockState;
        previousVisibility = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        WorkshopOrder order = WorkshopOrder.Current;
        if (order.IsTeachingDay)
            ShowLessonChoices();
        else
            ShowPaperwork(order);
        view.Root.SetActive(true);
        return true;
    }

    void ShowPaperwork(WorkshopOrder order, string feedback = "")
    {
        if (order.NeedsThreadRepair)
        {
            ShowStitchRepair(order, feedback);
            return;
        }

        if (order.NeedsWarpRepair)
        {
            ShowClampRepair(order, feedback);
            return;
        }

        if (order.Days == 4)
        {
            view.SetContent("老周", order.DeliveredCount == 0
                ? "老周：断线补好了。拿去纹样台，把这张改成扎花，再交到电脑上。"
                : "老周：翘边拉平了。拿去上漆边台，收一圈朱边青，再交到电脑上。");
            return;
        }

        view.SetContent("纸坊交接", "老周：今天没有要修的帘。");
    }

    void ShowStitchRepair(WorkshopOrder order, string feedback = "")
    {
        view.SetContent("返修 · 补断线",
            "参照旁边的好线：左上起针，沿上边到右上，再绕到右下，从左下收回起针孔。只补断掉的这一段。" +
            $"\n已接 {stitchCount}/{StitchRoute.Length} 孔。" + feedback);
        for (int i = 0; i < Corners.Length; i++)
        {
            int corner = i;
            view.SetOption(i, Corners[i] + "孔", () =>
            {
                if (stitchCount == StitchRoute.Length || corner != StitchRoute[stitchCount])
                    ShowStitchRepair(order, "\n这段被拉歪了。退回一针或接正确的孔。");
                else
                {
                    stitchCount++;
                    ShowStitchRepair(order);
                }
            });
        }
        view.SetClear(() => { stitchCount--; ShowStitchRepair(order); }, stitchCount > 0, "退回一针");
        view.SetPrimary("收线并检查", () => FinishThread(order), stitchCount == StitchRoute.Length);
    }

    void FinishThread(WorkshopOrder order)
    {
        if (!order.TryFinishThreadRepair())
        {
            ShowStitchRepair(order, "\n今天剩下的时间不够补线了。");
            return;
        }
        stitchCount = 0;
        view.SetContent("断线补上了", "老周：线顺着原来的龟纹接回去了。拿到纹样台，把这张改成扎花，再交到电脑上。");
    }

    void ShowClampRepair(WorkshopOrder order, string feedback = "")
    {
        int raised = frameTurned ? (1 << 1) | (1 << 3) : (1 << 0) | (1 << 2);
        bool ready = frameTurned && clamps == raised;
        string where = frameTurned ? "右上和右下" : "左上和左下";
        string body = ready
            ? $"翘边跟着帘走到了{where}，夹子也在这两角上。是帘松了，把松边拉平。"
            : frameTurned
                ? $"转过来以后，翘边在{where}。夹子要跟着帘一起走。取下夹子，只夹现在翘着的两角。"
                : "左上和左下翘着。先夹住这两个角，再转半圈，看翘边跟不跟着帘走。";
        view.SetContent("返修 · 压平翘边", body + feedback);
        for (int i = 0; i < Corners.Length; i++)
        {
            int corner = i;
            bool up = (raised & (1 << corner)) != 0;
            bool held = (clamps & (1 << corner)) != 0;
            view.SetOption(corner, Corners[corner] + (up ? " · 翘" : " · 平") + (held ? " · 已夹住" : " · 放夹子"), () =>
            {
                clamps ^= 1 << corner;
                ShowClampRepair(order);
            });
        }
        view.SetOption(4, "转半圈", () =>
        {
            int rotated = 0;
            for (int i = 0; i < Corners.Length; i++)
                if ((clamps & (1 << i)) != 0)
                    rotated |= 1 << (3 - i);
            clamps = rotated;
            frameTurned = !frameTurned;
            ShowClampRepair(order);
        });
        view.SetClear(() => { clamps = 0; ShowClampRepair(order); }, clamps != 0, "取下夹子");
        if (ready)
            view.SetPrimary("把松边拉平", () => FinishWarp(order));
    }

    void FinishWarp(WorkshopOrder order)
    {
        if (!order.TryFinishWarpRepair())
        {
            ShowClampRepair(order, "\n今天剩下的时间不够收边了。");
            return;
        }
        clamps = 0;
        frameTurned = false;
        view.SetContent("翘边压平了", "老周：松边拉平了。拿到上漆边台，收一圈朱边青，再交到电脑上。");
    }

    void ShowLessonChoices()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        if (order.AilinCurtainWoven)
        {
            if (!order.LessonAdviceGiven)
            {
                view.SetContent("艾琳", order.PatternCritique);
                view.SetPrimary("继续", () =>
                {
                    if (order.TryGivePatternAdvice(order.PatternFault))
                        ShowCurtainDestinations();
                });
            }
            else
                ShowCurtainDestinations();
            return;
        }
        if (order.LessonPrepared)
        {
            view.SetContent("艾琳跟着你", "艾琳：我跟着你。");
            return;
        }
        view.SetContent("艾琳", "艾琳：我从海外来。我想学做宣纸帘。");
        view.SetPrimary("跟我来", () =>
        {
            if (order.PrepareAilinLesson())
                Close();
            else
                view.SetContent("来不及了", "艾琳：天快黑了，这张帘今天做不完。");
        });
    }

    void ShowCurtainDestinations(string note = null)
    {
        bool tight = WorkshopOrder.Current.AilinWeaveGrade is WorkshopOrder.QualityGrade grade &&
            grade <= WorkshopOrder.QualityGrade.B;
        view.SetContent(tight ? "花心扎紧了" : "花心松了一圈",
            (string.IsNullOrEmpty(note) ? "" : note + "\n") + (tight
                ? "艾琳举起自己织的扎花帘：这张是我做的。要把它留在哪儿？"
                : "有几梭和你教的不一样，花心松着。艾琳还是把帘抱在手里。要把它留在哪儿？"));
        view.SetOption(0, "给艾琳带走", () => FinishLesson(WorkshopOrder.PaperDestination.Visitor));
        view.SetOption(1, "挂在工坊", () => FinishLesson(WorkshopOrder.PaperDestination.Workshop));
        view.SetOption(2, "收进手册", () => FinishLesson(WorkshopOrder.PaperDestination.Journal));
    }

    void FinishLesson(WorkshopOrder.PaperDestination destination)
    {
        var order = WorkshopOrder.Current;
        if (!order.TryFinishLesson(destination))
        {
            Close();
            return;
        }
        view.SetContent("这张帘有了去处", order.PaperDestinationText + "\n和舅舅结算五天的账吧。");
        view.ShowLessonProof(true, "扎花帘");
    }

    void Update()
    {
        if (closed == null)
            return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    void OnDisable() => Close();

    void Close()
    {
        if (closed == null)
            return;
        view.Root.SetActive(false);
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisibility;
        Action callback = closed;
        closed = null;
        callback();
    }
}
