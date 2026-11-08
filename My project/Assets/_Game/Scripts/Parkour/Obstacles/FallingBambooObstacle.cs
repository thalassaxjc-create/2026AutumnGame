using UnityEngine;
public sealed class FallingBambooObstacle : ParkourObstacle
{
    public enum FallState { Ready, Warning, Falling, Gone }
    [SerializeField] private MovingSurface[] segments=new MovingSurface[2];
    [SerializeField] private Renderer[] cracks=new Renderer[0];
    [SerializeField] private float warningDuration=.45f;
    [SerializeField] private float arrivalWarning=.8f;
    [SerializeField] private float acceleration=18f;
    [SerializeField] private float hideAfterDrop=9f;
    private MovingSurface[] parentSurfaces;
    private Vector3[] restOffsets;
    private float elapsed;
    public FallState State { get; private set; }
    public float StartZ=>obstacle.z;
    public float EndZ=>obstacle.endZ;
    public event System.Action<FallingBambooObstacle> Cracking;
    public event System.Action<FallingBambooObstacle> Dropping;
    public void SetGeometry(MovingSurface[] falling,Renderer[] warningCracks)
    {segments=falling;cracks=warningCracks;}
    private void Awake()
    {
        parentSurfaces=new MovingSurface[segments.Length];restOffsets=new Vector3[segments.Length];
        for(int i=0;i<segments.Length;i++)
        {
            parentSurfaces[i]=segments[i].transform.parent.GetComponent<MovingSurface>();
            restOffsets[i]=segments[i].transform.position-parentSurfaces[i].transform.position;
        }
        State=FallState.Ready;
        foreach(var crack in cracks)crack.enabled=false;
    }
    public override void ResetAttempt(float checkpointZ)
    {
        base.ResetAttempt(checkpointZ);elapsed=0f;State=FallState.Ready;
        for(int i=0;i<segments.Length;i++)
        {
            segments[i].gameObject.SetActive(true);
            segments[i].ResetTo(parentSurfaces[i].TargetPosition+restOffsets[i]);
        }
        foreach(var crack in cracks)crack.enabled=false;
    }
    private void Update()
    {
        if(player==null||obstacle==null)return;
        if(Running&&State==FallState.Ready&&player.Position.z<=EndZ)
        {
            float distance=StartZ-player.Position.z;
            if(distance<=Mathf.Max(4.4f,player.AutoRunSpeed*arrivalWarning))
            {State=FallState.Warning;elapsed=0f;foreach(var crack in cracks)crack.enabled=true;Cracking?.Invoke(this);}
        }
        if(player.Position.z>EndZ+.65f)Pass(100);
    }
    private void FixedUpdate()
    {
        if(parentSurfaces==null)return;
        if(Running&&(State==FallState.Warning||State==FallState.Falling))elapsed+=Time.fixedDeltaTime;
        if(State==FallState.Warning&&elapsed>=warningDuration)
        {State=FallState.Falling;elapsed=0f;Dropping?.Invoke(this);}
        float drop=State==FallState.Falling?.5f*acceleration*elapsed*elapsed:0f;
        if(State==FallState.Falling&&drop>=hideAfterDrop)
        {
            State=FallState.Gone;
            foreach(var segment in segments)segment.gameObject.SetActive(false);
            foreach(var crack in cracks)crack.enabled=false;
            return;
        }
        if(State==FallState.Gone)return;
        for(int i=0;i<segments.Length;i++)
        {
            Vector3 target=parentSurfaces[i].TargetPosition+restOffsets[i]+Vector3.down*drop;
            if(State==FallState.Warning)target+=Vector3.up*(Mathf.Sin(elapsed*65f)*.025f);
            segments[i].MoveTo(target);
        }
    }
}
