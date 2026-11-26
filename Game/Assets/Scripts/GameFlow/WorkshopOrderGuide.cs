using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>主线的场景接线、时间推进、任务指引与统一背包 HUD。</summary>
public sealed class WorkshopOrderGuide : MonoBehaviour
{
    const float NightTraderHighlightSeconds = 6f;
    static readonly Color32 NightTraderArrivalColor = new(119, 79, 38, 246);
    static readonly Color32 NightTraderHintColor = new(47, 35, 27, 226);

    Transform elder;
    Transform uncle;
    WorkshopComputer computer;
    BambooWorkbench bamboo;
    WeavingStationTrigger weaving;
    LacquerStationTrigger lacquer;
    DryingStationTrigger drying;
    PatternStationTrigger pattern;
    WorkshopDayOverlay dayOverlay;
    ChopableBamboo forestTarget;
    Camera sceneCamera;
    Text status;
    Text inventory;
    Text task;
    Text teaHint;
    Image nightTraderHint;
    Text marker;
    float nightTraderHighlightUntil;
    bool inventoryExpanded;
    int displayedOrderRevision = -1;
    int displayedInventoryRevision = -1;
    bool displayedPatternBlank;
    int displayedFinishedCurtains = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        WorkshopOrder.Current = null;
    }

    // WorldScene 是完整游戏入口。直接从室内场景播放时，仍保留各工位的独立预览能力。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartStory()
    {
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool("BambooHarvest.StartPreview", false))
            return;
