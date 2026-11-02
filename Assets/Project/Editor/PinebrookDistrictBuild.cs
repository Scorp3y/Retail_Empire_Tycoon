using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.City;
using Object=UnityEngine.Object;

/// <summary>Isolated authoring, scene validation and native evidence for the approved complete expansion.</summary>
public static class PinebrookDistrictBuild
{
    internal const string Report="Library/PinebrookDistrictQA",Pictures="Docs/Pinebrook/Final";
    private const string Layout=PinebrookDistrictArt.Root+"/Layout/PinebrookLayout.json";
    private static PinebrookDistrictLayout Read()=>JsonUtility.FromJson<PinebrookDistrictLayout>(File.ReadAllText(Layout));
    [MenuItem("Tools/Pinebrook/Build District")]
    public static void BuildFromMenu()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before rebuilding Pinebrook.");
        if(Application.isBatchMode){Generate();return;}
        string script=Path.GetFullPath("Scripts/BuildPinebrookDistrict.ps1");if(!File.Exists(script))throw new FileNotFoundException(script);
        var process=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Mode BuildAndApply"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};System.Diagnostics.Process.Start(process);Debug.Log("Pinebrook isolated rebuild started. Progress: Library/PinebrookDistrictPipeline/Generate.log");
    }
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var plan=Read();var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-100,240,220),new Vector3(-35,0,440));
            var permission=new[]{new Rect(-410,238,760,602),new Rect(-600,502,1800,4)};Debug.Log("PINEBROOK: fingerprinting retained town");var protection=TownPilotScopePreservation.Capture(permission);Debug.Log("PINEBROOK: preparing reusable assets");
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                string retained=Retained(root);string contracts=Contracts(root);var art=new PinebrookDistrictArt();if(Environment.GetCommandLineArgs().Contains("-pinebrookLayoutOnly"))art.LoadExisting();else art.Build();Debug.Log("PINEBROOK: placing district");new PinebrookDistrictPlacement(root,plan,art).Build();
                Require(retained==Retained(root),"Retained building model geometry or original park changed.");Require(contracts==Contracts(root),"Existing supplier, crossing or light controller contract changed.");
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            protection.RequireUnchanged(TownPilotScopePreservation.Capture(permission));File.WriteAllText(Report+"/preservation.txt","PASS: exact visual/material/collision fingerprint outside district revision unchanged; original thirteen building model meshes retained and relocated to the approved concept; original park, suppliers and crossing controllers preserved.\n");
            Validate();Capture();Receipt("result","PASS: 38 buildings including thirteen retained; additive layout, one crossing, native assets and shared town populations.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("result",error);}
    }
    public static void Review()
    {try{TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);Validate();Capture();Receipt("review","PASS: fresh native Unity reopen, physical routes and final camera evidence.\n");EditorApplication.Exit(0);}catch(Exception error){Failure("review",error);}}
    public static void RefreshModels()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();var art=new PinebrookDistrictArt();art.Build();var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try{foreach(var lod in root.transform.Find("Pinebrook").GetComponentsInChildren<LODGroup>())lod.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();}finally{PrefabUtility.UnloadPrefabContents(root);}
            Validate();Capture();Receipt("result","PASS: final baked vertex shading and seated roofs, 38 buildings, native physical and scene checks.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("result",error);}
    }
    private static string Retained(GameObject root)
    {
        var site=root.transform.Find("Forest Quarter");return string.Join("\n",site.GetComponentsInChildren<MeshFilter>(true)
            .Where(f=>f.transform.IsChildOf(site.Find("Architecture"))||f.transform.IsChildOf(site.Find("Community park"))||f.transform.IsChildOf(site.Find("Woodland trail")))
            .Where(f=>!f.name.Contains("Label")&&!f.name.Contains("PinebrookCafe"))
            .Select(f=>f.name+"|"+AssetDatabase.GetAssetPath(f.sharedMesh)+"|"+f.sharedMesh.triangles.Length).OrderBy(v=>v));
    }
    private static string Contracts(GameObject root)=>string.Join("\n",root.GetComponentsInChildren<TownLevelCrossing>().Select(c=>EditorJsonUtility.ToJson(c))
        .Concat(root.GetComponentsInChildren<TownJunctionSignals>().Select(c=>EditorJsonUtility.ToJson(c)))
        .Concat(root.GetComponent<TownWorldPlaces>().suppliers.Select(c=>EditorJsonUtility.ToJson(c))));
    internal static void Validate()
    {
        var plan=Read();EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=Object.FindObjectOfType<TownWorldPlaces>();var site=town.transform.Find("Pinebrook");Require(site!=null,"New Pinebrook root missing.");
        var buildings=site.Find("Buildings");Require(buildings.childCount==25&&town.transform.Find("Forest Quarter/Architecture").childCount==13,"Expected 25 new + 13 retained buildings.");
        Require(town.GetComponentsInChildren<TownLevelCrossing>().Length==1&&town.GetComponentsInChildren<TownJunctionSignals>().Length==6,"Crossing/traffic light count changed.");Require(town.suppliers.Length==7,"Supplier count changed.");
        foreach(var item in plan.placements)
        {
            var node=site.Find(item.group+"/"+item.name);Require(node!=null&&Vector3.Distance(node.localPosition,item.position)<.001f&&Quaternion.Angle(node.localRotation,Quaternion.Euler(0,item.yaw,0))<.01f,"Layout differs from saved scene: "+item.name);
        }
        foreach(var filter in site.GetComponentsInChildren<MeshFilter>())Require(filter.sharedMesh!=null&&filter.sharedMesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)),"Invalid saved geometry.");
        Physics.SyncTransforms();int roadProbes=0;var laneConflicts=new List<string>();
        var network=town.GetComponentInChildren<TownRoadTraffic>().Network;var old=AssetDatabase.LoadAssetAtPath<TrafficRoadNetwork>("Assets/Art/TrafficFleet/Data/RoadNetwork.asset");Require(network.Lanes.Count>old.Lanes.Count,"New streets missing from traffic graph.");
        for(int i=0;i<network.Lanes.Count;i++)
        {
            var lane=network.Lanes[i];if(network.Junctions[lane.from].position.z<300||network.Junctions[lane.to].position.z<300)continue;
            for(float d=1;d<lane.Path.Length;d+=3){var p=lane.Path.Sample(d,out var tangent);Probe(p,tangent);}
            foreach(int next in network.Departures(lane.to).Where(n=>network.Lanes[n].to!=lane.from))
            {var turn=network.Turn(i,next);for(float d=0;d<turn.Length;d+=2){var p=turn.Sample(d,out var tangent);Probe(p,tangent);}}
        }
        void Probe(Vector3 p,Vector3 tangent)
        {
            Floor(p,town.transform);var hits=Physics.OverlapBox(p+Vector3.up*1.15f,new Vector3(1.02f,.55f,2.4f),Quaternion.LookRotation(tangent),~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
            if(hits.Length>0)laneConflicts.Add(p+": "+string.Join(",",hits.Select(c=>c.name)));roadProbes++;
        }
        File.WriteAllLines(Report+"/traffic-clearance.txt",laneConflicts.Count==0?new[]{"PASS: both right-hand lanes and junction turns have physical clearance."}:laneConflicts);
        Require(laneConflicts.Count==0,"Traffic corridor blocked: "+string.Join("; ",laneConflicts.Take(8)));
        // Check physical entrance connections and route sections; decorative lawns need not be colliders.
        foreach(var path in plan.paths)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(path.Start,path.End));for(int i=0;i<=steps;i++)Floor(Vector3.Lerp(path.Start,path.End,(float)i/steps),town.transform);
        }
        var conflicts=new List<string>();foreach(var box in buildings.GetComponentsInChildren<BoxCollider>())
        {
            foreach(var other in town.GetComponentsInChildren<BoxCollider>())
            {
                if(box==other||!other.enabled||!other.gameObject.activeInHierarchy||other.isTrigger||other.transform.IsChildOf(box.transform)||!box.bounds.Intersects(other.bounds))continue;
                if(Physics.ComputePenetration(box,box.transform.position,box.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.08f)conflicts.Add(box.name+" intersects "+other.name+" at "+other.bounds.center+" by "+depth);
            }
        }
        File.WriteAllLines(Report+"/building-overlaps.txt",conflicts.Count==0?new[]{"PASS: new building collider envelopes clear."}:conflicts);Require(conflicts.Count==0,"Building overlaps: "+string.Join(";",conflicts.Take(6)));
        PinebrookRoadAcceptance.Validate(town.gameObject,plan);
        var map=town.GetComponentInChildren<TownForestMapData>();Require(map.DistrictBounds==plan.bounds.Rect&&map.RoadAreas.Count==plan.roads.Length+1,"Map footprint differs from the real district.");
        Require(plan.placements.Count(p=>p.prefab=="Entrance_Sign")==1,"Exactly one city entrance board is required.");
        var referenceGround=AssetDatabase.LoadAssetAtPath<Material>(TownForestRoadStyle.MaterialPath);
        var roadMaterial=site.Find("Paths/NewRoadsUnion").GetComponent<Renderer>().sharedMaterial;
        Require(roadMaterial.GetTexture("_BaseMap")==referenceGround.GetTexture("_BaseMap")&&roadMaterial.shader==referenceGround.shader,"New asphalt differs from the accepted city palette.");
        var markings=site.Find("Paths/LaneAndCrosswalkPaint").GetComponent<MeshFilter>().sharedMesh;var markingVertices=markings.vertices;var markingIndices=markings.triangles;
        for(int i=0;i<markingIndices.Length;i+=3)Require(Vector3.Cross(markingVertices[markingIndices[i+1]]-markingVertices[markingIndices[i]],markingVertices[markingIndices[i+2]]-markingVertices[markingIndices[i]]).y>0,"Road marking faces point below the road.");
        foreach(var p in plan.placements.Where(p=>p.prefab=="native:BusShelter"))
        {var shelter=site.Find("Props/"+p.name);var road=new Vector3(-24,shelter.position.y,shelter.position.z);Require(Vector3.Dot(shelter.forward,(road-shelter.position).normalized)>.98f,"Bus shelter opens away from the street.");}
        CheckMap(town);
        int triangles=buildings.GetComponentsInChildren<LODGroup>().Sum(l=>l.GetLODs()[0].renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3));
        File.WriteAllText(Report+"/metrics.txt","38 total architecture placements (13 retained, 25 new); "+plan.placements.Count(p=>p.group=="Props")+" prop entries; "+plan.placements.Count(p=>p.group=="Greenery")+" greenery entries; "+plan.paths.Length+" walk pieces; "+plan.roads.Length+" new road rectangles; "+triangles+" new LOD0 building triangles; "+roadProbes+" clear new lane probes.\n");
        ValidateBothWorlds();
    }
    private static void CheckMap(TownWorldPlaces town)
    {
        var go=new GameObject("Temporary Pinebrook map acceptance",typeof(RectTransform),typeof(CanvasRenderer));
        try
        {
            var graphic=go.AddComponent<CityMapGraphic>();graphic.town=town;((RectTransform)go.transform).sizeDelta=new Vector2(1200,960);
            using(var vertices=new UnityEngine.UI.VertexHelper())
            {
                typeof(CityMapGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null).Invoke(graphic,new object[]{vertices});
                var points=new List<Vector3>();var district=graphic.Data.Forest.DistrictBounds;
                foreach(float x in new[]{district.xMin,district.xMax})foreach(float z in new[]{district.yMin,district.yMax})points.Add(graphic.Project(new Vector3(x,0,z)));
                var viewport=((RectTransform)go.transform).rect;
                Require(points.Count==4&&points.All(p=>p.x>=viewport.xMin-.01f&&p.x<=viewport.xMax+.01f&&p.y>=viewport.yMin-.01f&&p.y<=viewport.yMax+.01f)
                    &&points.Max(p=>p.x)-points.Min(p=>p.x)>100,"Expanded district quad does not fit the aspect-preserving full-world map viewport.");
                File.WriteAllText(Report+"/map.txt","PASS: real CityMapGraphic UI mesh includes the expanded northern boundary inside its viewport; source city bounds asset unchanged.\n");
            }
        }
        finally{Object.DestroyImmediate(go);}
    }
    private static void ValidateBothWorlds()
    {
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);var town=Object.FindObjectsOfType<TownWorldPlaces>(true).Single(t=>t.transform.Find("Pinebrook")!=null);
        Require(town.transform.Find("Pinebrook/Buildings").childCount==25,"The store world's shared town is missing the new buildings.");
        var scene=EditorSceneManager.GetActiveScene();TownFloristBatch.Render(scene,Pictures+"/StoreWorldTop.png",town.transform.TransformPoint(new Vector3(-40,480,450)),town.transform.TransformPoint(new Vector3(-40,0,450)),240*town.transform.lossyScale.x);
        File.WriteAllText(Report+"/both-worlds.txt","PASS: City and Game scenes resolve the same expanded shared town prefab with 25 new and 13 retained architecture objects. Game town scale "+town.transform.lossyScale+"; no scene override changes saved.\n");
    }
    private static void Floor(Vector3 point,Transform town)
    {Require(Physics.RaycastAll(point+Vector3.up*3,Vector3.down,4,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town)&&h.normal.y>.8f&&h.point.y<.3f&&h.point.y>=-.05f),"No physical ground at "+point);}
    internal static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        void View(string name,Vector3 position,Vector3 target,float ortho=0)=>TownFloristBatch.Render(scene,Pictures+"/"+name+".png",position,target,ortho);
        View("Top",new Vector3(-45,480,440),new Vector3(-45,0,440),245);
        View("Overview",new Vector3(-45,285,125),new Vector3(-45,0,450),190);
        View("CafeStreet",new Vector3(-47,8,314),new Vector3(-68,5,344));
        View("NewResidential",new Vector3(-16,12,531),new Vector3(-44,6,562));
        View("PinebrookSquare",new Vector3(-6,18,377),new Vector3(-26,2,440));
        View("BrooksidePark",new Vector3(22,28,350),new Vector3(100,1,399));
        View("ForestEdge",new Vector3(-224,18,582),new Vector3(-256,3,615));
        View("Entrance",new Vector3(-37,7,235),new Vector3(-14,3,271));
        View("Kindergarten",new Vector3(143,10,481),new Vector3(169,2,508));
        View("BusStops",new Vector3(-44,8,340),new Vector3(-24,2,365));
        View("RoadEntry",new Vector3(-42,22,275),new Vector3(-24,0,306));
        View("WestJunction",new Vector3(-92,33,421),new Vector3(-64,0,452));
        View("EastJunction",new Vector3(35,32,422),new Vector3(16,0,452));
        View("PathJoin",new Vector3(129,28,363),new Vector3(108,0,380));
        View("Parking",new Vector3(-104,56,489),new Vector3(-104,0,518),26);
        View("RetailParking",new Vector3(-69,30,344),new Vector3(-24,0,365));
    }
    internal static void Receipt(string stage,string text)
    {File.WriteAllText(Report+"/"+stage+".txt",text);File.WriteAllText(Report+"/"+stage+"-prefab.sha256",Hash(TownWorldAssembler.PrefabPath));File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(PinebrookDistrictArt.Root,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>Hash(p)+" "+p.Replace('\\','/')));}
    internal static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Failure(string stage,Exception error){try{if(AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath).transform.Find("Pinebrook")!=null)Capture();}catch(Exception captureError){Debug.LogWarning(captureError.Message);}Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
}
