using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;
[InitializeOnLoad]
internal static class ParkourGateTest
{
    const string State="ParkourGate.State",Result="ParkourGate.Result";
    static Component player,director,gate;static Keyboard keyboard;
    static int stage,frames;static float phase;static double started;
    static ParkourGateTest()
    {
        var state=SessionState.GetString(State,"Idle");
        if(state=="Waiting")EditorApplication.update+=Enter;
        if(state=="Entering"){EditorApplication.playModeStateChanged+=Changed;if(EditorApplication.isPlaying)EditorApplication.update+=Tick;}
    }
    static void Enter(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Enter;SessionState.SetString(State,"Entering");EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();}
    static void Changed(PlayModeStateChange change){if(change!=PlayModeStateChange.EnteredPlayMode)return;EditorApplication.playModeStateChanged-=Changed;EditorApplication.update+=Tick;}
    static object Read(Component c,string p)=>c.GetType().GetProperty(p).GetValue(c);
    static object Call(Component c,string p,params object[] args)=>c.GetType().GetMethod(p,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).Invoke(c,args);
    static void Auto(){var t=player.GetType().GetMethod("SetGameplayMode").GetParameters()[0].ParameterType;Call(player,"SetGameplayMode",System.Enum.Parse(t,"AutoRun"));}
    static void Keys(params Key[] keys){InputSystem.EnableDevice(keyboard);InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||++frames<6)return;
        try
        {
            if(stage==0)
            {
                Application.runInBackground=true;started=EditorApplication.timeSinceStartup;
                foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
                {if(c==null)continue;if(c.GetType().Name=="PlayerController")player=c;if(c.GetType().Name=="ParkourDirector")director=c;}
                // Event data is a plain object, not a Component.
                foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
                if(c!=null&&c.GetType().Name=="ThreadGateObstacle"){object d=Read(c,"Data");if((float)d.GetType().GetField("z").GetValue(d)==34f)gate=c;}
                keyboard=InputSystem.AddDevice<Keyboard>("GateTestKeyboard");var input=player.GetComponent<PlayerInput>();input.user.UnpairDevices();UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);
                Call(player,"Respawn",new Vector3(0,.6f,23.5f));Auto();Keys();phase=Time.time;stage=1;
            }
            else if(stage==1&&Time.time-phase>.3f){Keys(Key.Space);phase=Time.time;stage=2;}
            else if(stage==2)
            {
                Keys();Vector3 p=(Vector3)Read(player,"Position");
                if(p.z>29.5f)
                {
                    var scoring=(Component)Read(director,"Scoring");if((int)Read(scoring,"Deaths")!=0)throw new System.Exception("Low rope collision despite jump");
                    Debug.Log("[GateTest] Low rope cleared by SPACE, position="+p);
                    int safe=(int)Read(gate,"SafeLane");Call(player,"Respawn",new Vector3((safe-2)*3.6f,.6f,31f));Auto();phase=Time.time;stage=3;
                }
            }
            else if(stage==3)
            {
                var colliders=(Collider[])gate.GetType().GetField("lanes",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(gate);int open=0;foreach(var c in colliders)if(!c.enabled)open++;
                if(open==0)throw new System.Exception("Moving gap closed all five lanes");
                if(Time.time-phase>.25f){ScreenCapture.CaptureScreenshot("Assets/ParkourGateVerification.png");SessionState.SetString("ParkourGate.Shot","Assets/ParkourGateVerification.png");stage=4;}
            }
            else if(stage==4&&((Vector3)Read(player,"Position")).z>35f)
            {
                var scoring=(Component)Read(director,"Scoring");if((int)Read(scoring,"Deaths")!=0)throw new System.Exception("Moving gap killed player in its previewed safe lane");Finish(true,"Low rope jumps and moving blue gap passes verified.");
            }
            if(EditorApplication.timeSinceStartup-started>12f)throw new System.Exception("Gate test timed out at stage "+stage+" position="+Read(player,"Position"));
        }
        catch(System.Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool success,string message){EditorApplication.update-=Tick;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);SessionState.SetString(Result,"success="+success+" "+message);SessionState.SetString(State,"Idle");Debug.Log("[GateTest] COMPLETE success="+success+" "+message);EditorApplication.isPlaying=false;}
}
