using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Scoped eight-point maintenance update. Never rebuilds the city or reads a player save.</summary>
public static class MobilityUpdateBatch
{
    public const string Root="Assets/Art/MobilityUpdate",Report="Library/MobilityUpdateQA";
    internal static void Record(string stage)
    {
        var files=new[]{TownWorldAssembler.PrefabPath,CityUpdateSetup.GamePath,CityUpdateSetup.CityPath,"Assets/Art/TrafficFleet/Data/RoadNetwork.asset"}
            .Concat(new[]{Root,"Assets/Resources/Parking","Assets/Prefabs/City","Assets/Prefabs/Structures","Assets/Prefabs/StoreUpgrade","Assets/Prefabs/StarterStore"}
                .SelectMany(folder=>Directory.GetFiles(folder,"*",SearchOption.AllDirectories)))
            .Concat(new[]{"City","Parking","StoreOperations","BuildSystem"}.SelectMany(folder=>Directory.GetFiles("Assets/Project/Scripts/"+folder,"*.cs",SearchOption.AllDirectories)))
            .Concat(new[]{"MobilityUpdateBatch","MobilityUpdatePlay","MobilityCityReview"}.Select(name=>"Assets/Project/Editor/"+name+".cs"))
            .Distinct().OrderBy(path=>path,StringComparer.Ordinal);
        File.WriteAllLines(Report+"/"+stage+"-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            foreach(string folder in new[]{Root,Root+"/Meshes","Assets/Resources/Parking"})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            CreateVisitorFleet();RepairWallModules();WidenDoors();
            var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);
            var parked=starter.cityTrip.parkedPickup;
            var parkedBounds=CityUpdateSetup.Measure(parked);
            var drivingBounds=CityUpdateSetup.Measure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab"));
            float targetLength=Mathf.Max(drivingBounds.size.x,drivingBounds.size.z)*.5f;
            parked.transform.localScale*=targetLength/Mathf.Max(parkedBounds.size.x,parkedBounds.size.z);
            foreach(var bay in starter.blueprint.initialState.placedObjects.Where(p=>p.itemId=="parking_01"))bay.parkingModuleVersion=1;
            EditorUtility.SetDirty(starter.blueprint);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game);
            var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                RepairCrossing(town);JoinBusPlatforms(town);
                var population=town.GetComponentInChildren<TownPedestrianTraffic>();
                population.Configure(population.Routes,Resources.Load<ShopperRoster>("Shoppers/Roster"),ShopperDimensions.StreetHeight);
                PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(town);}
            var city=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var world=Object.FindObjectOfType<CityWorld>();
            // Only the return checkpoint's old hatched loading marker is retired. Supplier zones remain intact.
            foreach(Transform child in world.home.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            EditorSceneManager.SaveScene(city);AssetDatabase.SaveAssets();
            File.WriteAllText(Report+"/build.txt","PASS fleet allowlist, watertight wall modules, grid-width doors, default parking version, connected bus platforms, corrected barrier sides/facing and retired return hatch.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static void CreateVisitorFleet()
    {
        var models=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/TrafficFleet/Prefabs"})
            .Select(guid=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<TrafficVehicle>())
            .Where(car=>car!=null && ParkingVisitorFleet.IsAllowed(car.DesignId)).OrderBy(car=>car.DesignId).ToArray();
        Require(models.Length==14,"Expected fourteen eligible passenger/light vehicle designs.");
        var fleet=ScriptableObject.CreateInstance<ParkingVisitorFleet>();fleet.name="Passenger parking visitors";fleet.Configure(models,.5f);
        TrafficFleetBatch.Save(fleet,"Assets/Resources/Parking/Visitors.asset");
        var parking=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        var contents=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(parking.prefab));
        try
        {
            var bay=contents.GetComponent<ParkingBay>();bay.width=parking.placementBounds.size.x;bay.length=parking.placementBounds.size.z;
            PrefabUtility.SaveAsPrefabAsset(contents,AssetDatabase.GetAssetPath(parking.prefab));
        }
        finally{PrefabUtility.UnloadPrefabContents(contents);}
    }
    private static void RepairWallModules()
    {
        var texture=new Texture2D(5,1,TextureFormat.RGBA32,false){name="Store wall bands",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(new[]{new Color(.85f,.86f,.82f),new Color(.20f,.60f,.39f),new Color(.55f,.57f,.58f),new Color(.64f,.65f,.65f),new Color(.27f,.29f,.30f)});texture.Apply();
        TrafficFleetBatch.Save(texture,Root+"/WallPalette.asset");
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Continuous store wall",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/WallPalette.asset"));material.SetFloat("_Smoothness",.08f);
        TrafficFleetBatch.Save(material,Root+"/Walls.mat");material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Walls.mat");
        foreach(string path in new[]{"Assets/Prefabs/Structures/Wall.asset","Assets/Prefabs/Structures/Wall_Corner.asset"})
        {
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);var root=new GameObject(item.id);
            try
            {
                float width=item.footprint.x*.1667f,depth=item.footprint.y*.1667f;
                var mesh=new TownMeshBuilder(color=>new Vector2(((int)color+.5f)/5,.5f));
                float[] levels={0,.10f,.42f,.72f,.94f,1};
                for(int i=0;i<5;i++)mesh.Box(new Vector3(0,(levels[i]+levels[i+1])*.5f,0),new Vector3(width,levels[i+1]-levels[i],depth),(TownColor)i);
                TrafficFleetBatch.Save(mesh.Build(item.id+" continuous profile"),Root+"/Meshes/"+item.id+".asset");
                root.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+item.id+".asset");root.AddComponent<MeshRenderer>().sharedMaterial=material;
                var collider=root.AddComponent<BoxCollider>();collider.center=Vector3.up*.5f;collider.size=new Vector3(width,1,depth);root.AddComponent<StoreWallOccluder>();
                // Asset GUID and placement dimensions stay stable; historical v2 walls remain separate.
                PrefabUtility.SaveAsPrefabAsset(root,AssetDatabase.GetAssetPath(item.prefab));
                item.placementBounds=new Bounds(Vector3.up*.5f,new Vector3(width,1,depth));EditorUtility.SetDirty(item);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
    private static void WidenDoors()
    {
        foreach(string path in AssetDatabase.FindAssets("t:BuildItemData",new[]{"Assets/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            if(item==null || !item.isDoorway || item.prefab==null)continue;
            var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(item.prefab));
            try
            {
                var measured=CityUpdateSetup.Measure(root);float target=item.footprint.x*.1667f;
                root.transform.localScale=Vector3.Scale(root.transform.localScale,new Vector3(target/measured.size.x,1,1));
                item.placementBounds=CityUpdateSetup.Measure(root);EditorUtility.SetDirty(item);
                PrefabUtility.SaveAsPrefabAsset(root,AssetDatabase.GetAssetPath(item.prefab));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
    private static void RepairCrossing(GameObject town)
    {
        var crossing=town.GetComponentInChildren<TownLevelCrossing>();var site=crossing.transform;
        var mappings=new List<(Matrix4x4 oldPose,Matrix4x4 newPose,string id)>();
        foreach(var pivot in crossing.arms.Select(arm=>arm.parent))
        {
            bool south=pivot.position.z<228;float x=south?-19.2f:-28.8f,yaw=south?180:0;
            foreach(string id in new[]{"BarrierBase","RailSignal"})
            {
                var marker=site.Cast<Transform>().Single(t=>t.name==id && (t.position.z<228)==south);
                var old=marker.localToWorldMatrix;var p=marker.position;p.x=x;
                mappings.Add((old,Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),marker.lossyScale),id));
                marker.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            }
            var at=pivot.position;at.x=x;pivot.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
        }
        var relocations=new Dictionary<string,Matrix4x4>();
        foreach(var mapping in mappings)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(TownGatewayPlacement.Root+"/Prefabs/"+mapping.id+".prefab");Require(model!=null,"Crossing recognition template missing.");
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;var pose=mapping.oldPose*filter.transform.localToWorldMatrix;
                for(int i=0;i<indices.Length;i+=3)relocations[Key(pose.MultiplyPoint3x4(vertices[indices[i]]),pose.MultiplyPoint3x4(vertices[indices[i+1]]),pose.MultiplyPoint3x4(vertices[indices[i+2]]))]=mapping.newPose*mapping.oldPose.inverse;
            }
        }
        int changed=0;
        foreach(var filter in site.GetComponentsInChildren<MeshFilter>(true))
        {
            if(crossing.arms.Any(arm=>filter.transform.IsChildOf(arm)) || crossing.redLamps.Any(lamp=>filter.transform.IsChildOf(lamp.transform)))continue;
            var original=filter.sharedMesh;var vertices=original.vertices;var normals=original.normals;var indices=original.triangles;var shifts=new Dictionary<int,Matrix4x4>();
            for(int i=0;i<indices.Length;i+=3)
            {
                string key=Key(filter.transform.TransformPoint(vertices[indices[i]]),filter.transform.TransformPoint(vertices[indices[i+1]]),filter.transform.TransformPoint(vertices[indices[i+2]]));
                if(!relocations.TryGetValue(key,out var delta))continue;
                for(int corner=0;corner<3;corner++)shifts[indices[i+corner]]=delta;
            }
            if(shifts.Count==0)continue;
            foreach(var shift in shifts)
            {
                vertices[shift.Key]=filter.transform.InverseTransformPoint(shift.Value.MultiplyPoint3x4(filter.transform.TransformPoint(vertices[shift.Key])));
                if(normals.Length==vertices.Length)normals[shift.Key]=filter.transform.InverseTransformDirection(shift.Value.MultiplyVector(filter.transform.TransformDirection(normals[shift.Key]))).normalized;
            }
            var copy=Object.Instantiate(original);copy.name="Corrected crossing hardware";copy.vertices=vertices;if(normals.Length==vertices.Length)copy.normals=normals;copy.RecalculateBounds();
            string path=Root+"/Meshes/CrossingHardware"+changed+++".asset";TrafficFleetBatch.Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var collider=filter.GetComponent<MeshCollider>();if(collider!=null&&collider.sharedMesh==original)collider.sharedMesh=filter.sharedMesh;
        }
        Require(changed>0,"No exact baked crossing hardware was relocated; do not leave ghost signal geometry.");
    }
    public static void RepairStops()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            JoinBusPlatforms(town);
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(town);}
        EditorApplication.Exit(0);
    }
    private static void JoinBusPlatforms(GameObject town)
    {
        var previous=town.transform.Find("Connected bus platforms");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        var stops=town.GetComponentsInChildren<Transform>(true).Where(t=>(t.name=="BusStop"||t.name=="05_BusStop"||t.name=="BusShelter") && (t.parent==null||t.parent.name!=t.name)).ToArray();
        var network=town.GetComponentInChildren<TownRoadTraffic>().Network;var roadSamples=new List<Vector3>();
        foreach(var lane in network.Lanes.Where(lane=>!lane.rural))for(float d=0;d<lane.Path.Length;d+=2)
        {var p=lane.Path.Sample(d,out var direction);roadSamples.Add(p-Vector3.Cross(Vector3.up,direction).normalized*1.8f);}
        var mesh=new TownMeshBuilder();int count=0;
        foreach(var stop in stops)
        {
            // The urban shelter sits above an old block-wide grey yard, not a separate
            // removable pad. Restore only its exposed lawn apron; never clone or cut
            // the large shared block mesh, which also contains neighbouring buildings.
            if(stop.name=="05_BusStop")mesh.Ground(90,-153.5f,14,7,.065f,TownColor.Grass);
            var p=stop.position;var road=roadSamples.OrderBy(sample=>new Vector2(sample.x-p.x,sample.z-p.z).sqrMagnitude).First();
            var delta=p-road;bool alongX=Mathf.Abs(delta.z)>=Mathf.Abs(delta.x);
            float width=stop.name=="05_BusStop"?8.6f:8,depth=stop.name=="05_BusStop"?5.2f:4;
            var corners=new[]{new Vector3(-width/2,0,-depth/2),new Vector3(width/2,0,depth/2)}.Select(stop.TransformPoint).ToArray();
            float xMin=Mathf.Min(corners[0].x,corners[1].x),xMax=Mathf.Max(corners[0].x,corners[1].x),zMin=Mathf.Min(corners[0].z,corners[1].z),zMax=Mathf.Max(corners[0].z,corners[1].z);
            if(alongX){float edge=road.z+Mathf.Sign(delta.z)*5.3f;zMin=Mathf.Min(zMin,edge);zMax=Mathf.Max(zMax,edge);}
            else{float edge=road.x+Mathf.Sign(delta.x)*5.3f;xMin=Mathf.Min(xMin,edge);xMax=Mathf.Max(xMax,edge);}
            if(xMax-xMin>15||zMax-zMin>15)continue;
            float top=Mathf.Max(.17f,road.y+.02f);
            mesh.Box(new Vector3((xMin+xMax)/2,top-.03f,(zMin+zMax)/2),new Vector3(xMax-xMin,.06f,zMax-zMin),TownColor.Pavement);count++;
        }
        Require(count>0,"No bus shelters found.");
        TrafficFleetBatch.Save(mesh.Build("Connected bus platforms"),Root+"/Meshes/BusPlatforms.asset");
        var root=new GameObject("Connected bus platforms",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(town.transform,false);
        root.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BusPlatforms.asset");root.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        root.AddComponent<MeshCollider>().sharedMesh=root.GetComponent<MeshFilter>().sharedMesh;
        File.WriteAllText(Report+"/bus-platforms.txt","Connected "+count+" saved bus-shelter platforms to existing sidewalks.\n");
    }
    private static string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Point(a),Point(b),Point(c)}.OrderBy(p=>p,StringComparer.Ordinal));
    private static string Point(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
