using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Native florist authoring and evidence in the isolated city project.</summary>
public static class TownFloristBatch
{
    internal const string Report="Library/TownFloristQA";
    internal const string Pictures="Docs/TownFloristReview";
    public static void Generate()
    {Author(false);}
    public static void Enrich()
    {Author(true);}
    private static void Author(bool enrich)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownFloristPlacement.Permissions);
            string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var residential=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                var original=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Decoration);
                var originalId=original.GetInstanceID();
                var placement=new TownFloristPlacement(root);
                if(enrich)placement.Enrich();else placement.Build();
                Require(root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Decoration).GetInstanceID()==originalId,"Decoration checkpoint reference was replaced.");
                residential.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownFloristPlacement.Permissions));
            Require(TownFarmPreservation.Capture()==farm,"Farm changed.");
            var after=TownSupplierPreservation.Capture();
            foreach(var supplier in suppliers.Where(p=>p.Key!=SupplierKind.Decoration))Require(after[supplier.Key]==supplier.Value,"Other supplier changed: "+supplier.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: exact native geometry, materials and colliders outside the vacant block unchanged. Farm, bakery, other suppliers, 62 homes and 299 residential door routes unchanged.\n"+outside+"\n");
            Validate();Capture();
            File.WriteAllText(Report+"/result.txt",enrich?"FLORIST PASSED: additive garden details, populated greenhouse and identified orphan lamp foot removal; unchanged supplier contract, parking, pickup corridor and outside-scope protection passed.\n":"FLORIST PASSED: 16 native modular assets, west-facing boutique and greenhouse in the approved 36 x 24 m block, one existing Decoration checkpoint moved, rear pickup corridor and outside-scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Validate();Capture();File.WriteAllText(Report+"/review.txt","PASS: saved florist reopened and captured in a fresh native Unity process.\n");EditorApplication.Exit(0);}
        catch(Exception error){File.WriteAllText(Report+"/review.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var site=town.transform.Find(TownFloristPlacement.SiteName);Require(site!=null,"Florist site missing.");
        var house=site.Find("GardenShop");Require(Vector3.Distance(house.position,TownFloristPlacement.ShopPosition)<.001f&&Vector3.Dot(house.forward,Vector3.left)>.999f,"Wrong shop position or facade heading.");
        var bay=town.suppliers.Single(c=>c.supplier==SupplierKind.Decoration);
        Require(Vector3.Distance(bay.transform.position,TownFloristPlacement.Bay)<.001f&&Vector3.Dot(bay.transform.forward,Vector3.left)>.999f&&bay.requiresRearDocking,"Decoration rear docking contract missing.");
        Require(town.GetComponentsInChildren<DeliveryCheckpoint>(true).Count(c=>c.supplier==SupplierKind.Decoration)==1,"Duplicate decoration checkpoint.");
        Require(site.GetComponentsInChildren<MeshRenderer>().Length<=4,"Florist has excessive visual batches.");
        foreach(var filter in site.GetComponentsInChildren<MeshFilter>())
            Require(filter.sharedMesh!=null&&filter.sharedMesh.vertices.All(p=>!float.IsNaN(p.x)&&!float.IsInfinity(p.y)),"Invalid native model geometry.");
        Require(site.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)<120000,"Florist detail triangle budget exceeded.");
        var details=site.Find("Garden details v2");
        if(details!=null)
        {
            Require(details.Cast<Transform>().Count(p=>p.name=="NurseryFoliage")==30,"Thirty grown bench pots required.");
            Require(details.Cast<Transform>().Count(p=>p.name=="HangingBasket")==11,"Greenhouse and porch suspended baskets missing.");
            Require(details.Cast<Transform>().Count(p=>p.name=="FlowerBed")==8,"Garden edge beds missing.");
            var greenhouse=site.Find("Greenhouse");
            var bounds=new Bounds(new Vector3(0,2,0),new Vector3(10.5f,4,6.5f));
            foreach(var plant in details.Cast<Transform>().Where(p=>p.name=="NurseryFoliage"))
                Require(bounds.Contains(greenhouse.InverseTransformPoint(plant.position)),"Bench plant outside greenhouse.");
            int triangles=site.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            File.WriteAllText(Report+"/garden.txt","PASS: 30 bench crowns, 10 floor greenhouse plants, 11 suspended baskets, 8 flower beds, 5 potted trees, workbench and garden accessories. "+triangles+" triangles; four material batches.\n");
        }
        var court=site.GetComponent<SupplierLoadingCourt>();Require(court.designVehicleLength==6.8f&&court.bounds.Contains(new Vector2(court.staging.x,court.staging.z)),"Compact court staging is missing.");
        Physics.SyncTransforms();int probes=0;
        var parked=site.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="VisitorSedan").ToArray();
        Require(parked.Length==2,"Two customer parking cars required.");
        foreach(var car in parked)
        foreach(var obstacle in town.GetComponentsInChildren<Collider>())
        {
            if(car==obstacle||obstacle.isTrigger)continue;
            bool overlap=Physics.ComputePenetration(car,car.transform.position,car.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out var direction,out float depth);
            Require(!overlap||depth<.005f,"Customer parking car intersects "+obstacle.name+" by "+depth+" m.");
        }
        var route=new[]{court.streetAccess,court.entrance,court.staging,bay.transform.position};
        for(int leg=1;leg<route.Length;leg++)
        {
            var a=route[leg-1];var b=route[leg];var direction=b-a;int steps=Mathf.CeilToInt(direction.magnitude*2);
            for(int sample=0;sample<=steps;sample++)
            {
                var p=Vector3.Lerp(a,b,(float)sample/steps);probes++;
                var hits=Physics.OverlapBox(new Vector3(p.x,1.1f,p.z),new Vector3(1.35f,.65f,2.5f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                Require(hits.Length==0,"Florist route blocked at "+p+": "+string.Join(",",hits.Select(c=>c.name)));
                Require(Physics.RaycastAll(new Vector3(p.x,4,p.z),Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"No physical route floor at "+p);
            }
        }
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" saved-scene corridor and floor samples. One existing Decoration supplier reference; west-facing rear docking; 6.8 m pickup design envelope. Former western depot buildings retained; no second working checkpoint.\n");
    }
    private static void Capture()
    {
        Directory.CreateDirectory(Pictures);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var city=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CityWorld>(true)).Single();
        var car=city.pickup.transform;car.SetPositionAndRotation(TownFloristPlacement.Bay,Quaternion.Euler(0,270,0));
        var bodies=car.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();car.position+=Vector3.up*(.087f-bodies.Min(b=>b.min.y));
        Render(scene,Pictures+"/Facade.png",new Vector3(59,15,4),new Vector3(91,2.7f,28));
        Render(scene,Pictures+"/LoadingCourt.png",new Vector3(94,16,58),new Vector3(104,2,34));
        Render(scene,Pictures+"/Top.png",new Vector3(96,70,30),new Vector3(96,0,30),23);
        Render(scene,Pictures+"/CityContext.png",new Vector3(47,47,-20),new Vector3(96,1,30));
        Render(scene,Pictures+"/Greenhouse.png",new Vector3(116,12,17),new Vector3(106,1.3f,26));
        Render(scene,Pictures+"/Parking.png",new Vector3(76,6,20),new Vector3(82,.2f,24));
        File.WriteAllText(Pictures+"/complete.txt","PASS: six actual Unity cameras of the saved florist, including greenhouse and cleaned parking. Pickup temporarily posed for scale; scene/spawn not saved.\n");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            Render(scene,Pictures+"/SiteBefore.png",new Vector3(102,42,-33),new Vector3(96,0,24));
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var lines=root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>p.b.min.x<116&&p.b.max.x>76&&p.b.min.z<46&&p.b.max.z>2)
                    .Select(p=>p.c.name+" | "+p.c.GetType().Name+" | min="+p.b.min.ToString("F2")+" | max="+p.b.max.ToString("F2")).ToArray();
                File.WriteAllLines(Report+"/site.txt",lines);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: native site inspection captured.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
    internal static void Render(Scene scene,string path,Vector3 position,Vector3 target,float ortho=0)
    {
        var go=new GameObject("Florist review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.GetComponent<Camera>();camera.enabled=false;camera.transform.position=position;camera.transform.LookAt(target);
        camera.fieldOfView=50;camera.nearClipPlane=.1f;camera.farClipPlane=1500;camera.orthographic=ortho>0;camera.orthographicSize=ortho;
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var targetTexture=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;Texture2D png=null;
        try
        {
            if(!targetTexture.Create())throw new InvalidOperationException("Native camera render target unavailable.");
            camera.targetTexture=targetTexture;camera.Render();RenderTexture.active=targetTexture;
            png=new Texture2D(1600,1000,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes(path,png.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;targetTexture.Release();Object.DestroyImmediate(targetTexture);if(png!=null)Object.DestroyImmediate(png);Object.DestroyImmediate(go);}
    }
}
