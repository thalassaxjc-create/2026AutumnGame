using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 本局背包里的完整青竹数量。
/// </summary>
public static class GreenBambooBag
{
    public const string ItemName = "完整青竹";

    static Text label;

    public static int Count { get; private set; }
    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        Count = 0;
        label = null;
        Changed = null;
    }

    public static void AddOne()
    {
        Count++;
        Debug.Log("获得1根完整青竹，当前 " + Count);
        EnsureLabel();
        if (label != null)
            label.text = ItemName + " ×" + Count;
        WorkshopInventory.NotifyGreenBambooChanged();
        Changed?.Invoke();
    }

    public static bool TryTakeOne()
    {
        if (Count <= 0)
            return false;
        Count--;
        if (label != null)
            label.text = ItemName + " ×" + Count;
        WorkshopInventory.NotifyGreenBambooChanged();
        Changed?.Invoke();
        return true;
    }

    public static void EnsureLabel()
    {
        // The story flow owns a unified backpack HUD. Standalone harvesting
        // previews keep the original compact bamboo counter.
        if (label != null || WorkshopOrder.Current != null)
            return;

        var canvasGo = new GameObject("GreenBambooBagHud");
        Object.DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGo = new GameObject("Count");
        textGo.transform.SetParent(canvasGo.transform, false);
        label = textGo.AddComponent<Text>();
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(36f, -28f);
        rect.sizeDelta = new Vector2(520f, 52f);
        label.alignment = TextAnchor.MiddleLeft;
        label.fontSize = 32;
        label.color = new Color(0.8f, 0.95f, 0.58f);
        label.raycastTarget = false;
        label.text = ItemName + " ×" + Count;
        InteractionPromptUI.EnsureChineseFont(label);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }
}

/// <summary>
/// 竹林砍伐。只标记六根，靠近时认最近的一根。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)] // Choose today's stalks before ChopableBamboo.Awake creates their marks.
public class BambooHarvest : MonoBehaviour
{
    public const int HarvestCount = 6;

    // 北门进竹林的小路终点附近，第一根可砍竹靠这条路。
    public static readonly Vector3 PathAnchor = new Vector3(-78f, 0f, 36f);

    ChopableBamboo locked;
    ChopableBamboo target;
    ChopableBamboo[] stalks = System.Array.Empty<ChopableBamboo>();
    float obtainUntil;
    PlayerController player;

    public ChopableBamboo CurrentTarget => target;

    public static int EnsureHarvestStalks(Transform forest, int count) => EnsureHarvestStalks(forest, count, 1);

    static int EnsureHarvestStalks(Transform forest, int count, int day)
    {
        if (forest == null || count <= 0)
            return 0;

        count = Mathf.Min(count, HarvestCount);

        var chosen = new List<Transform>(count);
        var candidates = new List<Transform>();
        var existingStalks = new List<ChopableBamboo>();
        for (int i = 0; i < forest.childCount; i++)
        {
            Transform child = forest.GetChild(i);
            if (child == null || !child.name.StartsWith("Bamboo_"))
                continue;

            ChopableBamboo existing = child.GetComponent<ChopableBamboo>();
            if (existing != null)
                existingStalks.Add(existing);
            if (existing != null && chosen.Count < count)
                chosen.Add(child);
            else
                candidates.Add(child);
        }

        FillHarvestGroup(candidates, chosen, count);
        // Keep the authored group on day one. Each later group uses different bamboos.
        int additionalGroups = candidates.Count / count;
        int groupForDay = (day - 1) % (1 + additionalGroups);
        for (int group = 0; group < groupForDay; group++)
        {
            chosen.Clear();
            FillHarvestGroup(candidates, chosen, count);
        }

        foreach (ChopableBamboo bamboo in existingStalks)
            bamboo.enabled = chosen.Contains(bamboo.transform);
        foreach (Transform pick in chosen)
        {
            if (!pick.TryGetComponent<ChopableBamboo>(out _))
                pick.gameObject.AddComponent<ChopableBamboo>();
        }

        var harvest = forest.GetComponent<BambooHarvest>();
        if (harvest == null)
            harvest = forest.gameObject.AddComponent<BambooHarvest>();
        harvest.Refresh();

        return chosen.Count;
    }

