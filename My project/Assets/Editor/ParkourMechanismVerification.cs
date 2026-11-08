using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Collections.Generic;
[InitializeOnLoad]
internal static class ParkourMechanismVerification
{
    const string State="MechanismTest.State",Result="MechanismTest.Result";
    static Component player,director,level,button,fall,clock,warp;
    static Component[] pressures;static Rigidbody parentBody,segmentBody;
    static int scenario,stage,frames;static float phase,maxFootGap,maxSegmentGap;
    static double started;static bool initialized,oldBackground,jumpSent;
    static readonly List<string> logs=new List<string>();
    static ParkourMechanismVerification(){string s=SessionState.GetString(State,"Idle");if(s=="Waiting")EditorApplication.update+=Enter;if(s=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}}
    static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
    static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
    static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
    static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
    static Vector3 Position=>(Vector3)Read(player,"Position");
    static int Deaths=>(int)Read((Component)Read(director,"Scoring"),"Deaths");
    static void Mode(string mode){var t=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(t,mode));}
    static void Spawn(float x,float z,float y=.6f){Call(player,"Respawn",new Vector3(x,y,z));Mode("AutoRun");}
    static void Jump(){var f=BindingFlags.Instance|BindingFlags.NonPublic;player.GetType().GetField("jumpQueued",f).SetValue(player,true);player.GetType().GetField("jumpUntil",f).SetValue(player,Time.time+.12f);}
    static void Check(bool value,string description){string text="case"+scenario+" "+description+"="+value+" pos="+Position;logs.Add(text);Debug.Log("[MechanismTest] "+text);if(!value)throw new System.Exception(description);}
    static void SeedThrows()
    {
        var mechanisms=(System.Array)Read(level,"Mechanisms");foreach(Component m in mechanisms)Call(m,"Finish");
        var second=(Component)mechanisms.GetValue(1);Call(player,"Respawn",Read((Component)Read(second,"Ascent"),"Top"));Call(director,"OnClimbFinished",true);
    }
    static void Bind()
    {
        var pressureList=new List<Component>();
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
        {
            if(c==null)throw new System.Exception("Missing component");
            switch(c.GetType().Name){case "PlayerController":player=c;break;case "ParkourDirector":director=c;break;case "FivePillarLevel":level=c;break;case "ButtonThreadObstacle":button=c;break;case "FallingBambooObstacle":fall=c;break;case "BeatClock":clock=c;break;case "WarpShedController":warp=c;break;case "PressureButton":pressureList.Add(c);break;}
            if(c is Rigidbody body&&c.name=="Pillar_4")parentBody=body;
            if(c is Rigidbody segment&&c.name=="FallingSegment"&&segment.transform.parent.name=="Pillar_4")segmentBody=segment;
        }
        pressures=pressureList.ToArray();phase=Time.time;stage=0;jumpSent=false;maxFootGap=0;maxSegmentGap=0;
        if(scenario==0)Spawn(-3.6f,31);
        else if(scenario==1)Spawn(3.6f,31);
        else if(scenario==2)Spawn(-3.6f,32.8f,3f);
        else if(scenario==3)Spawn(0,37);
        else if(scenario==4||scenario==5){SeedThrows();Spawn(scenario==4?-3.6f:3.6f,130);}
        else if(scenario==6)Spawn(-7.2f,7.5f);
        else if(scenario==7)Spawn(-7.2f,10,3f);
        else if(scenario==8){SeedThrows();Spawn(3.6f,65);}
        else if(scenario==9){SeedThrows();Spawn(0,84.2f);}
        else if(scenario==10){SeedThrows();Spawn(7.2f,84.2f);}
        else if(scenario==11){SeedThrows();Spawn(0,84.2f);}
    }
    static void Next()
    {
        scenario++;if(scenario>11){Finish(true,"");return;}
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));frames=0;stage=-1;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||++frames<8)return;
        try
        {
            if(!initialized){initialized=true;oldBackground=Application.runInBackground;Application.runInBackground=true;started=EditorApplication.timeSinceStartup;Bind();Application.logMessageReceived+=Capture;}
            if(stage==-1){Bind();return;}
            if(EditorApplication.timeSinceStartup-started>90)throw new System.Exception("Timeout scenario="+scenario+" stage="+stage+" pos="+Position);
            if(scenario==0||scenario==1)
            {
                bool open=(bool)Read(button,scenario==0?"WhiteOpen":"YellowOpen");
                if(stage==0&&open)
                {
                    Check(!(bool)Read(button,scenario==0?"YellowOpen":"WhiteOpen"),"Only matching color opens on real foot contact");
                    Call(player,"RequestLaneChange",scenario==0?1:-1);phase=Time.time;stage=1;
                }
                else if(stage==0&&Position.z>35.5f)throw new System.Exception("Pressure plate failed to trigger");
                else if(stage==1&&Time.time-phase>.23f){if(scenario==0)Call(player,"RequestLaneChange",1);stage=2;}
                else if(stage==2&&Position.z>41f)
                {
                    Check(Deaths==0&&Mathf.Abs(Position.x-(scenario==0?3.6f:0f))<.1f,"Crossed correct rope side using smooth lane changes");Check(open,"Rope stays open after leaving plate");
                    Mode("FreePuzzle");ScreenCapture.CaptureScreenshot(scenario==0?"Assets/MechanismWhiteRoute.png":"Assets/MechanismYellowRoute.png");phase=Time.time;stage=3;
                }
                else if(stage==3&&Time.time-phase>.3f)Next();
            }
            else if(scenario==2)
            {
                if(stage==0&&Time.time-phase>.4f)
                {
                    Check(!(bool)Read(button,"WhiteOpen")&&!(bool)Read(button,"YellowOpen"),"Flying over plate does not activate it");
                    var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="NonPlayerPressureProbe";probe.transform.position=new Vector3(3.6f,.1f,34);probe.transform.localScale=Vector3.one*.2f;probe.AddComponent<Rigidbody>().isKinematic=true;phase=Time.time;stage=1;
                }
                else if(stage==1&&Time.time-phase>.3f){Check(!(bool)Read(button,"WhiteOpen")&&!(bool)Read(button,"YellowOpen"),"Non-player object does not press plate");Next();}
            }
            else if(scenario==3&&Time.time-phase>1.6f)
            {Check(Deaths==1&&Position.z<5f,"Closed rope blocks and quickly respawns");Check(!(bool)Read(button,"WhiteOpen")&&!(bool)Read(button,"YellowOpen"),"Button state resets after death");Next();}
            else if((scenario==4||scenario==5)&&Position.z>135f)
            {Check(Deaths==0,"Empty lane passes 1/3/5 knot row");Next();}
            else if((scenario==6||scenario==7)&&Time.time-phase>.9f)
            {Check(Deaths==1,scenario==6?"Visible knot collision triggers retry":"Knot blocks maximum normal jump height");Next();}
            else if(scenario==8)
            {
                if((bool)Read(warp,"Active")){maxFootGap=Mathf.Max(maxFootGap,Mathf.Abs(Position.y-parentBody.position.y-2.3f));maxSegmentGap=Mathf.Max(maxSegmentGap,Mathf.Abs(segmentBody.position.y-parentBody.position.y));}
                if(Position.z>79f){Check(Deaths==0&&maxFootGap<.2f,"Split pillar carries player during shed; gap="+maxFootGap);Check(maxSegmentGap<.15f,"Detached fall body follows main pillar shed; gap="+maxSegmentGap);Next();}
            }
            else if(scenario==9)
            {
                if(stage==0&&Position.z>91f){ScreenCapture.CaptureScreenshot("Assets/MechanismFallingGap.png");stage=1;}
                if((stage==0||stage==1)&&Position.z>94f&&!jumpSent){Jump();Call(player,"RequestLaneChange",-1);jumpSent=true;}
                if(stage<=1&&Position.z>96.8f)
                {
                    Check(Deaths==0&&Position.x<=0,"Left three lanes cross real gap and low rope");Check(Read(fall,"State").ToString()=="Gone","Right two sections fall away");Mode("FreePuzzle");Call(director,"Fail",false);stage=2;
                }
                else if(stage==2&&Read(player,"GameplayMode").ToString()=="AutoRun")
                {Check((int)Read(level,"TransferCount")==2,"Retry retains both successful throws");Check(Read(fall,"State").ToString()=="Ready"&&segmentBody.gameObject.activeSelf,"Fall sections restored on retry");Check(((Vector3)Read(segmentBody.GetComponent("MovingSurface"),"Velocity")).sqrMagnitude<.001f,"Reset clears surface velocity");Next();}
            }
            else if(scenario==10)
            {
                if(Deaths>0&&Read(player,"GameplayMode").ToString()=="AutoRun")
                {Check(Deaths==1&&Position.z>120f,"Walking broken right lane falls and uses checkpoint");Check((int)Read(level,"TransferCount")==2,"Fall does not undo throws");Next();}
            }
            else if(scenario==11)
            {
                if(stage==0&&Read(fall,"State").ToString()=="Gone"){Spawn(7.2f,85.8f);phase=Time.time;stage=1;}
                else if(stage==1&&Time.time-phase>.02f&&!jumpSent){Jump();jumpSent=true;}
                else if(stage==1&&Deaths>0){Check(Deaths==1&&Position.y<-9f,"Normal running jump falls rather than landing beyond the gap");Next();}
            }
        }
        catch(System.Exception e){Finish(false,e.ToString());}
    }
    static void Capture(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!message.Contains("AssistantPerf"))logs.Add("RUNTIME_ERROR "+message);}
    static void Finish(bool success,string error){Application.logMessageReceived-=Capture;EditorApplication.update-=Tick;Application.runInBackground=oldBackground;SessionState.SetString(Result,JsonUtility.ToJson(new Report{success=success,error=error,logs=logs.ToArray()}));SessionState.SetString(State,"Idle");Debug.Log("[MechanismTest] COMPLETE success="+success+" error="+error);EditorApplication.isPlaying=false;}
    [System.Serializable]class Report{public bool success;public string error;public string[] logs;}
}
