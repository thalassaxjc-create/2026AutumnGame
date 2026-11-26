using System;
using UnityEngine;
using UnityEngine.InputSystem;

internal sealed class WorkshopCustomerDialogue : MonoBehaviour, IDialogueEntryPoint, IDialogueAvailability
{
    enum PullPhase { None, Practice, Observe }

    static readonly int[] StitchRoute = { 0, 1, 3, 2 };
    static readonly string[] Corners = { "左上", "右上", "左下", "右下" };
    const int RaisedCorners = (1 << 0) | (1 << 2);

    VillageActivityView view;
    int stitchCount;
    int clamps;
    bool frameTurned;
    WorkshopOrder.LessonTopic sample;
    PullPhase pullPhase;
    PaperPull pull;
    bool paperSucceeded;
    int visitorAttempts;
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

        if (order.AwaitingReturnedCurtain)
        {
            if (order.DeliveredCount == 1)
                ShowStitchRepair(order);
            else
                ShowClampRepair(order);
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
        if (fixedFault)
        {
            view.SetContent("薄角消失了", "老周又捞了一张，薄角消失了。把这张新帘交给工坊电脑，再回来领退修的旧帘。");
            view.ShowLessonProof(true, "复检试纸");
            return;
        }
        ShowPaperwork(order, hadTime ? "\n老周又捞了一张，薄角还在。看看旋转前后的区别。"
            : "\n剩下的时间不够修整了。");
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
        view.SetPrimary("收线并检查", () => FinishRepair(order, false), stitchCount == StitchRoute.Length);
    }

    void ShowClampRepair(WorkshopOrder order, string feedback = "")
    {
        string raised = frameTurned ? "转架后翘在右上和右下，看看夹子有没有跟着帘一起转。" :
            "左上和左下翘起了。夹住这两个纸角，再转半圈检查。";
        view.SetContent("返修 · 压平翘边", raised + feedback);
        for (int i = 0; i < Corners.Length; i++)
        {
            int localCorner = frameTurned ? 3 - i : i;
            bool clamped = (clamps & (1 << localCorner)) != 0;
            view.SetOption(i, Corners[i] + (clamped ? " · 已夹住" : " · 放夹子"), () =>
            {
                clamps ^= 1 << localCorner;
                ShowClampRepair(order);
            });
        }
        view.SetOption(4, $"请小皮修 · ¥{WorkshopOrder.PartnerFee}", () => FinishRepair(order, true),
            order.Funds >= WorkshopOrder.PartnerFee);
        view.SetClear(() => { frameTurned = !frameTurned; ShowClampRepair(order); }, true, "转半圈检查");
        view.SetPrimary("压平并检查", () =>
        {
            if (frameTurned && clamps == RaisedCorners)
                FinishRepair(order, false);
            else
                ShowClampRepair(order, "\n仍有翘边。只夹住两处翘角，再转架检查。");
        });
    }

    void FinishRepair(WorkshopOrder order, bool hired)
    {
        if (!order.TryFinishReturnedCurtain(hired))
        {
            ShowPaperwork(order);
            view.SetBody("现在来不及修整了，可以先交已完成的帘再收工。");
            return;
        }
        view.SetContent("旧帘修好了", hired
            ? "小皮压平了旧帘，工钱已结给他。成品帘已放进背包，回工坊电脑交付。"
            : "老周试过了，老帘能接着用了。成品帘已放进背包，回工坊电脑交付。");
    }

    void ShowLessonChoices(WorkshopOrder order)
    {
        view.SetContent("艾琳来学捞纸", "艾琳想带走一张自己捞出来的纸。先从工坊留的样帘里挑一张，你做一遍，她再试一次。");
        view.SetOption(0, "用普通旧帘练起帘", () => SelectSample(WorkshopOrder.LessonTopic.LevelFrame));
        view.SetOption(1, "用龟纹样帘看走浆", () => SelectSample(WorkshopOrder.LessonTopic.TurtleKnot),
            order.CanUseLessonSample(WorkshopOrder.LessonTopic.TurtleKnot));
        view.SetOption(2, "用修过的旧帘查薄角", () => SelectSample(WorkshopOrder.LessonTopic.ThinCorner),
            order.CanUseLessonSample(WorkshopOrder.LessonTopic.ThinCorner));
    }

    void SelectSample(WorkshopOrder.LessonTopic topic)
    {
        sample = topic;
        visitorAttempts = 0;
        string detail = sample switch
        {
            WorkshopOrder.LessonTopic.TurtleKnot => "龟纹帘的花纹容易看清，但要把帘端得更平，才不会糊住纹理。",
            WorkshopOrder.LessonTopic.ThinCorner => "旧帘容易朝左偏。艾琳把昨天的薄角试纸放在缸边当对照。",
            _ => "普通帘的纸浆容易看清。先示范怎么把两边一起提起来。"
        };
        view.SetContent("先做一张练习纸", detail +
            "\n用 A / D 或拖动平衡条扶正帘面。等纸浆铺匀、出现提示时，按 E 起帘。");
        view.SetPrimary("开始练习", StartPractice);
        view.SetClear(() => ShowLessonChoices(WorkshopOrder.Current), true, "换一张样帘");
    }

