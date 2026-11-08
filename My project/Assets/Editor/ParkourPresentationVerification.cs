using UnityEditor;
using UnityEngine;
using System.Reflection;
[InitializeOnLoad]
internal static class ParkourPresentationVerification
{
 const string State="ParkourVisual.State",Result="ParkourVisual.Result";
 static Component player,director,level,shed,chase,clock,mechanism,view;
 static Rigidbody pillar;static int stage,frames;static float phase,maxGap;static double started;static bool setup,oldBackground;
 static readonly System.Collections.Generic.List<string> logs=new System.Collections.Generic.List<string>();
 static ParkourPresentationVerification(){var s=SessionState.GetString(State,"Idle");if(s=="Waiting")EditorApplication.update+=Enter;if(s=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}}
 static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
 static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
 static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
 static Vector3 Position=>(Vector3)Read(player,"Position");
 static void Mode(string value){var t=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(t,value));}
 static void Check(bool value,string text){string log=text+"="+value+" pos="+Position;logs.Add(log);Debug.Log("[Presentation] "+log);if(!value)throw new System.Exception(text);}
 static void Shot(string path){ScreenCapture.CaptureScreenshot(path);logs.Add("Screenshot="+path);}
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<8)return;
  try
  {
   if(!setup)
   {
    setup=true;oldBackground=Application.runInBackground;Application.runInBackground=true;started=EditorApplication.timeSinceStartup;
    foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
    {if(c==null)continue;switch(c.GetType().Name){case "PlayerController":player=c;break;case "ParkourDirector":director=c;break;case "FivePillarLevel":level=c;break;case "WarpShedController":shed=c;break;case "ReedBeaterChase":chase=c;break;case "BeatClock":clock=c;break;case "PuzzleCamera":view=c;break;}if(c is Rigidbody body&&c.name=="Pillar_3")pillar=body;}
    mechanism=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(0);Call(player,"Respawn",Read(mechanism,"PullPoint"));Mode("AutoRun");phase=Time.time;
   }
   if(EditorApplication.timeSinceStartup-started>25)throw new System.Exception("Presentation timeout stage="+stage);
   if(stage==0&&Time.time-phase>.3f){Check((bool)Call(mechanism,"TryPull",player),"Throw pose entry");phase=Time.time;stage=1;}
   else if(stage==1&&Time.time-phase>.8f){Check(Read(player,"GameplayMode").ToString()=="ThrowPose","Timing pose stays active");Shot("Assets/ParkourTimingLayout.png");phase=Time.time;stage=2;}
   else if(stage==2&&Time.time-phase>.3f){Call(mechanism,"ResolveTiming",.5f);stage=3;}
   else if(stage==3&&(bool)Read(mechanism,"IsResolved"))
   {
    var second=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(1);Call(second,"Finish");var up=(Component)Read(second,"Ascent");
    Call(player,"Respawn",Read(up,"Top"));Call(director,"OnClimbFinished",true);
    Call(player,"Respawn",new Vector3(0,.6f,65f));Mode("AutoRun");phase=Time.time;stage=4;
   }
   else if(stage==4)
   {
    if((bool)Read(shed,"Active"))maxGap=Mathf.Max(maxGap,Mathf.Abs(Position.y-(pillar.position.y+2.3f)));
    if(Position.z>79f)
    {
     Check(maxGap<.35f,"Moving bamboo carries rider; max gap="+maxGap);
     Mode("FreePuzzle");Call(clock,"SetPaused",true);phase=Time.time;stage=5;
    }
   }
   else if(stage==5&&Time.time-phase>.5f)
   {
    Check(Mathf.Abs(pillar.position.y+1.8f)<.03f,"Bamboo restores standard height");
    Call(player,"Respawn",new Vector3(0,.6f,146f));Mode("AutoRun");Call(clock,"SetPaused",false);phase=Time.time;stage=6;
   }
   else if(stage==6&&Time.time-phase>1.1f)
   {
    Check((bool)Read(chase,"Chasing"),"Finale beater active");float distance=(float)Read(chase,"DistanceBehind");Check(distance>=5f&&distance<=8.3f,"Beater distance="+distance);
    Check(Camera.main.fieldOfView>69f,"Finale FOV="+Camera.main.fieldOfView);Shot("Assets/ParkourFinaleLayout.png");phase=Time.time;stage=7;
   }
   else if(stage==7&&Time.time-phase>.3f){Call(player,"Respawn",new Vector3(0,.6f,171.5f));Mode("AutoRun");phase=Time.time;stage=8;}
   else if(stage==8&&Time.time-phase>1.3f)
   {
    Check((bool)Read(director,"Finished"),"Final pattern displayed");Shot("Assets/ParkourFinishLayout.png");phase=Time.time;stage=9;
   }
   else if(stage==9&&Time.time-phase>.3f){Finish(true,"");}
  }
  catch(System.Exception e){Finish(false,e.ToString());}
 }
 static void Finish(bool success,string error){EditorApplication.update-=Tick;Application.runInBackground=oldBackground;SessionState.SetString(Result,JsonUtility.ToJson(new Report{success=success,error=error,logs=logs.ToArray()}));SessionState.SetString(State,"Idle");Debug.Log("[Presentation] COMPLETE success="+success+" error="+error);EditorApplication.isPlaying=false;}
 [System.Serializable]class Report{public bool success;public string error;public string[] logs;}
}
