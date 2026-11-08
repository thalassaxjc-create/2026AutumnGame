using UnityEngine;
[CreateAssetMenu(menuName = "Bamboo/Parkour Level")]
public sealed class ParkourLevelConfig : ScriptableObject
{
    [SerializeField] private float startZ = -3f;
    [SerializeField] private float finishZ = 172f;
    [SerializeField] private float endZ = 176f;
    [SerializeField] private float bpm = 120f;
    [SerializeField] private float[] throwSections = { 58f, 124f };
    [SerializeField] private float[] checkpoints = { -3f, 64f, 130f };
    [SerializeField] private AnimationCurve speed = new AnimationCurve(
        new Keyframe(-3,4.2f), new Keyframe(24,4.5f), new Keyframe(62.5f,4.8f),
        new Keyframe(88,5.4f), new Keyframe(128.5f,5.8f), new Keyframe(140,5.8f),
        new Keyframe(170,7.2f), new Keyframe(172,0f));
    [SerializeField] private ParkourObstacleEvent[] obstacles = new ParkourObstacleEvent[0];
    public float StartZ => startZ;
    public float FinishZ => finishZ;
    public float EndZ => endZ;
    public float BPM => bpm;
    public float[] ThrowSections => throwSections;
    public float[] Checkpoints => checkpoints;
    public ParkourObstacleEvent[] Obstacles => obstacles;
    public float SpeedAt(float z) => Mathf.Max(z >= 170f ? .8f : 0f, speed.Evaluate(z));
    public void SetObstacles(ParkourObstacleEvent[] events) { obstacles = events; }
}
