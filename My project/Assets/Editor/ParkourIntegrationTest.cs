using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;
using System.Collections.Generic;
[InitializeOnLoad]
internal static class ParkourIntegrationTest
{
 const string State="ParkourTest.State",Result="ParkourTest.Result";
 static Component player,level,director,mechanism,down,up;static Keyboard keyboard;
 static int stage,scenario,section,frames;static float phase,initialZ,releaseZ;
 static double started;static bool boot;static readonly List<string> logs=new List<string>();
 static UnityEngine.InputSystem.InputSettings originalSettings,testSettings;
 static bool originalBackground;
 static ParkourIntegrationTest()
 {
  string state=SessionState.GetString(State,"Idle");
  Debug.Log("[ParkourTest] Bootstrap state="+state);
  if(state=="Waiting")EditorApplication.update+=Enter;
  if(state=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}
 }
 static void Enter()
 {
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();
 }
 static void Changed(PlayModeStateChange c){if(c!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;SessionState.SetString(State,"Running");Debug.Log("[ParkourTest] Running");EditorApplication.update+=Tick;}
 static Component Locate(string type){foreach(var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in r.GetComponentsInChildren<Component>(true))if(c!=null&&c.GetType().Name==type)return c;return null;}
 static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
 static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
 static Vector3 Position=>(Vector3)Read(player,"Position");
 static void Check(bool ok,string name){string message="case"+scenario+"/section"+section+" "+name+"="+ok+" pos="+Position;logs.Add(message);Debug.Log("[ParkourTest] "+message);if(!ok)throw new System.Exception(name);}
 static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
 static void Bind()
 {
  player=Locate("PlayerController");level=Locate("FivePillarLevel");director=Locate("ParkourDirector");
  if(player==null||level==null||director==null)throw new System.Exception("Missing runtime components");
  var input=player.GetComponent<PlayerInput>();input.user.UnpairDevices();UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);Keys();
 }
 static void LoadCase()
 {Keys();EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));stage=10;phase=Time.time;frames=0;}
 static void StartThrow()
 {
  mechanism=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(section);down=(Component)Read(mechanism,"Descent");up=(Component)Read(mechanism,"Ascent");Call(player,"Respawn",Read(mechanism,"PullPoint"));
  var modeType=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(modeType,"AutoRun"));
  phase=Time.time;stage=12;
 }
 static bool Success()=>scenario==0||(scenario==1&&section==0)||(scenario==2&&section==1);
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<5)return;
  try
  {
   if(!boot)
   {
    boot=true;originalBackground=Application.runInBackground;Application.runInBackground=true;
    originalSettings=UnityEngine.InputSystem.InputSystem.settings;
    testSettings=UnityEngine.Object.Instantiate(originalSettings);
    testSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
    UnityEngine.InputSystem.InputSystem.settings=testSettings;
    started=EditorApplication.timeSinceStartup;keyboard=InputSystem.AddDevice<Keyboard>("ParkourTestKeyboard");Bind();initialZ=Position.z;phase=Time.time;Application.logMessageReceived+=Log;
   }
   if(EditorApplication.timeSinceStartup-started>95)throw new System.Exception("Test timeout at "+stage);
   if(stage==0&&Time.time-phase>1f){Check(Position.z-initialZ>3f,"AutoRun advances");Call(player,"RequestLaneChange",1);phase=Time.time;stage=1;}
   else if(stage==1&&Time.time-phase>.3f){Check(Mathf.Abs(Position.x-3.6f)<.1f,"Smooth lane switch ends on lane");Keys(Key.Space);phase=Time.time;stage=2;}
   else if(stage==2&&Time.time-phase>.25f){Check(Position.y>1.2f,"Input System SPACE jumps");Keys();phase=Time.time;stage=3;}
   else if(stage==3&&Time.time-phase>1.3f){Call(player,"Respawn",new Vector3(0,.6f,4f));phase=Time.time;stage=4;}
   else if(stage==4&&Time.time-phase>.3f){Call(player,"RequestSlide");Check((bool)Read(player,"IsSliding"),"Slide starts");phase=Time.time;stage=5;}
   else if(stage==5&&Time.time-phase>.75f){Check(!(bool)Read(player,"IsSliding"),"Slide recovers collider");Call(player,"Respawn",new Vector3(-7.2f,.55f,10f));phase=Time.time;stage=6;}
   else if(stage==6&&Time.time-phase>.85f){Check(Read(player,"GameplayMode").ToString()=="AutoRun"&&Position.z<4f,"Real ring collision fast checkpoint retry");LoadCase();}
   else if(stage==10&&frames>8){Bind();section=0;StartThrow();}
   else if(stage==12&&Time.time-phase>.25f){Check((bool)Call(mechanism,"TryPull",player),"Throw begins");Check(Read(player,"GameplayMode").ToString()=="ThrowPose","ThrowPose is exclusive");Call(mechanism,"ResolveTiming",Success()?.5f:0f);stage=13;phase=Time.time;}
   else if(stage==13&&(bool)Read(mechanism,"IsResolved"))
   {
    Check((bool)Read(mechanism,"BlocksPath")==Success(),"Barrier matches throw result");
    if(Success()){Check(Read(player,"GameplayMode").ToString()=="FreePuzzle","Success pauses AutoRun");Call(player,"Respawn",Read(down,"Bottom"));phase=Time.time;stage=14;}
    else {Check(!((Transform)Read(mechanism,"Block")).gameObject.activeSelf,"Failed plank lost");Check(Read(player,"GameplayMode").ToString()=="AutoRun","Failure continues AutoRun");Call(player,"Respawn",new Vector3(0,.6f,(float)Read(mechanism,"SectionZ")+3f));phase=Time.time;stage=19;}
   }
   else if(stage==14&&Time.time-phase>.4f){Check((bool)Call(down,"TryClimb",player),"Grab plank swing");releaseZ=Position.z;Keys(Key.W);phase=Time.time;stage=15;}
   else if(stage==15&&Time.time-phase>.4f)
   {
    var plank=(Transform)Read(down,"SwingPlatform");Check(Mathf.Abs(Position.z-plank.position.z)<.02f&&Mathf.Abs(Position.y-plank.position.y-1.35f)<.03f,"Plank and rider synchronized");Check(Mathf.Abs(Position.z-releaseZ)>.02f,"W pumps actual swing");Keys();
    var flags=BindingFlags.NonPublic|BindingFlags.Instance;var t=player.GetType();float a=35f*Mathf.Deg2Rad;t.GetField("swingAngle",flags).SetValue(player,a);t.GetField("swingAngularVelocity",flags).SetValue(player,.95f);
    var anchor=(Vector3)t.GetField("swingAnchor",flags).GetValue(player);var forward=(Vector3)t.GetField("swingForward",flags).GetValue(player);float length=(float)t.GetField("swingLength",flags).GetValue(player);
    player.GetComponent<Rigidbody>().position=(Vector3)Call(down,"DriveSwingPlatform",a,anchor,forward,length);Call(player,"EndSwing",true);phase=Time.time;stage=16;
   }
   else if(stage==16&&Time.time-phase>2f)
   {Check((bool)Read(player,"IsGrounded")&&Mathf.Abs(Position.z-((Vector3)Read(up,"Bottom")).z)<1.7f,"9m release fixture lands without assist");Check((bool)Call(up,"TryClimb",player),"Target rope available after landing");Keys(Key.W);phase=Time.time;stage=17;}
   else if(stage==17&&!(bool)Read(player,"IsClimbing"))
   {Keys();Check(Read(player,"GameplayMode").ToString()=="AutoRun","Climbing target top resumes AutoRun");phase=Time.time;stage=19;}
   else if(stage==19&&Time.time-phase>.3f)
   {if(section==0){section=1;StartThrow();}else{Call(player,"Respawn",new Vector3(0,.6f,171.5f));phase=Time.time;stage=20;}}
   else if(stage==20&&Time.time-phase>1.3f)
   {
    Check((bool)Read(director,"Finished"),"Finish reached");int percent=(int)Read(level,"CompletionPercent");Check(scenario==0?percent==100:percent<100,"Completion independent from score: "+percent);
    if(scenario==0){ScreenCapture.CaptureScreenshot("Assets/ParkourResultVerification.png");SessionState.SetString("ParkourTest.ResultShot","Assets/ParkourResultVerification.png");stage=21;phase=Time.time;}
    else if(scenario<3){scenario++;LoadCase();}else{Finish(true,"");}
   }
   else if(stage==21&&Time.time-phase>.3f){scenario++;LoadCase();}
  }
  catch(System.Exception e){Finish(false,e.ToString());}
 }
 static void Log(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!message.Contains("AssistantPerf"))logs.Add("RUNTIME_ERROR "+message);}
 static void Finish(bool success,string error)
 {
  Application.logMessageReceived-=Log;EditorApplication.update-=Tick;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
  if(originalSettings!=null)UnityEngine.InputSystem.InputSystem.settings=originalSettings;
  if(testSettings!=null)UnityEngine.Object.DestroyImmediate(testSettings);
  Application.runInBackground=originalBackground;
  SessionState.SetString(Result,JsonUtility.ToJson(new Report{success=success,error=error,logs=logs.ToArray()}));SessionState.SetString(State,"Idle");Debug.Log("[ParkourTest] COMPLETE success="+success+" error="+error);EditorApplication.isPlaying=false;
 }
 [System.Serializable] class Report{public bool success;public string error;public string[] logs;}
}
