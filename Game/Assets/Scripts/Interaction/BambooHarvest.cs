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
        Changed?.Invoke();
    }

    public static bool TryTakeOne()
    {
        if (Count <= 0)
            return false;
        Count--;
        if (label != null)
            label.text = ItemName + " ×" + Count;
        Changed?.Invoke();
        return true;
    }

    public static void EnsureLabel()
    {
        if (label != null)
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

    public static int EnsureHarvestStalks(Transform forest, int count)
    {
        if (forest == null || count <= 0)
            return 0;

        count = Mathf.Min(count, HarvestCount);

        var chosen = new List<Transform>();
        var candidates = new List<Transform>();
        for (int i = 0; i < forest.childCount; i++)
        {
            Transform child = forest.GetChild(i);
            if (child == null || !child.name.StartsWith("Bamboo_"))
                continue;

            ChopableBamboo existing = child.GetComponent<ChopableBamboo>();
            if (existing != null)
            {
                existing.enabled = chosen.Count < count;
                if (existing.enabled)
                    chosen.Add(child);
            }
            else
                candidates.Add(child);
        }

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
            if (pick.GetComponent<ChopableBamboo>() == null)
                pick.gameObject.AddComponent<ChopableBamboo>();
            chosen.Add(pick);
        }

        if (forest.GetComponent<BambooHarvest>() == null)
            forest.gameObject.AddComponent<BambooHarvest>();

        forest.GetComponent<BambooHarvest>().Refresh();

        return chosen.Count;
    }

    void Awake()
    {
        EnsureHarvestStalks(transform, HarvestCount);
        Refresh();
    }

    void OnDisable()
    {
        locked = null;
        SetTarget(null);
        InteractionPromptUI.HideImmediate();
    }

    void Update()
    {
        var order = WorkshopOrder.Current;
        if (order != null && !order.CanHarvest)
        {
            locked = null;
            if (target != null)
            {
                SetTarget(null);
                InteractionPromptUI.Hide();
            }
            return;
        }
        if (SceneTransitionState.IsLoading || Time.timeScale <= 0f || GameFlowModalState.IsModalOpen)
        {
            locked = null;
            SetTarget(null);
            InteractionPromptUI.Hide();
            return;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            locked = null;
            SetTarget(null);
            InteractionPromptUI.Hide();
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
        if (Time.time < obtainUntil)
            InteractionPromptUI.Show("获得1根完整青竹");
        else if (target != null)
            InteractionPromptUI.Show(target.PromptText);
        else
            InteractionPromptUI.Hide();
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
            if (distance > bamboo.InteractRadius || distance >= bestDistance)
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
