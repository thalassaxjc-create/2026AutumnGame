using UnityEngine;
public sealed class WovenPatternRecorder : MonoBehaviour
{
    [System.Serializable]
    public struct Stitch { public float z;public int lane;public bool jumping,sliding,combo; }
    [SerializeField] private PlayerController player;
    [SerializeField] private BeatClock clock;
    [SerializeField] private ScoreComboController score;
    private readonly System.Collections.Generic.List<Stitch> stitches = new System.Collections.Generic.List<Stitch>(160);
    private readonly int[] throwResults = new int[2];
    public System.Collections.Generic.IReadOnlyList<Stitch> Stitches => stitches;
    public int[] ThrowResults => throwResults;
    public void Configure(PlayerController controller,BeatClock beat,ScoreComboController scoring){player=controller;clock=beat;score=scoring;}
    private void OnEnable(){if(clock!=null)clock.Beat+=Record;}
    private void OnDisable(){if(clock!=null)clock.Beat-=Record;}
    private void Record(int index)
    {
        if(player.GameplayMode!=PlayerGameplayMode.AutoRun)return;
        stitches.Add(new Stitch{z=player.Position.z,lane=player.Lane,jumping=!player.IsGrounded,sliding=player.IsSliding,combo=score.Combo>0});
    }
    public void RecordThrow(int index,bool success){if(index>=0&&index<2)throwResults[index]=success?1:-1;}
    public void TrimAfter(float z){for(int i=stitches.Count-1;i>=0;i--)if(stitches[i].z>z)stitches.RemoveAt(i);}
}
