using UnityEngine;
[System.Serializable]
public class ParkourCheckpoint
{
    public Vector3 position = new Vector3(0f,.6f,-3f);
    public int lane = 2;
    public int stage;
    public float speed = 4.2f;
    public PullBlockMechanism.PullState[] throws = new PullBlockMechanism.PullState[2];
    public PlayerGameplayMode mode = PlayerGameplayMode.AutoRun;
}
