using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>第一天主线的场景接线、时间推进、任务指引与统一背包 HUD。</summary>
public sealed class WorkshopOrderGuide : MonoBehaviour
{
    Transform elder;
    Transform uncle;
    WorkshopComputer computer;
    BambooWorkbench bamboo;
    WeavingStationTrigger weaving;
    LacquerStationTrigger lacquer;
    DryingStationTrigger drying;
    Transform forestTarget;
    Camera sceneCamera;
    Text status;
    Text inventory;
    Text task;
    Text marker;
    bool inventoryExpanded;
    int displayedOrderRevision = -1;
    int displayedInventoryRevision = -1;

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

        var root = new GameObject("第一天订单引导", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<WorkshopOrderGuide>().Configure(scene);
    }

    void Configure(Scene scene)
    {
        sceneCamera = Camera.main;
        ConfigureCanvas();
        BuildHud();

        if (scene.name == SceneTransitionState.WorldSceneName)
        {
            elder = ConfigureDialogue("村口大爷", WorkshopOrder.Phase.Village,
                new[] { "村口大爷：哟，大学生回来啦？你舅舅最近可是接到了政府的支持款，非遗传承人有指望咯！" },
                new[] { "我就是回来休息一下", "我回来看看舅舅需要帮忙不" });

            uncle = ConfigureDialogue("舅舅", WorkshopOrder.Phase.Home,
                new[]
                {
                    "舅舅：咳咳……既然回来了，正好。我这身体，怕是做不了几年了。政府的非遗补助金下来了，还有我这套宅子和手艺，总得留给一个人。",
                    "舅舅：你们三个——你、小皮和小白——都想要这门手艺和资产。今天我给你们派发同样的订单。今天结束时，谁赚的钱最多，谁就先占上风，懂了吗？",
                    "【主线任务开启：继承权争夺战】\n第一天订单：普通宣纸帘（小尺寸、无纹样）。请在 24:00 前完成。"
                }, null, true, "舅舅：别急，阴干最忌心浮气躁。");

            if (uncle != null)
                CreateCompanions(uncle);
            ConfigureForestTarget();

            if (WorkshopOrder.Current.CurrentPhase == WorkshopOrder.Phase.Opening)
                gameObject.AddComponent<OpeningSequence>().Begin(WorkshopOrder.Current);
        }
        else if (scene.name == SceneTransitionState.WorkshopSceneName)
        {
            computer = FindFirstObjectByType<WorkshopComputer>();
            bamboo = FindFirstObjectByType<BambooWorkbench>();
            weaving = FindFirstObjectByType<WeavingStationTrigger>();
            lacquer = FindFirstObjectByType<LacquerStationTrigger>();
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
        status = CreateLabel("第一天状态", 28, new Vector2(850, 54));
        AnchorTopLeft(status.rectTransform, new Vector2(32, -24));
        status.alignment = TextAnchor.MiddleLeft;

        inventory = CreateLabel("背包", 25, new Vector2(650, 250));
        AnchorTopLeft(inventory.rectTransform, new Vector2(32, -86));
        inventory.alignment = TextAnchor.UpperLeft;

        task = CreateLabel("任务提示", 30, new Vector2(780, 58));
        task.alignment = TextAnchor.MiddleRight;
        task.rectTransform.anchorMin = task.rectTransform.anchorMax = Vector2.one;
        task.rectTransform.pivot = Vector2.one;
        task.rectTransform.anchoredPosition = new Vector2(-32, -48);

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
        task.enabled = status.enabled = inventory.enabled = unobstructed;

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

    void RefreshHud(WorkshopOrder order)
    {
        if (displayedOrderRevision != order.Revision)
        {
            displayedOrderRevision = order.Revision;
            status.text = $"第一天  {order.Hour:00}:{order.Minute:00}    主角 ¥{order.Funds}    小皮 ¥{order.XiaoPiFunds}    小白 ¥{order.XiaoBaiFunds}";
            task.text = TaskText(order);
        }

        if (displayedInventoryRevision == WorkshopInventory.Revision)
            return;
        displayedInventoryRevision = WorkshopInventory.Revision;
        inventory.text = inventoryExpanded
            ? $"背包  [B 收起]\n完整青竹 ×{WorkshopInventory.GreenBamboo}\n粗竹片 ×{WorkshopInventory.RoughPieces}    精竹片 ×{WorkshopInventory.FinePieces}    残次竹片 ×{WorkshopInventory.DefectivePieces}\n竹条 ×{WorkshopInventory.BambooRods}    残次竹条 ×{WorkshopInventory.DefectiveRods}\n竹丝 ×{WorkshopInventory.BambooThreads}    残次竹丝 ×{WorkshopInventory.DefectiveThreads}"
            : $"背包 [B 展开]    青竹 ×{WorkshopInventory.GreenBamboo}    竹丝 ×{WorkshopInventory.TotalThreads}";
    }

    Transform FindTarget(WorkshopOrder order)
    {
        // NPC 自身带有头顶光标，避免再叠一层屏幕光标。
        if (order.CurrentPhase == WorkshopOrder.Phase.Village || order.CurrentPhase == WorkshopOrder.Phase.Home)
            return null;
        if (order.CurrentPhase == WorkshopOrder.Phase.NewOrder || order.CurrentPhase == WorkshopOrder.Phase.ReadyToSubmit)
            return computer != null ? computer.transform : null;
        if (order.CurrentPhase != WorkshopOrder.Phase.Production || order.DayEnded)
            return null;
        if (order.CanHarvest)
            return forestTarget;
        if (order.GetStatus(WorkshopOrder.Stage.Preparation) != WorkshopOrder.StageStatus.Complete || order.CanWorkBamboo)
            return bamboo != null ? bamboo.transform : null;
        if (order.GetStatus(WorkshopOrder.Stage.Weaving) != WorkshopOrder.StageStatus.Complete)
            return weaving != null ? weaving.transform : null;
        if (order.GetStatus(WorkshopOrder.Stage.Lacquer) != WorkshopOrder.StageStatus.Complete)
            return lacquer != null ? lacquer.transform : null;
        return drying != null ? drying.transform : null;
    }

    static string TaskText(WorkshopOrder order)
    {
        if (order.DayEnded && order.CurrentPhase != WorkshopOrder.Phase.Complete)
            return "第一天已于 24:00 结束";
        return order.CurrentPhase switch
        {
            WorkshopOrder.Phase.Opening => "序章",
            WorkshopOrder.Phase.Village => "去和村口大爷说话",
            WorkshopOrder.Phase.Home => "去院子找舅舅",
            WorkshopOrder.Phase.NewOrder => "去工坊电脑查看第一天订单",
            WorkshopOrder.Phase.ReadyToSubmit => "阴干完成，返回工坊电脑提交订单",
            WorkshopOrder.Phase.Complete => "第一天订单已完成",
            _ => ProductionTarget(order)
        };
    }

    static string ProductionTarget(WorkshopOrder order)
    {
        if (order.CanHarvest) return "去竹林获得 1 根完整青竹";
        if (order.GetStatus(WorkshopOrder.Stage.Preparation) != WorkshopOrder.StageStatus.Complete)
            return "带青竹回篾竹区完成劈竹与剐青";
        if (order.CanWorkBamboo) return "在篾竹区完成过剑门与抽丝";
        if (order.GetStatus(WorkshopOrder.Stage.Weaving) != WorkshopOrder.StageStatus.Complete)
            return "到编帘区完成编帘";
        if (order.GetStatus(WorkshopOrder.Stage.Lacquer) != WorkshopOrder.StageStatus.Complete)
            return "到刷漆区完成上漆";
        return order.IsDrying ? $"等待阴干（剩余 {order.RemainingDryingMinutes / 60:00}:{order.RemainingDryingMinutes % 60:00}）"
            : "把漆帘放到晾晒架阴干";
    }

    void ConfigureForestTarget()
    {
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
            forestTarget = stalk.transform;
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

    static void CreateCompanions(Transform uncleTransform)
    {
        if (GameObject.Find("小皮") == null)
        {
            GameObject child = new GameObject("小皮");
            child.transform.position = uncleTransform.position + new Vector3(1.7f, 0f, .35f);
            CreatePrimitivePart(child.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, -.1f, 0f),
                new Vector3(.62f, .72f, .62f), new Color32(70, 126, 172, 255));
            CreatePrimitivePart(child.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, .82f, 0f),
                Vector3.one * .55f, new Color32(226, 184, 148, 255));
            ConfigureRuntimeNpc(child, "小皮", "小皮：哼，才做了一张吗？", 2.05f);
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
        interaction.ConfigureStoryPhase(WorkshopOrder.Phase.Production);
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
}
