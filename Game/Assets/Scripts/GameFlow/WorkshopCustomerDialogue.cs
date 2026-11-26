using System;
using UnityEngine;
using UnityEngine.InputSystem;

internal sealed class WorkshopCustomerDialogue : MonoBehaviour, IDialogueEntryPoint, IDialogueAvailability
{
    VillageActivityView view;
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
            ShowLessonChoices(order);
        else
            ShowPaperwork(order);
        view.Root.SetActive(true);
        return true;
    }

    void ShowPaperwork(WorkshopOrder order, string feedback = "")
    {
        if (order.PendingTrial)
        {
            string sheet = !order.TrialTurned ? "第一次试捞：左上角偏薄。"
                : order.ThinCornerFollowsCurtain
                    ? "第一次：左上角偏薄。旋转后：右下角偏薄。"
                    : "第一次：左上角偏薄。旋转后：仍是左上角偏薄。";
            view.SetContent("老周的试捞", sheet + "\n老周：把帘转半圈，夹架留在原地。看看薄角跟谁走。" + feedback);
            view.ShowTrialProof(order.TrialTurned, order.ThinCornerFollowsCurtain);
            if (!order.TrialTurned)
                view.SetPrimary("转半圈，再试一次", () => ShowPaperwork(order,
                    order.TryTurnTrial() ? "" : "\n今天来不及再试一张了。"));
            else
            {
                view.SetOption(0, "把帘的松边拉平", () => Repair(order, true));
                view.SetOption(1, "把纸坊夹架调平", () => Repair(order, false));
            }
            return;
        }

        if (order.CanPlanBatch)
        {
            bool hasMaterial = order.HasBatchMaterial;
            view.SetContent($"第 {order.CurrentSheetNumber} 张 · 按样加工",
                "用 1 根完整青竹或 100 根竹丝。其余工序按样做到 B 级，最后仍要上架阴干。也可以回工坊全程手做。" +
                (hasMaterial ? "" : "\n需要先备料。") + feedback);
            view.SetOption(0, "我来编帘 · 预备 45 分", () => Prepare(order, WorkshopOrder.BatchMethod.HandWeave), hasMaterial);
            view.SetOption(1, "我来刷漆 · 预备 75 分", () => Prepare(order, WorkshopOrder.BatchMethod.HandLacquer), hasMaterial);
            view.SetOption(2, $"请小皮帮做 · ¥{WorkshopOrder.PartnerFee}", () => Prepare(order, WorkshopOrder.BatchMethod.Partner),
                hasMaterial && order.Funds >= WorkshopOrder.PartnerFee);
            return;
        }

        view.SetContent("纸坊交接", order.TrialResolved
            ? "老周：修好了。薄的那张和匀的那张都给你。你拿回去，明天还能教人。"
            : "老周：第一张先做出来，阴干后带来试捞。两张试纸一对，比我在这讲半天都明白。");
    }

    void Repair(WorkshopOrder order, bool repairCurtain)
    {
        bool hadTime = order.MinutesUntilClose > 25;
        bool fixedFault = order.TryRepairTrial(repairCurtain);
        ShowPaperwork(order, fixedFault ? "" : hadTime
            ? "\n老周又捞了一张，薄角还在。看看旋转前后的区别。"
            : "\n剩下的时间不够修整了。");
    }

    void Prepare(WorkshopOrder order, WorkshopOrder.BatchMethod method)
    {
        if (!order.TryPrepareBatch(method))
        {
            ShowPaperwork(order, "\n现在来不及按这个办法加工。");
            return;
        }
        view.SetContent("样帘已交代好", method switch
        {
            WorkshopOrder.BatchMethod.HandWeave => "材料已备好。回工坊亲手编帘，接着把帘放上架。",
            WorkshopOrder.BatchMethod.HandLacquer => "帘已按样编好。回工坊刷漆，然后上架阴干。",
            _ => "小皮把帘交给你。工钱从你的账上转到他那边。回工坊上架阴干。"
        });
    }

    void ShowLessonChoices(WorkshopOrder order)
    {
        view.SetContent("艾琳来学捞纸", "艾琳：我想带走一张自己捞出来的纸。你从哪一步教我？我照着试。");
        view.SetOption(0, "用旧帘练起帘", () => ShowLesson(order, WorkshopOrder.LessonTopic.LevelFrame));
        view.SetOption(1, "用龟纹样帘练收线", () => ShowLesson(order, WorkshopOrder.LessonTopic.TurtleKnot),
            order.CanUseTurtleSample);
        view.SetOption(2, "用昨天的试纸找薄角", () => ShowLesson(order, WorkshopOrder.LessonTopic.ThinCorner), order.TrialResolved);
    }

    void ShowLesson(WorkshopOrder order, WorkshopOrder.LessonTopic topic)
    {
        var (prompt, correct, incorrect) = topic switch
        {
            WorkshopOrder.LessonTopic.TurtleKnot => ("艾琳走完一圈龟纹，线尾还松着。最后一步怎么走？", "回到起针孔收结", "就近挑个孔打结"),
            WorkshopOrder.LessonTopic.ThinCorner when order.ThinCornerFollowsCurtain =>
                ("夹架没动，帘转了半圈，薄角也跟着转了。先查哪里？", "找帘上的松边", "垫高原位置的夹架"),
            WorkshopOrder.LessonTopic.ThinCorner =>
                ("帘转了半圈，薄角还在原处。先查哪里？", "把原位置的夹架调平", "把帘的松边拉平"),
            _ => ("右边已经离水，左边还在浆里。怎么起帘才匀？", "放平，再把两边一起提起", "继续抬高右边")
        };
        view.SetContent("轮到艾琳试一次", prompt);
        view.SetOption(0, incorrect, () => FinishLesson(order, topic, false));
        view.SetOption(1, correct, () => FinishLesson(order, topic, true));
        view.SetClear(() => ShowLessonChoices(order), true, "换一张样帘");
        if (topic == WorkshopOrder.LessonTopic.ThinCorner)
            view.ShowTrialProof(true, order.ThinCornerFollowsCurtain);
    }

    void FinishLesson(WorkshopOrder order, WorkshopOrder.LessonTopic topic, bool correct)
    {
        if (!order.TryTeachLesson(topic, correct))
        {
            Close();
            return;
        }
        view.SetContent("艾琳的第一张纸", order.LessonDescription + "\n" + order.LessonResult);
        view.ShowLessonProof(order.LessonSucceeded);
    }

    void Update()
    {
        if (closed != null && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
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