    static void FillHarvestGroup(List<Transform> candidates, List<Transform> chosen, int count)
    {
        while (chosen.Count < count && candidates.Count > 0)
        {
            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                float score = ScoreCandidate(candidates[i].position, chosen);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestIndex = i;
            }

            Transform pick = candidates[bestIndex];
            candidates.RemoveAt(bestIndex);
            chosen.Add(pick);
        }
    }

    void Awake() => EnsureHarvestStalks(transform, HarvestCount, WorkshopOrder.Current?.Days ?? 1);

    void OnDisable()
    {
        locked = null;
        SetTarget(null);
        InteractionPromptUI.HideImmediate();
    }

    string shownPrompt;

    void ClearPrompt()
    {
        if (shownPrompt != null && InteractionPromptUI.CurrentMessage == shownPrompt)
            InteractionPromptUI.Hide();
        shownPrompt = null;
    }

    void Update()
    {
        var order = WorkshopOrder.Current;
        if (order != null && !order.CanHarvest)
        {
            locked = null;
            SetTarget(null);
            ClearPrompt();
            return;
        }
        if (SceneTransitionState.IsLoading || Time.timeScale <= 0f || GameFlowModalState.IsModalOpen ||
            VillageSpottedGrove.IsSneaking)
        {
            locked = null;
            SetTarget(null);
            ClearPrompt();
            return;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            locked = null;
            SetTarget(null);
            ClearPrompt();
            return;
        }

        Vector3 pos = player.transform.position;
        if (locked != null && (!locked.CanChop || !locked.IsInRange(pos)))
            locked = null;

        SetTarget(locked != null ? locked : FindNearest(pos));
        bool space = SpacePressed();

        if (space && target != null && target.CanPickup)
        {
            if (target.TryPickup(pos))
            {
                obtainUntil = Time.time + 1.35f;
                order?.PickUpBamboo();
            }
        }
        else if (space && target != null && target.CanChop)
        {
            if (target.TryChop(pos))
                locked = target.CanChop ? target : null;
        }

        // Refresh after the action to avoid displaying a felled/collected target's old prompt.
        SetTarget(locked != null ? locked : FindNearest(pos));
        string prompt = Time.time < obtainUntil ? "获得1根完整青竹" : target?.PromptText;
        if (prompt != null)
        {
            shownPrompt = prompt;
            InteractionPromptUI.Show(prompt);
        }
        else
            ClearPrompt();
    }

    void SetTarget(ChopableBamboo next)
    {
        if (target == next)
            return;
        if (target != null)
            target.SetHighlighted(false);
        target = next;
        if (target != null)
            target.SetHighlighted(true);
    }

    void Refresh()
    {
        stalks = GetComponentsInChildren<ChopableBamboo>(true);
    }

    ChopableBamboo FindNearest(Vector3 pos)
    {
        if (stalks == null || stalks.Length == 0)
            Refresh();

        ChopableBamboo best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < stalks.Length; i++)
        {
            ChopableBamboo bamboo = stalks[i];
            if (bamboo == null || !bamboo.IsInteractable)
                continue;

            float distance = FlatDistance(pos, bamboo.InteractPoint);
            if (distance > bamboo.InteractRadius)
                continue;
            if (best != null && (best.CanPickup != bamboo.CanPickup ? best.CanPickup : distance >= bestDistance))
                continue;

            best = bamboo;
            bestDistance = distance;
        }

        return best;
    }

    static float ScoreCandidate(Vector3 position, List<Transform> chosen)
    {
        if (chosen.Count == 0)
            return -FlatDistance(position, PathAnchor);

        float min = float.PositiveInfinity;
        for (int i = 0; i < chosen.Count; i++)
        {
            if (chosen[i] == null)
                continue;
            float distance = FlatDistance(position, chosen[i].position);
            if (distance < min)
                min = distance;
        }

        return min;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool SpacePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }
}
