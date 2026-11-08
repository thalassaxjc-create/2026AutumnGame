using UnityEngine;
[DefaultExecutionOrder(-100)]
public sealed class WarpShedController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private BeatClock clock;
    [SerializeField] private MovingSurface[] surfaces = new MovingSurface[5];
    [SerializeField] private float transition = .38f;
    [SerializeField] private float warning = .45f;
    private readonly Vector3[] rest = new Vector3[5];
    private readonly float[] from = new float[5];
    private readonly float[] to = new float[5];
    private int lastState = -2;
    private float changedAt;
    private float zoneStarted;
    private bool wasActive;
    private bool wasWarning;
    public bool Active { get; private set; }
    public bool WarningActive { get; private set; }
    public event System.Action ShedWarning;
    public void Configure(PlayerController controller, BeatClock beat, MovingSurface[] pillars)
    { player = controller; clock = beat; surfaces = pillars; }
    private void Awake() { for(int i=0;i<5;i++)rest[i]=surfaces[i].transform.position; }
    private void FixedUpdate()
    {
        float z = player.Position.z;
        bool running = player.GameplayMode == PlayerGameplayMode.AutoRun;
        float entry = z < 100f ? 66f : 150f;
        float exit = z < 100f ? 78f : 156f;
        Active = running && z >= entry && z <= exit;
        float period = entry == 150f ? 1f : 1.2f;
        if (Active && !wasActive) zoneStarted = clock.Elapsed;
        float age = Mathf.Max(0f, clock.Elapsed - zoneStarted);
        bool entryWarning = running && z < entry && (entry-z) / Mathf.Max(.1f,player.AutoRunSpeed) <= warning;
        WarningActive = entryWarning || (Active && period-age%period <= warning);
        if (WarningActive && !wasWarning) ShedWarning?.Invoke();
        wasWarning = WarningActive;
        wasActive = Active;
        int state = Active ? Mathf.FloorToInt(age / period)%2 : -1;
        if(state!=lastState)
        {
            for(int i=0;i<5;i++)
            {
                from[i]=surfaces[i].transform.position.y-rest[i].y;
                to[i]=state<0?0f:((i%2==state)?.75f:-.55f);
            }
            lastState=state;changedAt=Time.time;
        }
        float t=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((Time.time-changedAt)/transition));
        for(int i=0;i<5;i++)
        {
            Vector3 target=rest[i]+Vector3.up*Mathf.Lerp(from[i],to[i],t);
            surfaces[i].MoveTo(target);
        }
    }
}
