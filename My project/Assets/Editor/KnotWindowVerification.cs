using UnityEngine;
using UnityEditor;
using System.Reflection;
[InitializeOnLoad]
internal static class KnotWindowVerification
{
 const string State="KnotWindow.State";static Component player,knot,clock,view;static int stage,frames,samples;static float phase,period;static double started;static bool ready,oldBackground;
 static KnotWindowVerification(){var s=SessionState.GetString(State,"Idle");if(s=="Waiting")EditorApplication.update+=Enter;if(s=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}}
 static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
 static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
 static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(c,args);
 static void Pose(float x,float z){Call(player,"Respawn",new Vector3(x,.6f,z));var type=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(type,"FreePuzzle"));Call(clock,"SetPaused",true);}
 static void ClockAt(float value){clock.GetType().GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(clock,value);}
 static bool Open(int lane){var array=(Collider[])knot.GetType().GetField("knotColliders",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(knot);return !array[lane].enabled||!array[lane].gameObject.activeInHierarchy;}
 static void Check(bool value,string text){Debug.Log("[KnotWindow] "+text+"="+value);if(!value)throw new System.Exception(text);}
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<8)return;
  try
  {
   if(!ready)
   {
    ready=true;oldBackground=Application.runInBackground;Application.runInBackground=true;started=EditorApplication.timeSinceStartup;
    foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
    {if(c==null)continue;if(c.GetType().Name=="PlayerController")player=c;if(c.GetType().Name=="BeatClock")clock=c;if(c.GetType().Name=="PuzzleCamera")view=c;if(c.GetType().Name=="ThreadKnotObstacle"){var data=Read(c,"Data");if((float)data.GetType().GetField("z").GetValue(data)==82f)knot=c;}}
    period=(float)Read(knot,"Data").GetType().GetField("duration").GetValue(Read(knot,"Data"));Pose(0,30.5f);Call(view,"SnapToPlayer");phase=Time.time;
   }
   if(EditorApplication.timeSinceStartup-started>12f)throw new System.Exception("Window test timeout stage "+stage);
   if(stage==0&&Time.time-phase>.3f){ScreenCapture.CaptureScreenshot("Assets/MechanismButtonOverview.png");phase=Time.time;stage=1;}
   else if(stage==1&&Time.time-phase>.3f){Pose(0,78.6f);ClockAt(2f*period-.2f);phase=Time.time;stage=2;}
   else if(stage==2&&Time.time-phase>.15f){Check(Open(4),"Next safe lane opens in preview");Pose(7.2f,79f);phase=Time.time;stage=3;}
   else if(stage==3&&Time.time-phase>.15f){ClockAt(3.1f*period);Pose(7.2f,83.4f);phase=Time.time;stage=4;}
   else if(stage==4&&Time.time-phase>.15f){Check(Open(4),"Previewed opening survives phase change and thick-ball exit");Pose(7.2f,84.2f);phase=Time.time;stage=5;}
   else if(stage==5&&Time.time-phase>.12f)
   {
    int open=0;for(int i=0;i<5;i++)if(Open(i))open++;Check(open>0,"No all-lane closure phase "+samples);
    samples++;if(samples>=9){Finish(true,"");return;}ClockAt(samples*period+.1f);phase=Time.time;
   }
  }
  catch(System.Exception e){Finish(false,e.ToString());}
 }
 static void Finish(bool success,string error){EditorApplication.update-=Tick;Application.runInBackground=oldBackground;SessionState.SetString("KnotWindow.Result","success="+success+" "+error);SessionState.SetString(State,"Idle");Debug.Log("[KnotWindow] COMPLETE success="+success+" "+error);EditorApplication.isPlaying=false;}
}
