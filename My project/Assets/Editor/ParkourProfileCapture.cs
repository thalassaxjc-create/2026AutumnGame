using UnityEditor;
using UnityEngine;
using System.Reflection;
[InitializeOnLoad]
internal static class ParkourProfileCapture
{
 const string State="ParkourProfile.State";
 static int frames,stage;static float phase;static double started;
 static bool oldEnabled,oldBinary,oldBackground;static string oldLog;
 static ParkourProfileCapture(){var s=SessionState.GetString(State,"Idle");if(s=="Waiting")EditorApplication.update+=Enter;if(s=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}}
 static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
 static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
 static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<10)return;
  try
  {
   if(stage==0)
   {
    oldEnabled=UnityEngine.Profiling.Profiler.enabled;oldBinary=UnityEngine.Profiling.Profiler.enableBinaryLog;oldLog=UnityEngine.Profiling.Profiler.logFile;oldBackground=Application.runInBackground;Application.runInBackground=true;started=EditorApplication.timeSinceStartup;
    Component player=null,level=null,director=null;
    foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
    {if(c==null)continue;if(c.GetType().Name=="PlayerController")player=c;if(c.GetType().Name=="FivePillarLevel")level=c;if(c.GetType().Name=="ParkourDirector")director=c;}
    foreach(Component mechanism in (System.Array)Read(level,"Mechanisms"))Call(mechanism,"Finish");
    var second=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(1);Call(player,"Respawn",Read((Component)Read(second,"Ascent"),"Top"));Call(director,"OnClimbFinished",true);
    Call(player,"Respawn",new Vector3(0,.6f,146f));var type=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(type,"AutoRun"));phase=Time.time;stage=1;
   }
   else if(stage==1&&Time.time-phase>1.2f)
   {
    string path=System.IO.Path.GetFullPath("Temp/ParkourValidation.data");
    UnityEngine.Profiling.Profiler.enabled=false;UnityEngine.Profiling.Profiler.logFile=path;UnityEngine.Profiling.Profiler.enableBinaryLog=true;UnityEngine.Profiling.Profiler.enabled=true;
    SessionState.SetString("ParkourProfile.Path",path);phase=Time.time;stage=2;
   }
   else if(stage==2&&Time.time-phase>4f){Finish("");}
   if(EditorApplication.timeSinceStartup-started>15f)Finish("Capture timed out");
  }
  catch(System.Exception e){Finish(e.ToString());}
 }
 static void Finish(string error)
 {
  EditorApplication.update-=Tick;UnityEngine.Profiling.Profiler.enabled=false;UnityEngine.Profiling.Profiler.enableBinaryLog=oldBinary;UnityEngine.Profiling.Profiler.logFile=oldLog;UnityEngine.Profiling.Profiler.enabled=oldEnabled;Application.runInBackground=oldBackground;
  SessionState.SetString(State,"Idle");SessionState.SetString("ParkourProfile.Error",error);Debug.Log("[ProfileCapture] COMPLETE "+error);EditorApplication.isPlaying=false;
 }
}
