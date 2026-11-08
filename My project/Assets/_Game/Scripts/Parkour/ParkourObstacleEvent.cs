using UnityEngine;
public enum ObstacleType { StaticRing, LowThread, HighThread, MovingGap, DiagonalThread, Pendulum, WarpShed, ReedBeater, ButtonThreads, Knot, FallingBamboo }
[System.Serializable]
public class ParkourObstacleEvent
{
    public float z;
    public float interactionZ;
    public float endZ;
    public ObstacleType type;
    public int laneMask;
    public float activationDistance = 10f;
    public float duration = 1f;
    public float phaseOffset;
    public float warningTime = .8f;
    public int[] gapSequence = new int[0];
}
public abstract class ParkourObstacle : MonoBehaviour
{
    [SerializeField] protected PlayerController player;
    [SerializeField] protected BeatClock clock;
    [SerializeField] protected ParkourObstacleEvent obstacle;
    protected bool passed;
    protected bool hit;
    public ParkourObstacleEvent Data => obstacle;
    public event System.Action<ParkourObstacle> Collision;
    public event System.Action<ParkourObstacle, int> Cleared;
    public void Configure(PlayerController controller, BeatClock beat, ParkourObstacleEvent data)
    { player = controller; clock = beat; obstacle = data; }
    public virtual void ResetAttempt(float checkpointZ)
    { passed = obstacle.z < checkpointZ; hit = false; }
    protected bool Running => player != null && player.GameplayMode == PlayerGameplayMode.AutoRun;
    protected void Hit()
    { if (!Running || hit) return; hit = true; Collision?.Invoke(this); }
    protected void Pass(int score)
    { if (passed || hit || !Running || player.Position.z < obstacle.z + .65f) return; passed = true; Cleared?.Invoke(this, score); }
    protected virtual void OnTriggerEnter(Collider other)
    { if (other.attachedRigidbody != null && other.attachedRigidbody.gameObject == player.gameObject) Hit(); }
    protected virtual void OnTriggerStay(Collider other) { OnTriggerEnter(other); }
}
