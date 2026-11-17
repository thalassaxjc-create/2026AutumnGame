using UnityEngine;

[CreateAssetMenu(menuName = "宣纸帘/订单原型配置")]
public sealed class WorkshopOrderSettings : ScriptableObject
{
    [Min(0)] public int initialFunds;
    [Min(0)] public int initialXiaoPiFunds;
    [Min(0)] public int initialXiaoBaiFunds;
    [Range(6, 23)] public int initialHour = 6;
    [Range(0, 59)] public int initialMinute;
    [Min(.1f)] public float realSecondsPerGameMinute = 2f;
    [Min(0)] public int harvestMinutes = 10;
    [Min(0)] public int preparationMinutes = 35;
    [Min(0)] public int bambooFinishingMinutes = 40;
    [Min(1)] public int dryingMinutes = 120;
    [Min(0)] public int baseReward = 100;
    [Range(0, 2)] public float rewardS = 1.30f;
    [Range(0, 2)] public float rewardA = 1.15f;
    [Range(0, 2)] public float rewardB = 1f;
    [Range(0, 2)] public float rewardC = .90f;
    [Range(0, 2)] public float rewardD = .80f;
    [Range(0, 1)] public float defectiveMaterialMultiplier = .80f;
    [Min(0)] public int xiaoPiDayOneIncome = 95;
    [Min(0)] public int xiaoBaiDayOneIncome = 75;
}
