using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
internal static class PlayModeTestRunner
{
    private static List<string> logs = new List<string>();
    private static Component player, level;
    private static Keyboard keyboard;
    private static bool hasRun;
    private static int frames;
    static PlayModeTestRunner()
    {
        string state = SessionState.GetString("PlayModeTest.State", "Idle");
        if (state == "WaitingForCompile") EditorApplication.update += Enter;
        else if (state == "EnteringPlayMode" || state == "InPlayMode")
        {
            EditorApplication.playModeStateChanged += Changed;
            if (EditorApplication.isPlaying) EditorApplication.update += Run;
        }
    }
    private static void Enter()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Enter;
        SessionState.SetString("PlayModeTest.State", "EnteringPlayMode");
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= Changed;
        SessionState.SetString("PlayModeTest.State", "InPlayMode");
        EditorApplication.update += Run;
    }
    private static object Read(Component c, string name) => c.GetType().GetProperty(name).GetValue(c);
    private static object Call(Component c, string name, params object[] args) => c.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, args);
    private static Vector3 Position => (Vector3)Read(player,"Position");
    private static void Check(bool value, string description)
    {
        logs.Add(description + ": " + value + " player=" + Position);
        if (!value) throw new System.Exception(description + " player=" + Position);
    }
    private static void Keys(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        InputSystem.Update();
    }
    private static void Steps(int count)
    {
        for(int i=0;i<count;i++) { Call(player,"FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
    }
    [System.Serializable] private class Result { public bool success; public string error; public string[] logs; }
    private static void Run()
    {
        if (!EditorApplication.isPlaying || hasRun || ++frames < 3) return;
        hasRun=true;EditorApplication.update-=Run;
        var oldMode=Physics.simulationMode; bool oldBackground=Application.runInBackground;
        bool success=false;string error="";
        try
        {
            Application.runInBackground=true; Physics.simulationMode=SimulationMode.Script;
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if(root.GetComponent("PlayerController")!=null)player=root.GetComponent("PlayerController");
                if(root.GetComponent("FivePillarLevel")!=null)level=root.GetComponent("FivePillarLevel");
            }
            keyboard=InputSystem.AddDevice<Keyboard>("ClimbTestKeyboard");
            var input=player.GetComponent<PlayerInput>();input.user.UnpairDevices();UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);
            Keys();Steps(30);
            var camera=Camera.main;
            Check(camera!=null&&!camera.orthographic,"Perspective camera active");
            var cameraFollow=camera.GetComponent("PuzzleCamera");Call(cameraFollow,"SnapToPlayer");
            Check(Vector3.Distance(camera.transform.position,Position)<8f,"Close capsule-centered camera");
            var mechanisms=(System.Array)Read(level,"Mechanisms");
            for(int section=0;section<2;section++)
            {
                var mechanism=(Component)mechanisms.GetValue(section);
                var down=(Component)Read(mechanism,"Descent");var up=(Component)Read(mechanism,"Ascent");
                Call(player,"Respawn",(Vector3)Read(down,"Top"));Keys();Steps(30);
                Check((bool)Read(player,"IsGrounded"),"Standing at left rope top section "+section);
                Check((bool)Call(down,"TryClimb",player),"Enter left descent");
                Keys(Key.S);Steps(110);Keys();Steps(20);
                logs.Add("Post-descent body velocity="+player.GetComponent<Rigidbody>().linearVelocity);
                Check(!(bool)Read(player,"IsClimbing")&&(bool)Read(player,"IsGrounded")&&Mathf.Abs(Position.y+5f)<.15f,"Descent ends standing on original left cube");
                Call(cameraFollow,"SnapToPlayer");
                Check(camera.transform.position.y<0f,"Third-person camera follows below pillars");
                // Fixture for the already-transferred block: test bypass physics separately from swing timing.
                Call(mechanism,"Finish");logs.Add("Transferred fixture configured; this test does not verify swing playback timing.");
                Call(player,"Respawn",(Vector3)Read(up,"Bottom")+Vector3.up*.5f);Keys();Steps(1);
                Check(!(bool)Call(up,"TryClimb",player),"Cannot grab ascent rope before landing on cube");
                Call(player,"Respawn",(Vector3)Read(down,"Bottom")+Vector3.forward*1.1f);Keys();Steps(30);
                Check((bool)Read(player,"IsGrounded"),"Grounded before forward platform jump");
                Keys(Key.W,Key.Space);Steps(1);Keys(Key.W);Steps(37);Keys();Steps(42);
                Check(Position.z>((Vector3)Read(up,"Bottom")).z-1f&&Position.y>-5.2f&&(bool)Read(player,"IsGrounded"),"Forward jump lands on transferred cube");
                // Manual physics advances no game clock, so expire only the input cooldown in the test fixture.
                player.GetType().GetField("climbCooldown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,0f);
                Check((bool)Call(up,"TryClimb",player),"Enter transferred cube's ascent rope after landing");
                Keys(Key.W);Steps(110);Keys();Steps(20);
                Check(!(bool)Read(player,"IsClimbing")&&(bool)Read(player,"IsGrounded")&&Position.z>(float)Read(mechanism,"SectionZ"),"Ascent exits ahead of cross-pillar barrier");
            }
            success=true;
        }
        catch(System.Exception e){error=e.ToString();}
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            Physics.simulationMode=oldMode;Application.runInBackground=oldBackground;
            SessionState.SetString("PlayModeTest.Result",JsonUtility.ToJson(new Result{success=success,error=error,logs=logs.ToArray()}));
            SessionState.SetString("PlayModeTest.State","Done");EditorApplication.isPaused=false;EditorApplication.isPlaying=false;
        }
    }
}
