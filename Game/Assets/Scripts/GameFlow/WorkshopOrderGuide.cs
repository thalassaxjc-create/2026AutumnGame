using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class WorkshopOrderGuide : MonoBehaviour
{
    Transform elder;
    Transform uncle;
    WorkshopComputer computer;
    BambooWorkbench bamboo;
    WeavingStationTrigger weaving;
    LacquerStationTrigger lacquer;
    Transform forestTarget;
    Camera sceneCamera;
    Text task;
    Text marker;
    int displayedRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        WorkshopOrder.Current = null;
    }

    // WorldScene is the first scene in the build. Starting directly in an interior
    // from the editor keeps the standalone workbench and minigame previews usable.
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

        WorkshopOrder.Current = new WorkshopOrder(settings);
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(scene, LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || WorkshopOrder.Current == null)
            return;

        var root = new GameObject("订单引导", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<WorkshopOrderGuide>().Configure(scene);
    }

    void Configure(Scene scene)
    {
        sceneCamera = Camera.main;
        if (scene.name == SceneTransitionState.WorldSceneName)
        {
            elder = ConfigureDialogue("村口大爷", WorkshopOrder.Phase.Village, "你舅爷好像有事情要和你说嘞。");
            uncle = ConfigureDialogue("舅舅", WorkshopOrder.Phase.Home, "起床了？快去看看有没有新活儿啊。");
            BambooHarvest forest = FindFirstObjectByType<BambooHarvest>();
            if (forest != null)
            {
                float nearest = float.PositiveInfinity;
                foreach (var stalk in forest.GetComponentsInChildren<ChopableBamboo>())
                {
                    if (!stalk.CanChop && !stalk.CanPickup)
                        continue;
                    float distance = (stalk.transform.position - BambooHarvest.PathAnchor).sqrMagnitude;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        forestTarget = stalk.transform;
                    }
                }
            }
        }
        else if (scene.name == SceneTransitionState.WorkshopSceneName)
        {
            computer = FindFirstObjectByType<WorkshopComputer>();
            bamboo = FindFirstObjectByType<BambooWorkbench>();
            weaving = FindFirstObjectByType<WeavingStationTrigger>();
            lacquer = FindFirstObjectByType<LacquerStationTrigger>();
        }

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        task = CreateLabel("任务提示", 30, new Vector2(580, 58));
        // Keep the order objective in its own top-right row. The bamboo inventory
        // HUD occupies the top-left row, so sharing its position makes both labels
        // unreadable as soon as the player picks up the first bamboo.
        task.alignment = TextAnchor.MiddleRight;
        task.rectTransform.anchorMin = task.rectTransform.anchorMax = Vector2.one;
        task.rectTransform.pivot = Vector2.one;
        task.rectTransform.anchoredPosition = new Vector2(-32, -48);
        marker = CreateLabel("当前目标", 54, new Vector2(64, 76));
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
        marker.text = "!";
    }

    static Transform ConfigureDialogue(string name, WorkshopOrder.Phase phase, string line)
    {
        foreach (var npc in FindObjectsByType<NpcCharacter>(FindObjectsSortMode.None))
        {
            if (npc.name != name)
                continue;
            var interaction = npc.GetComponent<NpcDialogueInteraction>();
            if (interaction == null)
                interaction = npc.gameObject.AddComponent<NpcDialogueInteraction>();
            var dialogue = npc.GetComponent<TemporaryDialogueEntryPoint>();
            if (dialogue == null)
                dialogue = npc.gameObject.AddComponent<TemporaryDialogueEntryPoint>();
            dialogue.ConfigureStory(phase, line);
            interaction.Configure(dialogue, "按 E 交谈", interaction.InteractionRadius);
            interaction.ConfigureStoryPhase(phase);
            return npc.transform;
        }
        return null;
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

    void LateUpdate()
    {
        var order = WorkshopOrder.Current;
        if (order == null)
            return;
        if (displayedRevision != order.Revision)
        {
            displayedRevision = order.Revision;
            task.text = order.CurrentPhase switch
            {
                WorkshopOrder.Phase.Village => "去和村口大爷说话",
                WorkshopOrder.Phase.Home => "去家门口找舅爷",
                WorkshopOrder.Phase.NewOrder => "去工坊电脑查看订单",
                WorkshopOrder.Phase.ReadyToSubmit => "返回工坊电脑提交订单",
                WorkshopOrder.Phase.Complete => "本次订单已完成",
                _ => ProductionTarget(order)
            };
        }

        bool unobstructed = !GameFlowModalState.IsModalOpen && !SceneTransitionState.IsLoading;
        task.enabled = unobstructed;
        Transform target = order.CurrentPhase switch
        {
            WorkshopOrder.Phase.Village => elder,
            WorkshopOrder.Phase.Home => uncle,
            WorkshopOrder.Phase.NewOrder or WorkshopOrder.Phase.ReadyToSubmit => computer != null ? computer.transform : null,
            WorkshopOrder.Phase.Production when order.CanHarvest => forestTarget,
            WorkshopOrder.Phase.Production when order.CanWorkBamboo => bamboo != null ? bamboo.transform : null,
            WorkshopOrder.Phase.Production when order.GetStatus(WorkshopOrder.Stage.Weaving) != WorkshopOrder.StageStatus.Complete
                => weaving != null ? weaving.transform : null,
            WorkshopOrder.Phase.Production => lacquer != null ? lacquer.transform : null,
            _ => null
        };

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

    static string ProductionTarget(WorkshopOrder order)
    {
        if (order.CanHarvest) return "去竹林获得原材料";
        if (order.GetStatus(WorkshopOrder.Stage.Preparation) != WorkshopOrder.StageStatus.Complete)
            return "带青竹回篾竹区完成一劈与剐青";
        if (order.CanWorkBamboo) return "去篾竹区进行加工";
        return order.GetStatus(WorkshopOrder.Stage.Weaving) == WorkshopOrder.StageStatus.Complete
            ? "到漆帘处进行上漆" : "到编帘区开始编织";
    }
}