#endif
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneTransitionState.WorldSceneName)
            return;

        var settings = Resources.Load<WorkshopOrderSettings>("WorkshopOrderSettings");
        if (settings == null)
        {
            Debug.LogError("找不到 WorkshopOrderSettings 订单配置。");
            return;
        }

        WorkshopInventory.StartFirstDay();
        WorkshopOrder.Current = new WorkshopOrder(settings);
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(scene, LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || WorkshopOrder.Current == null)
            return;

        var root = new GameObject("主线订单引导", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<WorkshopOrderGuide>().Configure(scene);
    }

    void Configure(Scene scene)
    {
        sceneCamera = Camera.main;
        ConfigureCanvas();
        WorkshopStoryUI.EnsureEventSystem(transform);
        BuildHud();
        WorkshopOrder order = WorkshopOrder.Current;
        dayOverlay = gameObject.AddComponent<WorkshopDayOverlay>();
        dayOverlay.Build(order);
        gameObject.AddComponent<WorkshopJournalView>().Build(order.Journal);

        if (scene.name == SceneTransitionState.WorldSceneName)
        {
            elder = ConfigureDialogue("村口大爷", WorkshopOrder.Phase.Village,
                new[] { "村口大爷：哟，大学生回来啦？你舅舅最近可是接到了政府的支持款，非遗传承人有指望咯！" },
                new[] { "我就是回来休息一下", "我回来看看舅舅需要帮忙不" });

            uncle = ConfigureDialogue("舅舅", WorkshopOrder.Phase.Home, UncleLines(order.Days),
                order.Days == 4 ? new[] { "分批验收，每张 150 元起", "当面按样验货，每张 130 元起，合格整批加 120 元" } : null, true,
                UncleCasualLine(order.Days));

            if (uncle != null)
            {
                if (order.IsTeachingDay)
                    uncle.GetComponent<TemporaryDialogueEntryPoint>().EnableLessonDebrief();
                CreateCompanions(uncle, order.Days);
                if (order.Days >= 4)
                    CreateCustomer(uncle, order.Days);
            }
            ConfigureForestTarget();

            if (order.CurrentPhase == WorkshopOrder.Phase.Opening)
                gameObject.AddComponent<OpeningSequence>().Begin(order);
            else
                dayOverlay.ShowMorning();
        }
        else if (scene.name == SceneTransitionState.WorkshopSceneName)
        {
            computer = FindFirstObjectByType<WorkshopComputer>();
            bamboo = FindFirstObjectByType<BambooWorkbench>();
            weaving = FindFirstObjectByType<WeavingStationTrigger>();
            lacquer = FindFirstObjectByType<LacquerStationTrigger>();
            pattern = FindFirstObjectByType<PatternStationTrigger>();
            GameObject dryingPoint = GameObject.Find("DryingInteractionPoint");
            if (dryingPoint != null)
            {
                drying = dryingPoint.GetComponent<DryingStationTrigger>();
                if (drying == null)
                    drying = dryingPoint.AddComponent<DryingStationTrigger>();
            }
            else
                Debug.LogWarning("WorkshopScene 未找到 DryingInteractionPoint，无法开始阴干。", this);
        }
    }

    static string[] UncleLines(int day) => day switch
    {
        2 => new[]
        {
            "舅舅：纸坊要两张龟纹小帘。你看我留的半张样稿，照着左边把右边补齐。回到起针孔，把结收紧。",
            "小皮：左边两格我记住了！小白把叼来的竹丝搁在样稿旁边，摇了摇尾巴。",
            "舅舅：去工坊电脑看订单。两张帘都从竹子做起，刷漆阴干后再到纹样台补龟纹。"
        },
        3 => new[]
        {
            "舅舅：镇上体验馆订了两张朱边青示范帘。两张都要先编帘、刷漆阴干，再用青线织纹、朱线收边。",
            "舅舅：绕到左右两个接边孔，把青线尾压进朱边里。做完拿样稿对一眼，再去电脑交单。",
            "小皮：昨天我练熟了龟纹。今天看谁把两个线尾收得齐！"
        },
        4 => new[]
        {
            "舅舅：纸坊送回两张用坏的旧帘，还得补一张新帘。先做新帘，阴干了拿去给老周试捞。",
            "老周：试捞完，我把断线的和翘边的两张拿给你。你想分开交，还是三张一起按样验？",
            "舅舅：你来定。交不齐就把已经做好的交了，别为了赶数把湿帘从架上摘下来。"
        },
        5 => new[]
        {
            "舅舅：艾琳傍晚就走，想在走之前自己捞一张纸。院子里有旧帘，你挑个好懂的办法教她。",
            "舅舅：你头一天也是摸着做的。想想哪一句提醒，能让她一下就明白。",
            "舅舅：等她试完，咱们把五天的账算清楚，再聊聊你想去哪。"
        },
        _ =>
                new[]
                {
                    "舅舅：咳咳……既然回来了，正好。我这身体，怕是做不了几年了。政府的非遗补助金下来了，还有我这套宅子和手艺，总得留给一个人。",
                    "舅舅：这五天，我每天发给你们同样的订单。交帘和赶集挣的钱都算，最后谁攒得最多，谁就继承这门手艺。",
                    "【主线任务开启：继承权争夺战】\n第一天订单：普通宣纸帘两张（小尺寸、无纹样）。想收工的时候，点左上角进入下一天。"
                }
    };

    static string UncleCasualLine(int day) => day switch
    {
        1 => "舅舅：别急，阴干最忌心浮气躁。",
        4 => "舅舅：先交做好的。老周下次来，再拿前一张试捞的纸和新帘对一对。",
        5 => "舅舅：艾琳就在院里。给她找张样帘，或者拿昨天的试纸去。",
        _ => "舅舅：做完先对一对样稿，再去电脑上交单。"
    };

    void ConfigureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
    }

    void BuildHud()
    {
        status = CreateLabel("主线状态", 28, new Vector2(850, 54));
        AnchorTopLeft(status.rectTransform, new Vector2(32, -24));
        status.alignment = TextAnchor.MiddleLeft;

        inventory = CreateLabel("背包", 25, new Vector2(650, 250));
        AnchorTopLeft(inventory.rectTransform, new Vector2(32, -86));
        inventory.alignment = TextAnchor.UpperLeft;

        task = CreateLabel("任务提示", 30, new Vector2(780, 58));
        task.alignment = TextAnchor.MiddleRight;
        AnchorTopRight(task.rectTransform, new Vector2(-32, -48));

        teaHint = CreateLabel("茶摊支线提示", 24, new Vector2(720, 44));
        teaHint.alignment = TextAnchor.MiddleRight;
        teaHint.color = new Color32(255, 225, 156, 255);
        teaHint.text = "茶摊支线 · 村口过桥右转，帮王老板写告示";
        AnchorTopRight(teaHint.rectTransform, new Vector2(-32, -112));
        teaHint.enabled = false;

        nightTraderHint = WorkshopStoryUI.Panel(transform, "夜间小贩提示", new Rect(1, 1, 0, 0), NightTraderHintColor);
        nightTraderHint.rectTransform.sizeDelta = new Vector2(650, 53);
        AnchorTopRight(nightTraderHint.rectTransform, new Vector2(-32, -162));
        WorkshopStoryUI.Label(nightTraderHint.transform, "提示文字", "18:00 · 黑衣小贩已到村口牌楼旁，按 E 交易",
            new Rect(.03f, .07f, .94f, .86f), 25, WorkshopStoryUI.Paper, TextAnchor.MiddleRight, bold: true);
        nightTraderHint.gameObject.SetActive(false);

        marker = CreateLabel("当前目标", 54, new Vector2(64, 76));
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
        marker.text = "!";
    }

    void Update()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        if (order == null)
            return;

        bool unobstructed = !GameFlowModalState.IsModalOpen && !SceneTransitionState.IsLoading;
        order.Tick(Time.unscaledDeltaTime, unobstructed);
        Keyboard keyboard = Keyboard.current;
        if (unobstructed && keyboard != null && keyboard.bKey.wasPressedThisFrame)
        {
            inventoryExpanded = !inventoryExpanded;
            displayedInventoryRevision = -1;
        }
    }

    void LateUpdate()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        if (order == null)
            return;

        RefreshHud(order);
        bool unobstructed = !GameFlowModalState.IsModalOpen && !SceneTransitionState.IsLoading;
        dayOverlay.Refresh(unobstructed, inventoryExpanded);
        unobstructed = !GameFlowModalState.IsModalOpen && !SceneTransitionState.IsLoading;
        task.enabled = status.enabled = inventory.enabled = unobstructed;
        RefreshVillageHints(order, unobstructed);

        Transform target = FindTarget(order);
        marker.enabled = false;
        if (!unobstructed || target == null || sceneCamera == null)
            return;

        float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * .38f;
        Vector3 point = sceneCamera.WorldToScreenPoint(target.position + Vector3.up * (2.5f + bounce));
        if (point.z <= 0)
            return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, point, null, out Vector2 local);
        marker.rectTransform.anchoredPosition = local + ((RectTransform)transform).rect.size * .5f;
        marker.color = order.CurrentPhase == WorkshopOrder.Phase.NewOrder
            ? new Color32(227, 36, 36, 255) : new Color32(247, 85, 35, 255);
        marker.enabled = true;
    }

    void RefreshVillageHints(WorkshopOrder order, bool worldVisible)
    {
        teaHint.enabled = worldVisible && order.CanExplore && !order.TeaNoticeCompleted;
        bool showNightTrader = worldVisible && order.BlackMarketOpen;
        if (nightTraderHint.gameObject.activeSelf != showNightTrader)
            nightTraderHint.gameObject.SetActive(showNightTrader);
        if (!showNightTrader)
            return;

        if (order.TakeNightTraderArrivalNotice())
            nightTraderHighlightUntil = Time.unscaledTime + NightTraderHighlightSeconds;
        nightTraderHint.color = Time.unscaledTime < nightTraderHighlightUntil ? NightTraderArrivalColor : NightTraderHintColor;
        nightTraderHint.rectTransform.anchoredPosition = new Vector2(-32, teaHint.enabled ? -162 : -112);
    }

    void RefreshHud(WorkshopOrder order)
    {
        if (displayedOrderRevision != order.Revision)
        {
            displayedOrderRevision = order.Revision;
            status.text = $"{order.DayName}  {order.Hour:00}:{order.Minute:00}    主角 ¥{order.Funds}    小皮 ¥{order.XiaoPiFunds}    小白 ¥{order.XiaoBaiFunds}";
            string next = TaskText(order);
            task.text = !order.IsTeachingDay && (order.CurrentPhase == WorkshopOrder.Phase.Production ||
                order.CurrentPhase == WorkshopOrder.Phase.ReadyToSubmit)
                ? $"第 {order.CurrentSheetNumber}/{order.RequiredSheets} 张 · {next}"
                : next;
        }

        if (displayedInventoryRevision == WorkshopInventory.Revision && displayedPatternBlank == order.HasPatternBlank &&
            displayedFinishedCurtains == order.FinishedCurtainCount)
            return;
        displayedInventoryRevision = WorkshopInventory.Revision;
        displayedPatternBlank = order.HasPatternBlank;
        displayedFinishedCurtains = order.FinishedCurtainCount;
        inventory.text = inventoryExpanded
            ? $"背包  [B 收起]\n完整青竹 ×{WorkshopInventory.GreenBamboo}    斑竹 ×{order.SpottedBamboo}    增白剂 ×{order.Brightener}\n粗竹片 ×{WorkshopInventory.RoughPieces}    精竹片 ×{WorkshopInventory.FinePieces}    残次竹片 ×{WorkshopInventory.DefectivePieces}\n竹条 ×{WorkshopInventory.BambooRods}    残次竹条 ×{WorkshopInventory.DefectiveRods}\n竹丝 ×{WorkshopInventory.BambooThreads}    残次竹丝 ×{WorkshopInventory.DefectiveThreads}    其中斑竹丝 ×{WorkshopInventory.SpottedThreads}"
            : $"背包 [B 展开]    青竹 ×{WorkshopInventory.GreenBamboo}    斑竹 ×{order.SpottedBamboo}    竹丝 ×{WorkshopInventory.TotalThreads}";
        if (order.HasPatternBlank)
            inventory.text += inventoryExpanded ? "\n待织纹样的素帘 ×1" : "    素帘 ×1";
        if (order.FinishedCurtainCount > 0)
            inventory.text += (inventoryExpanded ? "\n" : "    ") + $"成品帘 ×{order.FinishedCurtainCount}";
    }

    Transform FindTarget(WorkshopOrder order)
    {
        // NPC 自身带有头顶光标，避免再叠一层屏幕光标。
        if (order.CurrentPhase == WorkshopOrder.Phase.Village || order.CurrentPhase == WorkshopOrder.Phase.Home)
            return null;
        if (order.IsTeachingDay || order.PendingTrial || order.AwaitingReturnedCurtain)
            return null;
        if (order.CurrentPhase == WorkshopOrder.Phase.NewOrder || order.CurrentPhase == WorkshopOrder.Phase.ReadyToSubmit)
            return computer != null ? computer.transform : null;
        if (order.CurrentPhase != WorkshopOrder.Phase.Production || order.DayEnded)
            return null;
        if (order.NeedsBamboo)
        {
            if (forestTarget != null && !forestTarget.IsInteractable)
                ConfigureForestTarget();
            return forestTarget != null ? forestTarget.transform : null;
        }
        if (order.CanUsePatternStation)
            return pattern != null ? pattern.transform : null;
        if (order.GetStatus(WorkshopOrder.Stage.BambooFinishing) != WorkshopOrder.StageStatus.Complete)
            return bamboo != null ? bamboo.transform : null;
        if (order.GetStatus(WorkshopOrder.Stage.Weaving) != WorkshopOrder.StageStatus.Complete)
            return weaving != null ? weaving.transform : null;
        if (order.GetStatus(WorkshopOrder.Stage.Lacquer) != WorkshopOrder.StageStatus.Complete)
            return lacquer != null ? lacquer.transform : null;
        return drying != null ? drying.transform : null;
    }

    static string TaskText(WorkshopOrder order)
    {
        if (order.PendingTrial)
            return "第一张已阴干，带到院子找老周试捞";
        if (order.IsTeachingDay && order.CurrentPhase == WorkshopOrder.Phase.Production)
            return order.CanTeachLesson ? "18:00 前回院子找艾琳，一起试捞并揭纸" : "艾琳已经启程，可以收工结算";
        if (order.IsTeachingDay && order.CurrentPhase == WorkshopOrder.Phase.OrderComplete)
            return "艾琳已试捞，去找舅舅结算五天的账";
        return order.CurrentPhase switch
        {
            WorkshopOrder.Phase.Opening => "序章",
            WorkshopOrder.Phase.Village => "去和村口大爷说话",
            WorkshopOrder.Phase.Home => "去院子找舅舅",
            WorkshopOrder.Phase.NewOrder => $"去工坊电脑查看{order.DayName}订单",
            WorkshopOrder.Phase.ReadyToSubmit => order.CollectorAvailable && order.PendingCurtainGrade is WorkshopOrder.QualityGrade.S or WorkshopOrder.QualityGrade.A
                ? "可去电脑交单，或带龟纹帘找集市东头的富商"
                : order.IsPatternOrder ? "纹样完成，回电脑交单" : "阴干完成，返回工坊电脑提交订单",
            WorkshopOrder.Phase.OrderComplete => "今日订单已交齐，可继续赶集赚钱",
            WorkshopOrder.Phase.Complete => order.CanAdvanceDay ? "今日已结算" : "五天比试已结束",
            _ => ProductionTarget(order)
        };
    }

    static string ProductionTarget(WorkshopOrder order)
    {
        if (order.CanCollectCurtain)
            return "阴干完成，回晾晒架按 E 收下纸帘";
        if (order.AwaitingReturnedCurtain)
            return order.DeliveredCount == 1 ? "去院子找老周，补第二张帘的断线" : "去院子找老周，压平第三张帘的翘边";
        if (order.NeedsBamboo)
            return order.HarvestedStalks.Count >= BambooHarvest.HarvestCount
                ? "今天的青竹已采完，明早竹林会长出新竹"
                : "去竹林采青竹，或到集市买斑竹";
        if (order.CanUsePatternStation) return $"阴干完成，到纹样台完成{order.OrderName}";
        if (order.GetStatus(WorkshopOrder.Stage.Preparation) != WorkshopOrder.StageStatus.Complete)
            return "带青竹回篾竹区完成劈竹与剐青";
        if (order.GetStatus(WorkshopOrder.Stage.BambooFinishing) != WorkshopOrder.StageStatus.Complete)
            return "在篾竹区完成过剑门与抽丝";
        if (order.GetStatus(WorkshopOrder.Stage.Weaving) != WorkshopOrder.StageStatus.Complete)
            return "到编帘区完成编帘";
        if (order.GetStatus(WorkshopOrder.Stage.Lacquer) != WorkshopOrder.StageStatus.Complete)
            return "到刷漆区完成上漆";
        return order.IsDrying ? $"等待阴干（剩余 {order.RemainingDryingMinutes / 60:00}:{order.RemainingDryingMinutes % 60:00}）"
            : "把漆帘放到晾晒架阴干";
    }

    void ConfigureForestTarget()
    {
        forestTarget = null;
        BambooHarvest forest = FindFirstObjectByType<BambooHarvest>();
        if (forest == null)
            return;
        float nearest = float.PositiveInfinity;
        foreach (ChopableBamboo stalk in forest.GetComponentsInChildren<ChopableBamboo>())
        {
            if (!stalk.CanChop && !stalk.CanPickup)
                continue;
            float distance = (stalk.transform.position - BambooHarvest.PathAnchor).sqrMagnitude;
            if (distance >= nearest)
                continue;
            nearest = distance;
            forestTarget = stalk;
        }
    }

    static Transform ConfigureDialogue(string npcName, WorkshopOrder.Phase phase, string[] lines,
        string[] choices = null, bool allowDuringProduction = false, params string[] casualLines)
    {
        foreach (NpcCharacter npc in FindObjectsByType<NpcCharacter>(FindObjectsSortMode.None))
        {
            if (npc.name != npcName && npc.DisplayName != npcName)
                continue;
            NpcDialogueInteraction interaction = npc.GetComponent<NpcDialogueInteraction>();
            if (interaction == null)
                interaction = npc.gameObject.AddComponent<NpcDialogueInteraction>();
            TemporaryDialogueEntryPoint dialogue = npc.GetComponent<TemporaryDialogueEntryPoint>();
            if (dialogue == null)
                dialogue = npc.gameObject.AddComponent<TemporaryDialogueEntryPoint>();
            dialogue.ConfigureStory(phase, lines, choices);
            if (casualLines != null && casualLines.Length > 0)
                dialogue.ConfigureCasual(casualLines);
            interaction.Configure(dialogue, "按 E 交谈", interaction.InteractionRadius);
            interaction.ConfigureStoryPhase(phase, allowDuringProduction);
            return npc.transform;
        }
        Debug.LogWarning($"未找到剧情 NPC：{npcName}");
        return null;
    }

    static void CreateCustomer(Transform uncleTransform, int day)
    {
        string customerName = day == 4 ? "老周" : "艾琳";
        var customer = new GameObject(customerName);
        customer.transform.position = uncleTransform.position + new Vector3(.3f, 0f, 5.5f);
        CreatePrimitivePart(customer.transform, PrimitiveType.Capsule, "Body", Vector3.zero,
            new Vector3(.64f, .9f, .64f), day == 4 ? new Color32(92, 113, 78, 255) : new Color32(148, 86, 94, 255));
        CreatePrimitivePart(customer.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, .94f, 0f),
            Vector3.one * .55f, new Color32(230, 190, 152, 255));
        var collider = customer.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up * .15f;
        collider.height = 2.1f;
        collider.radius = .34f;
        customer.AddComponent<NpcCharacter>().Configure(customerName);
        var dialogue = customer.AddComponent<WorkshopCustomerDialogue>();
        customer.AddComponent<NpcDialogueInteraction>().Configure(dialogue, "按 E 交谈", 2f);
    }

    static void CreateCompanions(Transform uncleTransform, int day)
    {
        if (GameObject.Find("小皮") == null)
        {
            GameObject child = new GameObject("小皮");
            child.transform.position = uncleTransform.position + new Vector3(1.7f, 0f, .35f);
            CreatePrimitivePart(child.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, -.1f, 0f),
                new Vector3(.62f, .72f, .62f), new Color32(70, 126, 172, 255));
            CreatePrimitivePart(child.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, .82f, 0f),
                Vector3.one * .55f, new Color32(226, 184, 148, 255));
            string line = day == 2 ? "小皮：我在找右边两个起针孔。"
                : day == 3 ? "小皮：朱线绕到接边孔时，我得记得压住青线尾。"
                : "小皮：哼，才做了一张吗？";
            ConfigureRuntimeNpc(child, "小皮", line, 2.05f);
        }

        if (GameObject.Find("小白") == null)
        {
            GameObject dog = new GameObject("小白");
            dog.transform.position = uncleTransform.position + new Vector3(-1.45f, -.55f, .55f);
            CreatePrimitivePart(dog.transform, PrimitiveType.Capsule, "Body", Vector3.zero,
                new Vector3(.48f, .46f, .72f), new Color32(238, 232, 211, 255), new Vector3(90f, 0f, 0f));
            CreatePrimitivePart(dog.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, .2f, .65f),
                Vector3.one * .48f, new Color32(246, 241, 222, 255));
            CreatePrimitivePart(dog.transform, PrimitiveType.Cube, "BambooThread", new Vector3(0f, .12f, 1.02f),
                new Vector3(.08f, .08f, .95f), new Color32(222, 190, 111, 255), new Vector3(0f, 90f, 0f));
            NpcDialogueInteraction interaction = ConfigureRuntimeNpc(dog, "小白", "小白叼着竹丝，冲你摇了摇尾巴。", 1.45f);
            Transform nameplate = dog.transform.Find("Nameplate");
            if (nameplate != null)
                nameplate.localPosition = Vector3.up * 1.05f;
            interaction.ConfigureMarkerHeight(1.45f);
        }
    }

    static NpcDialogueInteraction ConfigureRuntimeNpc(GameObject root, string npcName, string line, float markerHeight)
    {
        NpcCharacter npc = root.AddComponent<NpcCharacter>();
        npc.Configure(npcName);
        TemporaryDialogueEntryPoint dialogue = root.AddComponent<TemporaryDialogueEntryPoint>();
        dialogue.ConfigureCasual(line);
        NpcDialogueInteraction interaction = root.AddComponent<NpcDialogueInteraction>();
        interaction.Configure(dialogue, "按 E 交谈", 3f);
        interaction.ConfigureStoryPhase(WorkshopOrder.Phase.Production, true);
        interaction.ConfigureMarkerHeight(markerHeight);
        return interaction;
    }

    static void CreatePrimitivePart(Transform parent, PrimitiveType type, string partName, Vector3 localPosition,
        Vector3 localScale, Color color, Vector3 localEuler = default)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localEulerAngles = localEuler;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null)
            return;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    Text CreateLabel(string labelName, int size, Vector2 dimensions)
    {
        var label = new GameObject(labelName, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(transform, false);
        label.rectTransform.sizeDelta = dimensions;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.raycastTarget = false;
        InteractionPromptUI.EnsureChineseFont(label);
        var shadow = label.gameObject.AddComponent<Outline>();
        shadow.effectColor = new Color(.16f, .10f, .07f, .95f);
        shadow.effectDistance = new Vector2(2, -2);
        return label;
    }

    static void AnchorTopLeft(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = Vector2.up;
        rect.pivot = Vector2.up;
        rect.anchoredPosition = position;
    }

    static void AnchorTopRight(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
    }
}
