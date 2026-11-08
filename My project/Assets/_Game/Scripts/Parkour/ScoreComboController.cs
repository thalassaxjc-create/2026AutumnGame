using UnityEngine;
public sealed class ScoreComboController : MonoBehaviour
{
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Multiplier => Combo>=20?4:Combo>=10?3:Combo>=5?2:1;
    public int Deaths { get; private set; }
    private readonly System.Collections.Generic.HashSet<string> awarded = new System.Collections.Generic.HashSet<string>();
    public event System.Action Changed;
    public void Award(int points, string id = null)
    {
        if(id!=null&&!awarded.Add(id))return;
        Score+=points*Multiplier;Combo++;Changed?.Invoke();
    }
    public void BreakCombo(bool death) {Combo=0;if(death)Deaths++;Changed?.Invoke();}
}
