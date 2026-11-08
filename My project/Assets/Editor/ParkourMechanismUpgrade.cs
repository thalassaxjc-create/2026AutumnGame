using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
public static class ParkourMechanismUpgrade
{
    private const string Materials="Assets/_Game/Materials/ParkourMechanisms";
    private const string Prefabs="Assets/_Game/Prefabs/Parkour";
    private static void Folder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
    }
    private static ParkourDirector Director()
    {
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity")throw new System.Exception("Upgrade prototype only");
        foreach(var root in scene.GetRootGameObjects())foreach(var d in root.GetComponentsInChildren<ParkourDirector>(true))return d;
        throw new System.Exception("ParkourDirector missing");
    }
    private static BeatClock Clock(ParkourDirector director)
    {return new SerializedObject(director).FindProperty("clock").objectReferenceValue as BeatClock;}
    private static void Inspect(GameObject obj)
    {foreach(var c in obj.GetComponentsInChildren<Component>(true)){if(c==null)throw new System.Exception("Missing component: "+obj.name);Undo.RecordObject(c,"Upgrade bamboo mechanisms");}}
    private static GameObject Group(string name,Transform parent)
    {var obj=new GameObject(name);Undo.RegisterCreatedObjectUndo(obj,"Create bamboo mechanism");obj.transform.SetParent(parent,false);return obj;}
    private static Rigidbody Body(GameObject obj)
    {var body=Undo.AddComponent<Rigidbody>(obj);body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;return body;}
    private static Material Material(string name,Color color)
    {
        Folder(Materials);string path=Materials+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/SoftSilkRope.mat");
        material=new Material(source){name=name};material.color=color;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        AssetDatabase.CreateAsset(material,path);return material;
    }
    private static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collider)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;Undo.RegisterCreatedObjectUndo(obj,"Create bamboo mechanism");obj.transform.SetParent(parent,true);obj.transform.position=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
        if(!collider)Undo.DestroyObjectImmediate(obj.GetComponent<Collider>());return obj;
    }
    private static ParkourObstacleEvent Copy(ParkourObstacleEvent source,ObstacleType type)
    {
        var data=new ParkourObstacleEvent{z=source.z,interactionZ=source.interactionZ,endZ=source.endZ,type=type,laneMask=source.laneMask,activationDistance=source.activationDistance,duration=source.duration,phaseOffset=source.phaseOffset,warningTime=source.warningTime,gapSequence=source.gapSequence==null?new int[0]:(int[])source.gapSequence.Clone()};
        if(type==ObstacleType.Knot&&source.type==ObstacleType.MovingGap)
        {
            data.laneMask=21;
            data.gapSequence=new int[0];
        }
        return data;
    }
    private static void WriteReferences(ParkourDirector director,List<ParkourObstacle> obstacles)
    {
        Undo.RecordObject(director,"Update obstacle references");var so=new SerializedObject(director);var array=so.FindProperty("obstacles");array.arraySize=obstacles.Count;
        for(int i=0;i<obstacles.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=obstacles[i];so.ApplyModifiedProperties();
    }
    private static void Save(ParkourDirector director)
    {
        var config=AssetDatabase.LoadAssetAtPath<ParkourLevelConfig>("Assets/_Game/Data/ParkourLevel_01.asset");Undo.RecordObject(config,"Update mechanism timeline");
        var data=new List<ParkourObstacleEvent>();foreach(var obstacle in director.Obstacles)if(obstacle!=null&&obstacle.gameObject.activeSelf)data.Add(obstacle.Data);config.SetObstacles(data.ToArray());EditorUtility.SetDirty(config);
        foreach(var c in director.GetComponentsInChildren<Component>(true))if(c!=null)EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
    public static GameObject[] MigrateKnots()
    {
        var director=Director();var player=director.Level.Player;var beat=Clock(director);var parent=director.transform.Find("Obstacles");
        var references=new List<ParkourObstacle>(director.Obstacles);var created=new List<GameObject>();
        var laneSurfaces=new MovingSurface[5];var pillars=director.transform.parent.Find("FivePillars");
        for(int lane=0;lane<5;lane++)laneSurfaces[lane]=pillars.Find("Pillar_"+(lane+1)).GetComponent<MovingSurface>();
        var knotMaterial=Material("Knot",new Color(.86f,.80f,.66f));var shadowMaterial=Material("Shadow",new Color(.18f,.14f,.09f));
        Folder(Prefabs);const string path=Prefabs+"/ThreadKnot.prefab";
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(template==null)
        {
            var model=GameObject.CreatePrimitive(PrimitiveType.Sphere);model.name="ThreadKnot";model.transform.localScale=Vector3.one*3f;model.GetComponent<Renderer>().sharedMaterial=knotMaterial;model.GetComponent<SphereCollider>().isTrigger=true;
            template=PrefabUtility.SaveAsPrefabAsset(model,path);Object.DestroyImmediate(model);
        }
        for(int index=0;index<references.Count;index++)
        {
            var old=references[index] as ThreadGateObstacle;
            if(old==null||!old.gameObject.activeSelf||(old.Data.type!=ObstacleType.StaticRing&&old.Data.type!=ObstacleType.MovingGap)||Mathf.Abs(old.Data.z-34f)<.01f)continue;
            Inspect(old.gameObject);
            var obj=Group("ThreadKnots_Z"+old.Data.z,parent);obj.transform.position=new Vector3(0,0,old.Data.z);Body(obj);
            var obstacle=Undo.AddComponent<ThreadKnotObstacle>(obj);var data=Copy(old.Data,ObstacleType.Knot);obstacle.Configure(player,beat,data);
            var models=new Transform[5];var colliders=new Collider[5];var shadows=new Renderer[5];
            for(int lane=0;lane<5;lane++)
            {
                if((data.laneMask&(1<<lane))==0)continue;
                var model=PrefabUtility.InstantiatePrefab(template,EditorSceneManager.GetActiveScene()) as GameObject;Undo.RegisterCreatedObjectUndo(model,"Place thread knot");model.name="Knot_Lane_"+lane;model.transform.SetParent(obj.transform,false);model.transform.position=new Vector3((lane-2)*3.6f,1.5f,data.z);
                models[lane]=model.transform;colliders[lane]=model.GetComponent<Collider>();model.SetActive(true);
                var shadow=Primitive(PrimitiveType.Cylinder,"KnotShadow_"+lane,obj.transform,new Vector3((lane-2)*3.6f,.025f,data.z),new Vector3(2.5f,.012f,2.5f),shadowMaterial,false);shadows[lane]=shadow.GetComponent<Renderer>();shadows[lane].enabled=true;
            }
            obstacle.SetGeometry(models,colliders,shadows,laneSurfaces);EditorUtility.SetDirty(obstacle);references[index]=obstacle;
            WriteReferences(director,references);old.gameObject.SetActive(false);created.Add(obj);
        }
        foreach(var obstacle in references)
        {
            if(obstacle is ThreadGateObstacle && obstacle.Data.type==ObstacleType.DiagonalThread)
            {
                Inspect(obstacle.gameObject);var so=new SerializedObject(obstacle);var cues=so.FindProperty("indicators");
                for(int i=0;i<cues.arraySize;i++){var renderer=cues.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;if(renderer!=null)renderer.enabled=false;}
                cues.arraySize=0;so.ApplyModifiedProperties();
            }
        }
        WriteReferences(director,references);Save(director);return created.ToArray();
    }
    public static GameObject[] InstallButtons()
    {
        var director=Director();var parent=director.transform.Find("Obstacles");
        if(parent.Find("ButtonThreads_Z40")!=null)return new GameObject[0];
        var player=director.Level.Player;var beat=Clock(director);var references=new List<ParkourObstacle>(director.Obstacles);
        var white=Material("White",new Color(.97f,.97f,.90f));var yellow=Material("Yellow",new Color(1f,.78f,.05f));
        Folder(Prefabs);const string path=Prefabs+"/PressureButton.prefab";var template=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(template==null)
        {
            var model=new GameObject("PressureButton");var sensor=model.AddComponent<BoxCollider>();sensor.isTrigger=true;sensor.size=new Vector3(1.55f,.35f,1.55f);sensor.center=new Vector3(0,.10f,0);Body(model);
            Primitive(PrimitiveType.Cube,"Cap",model.transform,new Vector3(0,.025f,0),new Vector3(1.3f,.12f,1.3f),white,false);
            template=PrefabUtility.SaveAsPrefabAsset(model,path);Object.DestroyImmediate(model);
        }
        var obj=Group("ButtonThreads_Z40",parent);obj.transform.position=new Vector3(0,0,40);Body(obj);
        var gate=Undo.AddComponent<ButtonThreadObstacle>(obj);
        gate.Configure(player,beat,new ParkourObstacleEvent{z=40,interactionZ=34,endZ=40,type=ObstacleType.ButtonThreads,laneMask=31,activationDistance=12,warningTime=.8f});
        var left=Group("YellowThread_Left3",obj.transform);
        var leftBox=Undo.AddComponent<BoxCollider>(left);leftBox.center=new Vector3(-3.6f,1.8f,0);leftBox.size=new Vector3(10.8f,3.6f,.28f);leftBox.isTrigger=true;
        var leftRope=Undo.AddComponent<RopeVisual>(left);leftRope.Configure(new[]{new Vector3(-9,.7f,40),new Vector3(1.8f,.7f,40)},yellow,.24f);leftRope.SetStyle(yellow,.24f,.02f);
        var right=Group("WhiteThread_Right2",obj.transform);
        var rightBox=Undo.AddComponent<BoxCollider>(right);rightBox.center=new Vector3(5.4f,1.8f,0);rightBox.size=new Vector3(7.2f,3.6f,.28f);rightBox.isTrigger=true;
        var rightRope=Undo.AddComponent<RopeVisual>(right);rightRope.Configure(new[]{new Vector3(1.8f,.7f,40),new Vector3(9,.7f,40)},white,.24f);rightRope.SetStyle(white,.24f,.02f);
        var whiteObj=PrefabUtility.InstantiatePrefab(template,EditorSceneManager.GetActiveScene()) as GameObject;Undo.RegisterCreatedObjectUndo(whiteObj,"Place white pressure plate");whiteObj.name="WhiteButton_Lane2";whiteObj.transform.SetParent(obj.transform,false);whiteObj.transform.position=new Vector3(-3.6f,0,34);
        var whiteCap=whiteObj.transform.Find("Cap");whiteCap.GetComponent<Renderer>().sharedMaterial=white;var whiteButton=Undo.AddComponent<PressureButton>(whiteObj);whiteButton.Configure(player,whiteCap);
        var yellowObj=PrefabUtility.InstantiatePrefab(template,EditorSceneManager.GetActiveScene()) as GameObject;Undo.RegisterCreatedObjectUndo(yellowObj,"Place yellow pressure plate");yellowObj.name="YellowButton_Lane4";yellowObj.transform.SetParent(obj.transform,false);yellowObj.transform.position=new Vector3(3.6f,0,34);
        var yellowCap=yellowObj.transform.Find("Cap");yellowCap.GetComponent<Renderer>().sharedMaterial=yellow;var yellowButton=Undo.AddComponent<PressureButton>(yellowObj);yellowButton.Configure(player,yellowCap);
        gate.SetGeometry(whiteButton,yellowButton,right,left);
        for(int i=0;i<references.Count;i++)
        {
            if(references[i].Data.type==ObstacleType.MovingGap&&Mathf.Abs(references[i].Data.z-34f)<.01f)
            {var old=references[i];Inspect(old.gameObject);references[i]=gate;WriteReferences(director,references);old.gameObject.SetActive(false);}
            else if(references[i] is HangingWeightPendulum&&Mathf.Abs(references[i].Data.z-40f)<.01f)
            {
                var old=references[i];Inspect(old.gameObject);var position=old.transform.position;position.z=44;old.transform.position=position;old.Data.z=44;old.name="CrescentPendulum_44_R";
                foreach(var rope in old.GetComponentsInChildren<RopeVisual>(true))rope.SetPlaneZ(44);
            }
        }
        if(!references.Contains(gate))references.Add(gate);WriteReferences(director,references);Save(director);return new[]{obj};
    }
    private static PhysicsMaterial SurfaceMaterial()
    {
        Folder(Materials);const string path=Materials+"/BambooNoFriction.physicMaterial";
        var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);if(material!=null)return material;
        material=new PhysicsMaterial("BambooNoFriction"){dynamicFriction=0f,staticFriction=0f,frictionCombine=PhysicsMaterialCombine.Minimum};
        AssetDatabase.CreateAsset(material,path);return material;
    }
    public static void FixSegmentFriction()
    {
        var director=Director();var material=SurfaceMaterial();
        for(int i=4;i<=5;i++)
        {
            var pillar=director.transform.parent.Find("FivePillars/Pillar_"+i);Inspect(pillar.gameObject);
            foreach(var collider in pillar.GetComponentsInChildren<Collider>(true))
                if(collider.name=="FrontSegment"||collider.name=="FallingSegment"||collider.name=="RearSegment")collider.sharedMaterial=material;
        }
        Save(director);
    }
    private static GameObject Section(GameObject template,Transform pillar,string name,float start,float end)
    {
        var obj=PrefabUtility.InstantiatePrefab(template,EditorSceneManager.GetActiveScene()) as GameObject;Undo.RegisterCreatedObjectUndo(obj,"Split affected bamboo");obj.name=name;obj.transform.SetParent(pillar,false);obj.transform.localRotation=Quaternion.identity;
        obj.transform.localScale=new Vector3(1f,(end-start)*.5f/pillar.localScale.y,1f);
        obj.transform.position=new Vector3(pillar.position.x,pillar.position.y,(start+end)*.5f);obj.layer=pillar.gameObject.layer;
        obj.GetComponent<Collider>().sharedMaterial=SurfaceMaterial();return obj;
    }
    public static GameObject[] SplitFallingBamboo()
    {
        var director=Director();var parent=director.transform.Find("Obstacles");if(parent.Find("FallingBamboo_86_96")!=null)return new GameObject[0];
        var bamboo=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Generated/BambooYellow.mat");var crackMaterial=Material("Shadow",new Color(.18f,.14f,.09f));
        Folder(Prefabs);const string path=Prefabs+"/FallingSegment.prefab";var template=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(template==null)
        {
            var model=GameObject.CreatePrimitive(PrimitiveType.Cylinder);model.name="BambooSegment";Object.DestroyImmediate(model.GetComponent<Collider>());model.AddComponent<BoxCollider>().size=new Vector3(1,2,1);model.GetComponent<Renderer>().sharedMaterial=bamboo;
            template=PrefabUtility.SaveAsPrefabAsset(model,path);Object.DestroyImmediate(model);
        }
        var obj=Group("FallingBamboo_86_96",parent);var obstacle=Undo.AddComponent<FallingBambooObstacle>(obj);
        obstacle.Configure(director.Level.Player,Clock(director),new ParkourObstacleEvent{z=86,endZ=96,type=ObstacleType.FallingBamboo,laneMask=24,activationDistance=14f,warningTime=.8f});
        var segments=new MovingSurface[2];var cracks=new List<Renderer>();
        for(int index=0;index<2;index++)
        {
            var pillar=director.transform.parent.Find("FivePillars/Pillar_"+(index+4));Inspect(pillar.gameObject);
            if(pillar.GetComponent<MovingSurface>()==null||pillar.GetComponent<Rigidbody>()==null)throw new System.Exception("Main pillar moving surface missing");
            Section(template,pillar,"FrontSegment",-4,86);
            var middle=Section(template,pillar,"FallingSegment",86,96);Body(middle);segments[index]=Undo.AddComponent<MovingSurface>(middle);
            Section(template,pillar,"RearSegment",96,176);
            pillar.GetComponent<MeshRenderer>().enabled=false;pillar.GetComponent<BoxCollider>().enabled=false;
            foreach(float z in new[]{86.15f,95.85f})
            {
                var crack=GameObject.CreatePrimitive(PrimitiveType.Cube);crack.name="FractureWarning";Undo.RegisterCreatedObjectUndo(crack,"Create fracture cue");
                crack.transform.position=new Vector3(pillar.position.x,.022f,z);crack.transform.localScale=new Vector3(2.8f,.025f,.08f);crack.transform.SetParent(middle.transform,true);
                Undo.DestroyObjectImmediate(crack.GetComponent<Collider>());crack.GetComponent<Renderer>().sharedMaterial=crackMaterial;crack.GetComponent<Renderer>().enabled=false;cracks.Add(crack.GetComponent<Renderer>());
            }
        }
        obstacle.SetGeometry(segments,cracks.ToArray());var references=new List<ParkourObstacle>(director.Obstacles);references.Add(obstacle);WriteReferences(director,references);Save(director);return new[]{obj};
    }
}
