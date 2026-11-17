using UnityEngine;

[CreateAssetMenu(menuName = "宣纸帘/订单原型配置")]
public sealed class WorkshopOrderSettings : ScriptableObject
{
    [Min(0)] public int initialFunds = 102;
    [Min(1)] public int initialDay = 1;
    [Range(0, 23)] public int initialHour = 13;
    [Range(0, 59)] public int initialMinute = 5;
    [Min(0)] public int preparationMinutes = 30;
    [Min(0)] public int bambooFinishingMinutes = 30;
    [Min(0)] public int baseReward = 1000;
    [Range(0, 1)] public float rewardS = 1f;
    [Range(0, 1)] public float rewardA = .9f;
    [Range(0, 1)] public float rewardB = .8f;
    [Range(0, 1)] public float rewardC = .65f;
    [Range(0, 1)] public float rewardD = .4f;
}
