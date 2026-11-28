using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class VillageMarketDialogue : MonoBehaviour, IDialogueEntryPoint, IDialogueAvailability
{
    public enum Role { Blacksmith, TeaOwner, LiAunt, GoodsMerchant, Collector, NightTrader, XiaoPiBucket, OfficialSteward }

    const string TeaTitle = "茶摊的大学问";
    static readonly string[] TeaWords =
    {
        "骗走旧物的人", "请乡亲们", "上好的", "留意", "今晚", "一只茶杯", "以鉴宝为由", "送来"
    };
    static readonly int[] CorrectTeaOrder = { 1, 3, 6, 0 };

    [SerializeField] Role role;
    [SerializeField] GameObject appearance;

    readonly List<int> chosenWords = new(4);
    VillageActivityView view;
    PaperInspectionView paperInspectionView;
    NpcCharacter character;
    Action closedCallback;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    string feedback;
    bool viewingOfficialTip;
    int displayedOrderRevision = -1;
    int displayedInventoryRevision = -1;

    public Role MerchantRole => role;
    public bool IsDialogueAvailable => WorkshopOrder.Current is { } order
        ? role switch
        {
            Role.TeaOwner => order.CanVisitTeaStall,
            Role.LiAunt => order.CanVisitSpottedGrove,
            Role.Collector => order.CollectorVisiting,
            Role.NightTrader => order.BlackMarketOpen,
            Role.XiaoPiBucket => order.CanDoSideQuests,
            Role.OfficialSteward => order.OfficialStewardAvailable,
            _ => order.CanExplore
        }
        : role == Role.XiaoPiBucket;

    public void Configure(Role value, GameObject model)
    {
        role = value;
        appearance = model;
    }

    void Awake()
    {
        character = GetComponent<NpcCharacter>();
        UpdateAppearance();
    }

    void Update()
    {
        UpdateAppearance();
        if (closedCallback != null && role is Role.NightTrader or Role.XiaoPiBucket)
        {
            if (!IsDialogueAvailable)
            {
                Close();
                return;
            }
            if (displayedOrderRevision != (WorkshopOrder.Current?.Revision ?? -1) ||
                displayedInventoryRevision != WorkshopInventory.Revision)
                Refresh();
        }
        if (closedCallback != null && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    void UpdateAppearance()
    {
        if (role is not (Role.NightTrader or Role.Collector or Role.OfficialSteward))
            return;
        bool visible = IsDialogueAvailable || closedCallback != null;
        if (appearance != null && appearance.activeSelf != visible)
            appearance.SetActive(visible);
        character?.SetNameplateVisible(visible);
    }

    public bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed)
    {
        if (!IsDialogueAvailable || closedCallback != null)
            return false;
        view ??= new VillageActivityView(transform, Close);
        feedback = null;
        viewingOfficialTip = false;
        chosenWords.Clear();
        closedCallback = onDialogueClosed;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
        view.Root.SetActive(true);
        return true;
    }

    void Close()
    {
        if (closedCallback == null)
            return;
        if (paperInspectionView != null)
            paperInspectionView.Hide();
        view.Root.SetActive(false);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        Action callback = closedCallback;
        closedCallback = null;
        callback();
    }

    void OnDisable() => Close();

    string Message(string description) => string.IsNullOrEmpty(feedback) ? description : description + "\n\n" + feedback;

    void Refresh()
    {
        var order = WorkshopOrder.Current;
        displayedOrderRevision = order?.Revision ?? -1;
        displayedInventoryRevision = WorkshopInventory.Revision;
        if (order == null)
        {
            view.SetContent("小皮的编帘水桶", "小皮在这里给编帘用的线过水。\n想找增白剂，可以从村口开始正式游戏，18:00 后找牌楼旁的黑衣小贩。");
            view.SetExpandedBody();
            return;
        }

        switch (role)
        {
            case Role.Blacksmith:
                ShowBlacksmith(order);
                break;

            case Role.TeaOwner:
                if (viewingOfficialTip)
                    ShowOfficialTip(order);
                else
                    ShowTea(order);
                break;

            case Role.OfficialSteward:
                ShowOfficialSteward(order);
                break;

            case Role.LiAunt:
                string grove = order.SpottedGroveHarvested ? "竹丛少了两根。谁动了我的斑竹？" :
                    "竹林靠我家那边有一片紫斑竹，是做高级花纹帘的好料，黄狗替我看着。要用就去集市买，100 元一根，可别打我家竹子的主意。";
                view.SetContent("李婶的斑竹", "李婶：" + grove);
                break;

            case Role.GoodsMerchant:
                view.SetContent("集市杂货摊", Message($"这里收购和出售斑竹。篾竹时按 V 放斑竹，整张帘用斑竹，舅舅按双倍价验收。\n" +
                    $"你的斑竹 {order.SpottedBamboo} 根    资金 ¥{order.Funds}"));
                view.SetOption(0, $"买 1 根斑竹  ¥{WorkshopOrder.SpottedBambooPurchasePrice}",
                    () => Trade(order.TryBuySpottedBamboo, "买入 1 根斑竹。"),
                    order.CanExplore && order.Funds >= WorkshopOrder.SpottedBambooPurchasePrice);
                view.SetOption(1, $"卖 1 根斑竹  +¥{WorkshopOrder.SpottedBambooSalePrice}",
                    () => Trade(order.TrySellSpottedBamboo, $"卖出斑竹，收入 {WorkshopOrder.SpottedBambooSalePrice} 元。"),
                    order.CanExplore && order.SpottedBamboo > 0);
                break;

            case Role.Collector:
                ShowCollector(order);
                break;

            case Role.NightTrader:
                view.SetContent("毁尸灭迹的残次品", Message("黑衣小贩：每天 18:00 后在村口找我。废竹丝一把 25 根，只给 5 元。\n" +
                    "特效增白剂 20 元/瓶：会腐蚀宣纸，是行内禁品。小皮的编帘水桶在工坊编帘台旁。\n" +
                    $"残次竹丝 {WorkshopInventory.DefectiveThreads} 根    合格竹丝 {WorkshopInventory.BambooThreads} 根    增白剂 {order.Brightener} 瓶    资金 ¥{order.Funds}"));
                view.SetOption(0, $"卖 {WorkshopOrder.ScrapBundle} 根残次竹丝  +¥{WorkshopOrder.ScrapBundlePrice}",
                    () => Trade(() => order.TrySellThreads(true), $"卖出一把残次竹丝，收入 {WorkshopOrder.ScrapBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.DefectiveThreads >= WorkshopOrder.ScrapBundle);
                view.SetOption(1, $"卖 {WorkshopOrder.ThreadBundle} 根竹丝  +¥{WorkshopOrder.ThreadBundlePrice}",
                    () => Trade(() => order.TrySellThreads(false), $"卖出一捆竹丝，收入 {WorkshopOrder.ThreadBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.BambooThreads >= WorkshopOrder.ThreadBundle);
                view.SetOption(2, $"买特效增白剂  ¥{WorkshopOrder.BrightenerPrice}",
                    () => Trade(order.TryBuyBrightener, "花费 20 元，获得 1 瓶增白剂。靠近小皮水桶按 E，可选择偷偷倒入。"),
                    order.BlackMarketOpen && order.Funds >= WorkshopOrder.BrightenerPrice);
                break;

            case Role.XiaoPiBucket:
                view.SetContent("小皮的编帘水桶", Message(order.XiaoPiSabotaged
                    ? "水面泛起不自然的白色，旁边小皮今天的帘子也发白了。\n今晚舅舅验货会发现质量问题，从小皮今天的收入中扣除 100 元。\n今天已投放过，不能重复投放或叠加扣款。"
                    : "小皮在这里给编帘用的线过水。\n偷偷倒入 1 瓶特效增白剂，会让他今天做的帘子发白；晚上验货时，小皮当天收入扣除 100 元。\n" +
                        $"背包：增白剂 {order.Brightener} 瓶。" + (order.Brightener == 0 ? "\n18:00 后可到村口黑衣小贩处花 20 元购买。" : "\n可以选择离开，不消耗瓶子。")));
                view.SetExpandedBody();
                if (!order.XiaoPiSabotaged)
                    view.SetPrimary("偷偷倒入 1 瓶", () => Trade(order.TrySabotageXiaoPi, "用掉了 1 瓶增白剂。"),
                        order.CanSabotageXiaoPi);
                break;
        }
    }

    void ShowBlacksmith(WorkshopOrder order)
    {
        view.SetContent("铁匠铺", Message("铁匠：看看新打的黄铜篾刀，还有缠了青线的木梭。装好就能在工位用。\n" +
            $"篾刀一次剐青四片，一次过剑门出 100 根竹条。木梭可织出 S+，成品综合 S+ 加价 {order.SPlusPremiumPercent}%。\n\n" +
            $"青竹 {WorkshopInventory.GreenBamboo} 根 · 每根收 {WorkshopOrder.GreenBambooSalePrice} 元    资金 ¥{order.Funds}"));
        view.SetOption(0, "卖 1 根青竹", () => Trade(order.TrySellGreenBamboo, $"卖出青竹，收入 {WorkshopOrder.GreenBambooSalePrice} 元。"),
            order.CanExplore && WorkshopInventory.GreenBamboo > 0);
        view.SetOption(2, order.HasUpgradedKnife ? "篾刀 · 已装备" : $"好一点的篾刀  ¥{WorkshopOrder.KnifeUpgradePrice}",
            () => Trade(order.TryBuyKnifeUpgrade, "黄铜嵌边篾刀已装到篾竹台。"),
            !order.HasUpgradedKnife && order.Funds >= WorkshopOrder.KnifeUpgradePrice);
        view.SetOption(3, order.HasAutomaticShuttle ? "木梭 · 已装备" : $"自动绑线木梭  ¥{WorkshopOrder.ShuttleUpgradePrice}",
            () => Trade(order.TryBuyShuttleUpgrade, "自动绑线木梭已装到编帘台。"),
            !order.HasAutomaticShuttle && order.Funds >= WorkshopOrder.ShuttleUpgradePrice);
        view.ShowTool(2, false);
        view.ShowTool(3, true);
    }

    void ShowOfficialTip(WorkshopOrder order)
    {
        string description = order.OfficialJobCompleted ? "王老板：府上的纸已经验完了，管事把工钱结清了。"
            : order.HasOfficialTip
            ? $"下午 {WorkshopOrder.OfficialOpeningHour:00}:00–{WorkshopOrder.OfficialClosingHour:00}:00 去集市北侧晒谷人家门口找府上管事。\n" +
              $"府上招人验纸。用斜光挑出瑕疵，最多领 {WorkshopOrder.OfficialJobReward} 元工钱。"
            : "王老板：府上下午派管事出门办货。你付茶钱，我给你带个准信儿。";
        view.SetContent("高官府上的消息", Message(description + $"\n\n资金 ¥{order.Funds}"));
        view.SetHeaderAction("返回告示", () => { viewingOfficialTip = false; feedback = null; Refresh(); });
        if (!order.HasOfficialTip)
            view.SetPrimary($"打听消息  ¥{WorkshopOrder.OfficialTipPrice}",
                () => Trade(order.TryBuyOfficialTip, "已打听到府上的差事。"), order.CanBuyOfficialTip);
    }

    void ShowOfficialSteward(WorkshopOrder order)
    {
        if (order.OfficialJobCompleted)
        {
            view.SetContent("高官府上 · 验纸", $"管事：这批纸已经分好了，工钱 {order.OfficialJobPay} 元已结。");
            return;
        }
        string description = $"管事：府里刚收了一批宣纸。我给你支一盏灯，你帮我把这 {PaperInspectionRound.SheetCount} 张纸分一分。\n\n" +
            $"分对一张 {WorkshopOrder.OfficialPaperPay} 元 · 用时 {WorkshopOrder.OfficialJobMinutes} 分钟。";
        if (!order.CanInspectOfficialPaper)
            description += "\n今天离收摊太近了，明天下午再来。";
        view.SetContent("高官府上 · 验纸", description);
        string action = order.OfficialInspection?.Inspected > 0 ? "继续验纸" : "开始验纸";
        view.SetPrimary(action, OpenOfficialInspection, order.CanInspectOfficialPaper);
    }

    void OpenOfficialInspection()
    {
        var order = WorkshopOrder.Current;
        var inspection = order?.OpenOfficialInspection();
        if (inspection == null)
        {
            Refresh();
            return;
        }
        view.Root.SetActive(false);
        paperInspectionView ??= PaperInspectionView.Create(transform, Close);
        paperInspectionView.Open(order, inspection);
    }

    void ShowTeaTipButton(WorkshopOrder order)
    {
        if (order.OfficialTipUnlocked)
            view.SetHeaderAction(order.HasOfficialTip ? "府上差事" : "打听消息",
                () => { viewingOfficialTip = true; feedback = null; Refresh(); });
    }

    void ShowTea(WorkshopOrder order)
    {
        if (order.TeaNoticeCompleted)
        {
            string tip = !order.HasCollectorTip ? "乡亲们看到告示，都说要多留个心眼。" : order.Days == 1
                ? "明天集市东头有个富商收综合至少 A 级的罗纹帘，你可以提前准备。"
                : order.Days == 2 ? "富商今天就在集市东头，收综合至少 A 级的罗纹帘。" : "那位富商第二天来过集市，告示还替大家提着醒呢。";
            view.SetContent(TeaTitle + " · 已完成", "王老板（感激地）：大学生就是有学问！告示写得明明白白，这下乡亲们不会再上当了。\n" +
                $"茶水费 {WorkshopOrder.TeaNoticeReward} 元已领取 · 用时 {WorkshopOrder.TeaNoticeMinutes} 分钟 · 不耗体力\n\n「{tip}」");
            view.SetTeaDraft("已张贴：请乡亲们留意以鉴宝为由骗走旧物的人。", null, false);
            ShowTeaTipButton(order);
            return;
        }

        string sentence = string.Join("  ", chosenWords.ConvertAll(index => TeaWords[index]));
        for (int i = chosenWords.Count; i < CorrectTeaOrder.Length; i++) sentence += "  ____";
        view.SetContent(TeaTitle, Message("王老板（愁眉苦脸）：收古董的借鉴宝骗走了我的旧茶壶！我不识几个字，你是大学生，帮我写告示提醒乡亲们吧。\n" +
            $"按顺序选 4 个词组 · 完成用时 {WorkshopOrder.TeaNoticeMinutes} 分钟 · 不耗体力 · 茶水费 {WorkshopOrder.TeaNoticeReward} 元"));
        for (int i = 0; i < TeaWords.Length; i++)
        {
            int index = i;
            view.SetOption(i, TeaWords[i], () =>
            {
                if (chosenWords.Count < CorrectTeaOrder.Length && !chosenWords.Contains(index))
                    chosenWords.Add(index);
                feedback = null;
                ShowTea(order);
            }, !chosenWords.Contains(i) && chosenWords.Count < CorrectTeaOrder.Length);
        }
        view.SetClear(() =>
        {
            chosenWords.Clear();
            feedback = null;
            ShowTea(order);
        }, chosenWords.Count > 0);
        view.SetTeaDraft($"告示草稿（{chosenWords.Count}/4）：{sentence}", () =>
        {
            if (chosenWords.Count > 0) chosenWords.RemoveAt(chosenWords.Count - 1);
            feedback = null;
            ShowTea(order);
        }, chosenWords.Count > 0);
        view.SetPrimary("贴告示", () => PostTeaNotice(order), chosenWords.Count == CorrectTeaOrder.Length);
        ShowTeaTipButton(order);
    }

    void PostTeaNotice(WorkshopOrder order)
    {
        if (chosenWords.Count != CorrectTeaOrder.Length)
            return;
        for (int i = 0; i < CorrectTeaOrder.Length; i++)
        {
            if (chosenWords[i] == CorrectTeaOrder[i])
                continue;
            feedback = "王老板：这是提醒谁、留意什么人呢？可以撤回或清空重排。选错不扣时间，也不耗体力。";
            ShowTea(order);
            return;
        }
        string thanks = $"王老板给了你 {WorkshopOrder.TeaNoticeReward} 元茶水费。" +
            (order.Days == 1 ? "\n「明天集市东头有个富商收罗纹帘，你可以提前准备。」" : "");
        Trade(order.TryFinishTeaNotice, thanks, "今天剩余时间不足 30 分钟，明早再来吧。");
    }

    void ShowCollector(WorkshopOrder order)
    {
        WorkshopOrder.QualityGrade? grade = order.CollectorCurtainGrade;
        bool eligible = grade.HasValue && grade.Value <= WorkshopOrder.QualityGrade.A;
        view.SetContent("富商收购罗纹帘", Message(order.CollectorBoughtToday ? "富商：多谢，这一张罗纹就够了。" :
            $"富商：我收一张罗纹帘，综合品相至少 A 级。底价 {WorkshopOrder.CollectorBasePrice} 元，按品相加价。\n" +
            $"手上罗纹帘 {(grade.HasValue ? WorkshopOrder.GradeText(grade.Value) : "无")}    货款计入今日收入。"));
        if (!order.CollectorBoughtToday)
            view.SetPrimary("出售罗纹帘", () => Trade(order.TrySellToCollector, "已售出罗纹帘。"), order.CollectorAvailable && eligible);
    }

    void Trade(Func<bool> transact, string success, string failure = "当前无法交易。")
    {
        feedback = transact() ? success : failure;
        Refresh();
    }
}
