using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
[InitializeOnLoad]
internal static class ParkourBaselineTest
{
 const string State="ParkourBaseline.State", Result="ParkourBaseline.Result";
 static Component player,level,mechanism,down,up;
 static int stage,frames; static float started,phase;
 static readonly List<string> logs=new List<string>();
 static ParkourBaselineTest()
 {
  var state=SessionState.GetString(State,"Idle");
  if(state=="Waiting") EditorApplication.delayCall+=()=>{SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.isPlaying=true;};
  if(state=="Entering") {EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying) EditorApplication.update+=Tick;}
 }
 static void Changed(PlayModeStateChange change) {if(change!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
 static object Read(Component c,string name)=>c.GetType().GetProperty(name).GetValue(c);
 static object Call(Component c,string name,params object[] args)=>c.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
 static void Check(bool ok,string text) {logs.Add(text+": "+ok);if(!ok)throw new System.Exception(text);}
 static Component Locate(string type) {foreach(var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in r.GetComponentsInChildren<Component>(true))if(c!=null&&c.GetType().Name==type)return c;return null;}
 static void Tick()
 {
  if(!EditorApplication.isPlaying||++frames<5)return;
  try {
   if(stage==0) {player=Locate("PlayerController");level=Locate("FivePillarLevel");Check(player!=null&&level!=null,"Baseline components");started=Time.time;mechanism=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(0);down=(Component)Read(mechanism,"Descent");up=(Component)Read(mechanism,"Ascent");Call(player,"Respawn",Read(mechanism,"PullPoint"));stage=1;phase=Time.time;}
   else if(stage==1&&Time.time-phase>.3f) {Check((bool)Call(mechanism,"TryPull",player),"First throw start");Call(mechanism,"ResolveTiming",.5f);stage=2;}
   else if(stage==2&&(bool)Read(mechanism,"IsResolved")) {Check(Read(mechanism,"State").ToString()=="Transferred"&&(bool)Read(mechanism,"BlocksPath"),"Success creates blocking rope");Call(player,"Respawn",Read(down,"Bottom"));phase=Time.time;stage=3;}
   else if(stage==3&&Time.time-phase>.4f) {Check((bool)Read(player,"IsGrounded"),"Standing on crescent plank");Check((bool)Call(down,"TryClimb",player),"Grab swing from plank");phase=Time.time;stage=4;}
   else if(stage==4&&Time.time-phase>.2f) {
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;player.GetType().GetField("swingAngle",flags).SetValue(player,35f*Mathf.Deg2Rad);player.GetType().GetField("swingAngularVelocity",flags).SetValue(player,.95f);
    var body=player.GetComponent<Rigidbody>();var anchor=(Vector3)player.GetType().GetField("swingAnchor",flags).GetValue(player);var forward=(Vector3)player.GetType().GetField("swingForward",flags).GetValue(player);var length=(float)player.GetType().GetField("swingLength",flags).GetValue(player);
    body.position=(Vector3)Call(down,"DriveSwingPlatform",35f*Mathf.Deg2Rad,anchor,forward,length);
    logs.Add("Swing fixture release at 35 degrees, angular speed .95; no auto landing. length="+length);
    Call(player,"EndSwing",true);phase=Time.time;stage=5;
   }
   else if(stage==5&&Time.time-phase>2f) {
    Vector3 pos=(Vector3)Read(player,"Position"),target=(Vector3)Read(up,"Bottom");logs.Add("Ballistic landing position="+pos+" target="+target+" grounded="+Read(player,"IsGrounded"));
    Check((bool)Read(player,"IsGrounded")&&Mathf.Abs(pos.z-target.z)<1.7f&&Mathf.Abs(pos.y-target.y)<.25f,"9m ballistic swing landing");
    Call(player,"Respawn",Read(up,"Top"));phase=Time.time;stage=6;
   }
   else if(stage==6&&Time.time-phase>.4f) {mechanism=(Component)((System.Array)Read(level,"Mechanisms")).GetValue(1);Call(player,"Respawn",Read(mechanism,"PullPoint"));phase=Time.time;stage=7;}
   else if(stage==7&&Time.time-phase>.4f) {Check((bool)Call(mechanism,"TryPull",player),"Second throw start");Call(mechanism,"ResolveTiming",0f);stage=8;}
   else if(stage==8&&(bool)Read(mechanism,"IsResolved")) {Check(Read(mechanism,"State").ToString()=="Failed"&&!(bool)Read(mechanism,"BlocksPath"),"Failure leaves no blocking rope");Check(!((Transform)Read(mechanism,"Block")).gameObject.activeSelf,"Failed plank disappears");Finish(true,"");}
   if(Time.time-started>16f)throw new System.Exception("Baseline timed out at stage "+stage);
  }catch(System.Exception e){Finish(false,e.ToString());}
 }
 static void Finish(bool success,string error) {EditorApplication.update-=Tick;SessionState.SetString(Result,JsonUtility.ToJson(new Report{success=success,error=error,logs=logs.ToArray()}));SessionState.SetString(State,"Idle");EditorApplication.isPlaying=false;}
 [System.Serializable] class Report {public bool success;public string error;public string[] logs;}
}
