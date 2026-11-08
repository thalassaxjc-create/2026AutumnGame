using UnityEngine;
[DefaultExecutionOrder(50)]
public sealed class ParkourDirector : MonoBehaviour
{
    [SerializeField] private FivePillarLevel level;
    [SerializeField] private ParkourLevelConfig config;
    [SerializeField] private BeatClock clock;
    [SerializeField] private ScoreComboController score;
    [SerializeField] private WovenPatternRecorder pattern;
    [SerializeField] private ParkourObstacle[] obstacles = new ParkourObstacle[0];
    [SerializeField] private GameObject[] routeCollectibles = new GameObject[0];
    [SerializeField] private Transform[] wovenRegions = new Transform[0];
    [SerializeField] private float stunDuration = .25f;
    [SerializeField] private float restartDuration = .45f;
    private PlayerController player;
    private PullBlockMechanism puzzle;
    private ParkourCheckpoint checkpoint = new ParkourCheckpoint();
    private float respawnAt, runAt, modifierUntil;
    private float speedModifier = 1f;
    private bool retryPuzzle, released, landed, recovering;
    private int progressStage;
    private readonly float[] regionEnds = {24f,48f,88f,114f,140f,170f};
    private int region, regionDeaths;
    public float RunTime { get; private set; }
    public bool Finished { get; private set; }
    public PlayerController Player => player;
    public ScoreComboController Scoring => score;
    public WovenPatternRecorder Pattern => pattern;
    public FivePillarLevel Level => level;
    public float HitFlash { get; private set; }
    public ParkourObstacle[] Obstacles => obstacles;
    public PullBlockMechanism ActivePuzzle => puzzle;
    public event System.Action Respawned;
    public event System.Action FinishedLevel;
    public event System.Action<PullBlockMechanism> TraversalReturned;
    public string Instruction
    {
        get
        {
            if(Finished)return "FINISHED - YOUR WOVEN BAMBOO PATTERN";
            if(recovering)return "RETRY";
            if(player.GameplayMode==PlayerGameplayMode.ThrowPose)
            {
                return level.ActiveTimingMechanism!=null?"HOLDING PLANK - PRESS SPACE IN GREEN":"THROW RESOLVING - WAIT FOR THE PLANK";
            }
            if(player.GameplayMode==PlayerGameplayMode.AutoRun)
            {
                if(player.AllowParkourInteraction)return "RIGHT LANE: E TO THROW - SPACE IN GREEN";
                if(player.Position.z<8f)return "A / D OR ARROWS: CHANGE LANE   SPACE: JUMP   S: SLIDE";
                ParkourObstacle next=null;float nearest=8f;
                foreach(var hazard in obstacles)
                {
                    float distance=hazard.Data.z-player.Position.z;
                    if(hazard is ButtonThreadObstacle buttons && player.Position.z<buttons.Data.z)
                        distance=Mathf.Max(0f,buttons.ButtonZ-player.Position.z);
                    if(hazard is FallingBambooObstacle falling && player.Position.z<falling.EndZ)
                        distance=Mathf.Max(0f,falling.StartZ-player.Position.z);
                    if(distance>=0f&&distance<nearest){nearest=distance;next=hazard;}
                }
                if(next!=null)
                {
                    if(next.Data.type==ObstacleType.LowThread)return "LOW ROPE - ALL LANES: PRESS SPACE TO JUMP, NOT CHANGE LANE";
                    if(next.Data.type==ObstacleType.HighThread)return "HIGH ROPE: PRESS S / DOWN TO SLIDE";
                    if(next is ButtonThreadObstacle buttons)
                    {
                        if(buttons.WhiteOpen&&buttons.YellowOpen)return "BOTH THREADS OPEN - CONTINUE";
                        if(buttons.WhiteOpen)return "WHITE THREAD OPEN - MOVE TO THE RIGHT TWO LANES";
                        if(buttons.YellowOpen)return "YELLOW THREAD OPEN - MOVE TO THE LEFT THREE LANES";
                        return "STEP WHITE BUTTON, GO RIGHT / STEP YELLOW BUTTON, GO LEFT";
                    }
                    if(next is FallingBambooObstacle)return "RIGHT TWO BAMBOOS BREAK - USE LEFT THREE; JUMP LOW ROPES";
                    if(next.Data.type==ObstacleType.Knot)return "AVOID THE THREAD KNOTS - TAKE AN EMPTY LANE";
                    if(next.Data.type==ObstacleType.MovingGap||next.Data.type==ObstacleType.StaticRing)return "CYAN / BLUE GAP = SAFE    RED ROPE = BLOCKED";
                }
                return "";
            }
            if(puzzle!=null&&!player.IsSwinging&&!player.IsClimbing)
                return released?"LAND ON THE FORWARD PLANK, THEN E + W TO CLIMB":"RETURN TO THE CYAN LEFT ROPE BEHIND YOU: E, THEN S TO DESCEND";
            return level.Interaction!=null?level.Interaction.CurrentPrompt:"";
        }
    }
    public void Configure(FivePillarLevel puzzleLevel,ParkourLevelConfig data,BeatClock beat,ScoreComboController scoring,WovenPatternRecorder recorder,ParkourObstacle[] hazards,GameObject[] pickups,Transform[] woven)
    {level=puzzleLevel;config=data;clock=beat;score=scoring;pattern=recorder;obstacles=hazards;routeCollectibles=pickups;wovenRegions=woven;}
    private void Awake(){player=level.Player;level.ExternalRespawn=true;}
    private void OnEnable()
    {
        foreach(var mechanism in level.Mechanisms)
        {mechanism.ThrowStarted+=OnThrowStarted;mechanism.ThrowResolved+=OnThrowResolved;mechanism.Descent.SwingReleased+=OnSwingReleased;}
        player.ClimbFinished+=OnClimbFinished;
        foreach(var hazard in obstacles){hazard.Collision+=OnHit;hazard.Cleared+=OnCleared;}
    }
    private void OnDisable()
    {
        if(level!=null) {level.ExternalRespawn=false;foreach(var m in level.Mechanisms){m.ThrowStarted-=OnThrowStarted;m.ThrowResolved-=OnThrowResolved;m.Descent.SwingReleased-=OnSwingReleased;}}
        if(player!=null)player.ClimbFinished-=OnClimbFinished;
        foreach(var hazard in obstacles)if(hazard!=null){hazard.Collision-=OnHit;hazard.Cleared-=OnCleared;}
    }
    private void Start(){player.SetGameplayMode(PlayerGameplayMode.AutoRun);clock.SetPaused(false);SaveCheckpoint(new Vector3(0,.6f,config.StartZ),PlayerGameplayMode.AutoRun);}
    private void OnThrowStarted(PullBlockMechanism mechanism){puzzle=mechanism;clock.SetPaused(true);}
    private void OnThrowResolved(PullBlockMechanism mechanism,bool success)
    {
        int index=System.Array.IndexOf(level.Mechanisms,mechanism);pattern.RecordThrow(index,success);
        if(index<wovenRegions.Length&&wovenRegions[index]!=null)wovenRegions[index].gameObject.SetActive(success);
        if(success)
        {
            puzzle=mechanism;released=false;landed=false;score.Award(1000,"throw"+index);
            player.SetGameplayMode(PlayerGameplayMode.FreePuzzle);clock.SetPaused(true);
            SaveCheckpoint(mechanism.Descent.Top,PlayerGameplayMode.FreePuzzle);
            for(int i=index*3;i<Mathf.Min(index*3+3,routeCollectibles.Length);i++)routeCollectibles[i].SetActive(true);
        }
        else
        {
            puzzle=null;score.BreakCombo(false);speedModifier=.85f;modifierUntil=Time.time+4f;
            player.SetGameplayMode(PlayerGameplayMode.AutoRun);clock.SetPaused(false);
            SaveCheckpoint(new Vector3((player.Lane-2)*3.6f,.6f,mechanism.SectionZ-1.95f),PlayerGameplayMode.AutoRun);
        }
    }
    private void OnSwingReleased(ClimbableRope rope){released=true;}
    private void OnClimbFinished(bool atTop)
    {
        if(!atTop||puzzle==null||Vector3.Distance(player.Position,puzzle.Ascent.Top)>.5f)return;
        int index=System.Array.IndexOf(level.Mechanisms,puzzle);
        PullBlockMechanism completed=puzzle;
        score.Award(800,"swing"+index);landed=true;
        progressStage=index+1;puzzle=null;released=false;
        speedModifier=1.1f;modifierUntil=Time.time+4f;
        player.SetGameplayMode(PlayerGameplayMode.AutoRun);clock.SetPaused(false);
        SaveCheckpoint(player.Position+Vector3.right*.5f,PlayerGameplayMode.AutoRun);
        TraversalReturned?.Invoke(completed);
    }
    private void OnHit(ParkourObstacle obstacle){Fail(false);}
    private void OnCleared(ParkourObstacle obstacle,int points)
    {
        score.Award(points,"obstacle"+obstacle.GetInstanceID());
        if(Mathf.Abs(player.Position.x-(player.Lane-2)*3.6f)<.35f && Time.time-player.LastLaneChangeAt<.8f)
            score.Award(50,"perfect"+obstacle.GetInstanceID());
        HangingWeightPendulum pendulum=obstacle as HangingWeightPendulum;
        if(pendulum!=null && (pendulum.NearMiss || pendulum.Ridden))
            score.Award(150,"pendulum"+obstacle.GetInstanceID());
    }
    public void Fail(bool swingFailure)
    {
        if(recovering||Finished||level.IsSwinging)return;
        recovering=true;retryPuzzle=swingFailure&&puzzle!=null;
        score.BreakCombo(true);clock.SetPaused(true);HitFlash=1f;
        player.MovementLocked=true;player.SetGameplayMode(PlayerGameplayMode.Stunned);
        respawnAt=Time.time+stunDuration;runAt=respawnAt+restartDuration;
    }
    private void SaveCheckpoint(Vector3 position,PlayerGameplayMode mode)
    {
        checkpoint.position=position;checkpoint.lane=player.Lane;checkpoint.stage=progressStage;
        checkpoint.mode=mode;checkpoint.speed=player.AutoRunSpeed;
        for(int i=0;i<level.Mechanisms.Length;i++)checkpoint.throws[i]=level.Mechanisms[i].State;
    }
    private void Update()
    {
        HitFlash=Mathf.MoveTowards(HitFlash,0f,Time.unscaledDeltaTime*3f);
        if(Finished)return;
        if(recovering)
        {
            if(player.GameplayMode==PlayerGameplayMode.Stunned&&Time.time>=respawnAt)
            {
                if(puzzle!=null)puzzle.Descent.CancelRiderAndRestore();
                player.Respawn(retryPuzzle?puzzle.Descent.Top:checkpoint.position);
                player.SetGameplayMode(PlayerGameplayMode.Respawning);
                pattern.TrimAfter(checkpoint.position.z);
                foreach(var hazard in obstacles)hazard.ResetAttempt(checkpoint.position.z);
                released=false;landed=false;
            }
            if(Time.time>=runAt)
            {
                recovering=false;player.MovementLocked=false;
                player.SetGameplayMode(puzzle!=null?PlayerGameplayMode.FreePuzzle:checkpoint.mode);
                clock.SetPaused(player.GameplayMode!=PlayerGameplayMode.AutoRun);
                Respawned?.Invoke();
            }
            return;
        }
        if(player.Position.y<-10f&&!player.IsClimbing&&!player.IsSwinging) {Fail(puzzle!=null);return;}
        if(player.GameplayMode==PlayerGameplayMode.AirborneFromSwing&&player.IsGrounded&&player.Velocity.y<=.1f)
        {
            if(puzzle!=null&&Mathf.Abs(player.Position.z-puzzle.Ascent.Bottom.z)<1.8f&&Mathf.Abs(player.Position.y-puzzle.Ascent.Bottom.y)<.3f)
            {
                if (!landed) puzzle.Ascent.NotifyRiderLanded(player);
                landed=true;score.Award(800,"swing"+System.Array.IndexOf(level.Mechanisms,puzzle));player.SetGameplayMode(PlayerGameplayMode.FreePuzzle);
            }
            else {Fail(true);return;}
        }
        for(int i=0;i<routeCollectibles.Length;i++)if(routeCollectibles[i].activeSelf&&Vector3.Distance(player.Position,routeCollectibles[i].transform.position)<1.2f)
        {routeCollectibles[i].SetActive(false);score.Award(400,"pickup"+i);}
        if(player.GameplayMode!=PlayerGameplayMode.AutoRun)return;
        if(player.AutoRunSpeed > .01f) RunTime+=Time.deltaTime;
        float z=player.Position.z;
        float speed=config.SpeedAt(z);
        player.AllowParkourInteraction=false;
        foreach(var mechanism in level.Mechanisms)
        {
            if(mechanism.IsResolved)continue;
            float stop=mechanism.SectionZ-1.95f;
            if(z>stop-8.05f)
            {
                float remaining=Mathf.Max(0f,stop-z);
                speed=Mathf.Min(speed,Mathf.Sqrt(2f*1.6f*remaining));
                if(remaining<.12f)speed=0f;
                player.AllowParkourInteraction=remaining<1f;
            }
            break;
        }
        if(Time.time<modifierUntil)speed*=speedModifier;
        player.AutoRunSpeed=speed;
        clock.SetPaused(speed <= .01f);
        if(z>=config.FinishZ)
        {
            Finished=true;player.SetGameplayMode(PlayerGameplayMode.Finished);player.MovementLocked=true;
            player.AutoRunSpeed=0f;clock.SetPaused(true);FinishedLevel?.Invoke();return;
        }
        if(region<regionEnds.Length&&z>regionEnds[region])
        {
            if(score.Deaths==regionDeaths)score.Award(500,"region"+region);
            region++;regionDeaths=score.Deaths;
        }
        for(int i=0;i<level.Mechanisms.Length;i++)
        {
            var m=level.Mechanisms[i];
            if(m.State==PullBlockMechanism.PullState.Failed&&progressStage<=i&&z>=m.SectionZ+2.5f)
            {progressStage=i+1;SaveCheckpoint(new Vector3(0,.6f,z),PlayerGameplayMode.AutoRun);}
        }
    }
}
