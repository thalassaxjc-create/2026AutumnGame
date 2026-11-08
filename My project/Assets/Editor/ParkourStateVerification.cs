using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
[InitializeOnLoad]
internal static class ParkourStateVerification
{
 const string State="ParkourState.State", Result="ParkourState.Result";
 static Component player,level,director,mechanism,down,up;
 static int stage,scenario,section,frames;
 static float phase;static double started;static bool initialized,badReleaseDone,oldBackground;
 static readonly List<string> logs=new List<string>();
 static ParkourStateVerification()
 {
  var state=SessionState.GetString(State,"Idle");
  if(state=="Waiting")EditorApplication.update+=Enter;
  if(state=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}
 }
 static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
 static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
 static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(c,args);
 static Vector3 Position=>(Vector3)Read(player,"Position");
 static void Move(float y){player.GetType().GetField("moveInput",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,new Vector2(0,y));}
 static void Mode(string value){var type=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(type,value));}
 static void Check(bool value,string description){string text="case"+scenario+" section"+section+": "+description+"="+value+" pos="+Position;logs.Add(text);Debug.Log("[StateTest] "+text);if(!value)throw new System.Exception(description);}
 static void Bind()
 {
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
  {if(c==null)continue;switch(c.GetType().Name){case "PlayerController":player=c;break;case "FivePillarLevel":level=c;break;case "ParkourDirector":director=c;break;}}
 }
 static bool Success=>scenario==0||(scenario==1&&section==0)||(scenario==2&&section==1);
 static void ThrowSetup()
 {
  mechanism=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(section);down=(Component)Read(mechanism,"Descent");up=(Component)Read(mechanism,"Ascent");
  Call(player,"Respawn",Read(mechanism,"PullPoint"));Mode("AutoRun");phase=Time.time;stage=1;
 }
 static void Release(float degrees,float velocity)
 {
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;var type=player.GetType();float angle=degrees*Mathf.Deg2Rad;
  type.GetField("swingAngle",flags).SetValue(player,angle);type.GetField("swingAngularVelocity",flags).SetValue(player,velocity);
  var anchor=(Vector3)type.GetField("swingAnchor",flags).GetValue(player);var forward=(Vector3)type.GetField("swingForward",flags).GetValue(player);float length=(float)type.GetField("swingLength",flags).GetValue(player);
  player.GetComponent<Rigidbody>().position=(Vector3)Call(down,"DriveSwingPlatform",angle,anchor,forward,length);Call(player,"EndSwing",true);Move(0);phase=Time.time;
 }
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<8)return;
  try
  {
   if(!initialized){initialized=true;started=EditorApplication.timeSinceStartup;oldBackground=Application.runInBackground;Application.runInBackground=true;Bind();section=0;ThrowSetup();Application.logMessageReceived+=CaptureErrors;logs.Add("Control fixture drives moveInput; physical integration and public interaction methods remain real. Keyboard dispatch is not asserted.");}
   if(EditorApplication.timeSinceStartup-started>95)throw new System.Exception("Timeout stage "+stage);
   if(stage==1&&Time.time-phase>.3f){Check((bool)Call(mechanism,"TryPull",player),"Throw starts");phase=Time.time;stage=2;}
   else if(stage==2&&Time.time-phase>.6f)
   {
    Check(Read(player,"GameplayMode").ToString()=="ThrowPose","Exclusive throw mode");
    if(scenario==0&&section==0){ScreenCapture.CaptureScreenshot("Assets/ParkourThrowCheck.png");SessionState.SetString("ParkourState.ThrowShot","Assets/ParkourThrowCheck.png");}
    Call(mechanism,"ResolveTiming",Success?.5f:0f);stage=3;
   }
   else if(stage==3&&(bool)Read(mechanism,"IsResolved"))
   {
    Check((bool)Read(mechanism,"BlocksPath")==Success,"Barrier follows result");
    if(Success){Check(Read(player,"GameplayMode").ToString()=="FreePuzzle","Success pauses AutoRun");Call(player,"Respawn",Read(down,"Top"));phase=Time.time;stage=4;}
    else{Check(!((Transform)Read(mechanism,"Block")).gameObject.activeSelf,"Failed plank remains lost");Check(Read(player,"GameplayMode").ToString()=="AutoRun","Miss resumes AutoRun");Call(player,"Respawn",new Vector3(0,.6f,(float)Read(mechanism,"SectionZ")+3f));phase=Time.time;stage=12;}
   }
   else if(stage==4&&Time.time-phase>.4f){Check((bool)Call(down,"TryClimb",player),"Descent rope enter");Move(-1);phase=Time.time;stage=5;}
   else if(stage==5)
   {
    Move(-1);
    if(!(bool)Read(player,"IsClimbing")){Move(0);Check(Mathf.Abs(Position.y-((Vector3)Read(down,"Bottom")).y)<.25f,"Descend onto source plank");phase=Time.time;stage=6;}
   }
   else if(stage==6&&Time.time-phase>.4f){Check((bool)Call(down,"TryClimb",player),"Grab whole-plank swing");Move(1);phase=Time.time;stage=7;}
   else if(stage==7&&Time.time-phase>.4f)
   {
    Move(1);var platform=(Transform)Read(down,"SwingPlatform");Check(Mathf.Abs(Position.z-platform.position.z)<.03f&&Mathf.Abs(Position.y-platform.position.y-1.35f)<.04f,"Synchronized rider and plank");
    if(scenario==0&&section==0&&!badReleaseDone){badReleaseDone=true;Release(0,.05f);stage=8;}
    else{Release(35,.95f);stage=9;}
   }
   else if(stage==8&&Time.time-phase>2.5f)
   {
    Check(Read(player,"GameplayMode").ToString()=="FreePuzzle"&&Mathf.Abs(Position.z-((Vector3)Read(down,"Top")).z)<.5f,"Early release returns to descent checkpoint");Check((bool)Read(mechanism,"BlocksPath"),"Retry retains successful barrier");phase=Time.time;stage=4;
   }
   else if(stage==9&&Time.time-phase>2f)
   {
    Check((bool)Read(player,"IsGrounded")&&Mathf.Abs(Position.z-((Vector3)Read(up,"Bottom")).z)<1.8f,"35-degree fixture lands 9m away without assist");Check((bool)Call(up,"TryClimb",player),"Target rope enter");Move(1);phase=Time.time;stage=10;
   }
   else if(stage==10)
   {
    Move(1);
    if(!(bool)Read(player,"IsClimbing")){Move(0);Check(Read(player,"GameplayMode").ToString()=="AutoRun","Target climb resumes AutoRun");phase=Time.time;stage=12;}
   }
   else if(stage==12&&Time.time-phase>.4f)
   {if(section==0){section=1;ThrowSetup();}else{Call(player,"Respawn",new Vector3(0,.6f,171.5f));Mode("AutoRun");phase=Time.time;stage=13;}}
   else if(stage==13&&Time.time-phase>1.3f)
   {
    Check((bool)Read(director,"Finished"),"Finish stops run");int percent=(int)Read(level,"CompletionPercent");Check(scenario==0?percent==100:percent<100,"Completion="+percent);
    if(scenario==0){ScreenCapture.CaptureScreenshot("Assets/ParkourResultCheck.png");SessionState.SetString("ParkourState.ResultShot","Assets/ParkourResultCheck.png");}
    phase=Time.time;stage=14;
   }
   else if(stage==14&&Time.time-phase>.3f)
   {
    if(scenario==3){Finish(true,"");return;}
    scenario++;section=0;EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));stage=15;frames=0;
   }
   else if(stage==15&&frames>10){Bind();ThrowSetup();}
  }
  catch(System.Exception e){Finish(false,e.ToString());}
 }
 static void CaptureErrors(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!message.Contains("AssistantPerf"))logs.Add("RUNTIME_ERROR "+message);}
 static void Finish(bool success,string error)
 {
  Application.logMessageReceived-=CaptureErrors;EditorApplication.update-=Tick;Application.runInBackground=oldBackground;
  SessionState.SetString(Result,JsonUtility.ToJson(new Report{success=success,error=error,logs=logs.ToArray()}));SessionState.SetString(State,"Idle");Debug.Log("[StateTest] COMPLETE success="+success+" error="+error);EditorApplication.isPlaying=false;
 }
 [System.Serializable]class Report{public bool success;public string error;public string[] logs;}
}
