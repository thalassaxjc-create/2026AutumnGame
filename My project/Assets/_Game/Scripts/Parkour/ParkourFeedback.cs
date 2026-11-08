using UnityEngine;
[DefaultExecutionOrder(100)]
public sealed class ParkourFeedback : MonoBehaviour
{
    [SerializeField] private ParkourDirector director;
    [SerializeField] private BeatClock clock;
    [SerializeField] private WarpShedController shed;
    [SerializeField] private ReedBeaterChase chase;
    [SerializeField] private PuzzleCamera cameraView;
    [SerializeField] private AudioSource effects;
    [SerializeField] private AudioSource rhythm;
    [SerializeField] private Renderer recoveryMarker;
    [SerializeField] private LineRenderer[] throwLines = new LineRenderer[2];
    [SerializeField, Range(0f,1f)] private float effectVolume = .45f;
    [SerializeField, Range(0f,1f)] private float rhythmVolume = .16f;
    private AudioClip bamboo, wood, snap, wind, success;
    private PlayerController player;
    private PullBlockMechanism[] mechanisms;
    private PullBlockMechanism.PullState[] previousStates;
    private int successfulLayers;
    private float nextChaseHit;
    private MaterialPropertyBlock properties;
    private readonly System.Collections.Generic.HashSet<ParkourObstacle> announced = new System.Collections.Generic.HashSet<ParkourObstacle>();
    public void Configure(ParkourDirector gameplay, BeatClock beat, WarpShedController warp, ReedBeaterChase beater, PuzzleCamera view, AudioSource effectSource, AudioSource rhythmSource, Renderer marker, LineRenderer[] throwingRopes)
    { director=gameplay;clock=beat;shed=warp;chase=beater;cameraView=view;effects=effectSource;rhythm=rhythmSource;recoveryMarker=marker;throwLines=throwingRopes; }
    private void Awake()
    {
        player=director.Player;mechanisms=director.Level.Mechanisms;
        previousStates=new PullBlockMechanism.PullState[mechanisms.Length];
        properties=new MaterialPropertyBlock();
        bamboo=MakeClip("BambooTap",.12f,880f,.12f,31);
        wood=MakeClip("WoodKnock",.22f,180f,.25f,47);
        snap=MakeClip("ThreadSnap",.20f,1400f,.9f,53);
        wind=MakeClip("ThreadWhoosh",.16f,320f,.65f,71);
        success=MakeClip("WovenChime",.45f,660f,.06f,89);
        effects.playOnAwake=false;rhythm.playOnAwake=false;effects.spatialBlend=0f;rhythm.spatialBlend=0f;
    }
    private void OnEnable()
    {
        player.LaneChanged+=OnLane;
        player.JumpStarted+=OnJump;
        player.SlideStarted+=OnSlide;
        director.Respawned+=OnRespawn;
        director.FinishedLevel+=OnFinished;
        director.TraversalReturned+=OnReturned;
        clock.Beat+=OnBeat;
        shed.ShedWarning+=OnShedWarning;
        foreach(var mechanism in mechanisms)
        {mechanism.ThrowResolved+=OnResolved;mechanism.Ascent.RiderLanded+=OnLanding;}
        foreach(var obstacle in director.Obstacles)
        {
            if(obstacle is ButtonThreadObstacle button)button.ThreadOpened+=OnThreadOpened;
            if(obstacle is FallingBambooObstacle falling){falling.Cracking+=OnCracking;falling.Dropping+=OnDropping;}
        }
    }
    private void OnDisable()
    {
        if(player!=null){player.LaneChanged-=OnLane;player.JumpStarted-=OnJump;player.SlideStarted-=OnSlide;}
        if(director!=null){director.Respawned-=OnRespawn;director.FinishedLevel-=OnFinished;director.TraversalReturned-=OnReturned;}
        if(clock!=null)clock.Beat-=OnBeat;
        if(shed!=null)shed.ShedWarning-=OnShedWarning;
        if(mechanisms!=null)foreach(var mechanism in mechanisms)
        {if(mechanism==null)continue;mechanism.ThrowResolved-=OnResolved;if(mechanism.Ascent!=null)mechanism.Ascent.RiderLanded-=OnLanding;}
        if(director!=null)foreach(var obstacle in director.Obstacles)
        {
            if(obstacle is ButtonThreadObstacle button)button.ThreadOpened-=OnThreadOpened;
            if(obstacle is FallingBambooObstacle falling){falling.Cracking-=OnCracking;falling.Dropping-=OnDropping;}
        }
    }
    private void OnThreadOpened(ButtonThreadObstacle obstacle,bool white){Play(wood,.7f);Play(wind,.8f);}
    private void OnCracking(FallingBambooObstacle obstacle){Play(wood,.75f);}
    private void OnDropping(FallingBambooObstacle obstacle){Play(snap,.45f);Play(wood,.7f);cameraView.PulseShake(.02f,.15f);}
    private void OnLane(int lane){Play(wind,.35f);}
    private void OnJump(){if(player.GameplayMode==PlayerGameplayMode.AutoRun)Play(bamboo,.7f);}
    private void OnSlide(){Play(wind,.4f);}
    private void OnShedWarning(){Play(wood,.65f);}
    private void OnLanding(ClimbableRope rope,PlayerController rider){Play(wood,1f);cameraView.PulseShake(.025f,.16f);}
    private void OnReturned(PullBlockMechanism mechanism){Play(success,.8f);}
    private void OnResolved(PullBlockMechanism mechanism,bool ok)
    {if(ok){successfulLayers++;Play(success,1f);}}
    private void OnRespawn(){announced.Clear();Play(wood,.6f);cameraView.PulseShake(.04f,.18f);}
    private void OnFinished(){Play(success,1f);Play(wood,.5f);}
    private void OnBeat(int beat)
    {
        rhythm.PlayOneShot(beat%4==0?wood:bamboo,rhythmVolume);
        if(successfulLayers>0&&beat%2==0)rhythm.PlayOneShot(bamboo,rhythmVolume*.45f*successfulLayers);
        if(director.Scoring.Multiplier>1)rhythm.PlayOneShot(wind,rhythmVolume*.15f*director.Scoring.Multiplier);
    }
    private void Play(AudioClip clip,float weight){if(effects!=null&&clip!=null)effects.PlayOneShot(clip,effectVolume*weight);}
    private void Update()
    {
        for(int i=0;i<mechanisms.Length;i++)
        {
            var state=mechanisms[i].State;
            if(state!=previousStates[i])
            {
                if(state==PullBlockMechanism.PullState.Throwing)Play(wind,1f);
                if(state==PullBlockMechanism.PullState.Failing){Play(snap,1f);cameraView.PulseShake(.035f,.2f);}
                previousStates[i]=state;
            }
            if(i<throwLines.Length&&throwLines[i]!=null)
            {
                Color tint=state==PullBlockMechanism.PullState.Failing?new Color(1f,.1f,.05f):Color.white;
                properties.SetColor("_BaseColor",tint);properties.SetColor("_Color",tint);throwLines[i].SetPropertyBlock(properties);
            }
        }
        if(player.GameplayMode==PlayerGameplayMode.AutoRun)
        {
            foreach(var obstacle in director.Obstacles)
            {
                if(obstacle is ButtonThreadObstacle || obstacle is FallingBambooObstacle)continue;
                float distance=obstacle.Data.z-player.Position.z;
                if(distance>=0f&&distance/Mathf.Max(.1f,player.AutoRunSpeed)<=obstacle.Data.warningTime&&announced.Add(obstacle))
                    Play(obstacle is HangingWeightPendulum?wind:wood,.4f);
            }
        }
        var puzzle=director.ActivePuzzle;
        bool show=puzzle!=null&&!director.Finished&&player.GameplayMode!=PlayerGameplayMode.ThrowPose&&player.GameplayMode!=PlayerGameplayMode.AirborneFromSwing;
        recoveryMarker.enabled=show;
        if(show)
        {
            Vector3 point=player.Position.z>puzzle.SectionZ?puzzle.Ascent.Bottom:puzzle.Descent.Top;
            recoveryMarker.transform.position=point+Vector3.down*.45f;
            Color color=new Color(.1f,1f,.85f)*(1f+.15f*Mathf.Sin(Time.time*6f));
            properties.SetColor("_BaseColor",color);properties.SetColor("_Color",color);recoveryMarker.SetPropertyBlock(properties);
        }
        if(chase.Chasing&&Time.time>=nextChaseHit)
        {
            float urgency=1f-Mathf.InverseLerp(.8f,8f,chase.DistanceBehind);
            nextChaseHit=Time.time+Mathf.Lerp(.55f,.18f,urgency);
            Play(wood,Mathf.Lerp(.35f,.8f,urgency));
            cameraView.PulseShake(.012f+urgency*.02f,.10f);
        }
    }
    private static AudioClip MakeClip(string name,float duration,float frequency,float noise,int seed)
    {
        const int rate=22050;
        int count=Mathf.CeilToInt(duration*rate);var samples=new float[count];var random=new System.Random(seed);
        for(int i=0;i<count;i++)
        {
            float t=i/(float)rate;float envelope=Mathf.Exp(-t*24f)*Mathf.Min(1f,t*250f);
            float tone=Mathf.Sin(2f*Mathf.PI*frequency*t)+.25f*Mathf.Sin(2f*Mathf.PI*frequency*2.7f*t);
            samples[i]=Mathf.Clamp((tone*(1f-noise)+((float)random.NextDouble()*2f-1f)*noise)*envelope*.45f,-1f,1f);
        }
        var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
    }
    private void OnDestroy()
    {
        if(bamboo!=null)Destroy(bamboo);if(wood!=null)Destroy(wood);if(snap!=null)Destroy(snap);if(wind!=null)Destroy(wind);if(success!=null)Destroy(success);
    }
}
