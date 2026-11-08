using UnityEngine;
public sealed class ParkourHUD : MonoBehaviour
{
    [SerializeField] private ParkourDirector director;
    [SerializeField] private TMPro.TextMeshProUGUI metrics;
    [SerializeField] private TMPro.TextMeshProUGUI result;
    [SerializeField] private GameObject resultRoot;
    [SerializeField] private UnityEngine.UI.Image[] cells = new UnityEngine.UI.Image[0];
    [SerializeField] private UnityEngine.UI.Image hitFlash;
    private bool rendered;
    private UnityEngine.UI.Image metricsBackground;
    private float nextUpdate;
    private void Awake() { metricsBackground = GetComponent<UnityEngine.UI.Image>(); }
    private readonly System.Text.StringBuilder builder = new System.Text.StringBuilder(256);
    public void Configure(ParkourDirector gameplay,TMPro.TextMeshProUGUI stats,TMPro.TextMeshProUGUI title,GameObject board,UnityEngine.UI.Image[] stitches,UnityEngine.UI.Image flash)
    {director=gameplay;metrics=stats;result=title;resultRoot=board;cells=stitches;hitFlash=flash;}
    private void Start() {resultRoot.SetActive(false);}
    private void Update()
    {
        bool showMetrics = !director.Finished && director.Player.GameplayMode != PlayerGameplayMode.ThrowPose;
        metrics.enabled = showMetrics;
        if (metricsBackground != null) metricsBackground.enabled = showMetrics;
        hitFlash.color = new Color(1f,.15f,.05f,director.HitFlash*.18f);
        if(Time.unscaledTime>=nextUpdate)
        {
            nextUpdate=Time.unscaledTime+.1f;
            builder.Clear();builder.Append("SCORE ").Append(director.Scoring.Score).Append("    COMBO ").Append(director.Scoring.Combo).Append("  x").Append(director.Scoring.Multiplier);
            builder.Append("\nSPEED ").Append(director.Player.AutoRunSpeed.ToString("F1")).Append(" m/s    RUN ").Append(director.RunTime.ToString("F1")).Append(" s");
            metrics.SetText(builder.ToString());
        }
        if(!director.Finished||rendered)return;
        rendered=true;resultRoot.SetActive(true);
        builder.Clear();builder.Append("WOVEN BAMBOO  ").Append(director.Level.CompletionPercent).Append("%\nSCORE ").Append(director.Scoring.Score).Append("    RUN ").Append(director.RunTime.ToString("F1")).Append(" s\nLANDED ").Append(director.Level.TransferCount).Append(" / 2    LOST ").Append(director.Level.FailureCount);
        result.SetText(builder.ToString());
        var stitches=director.Pattern.Stitches;int rows=cells.Length/5;
        int index=0;
        for(int row=0;row<rows;row++)
        {
            float z=Mathf.Lerp(-3f,172f,row/(float)Mathf.Max(1,rows-1));
            while(index+1<stitches.Count && stitches[index+1].z<=z)index++;
            for(int lane=0;lane<5;lane++)
            {
                var cell=cells[row*5+lane];
                cell.color=new Color(.5f,.38f,.19f);
                int throwIndex=z>53f&&z<65f?0:z>119f&&z<131f?1:-1;
                int outcome=throwIndex<0?0:director.Pattern.ThrowResults[throwIndex];
                if(outcome<0){cell.color=lane==2?new Color(.14f,.10f,.06f):new Color(.8f,.25f,.13f);continue;}
                if(outcome>0){cell.color=row%2==lane%2?new Color(.25f,.9f,.75f):new Color(1f,.83f,.35f);continue;}
                if(stitches.Count==0)continue;
                var s=stitches[index];
                if(lane==s.lane)cell.color=s.jumping?Color.cyan:s.sliding?new Color(.8f,.4f,1f):s.combo?new Color(1f,.83f,.35f):Color.white;
                else if(row%2==lane%2)cell.color=new Color(.78f,.67f,.41f);
            }
        }
    }
}
