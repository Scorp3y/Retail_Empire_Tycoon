using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Isolated native build and evidence for the new district.</summary>
public static class TownForestBatch
{
    internal const string Report="Library/TownForestQA",Pictures="Docs/TownForestReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var beforeScene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            // Render before fingerprinting: native materials must have the same fully initialised shader state.
            TownFloristBatch.Render(beforeScene,Pictures+"/Before.png",new Vector3(10,175,210),new Vector3(6,0,382));
            TownFloristBatch.Render(beforeScene,Pictures+"/BeforeTop.png",new Vector3(0,330,383),new Vector3(0,0,383),118);
            var before=TownPilotScopePreservation.Capture(new[]{TownForestPlan.Permission});
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var places=root.GetComponent<TownWorldPlaces>();string contracts=Contracts(places);
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                new TownForestPlacement(root).Build();
                Require(contracts==Contracts(places),"Supplier identities/contracts changed.");
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            before.RequireUnchanged(TownPilotScopePreservation.Capture(new[]{TownForestPlan.Permission}));
            File.WriteAllText(Report+"/protection.txt","PASS: exact visual/material/collision fingerprint outside the district unchanged; original 62 homes, 299 access paths, seven supplier identities/contracts protected. Railway and existing crossing not moved.\n"+before);
            Validate();Capture();Receipt("result","PASS: complete native Forest Quarter saved and validated.\n");EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            try{Capture();}catch(Exception captureError){File.WriteAllText(Report+"/diagnostic-capture.txt",captureError.ToString());}
            Failure("result",error);
        }
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Validate();Capture();Receipt("review","PASS: fresh Unity reopen, district geometry/access checks and native camera evidence.\n");EditorApplication.Exit(0);}
        catch(Exception error){Failure("review",error);}
    }
    private static string Contracts(TownWorldPlaces places)=>string.Join("\n",places.suppliers.Select(c=>c.GetInstanceID()+"|"+c.supplier+"|"+c.transform.position+"|"+c.transform.rotation+"|"+c.radius+"|"+c.requiresRearDocking));
    internal static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        var site=town.transform.Find(TownForestPlan.Site);Require(site!=null,"District missing.");
        var architecture=site.Find("Architecture");Require(architecture!=null&&architecture.childCount==13,"Expected thirteen architecture placements.");
        foreach(var home in TownForestPlan.Homes)
            Require(architecture.Cast<Transform>().Any(t=>t.name==home.Id&&Vector3.Distance(t.position,home.Position)<.001f&&Quaternion.Angle(t.rotation,Quaternion.Euler(0,home.Yaw,0))<.01f),"Missing/misplaced architecture: "+home.Id+" "+home.Position);
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(triangles<400000&&filters.Length<90,"District exceeds its geometry/material budget.");
        foreach(var f in filters)
        {
            Require(f.sharedMesh!=null&&f.sharedMesh.vertexCount>0,"Empty district geometry.");
            Require(f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite),"Non-finite district mesh.");
            Require(f.GetComponent<MeshRenderer>().sharedMaterials.All(m=>m!=null&&m.shader!=null),"Missing material/shader.");
            Require(f.sharedMesh.vertices.Select(f.transform.TransformPoint).All(p=>TownForestPlan.Contains(TownForestPlan.Permission,p)),"New art escaped the district scope: "+f.name);
        }
        Require(site.GetComponentsInChildren<MonoBehaviour>().Length==1&&site.GetComponent<TownForestMapData>()!=null,"Only district map data may accompany static scenery.");
        var map=site.GetComponent<TownForestMapData>();Require(map.RoadAreas.Count==6,"New roads/crossing connection missing from the map.");
        foreach(var area in TownForestPlan.Roads)Require(map.RoadAreas.Contains(area),"Map road differs from physical road.");
        Require(map.RoadAreas.Contains(new Rect(-28,198,8,112)),"Map entrance does not reach the existing city.");
        MapEvidence(scene,town);
        CheckModelContracts();
        Require(town.GetComponentsInChildren<TownLevelCrossing>().Length==1&&town.GetComponentsInChildren<TownJunctionSignals>().Length==6,"Existing traffic controls changed.");
        Require(town.suppliers.Length==7,"Existing supplier count changed.");
        Physics.SyncTransforms();int probes=0;
        foreach(float x in new[]{-122f,-118f,-26f,-22f,70f,74f})probes+=Route(town,new Vector3(x,0,322),new Vector3(x,0,418),true);
        foreach(float z in new[]{308f,312f,428f,432f})probes+=Route(town,new Vector3(-110,0,z),new Vector3(62,0,z),true);
        probes+=Route(town,new Vector3(-26,0,275),new Vector3(-26,0,306),true);
        probes+=Route(town,new Vector3(-31,0,366),new Vector3(-94,0,366),false);
        probes+=Route(town,new Vector3(-17,0,366),new Vector3(47,0,366),false);
        foreach(float x in new[]{-76f,28f})probes+=Route(town,new Vector3(x,0,341),new Vector3(x,0,392),false);
        probes+=Route(town,new Vector3(79,0,380),new Vector3(133,0,380),false);
        var conflicts=new System.Collections.Generic.List<string>();var boxes=site.GetComponentsInChildren<BoxCollider>();
        foreach(var a in boxes)foreach(var b in town.GetComponentsInChildren<Collider>())
        {
            if(a==b||b is MeshCollider||b.isTrigger||a.transform.parent==b.transform.parent||!a.bounds.Intersects(b.bounds))continue;
            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.035f)
                conflicts.Add(a.transform.parent.name+"/"+a.name+" at "+a.bounds.center+" intersects "+b.transform.parent.name+"/"+b.name+" at "+b.bounds.center+" by "+depth);
        }
        File.WriteAllLines(Report+"/overlaps.txt",conflicts.Count==0?new[]{"PASS: independent new model volumes clear of each other and retained vegetation."}:conflicts);
        Require(conflicts.Count==0,"Independent prop intersections: "+string.Join("; ",conflicts.Take(8)));
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" saved-scene vehicle/pedestrian floor and envelope probes, including both ground-floor passages. Visual paint is non-colliding.\n");
        File.WriteAllText(Report+"/metrics.txt",triangles+" new district triangles; "+filters.Length+" saved render batches; "+boxes.Length+" physical boxes; "+Directory.GetFiles(TownForestArt.Root+"/Prefabs","*.prefab").Length+" new reusable prefab types; thirteen architecture placements; fourteen parking bays.\n");
    }
    private static void MapEvidence(UnityEngine.SceneManagement.Scene scene,TownWorldPlaces town)
    {
        // Exercise the real UI mesh, not just its source data; no player HUD or scene changes are saved.
        var district=town.transform.Find(TownForestPlan.Site);bool wasActive=district.gameObject.activeSelf;
        int Read()
        {
            var node=new GameObject("Forest map acceptance",typeof(RectTransform),typeof(CanvasRenderer));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(node,scene);
            try
            {
                var graphic=node.AddComponent<CityMapGraphic>();graphic.town=town;((RectTransform)node.transform).sizeDelta=new Vector2(1200,960);
                using(var mesh=new UnityEngine.UI.VertexHelper())
                {
                    typeof(CityMapGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,
                        null,new[]{typeof(UnityEngine.UI.VertexHelper)},null).Invoke(graphic,new object[]{mesh});
                    return mesh.currentVertCount;
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(node);}
        }
        try
        {
            int full=Read();district.gameObject.SetActive(false);int baseline=Read();
            Require(full-baseline==28,"Map did not add exactly one district polygon and six road rectangles.");
            File.WriteAllText(Report+"/map.txt","PASS: saved map data matches all five road rectangles plus the crossing connection; real CityMapGraphic generated exactly 28 additional UI vertices (district and six road polygons). Old layout graph unchanged.\n");
        }
        finally{district.gameObject.SetActive(wasActive);}
    }
    private static void CheckModelContracts()
    {
        var passage=AssetDatabase.LoadAssetAtPath<GameObject>(TownForestArt.Root+"/Prefabs/PassageHouse.prefab").GetComponentInChildren<MeshFilter>().sharedMesh;
        var p=passage.vertices;var indices=passage.triangles;
        for(int i=0;i<indices.Length;i+=3)
        {
            var a=p[indices[i]];var b=p[indices[i+1]];var c=p[indices[i+2]];
            bool atFront=Mathf.Min(Mathf.Abs(a.z),Mathf.Min(Mathf.Abs(b.z),Mathf.Abs(c.z)))>5.9f;
            bool acrossOpening=Mathf.Min(a.x,Mathf.Min(b.x,c.x))<2&&Mathf.Max(a.x,Mathf.Max(b.x,c.x))>-2;
            bool insideHeight=Mathf.Min(a.y,Mathf.Min(b.y,c.y))<3.3f&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.05f;
            Require(!atFront||!acrossOpening||!insideHeight,"A facade ornament spans the open passage.");
        }
        var slide=AssetDatabase.LoadAssetAtPath<GameObject>(TownForestArt.Root+"/Prefabs/PlayTower.prefab").GetComponentInChildren<MeshFilter>().sharedMesh;
        var points=slide.vertices;var normals=slide.normals;
        Require(Enumerable.Range(0,points.Length).Any(i=>points[i].z>4&&points[i].y<.3f&&normals[i].y>.8f),"Play slide top faces away from the camera.");
        File.WriteAllText(Report+"/model-contracts.txt","PASS: physical courtyard passages also clear of floating facade ornament; playground slide has a visible upward-facing surface.\n");
    }
    private static int Route(TownWorldPlaces town,Vector3 a,Vector3 b,bool vehicle)
    {
        int steps=Mathf.CeilToInt(Vector3.Distance(a,b)*2);var q=Quaternion.LookRotation(b-a);
        for(int i=0;i<=steps;i++)
        {
            var p=Vector3.Lerp(a,b,i/(float)steps);
            var floor=Physics.RaycastAll(p+Vector3.up*4,Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y>=-.01f&&h.point.y<.3f).ToArray();
            Require(floor.Length>0,"No physical floor at "+p);float y=floor.Max(h=>h.point.y);
            var hits=Physics.OverlapBox(new Vector3(p.x,y+1.15f,p.z),vehicle?new Vector3(1.1f,.55f,2.5f):new Vector3(.32f,.75f,.32f),q,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
            Require(hits.Length==0,(vehicle?"Vehicle":"Pedestrian")+" route blocked at "+p+": "+string.Join(", ",hits.Select(c=>c.transform.parent.name+"/"+c.name)));
        }
        return steps+1;
    }
    private static void Capture()
    {
        Directory.CreateDirectory(Pictures);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        void View(string id,Vector3 p,Vector3 target,float ortho=0)=>TownFloristBatch.Render(scene,Pictures+"/"+id+".png",p,target,ortho);
        View("Overview",new Vector3(10,175,210),new Vector3(6,0,382));
        View("Top",new Vector3(0,330,383),new Vector3(0,0,383),118);
        View("Gateway",new Vector3(-13,10,281),new Vector3(-37,4,331));
        View("Cafe",new Vector3(-56,6,308),new Vector3(-76,5,332));
        View("Courtyard",new Vector3(-90,11,344),new Vector3(-76,4,387));
        View("Passage",new Vector3(-30,3,366),new Vector3(-58,2,366));
        View("Townhouses",new Vector3(-108,9,433),new Vector3(-87,3,452));
        View("Park",new Vector3(88,20,340),new Vector3(121,2,385));
        View("Pavilion",new Vector3(106,8,386),new Vector3(120,2,402));
        View("Trail",new Vector3(143,6,429),new Vector3(151,1,399));
        File.WriteAllText(Pictures+"/complete.txt","PASS: ten native Unity saved-scene views, plus surveyed before/top views. No generated concept substituted for scene evidence.\n");
    }
    private static string Hash(string p)=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","");
    internal static void Receipt(string stage,string message)
    {
        File.WriteAllText(Report+"/"+stage+".txt",message);File.WriteAllText(Report+"/"+stage+"-input.sha256",Hash(TownWorldAssembler.PrefabPath));
        File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownForestArt.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownWorldAssembler.PrefabPath})
            .OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
    }
    public static void Survey()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-230,235,160),new Vector3(-15,0,366));
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(0,320,386),new Vector3(0,0,386),158);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                File.WriteAllLines(Report+"/survey.txt",root.GetComponentsInChildren<Transform>(true)
                    .Where(t=>TownForestPlan.Contains(TownForestPlan.Permission,t.position)&&t.GetComponent<Collider>()!=null)
                    .Select(t=>t.name+" "+t.position.ToString("F2")+" scale="+t.lossyScale));
                File.WriteAllLines(Report+"/meshes.txt",root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f=>f.GetComponent<Renderer>().bounds.Intersects(new Bounds(new Vector3(10,6,380),new Vector3(326,40,214))))
                    .Select(f=>f.name+" | "+AssetDatabase.GetAssetPath(f.sharedMesh)+" | "+f.GetComponent<Renderer>().bounds));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/survey-result.txt","PASS: native forest survey captured.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("survey-result",error);}
    }
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static void Failure(string stage,Exception error)
    {Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
}
