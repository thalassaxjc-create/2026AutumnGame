/// <summary>
/// 场景切换的静态状态。只保存目标出生点与输入锁，不持有场景或玩家引用。
/// </summary>
public static class SceneTransitionState
{
    public const string WorldSceneName = "WorldScene";
    public const string WorkshopSceneName = "WorkshopScene";
    public const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    public const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    public const string SpawnWorkshopEntrance = "WorkshopEntrance";
    public const string SpawnWorkshopExterior = "WorkshopExterior";

    public static string PendingSpawnId { get; set; }
    public static bool IsLoading { get; set; }
    public static bool WaitForInteractRelease { get; set; }
    public static bool MustLeaveTrigger { get; set; }

    public static bool IsLocked => IsLoading || WaitForInteractRelease || MustLeaveTrigger;

    public static void BeginLoad(string spawnId)
    {
        IsLoading = true;
        PendingSpawnId = spawnId;
        WaitForInteractRelease = true;
        MustLeaveTrigger = false;
    }

    public static void FinishLoad()
    {
        IsLoading = false;
    }

    public static void FailLoad()
    {
        IsLoading = false;
        PendingSpawnId = null;
        WaitForInteractRelease = false;
        MustLeaveTrigger = false;
    }

    public static string ConsumePendingSpawnId()
    {
        string id = PendingSpawnId;
        PendingSpawnId = null;
        return id;
    }
}
