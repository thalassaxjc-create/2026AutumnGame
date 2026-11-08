using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
public static class ParkourSystemBuilder
{
    private static Transform Group(string name,Transform parent)
    {
        var obj=new GameObject(name);Undo.RegisterCreatedObjectUndo(obj,"Build bamboo parkour");obj.transform.SetParent(parent,false);return obj.transform;
    }
    private static GameObject Primitive(string name,Transform parent,Vector3 position,Vector3 scale,Material material, bool collision=false)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;Undo.RegisterCreatedObjectUndo(obj,"Build bamboo parkour");obj.transform.SetParent(parent,true);obj.transform.position=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
        if(!collision)Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
    }
    private static Rigidbody Kinematic(GameObject obj)
    {var body=Undo.AddComponent<Rigidbody>(obj);body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;return body;}
    private static ParkourObstacleEvent Event(float z,ObstacleType type,int mask=31,float duration=1f,int[] sequence=null,float phase=0f)
    {return new ParkourObstacleEvent{z=z,type=type,laneMask=mask,duration=duration,gapSequence=sequence??new int[0],phaseOffset=phase,warningTime=type==ObstacleType.LowThread?.8f:.45f};}
    public static void Build()
    {
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity")throw new System.Exception("Prototype only");
        Transform puzzle=null;Canvas canvas=null;InteractionPrompt prompt=null;PuzzleCamera camera=null;
        foreach(var root in scene.GetRootGameObjects())
        {
            if(root.name=="FivePillarPuzzle")puzzle=root.transform;
            foreach(var c in root.GetComponentsInChildren<Canvas>(true))if(c.renderMode==RenderMode.ScreenSpaceOverlay)canvas=c;
            foreach(var c in root.GetComponentsInChildren<InteractionPrompt>(true))prompt=c;
            foreach(var c in root.GetComponentsInChildren<PuzzleCamera>(true))camera=c;
        }
        if(puzzle==null||canvas==null||prompt==null||camera==null)throw new System.Exception("Missing scene/UI dependencies");
        if(puzzle.Find("ParkourSystem")!=null)throw new System.Exception("ParkourSystem already exists; do not duplicate");
        var level=puzzle.GetComponent<FivePillarLevel>();var player=level.Player;
        var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/WorkbenchWood.mat");
        var ropeMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/SoftSilkRope.mat");
        var plankMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/CrescentPlankWood.mat");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Generated/CrescentPlank.asset");
        if(wood==null||ropeMaterial==null||plankMaterial==null||mesh==null)throw new System.Exception("Missing existing theme assets");
        // Existing components are inspected and recorded before touching their copied instances.
        foreach(var c in puzzle.GetComponentsInChildren<Component>(true)){if(c==null)throw new System.Exception("Missing component");Undo.RecordObject(c,"Integrate prototype");}
        foreach(var c in prompt.GetComponents<Component>())Undo.RecordObject(c,"Integrate HUD");
        foreach(var c in camera.GetComponents<Component>())Undo.RecordObject(c,"Integrate camera");
        var levelSettings=new SerializedObject(level);levelSettings.FindProperty("ringObjects").arraySize=0;levelSettings.ApplyModifiedProperties();
        foreach(var ring in puzzle.GetComponentsInChildren<RopeVisual>(true))if(ring.name.StartsWith("Ring_Row"))ring.gameObject.SetActive(false);
        var rootSystem=Group("ParkourSystem",puzzle);
        var director=Undo.AddComponent<ParkourDirector>(rootSystem.gameObject);
        var clock=Undo.AddComponent<BeatClock>(Group("BeatClock",rootSystem).gameObject);
        var score=Undo.AddComponent<ScoreComboController>(Group("ScoreComboController",rootSystem).gameObject);
        var pattern=Undo.AddComponent<WovenPatternRecorder>(Group("WovenPatternRecorder",rootSystem).gameObject);
        pattern.Configure(player,clock,score);
        var hazardRoot=Group("Obstacles",rootSystem);
        var events=new List<ParkourObstacleEvent>{
            Event(10,ObstacleType.StaticRing,17),Event(16,ObstacleType.StaticRing,4),Event(22,ObstacleType.StaticRing,10),
            Event(28,ObstacleType.LowThread),Event(34,ObstacleType.MovingGap,31,1f,new[]{2,3,1}),
            Event(40,ObstacleType.Pendulum,24),Event(46,ObstacleType.LowThread),Event(49.15f,ObstacleType.StaticRing,10),
            Event(82,ObstacleType.StaticRing,21),
            Event(88,ObstacleType.Pendulum,3),Event(88,ObstacleType.Pendulum,24,1,null,.6f),
            Event(96,ObstacleType.LowThread),Event(98,ObstacleType.StaticRing,4),Event(104,ObstacleType.DiagonalThread,27),
            Event(110,ObstacleType.StaticRing,5),Event(113,ObstacleType.StaticRing,18),
            Event(133,ObstacleType.StaticRing,21),Event(138,ObstacleType.StaticRing,10),
            Event(143,ObstacleType.LowThread),Event(158,ObstacleType.Pendulum,3),Event(158,ObstacleType.Pendulum,24,1,null,.6f),
            Event(162,ObstacleType.StaticRing,21),Event(165,ObstacleType.StaticRing,26),Event(168,ObstacleType.StaticRing,15),
            Event(170,ObstacleType.ReedBeater)
        };
        var hazards=new List<ParkourObstacle>();
        foreach(var data in events)
        {
            if(data.type==ObstacleType.Pendulum)
            {
                bool right=(data.laneMask&24)!=0;float x=right?8.4f:-8.4f;
                var group=Group("CrescentPendulum_"+data.z+(right?"_R":"_L"),hazardRoot);group.position=new Vector3(x,5.6f,data.z);Kinematic(group.gameObject);
                var pendulum=Undo.AddComponent<HangingWeightPendulum>(group.gameObject);pendulum.Configure(player,clock,data);
                var plank=Group("StandableCrescent",group);plank.position=group.position+Vector3.down*6.73f;
                Undo.AddComponent<MeshFilter>(plank.gameObject).sharedMesh=mesh;Undo.AddComponent<MeshRenderer>(plank.gameObject).sharedMaterial=plankMaterial;
                var solid=Undo.AddComponent<BoxCollider>(plank.gameObject);solid.size=new Vector3(3.15f,.38f,3.35f);solid.center=new Vector3(0,.58f,0);
                Kinematic(plank.gameObject);var surface=Undo.AddComponent<MovingSurface>(plank.gameObject);
                var sensor=Group("DangerSensor",group);sensor.position=plank.position+Vector3.up*.58f;
                var trigger=Undo.AddComponent<BoxCollider>(sensor.gameObject);trigger.isTrigger=true;trigger.size=new Vector3(3.15f,.8f,1f);
                var rope=Undo.AddComponent<RopeVisual>(Group("SoftTensionRope",group).gameObject);
                rope.Configure(new[]{group.position,plank.position+Vector3.up*.73f},ropeMaterial,.24f);
                var cue=Primitive("TensionWarning",group,new Vector3(x,.08f,data.z-2f),new Vector3(1.5f,.05f,.4f),ropeMaterial).GetComponent<Renderer>();
                pendulum.SetGeometry(surface,rope,sensor,cue,data.z>=158f?30f:35f,data.z>=158f?2f:2.4f);hazards.Add(pendulum);continue;
            }
            if(data.type==ObstacleType.ReedBeater)
            {
                var group=Group("ReedBeater",hazardRoot);var chase=Undo.AddComponent<ReedBeaterChase>(group.gameObject);chase.Configure(player,clock,data);
                var beam=Primitive("WoodenReed",group,Vector3.zero,new Vector3(18f,1.2f,.65f),wood);
                for(int tooth=0;tooth<18;tooth++)Primitive("CombTooth_"+tooth,beam.transform,new Vector3(tooth-8.5f,-.15f,0f),new Vector3(.2f,1.4f,.3f),wood);
                // Children were world-scaled; beam starts hidden and later translates as one assembly.
                chase.SetGeometry(beam.transform);beam.SetActive(false);hazards.Add(chase);continue;
            }
            var gateObject=Group(data.type+"_Z"+data.z,hazardRoot);Kinematic(gateObject.gameObject);
            var gate=Undo.AddComponent<ThreadGateObstacle>(gateObject.gameObject);gate.Configure(player,clock,data);
            var colliders=new Collider[5];var renders=new Renderer[5];var indicators=new Renderer[5];
            for(int lane=0;lane<5;lane++)
            {
                float x=(lane-2)*3.6f;
                var laneRoot=Group("Lane_"+lane,gateObject);laneRoot.position=new Vector3(x,0,data.z);
                var collider=Undo.AddComponent<BoxCollider>(laneRoot.gameObject);collider.isTrigger=true;
                var visual=Undo.AddComponent<RopeVisual>(laneRoot.gameObject);
                bool ring=data.type==ObstacleType.StaticRing||data.type==ObstacleType.MovingGap;
                if(ring)
                {
                    collider.size=new Vector3(2.8f,2.4f,.24f);collider.center=new Vector3(0,1.2f,0);
                    visual.Configure(new[]{new Vector3(x-1.4f,.05f,data.z),new Vector3(x-1.4f,2.2f,data.z),new Vector3(x+1.4f,2.2f,data.z),new Vector3(x+1.4f,.05f,data.z),new Vector3(x-1.4f,.05f,data.z)},ropeMaterial,.2f);
                }
                else
                {
                    float y=data.type==ObstacleType.HighThread?.85f:data.type==ObstacleType.DiagonalThread?(lane<2?.35f:.85f):.35f;
                    collider.size=new Vector3(3.6f,.24f,.24f);collider.center=new Vector3(0,y,0);
                    visual.Configure(new[]{new Vector3(x-1.8f,y,data.z),new Vector3(x+1.8f,y,data.z)},ropeMaterial,.24f);
                }
                colliders[lane]=collider;renders[lane]=visual.GetComponent<LineRenderer>();
                indicators[lane]=Primitive("SafetyCue_"+lane,gateObject,new Vector3(x,.035f,data.z-2.5f),new Vector3(2.7f,.04f,.45f),ropeMaterial).GetComponent<Renderer>();
            }
            gate.SetGeometry(colliders,renders,indicators);hazards.Add(gate);
        }
        var moving=new MovingSurface[5];
        for(int i=0;i<5;i++)
        {
            var pillar=puzzle.Find("FivePillars/Pillar_"+(i+1));Kinematic(pillar.gameObject);moving[i]=Undo.AddComponent<MovingSurface>(pillar.gameObject);
        }
        var warp=Undo.AddComponent<WarpShedController>(Group("WarpShed",hazardRoot).gameObject);warp.Configure(player,clock,moving);
        var pickups=new GameObject[6];var woven=new Transform[2];
        for(int section=0;section<2;section++)
        {
            var mechanism=level.Mechanisms[section];
            for(int i=0;i<3;i++)
            {
                float z=mechanism.Descent.Bottom.z+3f+i*3f;float y=i==2?-4.3f:-2f;
                pickups[section*3+i]=Primitive("WovenThreadReward_"+section+"_"+i,rootSystem,new Vector3(-10.5f,y,z),Vector3.one*.45f,ropeMaterial);pickups[section*3+i].SetActive(false);
            }
            woven[section]=Group("CompletedWeave_"+section,rootSystem);
            for(int row=0;row<8;row++)
            {
                float z=mechanism.SectionZ-12f+row*.8f;var crossing=Undo.AddComponent<RopeVisual>(Group("CrossThread_"+row,woven[section]).gameObject);
                crossing.Configure(new[]{new Vector3(-8f,-.1f,z),new Vector3(8f,-.1f,z+.4f)},ropeMaterial,.08f);
            }
            woven[section].gameObject.SetActive(false);
        }
        var checkpoints=Group("Checkpoints",rootSystem);foreach(float z in new[]{-3f,64f,130f})Group("Checkpoint_Z"+z,checkpoints).position=new Vector3(0,.6f,z);
        const string configPath="Assets/_Game/Data/ParkourLevel_01.asset";
        if(!AssetDatabase.IsValidFolder("Assets/_Game/Data"))AssetDatabase.CreateFolder("Assets/_Game","Data");
        var config=AssetDatabase.LoadAssetAtPath<ParkourLevelConfig>(configPath);
        if(config==null){config=ScriptableObject.CreateInstance<ParkourLevelConfig>();AssetDatabase.CreateAsset(config,configPath);}config.SetObstacles(events.ToArray());EditorUtility.SetDirty(config);
        director.Configure(level,config,clock,score,pattern,hazards.ToArray(),pickups,woven);
        prompt.ConfigureParkour(director);camera.ConfigureParkour(director);
        MoveLabels(prompt);
        BuildHUD(canvas,director);
        foreach(var component in rootSystem.GetComponentsInChildren<Component>(true))EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(prompt);EditorUtility.SetDirty(camera);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        ParkourMechanismUpgrade.MigrateKnots();
        ParkourMechanismUpgrade.InstallButtons();
        ParkourMechanismUpgrade.SplitFallingBamboo();
    }
    private static void MoveLabels(InteractionPrompt prompt)
    {
        var so=new SerializedObject(prompt);var labels=so.FindProperty("pullLabels");
        for(int i=0;i<labels.arraySize;i++)
        {
            var text=labels.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TextMeshProUGUI;if(text==null)continue;
            foreach(var c in text.transform.parent.GetComponents<Component>())Undo.RecordObject(c,"Move throw label");
            var p=text.transform.parent.position;p.z+=(i==0?38f:77f);text.transform.parent.position=p;
        }
        var ropes=so.FindProperty("ropes");var climbs=so.FindProperty("climbLabels");
        for(int i=0;i<Mathf.Min(ropes.arraySize,climbs.arraySize);i++)
        {
            var rope=ropes.GetArrayElementAtIndex(i).objectReferenceValue as ClimbableRope;var text=climbs.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TextMeshProUGUI;if(rope==null||text==null)continue;
            foreach(var c in text.transform.parent.GetComponents<Component>())Undo.RecordObject(c,"Move rope label");
            var p=text.transform.parent.position;p.z+=rope.Top.z>90f?77f:38f;text.transform.parent.position=p;
        }
    }
    private static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,Color color)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));Undo.RegisterCreatedObjectUndo(obj,"Build parkour HUD");obj.transform.SetParent(parent,false);
        var rect=obj.GetComponent<RectTransform>();rect.anchorMin=anchor;rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=pos;rect.sizeDelta=size;
        var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
    }
    private static TMPro.TextMeshProUGUI Text(string name,Transform parent,float size)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));Undo.RegisterCreatedObjectUndo(obj,"Build parkour HUD");obj.transform.SetParent(parent,false);
        var text=obj.GetComponent<TMPro.TextMeshProUGUI>();text.fontSize=size;text.color=new Color(1f,.9f,.65f);text.raycastTarget=false;text.alignment=TMPro.TextAlignmentOptions.Center;
        var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,8);rect.offsetMax=new Vector2(-12,-8);return text;
    }
    private static void BuildHUD(Canvas canvas,ParkourDirector director)
    {
        var stats=Panel("ParkourMetrics",canvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-20),new Vector2(520,96),new Color(.05f,.04f,.03f,.9f));
        var metrics=Text("ScoreComboSpeed",stats,24f);metrics.alignment=TMPro.TextAlignmentOptions.TopLeft;
        var board=Panel("WovenPatternResult",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(760,560),new Color(.05f,.04f,.03f,.96f));
        var titleRoot=Panel("ResultTitle",board,new Vector2(.5f,1),new Vector2(.5f,1),Vector2.zero,new Vector2(740,130),Color.clear);
        var title=Text("ResultText",titleRoot,28f);
        var grid=Panel("FiveWarpPattern",board,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,24),new Vector2(680,390),Color.clear);
        var cells=new UnityEngine.UI.Image[400];
        for(int row=0;row<80;row++)for(int lane=0;lane<5;lane++)
        {
            var cell=Panel("Stitch_"+row+"_"+lane,grid,Vector2.zero,Vector2.zero,new Vector2(lane*136f,row*4.8f),new Vector2(132,3.8f),new Color(.7f,.5f,.2f));cells[row*5+lane]=cell.GetComponent<UnityEngine.UI.Image>();
        }
        var flash=Panel("ParkourHitFlash",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);flash.anchorMax=Vector2.one;flash.offsetMin=Vector2.zero;flash.offsetMax=Vector2.zero;
        var hud=Undo.AddComponent<ParkourHUD>(stats.gameObject);hud.Configure(director,metrics,title,board.gameObject,cells,flash.GetComponent<UnityEngine.UI.Image>());
        board.gameObject.SetActive(false);EditorUtility.SetDirty(hud);
    }
}
