using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StorePolishBatch
{
    public const string Root="Assets/Art/StorePolish", Report="Library/StorePolishQA";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);
            var catalog=starter.catalog;
            var outputs=new HashSet<string>();
            var door=catalog.GetById("door_01");door.prefab=Opening(false);door.placementBounds=CityUpdateSetup.Measure(door.prefab);
            var gate=catalog.GetById("unloading_gate_01");gate.prefab=Opening(true);gate.placementBounds=CityUpdateSetup.Measure(gate.prefab);
            foreach(var item in new[]{door,gate}){EditorUtility.SetDirty(item);outputs.Add(AssetDatabase.GetAssetPath(item));}
            var bayItem=catalog.GetById("parking_01");
            var bayCopy=(GameObject)PrefabUtility.InstantiatePrefab(bayItem.prefab);
            try
            {
                // Absolute dimensions make authoring repeatable. Occupied cells retain a small safety margin.
                var target=new Vector3(1.9629f*.9f,.006f,3.9633f*.9f);
                var bay=bayCopy.GetComponent<ParkingBay>();
                var measured=CityUpdateSetup.Measure(bayCopy);
                foreach(Transform child in bayCopy.transform)
                {
                    var factor=new Vector3(target.x/measured.size.x,1,target.z/measured.size.z);
                    child.localPosition=Vector3.Scale(child.localPosition,factor);child.localScale=Vector3.Scale(child.localScale,factor);
                }
                bay.width=target.x;bay.length=target.z;
                bayItem.prefab=PrefabUtility.SaveAsPrefabAsset(bayCopy,Root+"/Prefabs/ParkingBay.prefab");
                bayItem.placementBounds=CityUpdateSetup.Measure(bayItem.prefab);EditorUtility.SetDirty(bayItem);outputs.Add(AssetDatabase.GetAssetPath(bayItem));
            }
            finally{Object.DestroyImmediate(bayCopy);}
            var stock=new SerializedObject(starter.blueprint);var config=stock.FindProperty("constructionStock");
            var shelves=catalog.Items.Where(item=>item!=null && item.category==BuildCategory.Shelf && !item.hiddenFromShop).ToArray();
            if(shelves.Length<5)throw new InvalidOperationException("Five retail shelf choices are required.");
            var choices=config.FindPropertyRelative("shelves");choices.arraySize=shelves.Length;
            for(int i=0;i<shelves.Length;i++)choices.GetArrayElementAtIndex(i).objectReferenceValue=shelves[i];
            foreach(var pair in new[]{("floor","floor_cream"),("wall","wall_01"),("door","door_01"),("gate","unloading_gate_01"),("parking","parking_01")})
                config.FindPropertyRelative(pair.Item1).objectReferenceValue=catalog.GetById(pair.Item2);
            stock.ApplyModifiedPropertiesWithoutUndo();outputs.Add(AssetDatabase.GetAssetPath(starter.blueprint));
            var traffic=Object.FindObjectOfType<TownRoadTraffic>(true);
            var network=new System.Text.StringBuilder("entry="+starter.blueprint.streetEntry+" driveway="+starter.blueprint.drivewayEntry+" town="+traffic.transform.position+" scale="+traffic.transform.lossyScale+"\n");
            for(int i=0;i<traffic.Network.Lanes.Count;i++)
            {
                var lane=traffic.Network.Lanes[i];
                if(lane.points.Any(p=>Vector3.Distance(traffic.transform.TransformPoint(p),starter.blueprint.streetEntry)<45))
                    network.AppendLine(i+" "+string.Join(";",lane.points.Select(p=>traffic.transform.TransformPoint(p).ToString())));
            }
            File.WriteAllText(Report+"/streets.txt",network.ToString());
            var parking=Object.FindObjectOfType<ParkingTraffic>(true);var settings=new SerializedObject(parking);
            if(settings.FindProperty("streetTraffic")!=null){settings.FindProperty("streetTraffic").objectReferenceValue=traffic;settings.ApplyModifiedPropertiesWithoutUndo();}
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());outputs.Add(CityUpdateSetup.GamePath);
            var town=PrefabUtility.LoadPrefabContents("Assets/Prefabs/TownWorld/TownEnvironment.prefab");
            try
            {
                var roads=town.GetComponentInChildren<TownRoadTraffic>(true);var data=new SerializedObject(roads);data.FindProperty("maximumCars").intValue=29;data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(town,"Assets/Prefabs/TownWorld/TownEnvironment.prefab");outputs.Add("Assets/Prefabs/TownWorld/TownEnvironment.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(town);}
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Report+"/outputs.txt",outputs.OrderBy(path=>path,StringComparer.Ordinal));
            File.WriteAllText(Report+"/build.txt","PASS flush 1.5 x 1.5 x .5 openings; matching dark wall trim; parking 90%; 29 traffic capacity; configured new-game stock.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static GameObject Opening(bool gate)
    {
        string id=gate?"unloading_gate_01":"door_01";var root=new GameObject(id);
        var wallMaterial=AssetDatabase.LoadAssetAtPath<Material>(MobilityUpdateBatch.Root+"/Walls.mat");
        var townMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        void Part(Transform parent,string name,TownMeshBuilder mesh,Material material,int wallColor=-1)
        {
            var model=mesh.Build(name);
            if(wallColor>=0)model.uv=Enumerable.Repeat(new Vector2((wallColor+.5f)/5,.5f),model.vertexCount).ToArray();
            string path=Root+"/Meshes/"+id+"_"+name+".asset";TrafficFleetBatch.Save(model,path);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        void Trim(string name,Vector3 position,Vector3 size,int color)
        {
            var mesh=new TownMeshBuilder();mesh.Box(position,size,TownColor.Frame);Part(root.transform,name,mesh,wallMaterial,color);
        }
        try
        {
            foreach(int sign in new[]{-1,1})
            {
                Trim(sign<0?"LeftJamb":"RightJamb",new Vector3(sign*.71f,.705f,0),new Vector3(.08f,1.41f,.5f),2);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(sign*.71f,.705f,0);collider.size=new Vector3(.08f,1.41f,.5f);
            }
            Trim("ContinuousTopBand",new Vector3(0,1.455f,0),new Vector3(1.5f,.09f,.5f),4);
            if(gate)
            {
                // Recess the shutter inside the wall thickness, not on the outside face.
                Trim("Shutter",new Vector3(0,.705f,0),new Vector3(1.34f,1.41f,.38f),2);
                var seams=new TownMeshBuilder();
                for(float y=.15f;y<1.4f;y+=.12f)
                    foreach(int side in new[]{-1,1})seams.Box(new Vector3(0,y,side*.195f),new Vector3(1.32f,.009f,.009f),TownColor.Metal);
                Part(root.transform,"ShutterSlats",seams,townMaterial);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.705f,0);collider.size=new Vector3(1.34f,1.41f,.38f);
            }
            else
            {
                foreach(int sign in new[]{-1,1})
                {
                    var hinge=new GameObject(sign<0?"door-left":"door-right").transform;hinge.SetParent(root.transform,false);hinge.localPosition=new Vector3(sign*.67f,0,0);
                    float center=-sign*.33f;var mesh=new TownMeshBuilder();
                    mesh.Box(new Vector3(center,.77f,0),new Vector3(.65f,1.24f,.065f),TownColor.Frame);
                    foreach(int side in new[]{-1,1})
                    {
                        mesh.Box(new Vector3(center,.91f,side*.038f),new Vector3(.55f,.84f,.018f),TownColor.Glass);
                        mesh.Box(new Vector3(center,.30f,side*.04f),new Vector3(.55f,.23f,.02f),TownColor.GreenStripe);
                        mesh.Box(new Vector3(center-sign*.21f,.78f,side*.075f),new Vector3(.025f,.25f,.05f),TownColor.Metal);
                    }
                    Part(hinge,sign<0?"LeftPanel":"RightPanel",mesh,townMaterial);
                }
                root.AddComponent<RetailEmpireTycoon.StoreOperations.StoreDoorway>();
            }
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+id+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void Record(string stage)
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles(Root,"*",SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(path)))
            .Concat(Directory.GetFiles("Assets/Project/Editor","*.cs",SearchOption.TopDirectoryOnly))
            .Distinct().OrderBy(path=>path,StringComparer.Ordinal);
        File.WriteAllLines(Report+"/"+stage+"-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