    void StartPractice()
    {
        pull = new PaperPull(sample);
        pullPhase = PullPhase.Practice;
        view.SetContent("玩家试捞", "用 A / D 或拖动平衡条，把帘保持在绿色区。看准提示后按 E 或点「起帘」。");
        view.SetPrimary("起帘", EndPractice);
        view.ShowPull(0f, 0f, "纸浆正在铺开");
    }

    void EndPractice()
    {
        if (pullPhase != PullPhase.Practice)
            return;
        pullPhase = PullPhase.None;
        bool even = pull.Lift();
        string result = even ? "两边同时离水，纸面均匀。" :
            pull.Time > PaperPull.LiftWindowEnd ? "帘在浆里停得太久，边缘厚了一圈。" :
            "帘起得太早或没端平，纸面露出了薄角。";
        view.SetContent("练习结果", result + "\n接下来轮到艾琳。看见她的一边先抬高时，就及时提醒她放平。");
        view.ShowLessonProof(even, "练习纸");
        view.SetClear(StartPractice, true, "再练一次");
        view.SetPrimary("轮到艾琳", StartVisitorPull);
    }

    void StartVisitorPull()
    {
        visitorAttempts++;
        pull = new PaperPull(sample);
        pullPhase = PullPhase.Observe;
        view.SetContent("艾琳试捞", "等帘面明显歪斜时，按 E 或点「提醒放平」。你只有一次提醒机会。");
        view.SetPrimary("提醒放平", RemindVisitor);
        view.ShowPull(0f, 0f, "艾琳把帘放进纸浆");
    }

    void RemindVisitor()
    {
        if (pullPhase != PullPhase.Observe || pull.Reminded)
            return;
        pull.Remind();
        view.SetPrimary("已经提醒", RemindVisitor, false);
    }

    void EndVisitorPull()
    {
        pullPhase = PullPhase.None;
        paperSucceeded = pull.TimelyReminder;
        view.SetContent("艾琳的第一张纸", paperSucceeded
            ? "艾琳听见提醒，把帘放平了。纸面完整，等你们一起揭下来。"
            : "纸的一角透光。你们可以留着这一张，也可以再一起试一次。");
        view.ShowLessonProof(paperSucceeded);
        if (!paperSucceeded && visitorAttempts < 2)
            view.SetClear(StartVisitorPull, true, "再试一次");
        view.SetPrimary("揭纸并盖村印", ShowPaperDestinations);
    }

    void ShowPaperDestinations()
    {
        view.SetContent("在纸角盖上村印", "艾琳扶着纸边，你盖好了村印。要把这张纸留在哪儿？");
        view.SetOption(0, "给艾琳带走", () => FinishLesson(WorkshopOrder.PaperDestination.Visitor));
        view.SetOption(1, "挂在工坊", () => FinishLesson(WorkshopOrder.PaperDestination.Workshop));
        view.SetOption(2, "夹进小册子", () => FinishLesson(WorkshopOrder.PaperDestination.Journal));
    }

    void FinishLesson(WorkshopOrder.PaperDestination destination)
    {
        var order = WorkshopOrder.Current;
        if (!order.TryFinishLesson(sample, paperSucceeded, destination))
        {
            Close();
            return;
        }
        view.SetContent("这张纸有了去处", order.PaperDestinationText + "\n和舅舅结算五天的账吧。");
        view.ShowLessonProof(paperSucceeded, "已盖村印");
    }

    void Update()
    {
        if (closed == null)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true)
        {
            Close();
            return;
        }
        if (pullPhase == PullPhase.None)
            return;

        bool pressed = keyboard?.eKey.wasPressedThisFrame == true;
        float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (pullPhase == PullPhase.Practice)
        {
            float steering = (keyboard?.dKey.isPressed == true ? 1f : 0f) -
                (keyboard?.aKey.isPressed == true ? 1f : 0f);
            Mouse mouse = Mouse.current;
            float? pointer = mouse?.leftButton.isPressed == true ? view.ReadPullPointer(mouse.position.ReadValue()) : null;
            pull.Practice(delta, steering, pointer);
            string cue = pull.Time > PaperPull.LiftWindowEnd ? "起帘晚了，边缘开始变厚" :
                pull.Lift() ? "纸浆铺匀了，现在起帘" : "保持平衡，等纸浆铺匀";
            view.ShowPull(pull.Balance, pull.Coverage, cue);
            if (pressed || pull.Time > PaperPull.LiftWindowEnd + 1f)
                EndPractice();
        }
        else
        {
            pull.Observe(delta);
            view.ShowPull(pull.Balance, pull.Coverage, pull.TimelyReminder ? "艾琳放平了帘面" :
                pull.CueVisible ? "一边抬高了，现在提醒" : "看帘面有没有歪斜");
            if (pressed)
                RemindVisitor();
            if (pull.Time >= 4.8f)
                EndVisitorPull();
        }
    }

    void OnDisable() => Close();

    void Close()
    {
        if (closed == null)
            return;
        pullPhase = PullPhase.None;
        view.Root.SetActive(false);
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisibility;
        Action callback = closed;
        closed = null;
        callback();
    }
}
