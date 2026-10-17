using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scoped authoring and acceptance of the Building Supplies concept.</summary>
public static class TownConstructionBatch
{
    internal const string Report="Library/TownConstructionQA",Pictures="Docs/TownConstructionReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownConstructionPlacement.Permissions);string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Construction);int identity=checkpoint.GetInstanceID();
                new TownConstructionPlacement(root).Build();
                Require(root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Construction).GetInstanceID()==identity,"Original Construction checkpoint replaced.");
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownConstructionPlacement.Permissions));Require(TownFarmPreservation.Capture()==farm,"Farm changed.");
            var after=TownSupplierPreservation.Capture();foreach(var supplier in suppliers.Where(s=>s.Key!=SupplierKind.Construction))Require(after[supplier.Key]==supplier.Value,"Other supplier changed: "+supplier.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: exact geometry/materials/colliders outside marked square and street apron unchanged; residential buildings/routes, farm, bakery, florist, showroom and other supplier contracts preserved.\n"+outside+"\n");
            Validate();Capture();File.WriteAllText(Report+"/result.txt","CONSTRUCTION PASSED: native construction complex installed on marked square; six parcel trees replaced with planned planting; original construction checkpoint retained; doors, props, pickup corridor and outside-scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Validate();Capture();File.WriteAllText(Report+"/review.txt","PASS: saved construction depot reopened and captured in a fresh Unity process.\n");File.WriteAllText(Report+"/review-input.sha256",PrefabHash());EditorApplication.Exit(0);}
        catch(Exception e){File.WriteAllText(Report+"/review.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();var site=town.transform.Find(TownConstructionPlacement.SiteName);Require(site!=null,"Construction depot missing.");
        var checkpoint=town.suppliers.Single(c=>c.supplier==SupplierKind.Construction);
        Require(checkpoint.kind==CheckpointKind.Depot&&checkpoint.requiresRearDocking&&Vector3.Distance(checkpoint.transform.position,TownConstructionPlacement.Bay)<.001f&&Vector3.Dot(checkpoint.transform.forward,Vector3.back)>.999f,"Construction rear collection contract or heading invalid.");
        Require(town.GetComponentsInChildren<DeliveryCheckpoint>(true).Count(c=>c.supplier==SupplierKind.Construction)==1,"Duplicate Construction pickup checkpoint.");
        var courts=town.GetComponentsInChildren<SupplierLoadingCourt>(true);Require(courts.Length==6&&courts.All(c=>c.checkpoint.requiresRearDocking&&c.checkpoint.supplier!=SupplierKind.Showroom),"Expected six rear goods collection courts.");
        var court=site.GetComponent<SupplierLoadingCourt>();Require(court!=null&&court.checkpoint==checkpoint&&court.designVehicleLength==6.8f,"Construction compact-pickup court missing.");
        Require(!town.GetComponentsInChildren<Transform>(true).Any(t=>(t.name=="TreeTall")&&Inside(TownConstructionPlacement.ClearArea,t.position)),"Original parcel tree metadata remains.");
        // Existing curb faces and street lamps belong to the retained boundary, not to removable lot scenery.
        var innerParcel=new Rect(-207,153,74,30);
        int residual=0;var residualDetails=new System.Collections.Generic.List<string>();
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.transform.IsChildOf(site)))
        {
            var p=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray();var triangles=f.sharedMesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=p[triangles[i]];var b=p[triangles[i+1]];var c=p[triangles[i+2]];
                if(Inside(innerParcel,a)&&Inside(innerParcel,b)&&Inside(innerParcel,c)&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.02f)
                {residual++;residualDetails.Add(f.name+" | "+AssetDatabase.GetAssetPath(f.sharedMesh)+" | "+a+" / "+b+" / "+c);}
            }
        }
        if(residual>0)File.WriteAllLines(Report+"/residuals.txt",residualDetails);
        Require(residual==0,"Original parcel still has "+residual+" raised local triangles. See residuals.txt.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int total=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(total<65000&&site.GetComponentsInChildren<MeshRenderer>().Length==3,"Construction geometry/material budget exceeded.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.All(p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)),"Non-finite construction mesh coordinates.");
        Physics.SyncTransforms();
        var all=town.GetComponentsInChildren<Collider>();
        var solids=site.GetComponentsInChildren<BoxCollider>().Where(c=>!c.transform.IsChildOf(site.Find("SupplyShop"))&&c.name!="Fence"&&c.name!="Brick pier"&&c.name!="Tree trunk").ToArray();
        foreach(var prop in solids)foreach(var other in all)
        {
            if(prop==other||other.isTrigger||prop.transform.parent==other.transform.parent)continue;
            bool overlap=Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth);
            Require(!overlap||depth<.005f,"Construction prop "+prop.transform.parent.name+" intersects "+other.transform.parent.name+"/"+other.name+" by "+depth+" m.");
        }
        Require(site.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Customer car")==2,"Expected two customer cars and one free parking space.");
        int probes=CheckRoute(town,new[]{TownConstructionPlacement.Street,TownConstructionPlacement.Entrance,TownConstructionPlacement.Staging,TownConstructionPlacement.Bay});
        // The facade and personnel door each have an independent clear pedestrian approach.
        Door(town,new Vector3(-182,1.2f,159.0f),new Vector3(1.3f,.8f,.8f));
        Door(town,new Vector3(-147.3f,1.2f,171.4f),new Vector3(1.0f,.8f,1.0f));
        foreach(var planter in site.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Planter"))Require(planter.bounds.min.x>-209.45f&&planter.bounds.max.x<-130.55f&&planter.bounds.min.z>151.5f&&planter.bounds.max.z<184.5f,"Planter crosses the fence.");
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" actual corridor/floor samples; 2.7 x 6.8 m design envelope; shop/personnel doors clear; two customer cars; exterior props have no physical intersections; old square residual geometry absent. "+total+" triangles, three material batches. Construction rear pickup retained; exactly six goods courts.\n");
    }
    private static void Door(TownWorldPlaces town,Vector3 p,Vector3 half)
    {var hits=Physics.OverlapBox(p,half,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();Require(hits.Length==0,"Construction door approach blocked: "+string.Join(",",hits.Select(c=>c.name)));}
    private static int CheckRoute(TownWorldPlaces town,Vector3[] points)
    {
        int probes=0;
        for(int leg=1;leg<points.Length;leg++)
        {
            var direction=points[leg]-points[leg-1];int steps=Mathf.CeilToInt(direction.magnitude*2);
            for(int i=0;i<=steps;i++)
            {
                var p=Vector3.Lerp(points[leg-1],points[leg],(float)i/steps);probes++;
                var hits=Physics.OverlapBox(new Vector3(p.x,1.1f,p.z),new Vector3(1.35f,.65f,3.4f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                Require(hits.Length==0,"Construction loading route blocked at "+p+": "+string.Join(",",hits.Select(c=>c.name)));
                Require(Physics.RaycastAll(new Vector3(p.x,4,p.z),Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"No physical floor at construction route "+p);
            }
        }
        return probes;
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);Directory.CreateDirectory(Pictures);
        var city=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();var pickup=city.pickup.transform;
        pickup.SetPositionAndRotation(TownConstructionPlacement.Bay,Quaternion.Euler(0,180,0));pickup.position+=Vector3.up*(.087f-pickup.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        TownFloristBatch.Render(scene,Pictures+"/Facade.png",new Vector3(-225,21,126),new Vector3(-178,2.3f,168));
        TownFloristBatch.Render(scene,Pictures+"/Yard.png",new Vector3(-154,12,148),new Vector3(-155,2.9f,176));
        TownFloristBatch.Render(scene,Pictures+"/Top.png",new Vector3(-169,96,169),new Vector3(-169,0,169),44);
        TownFloristBatch.Render(scene,Pictures+"/Materials.png",new Vector3(-151,12,142),new Vector3(-175,1.2f,173));
        TownFloristBatch.Render(scene,Pictures+"/StreetContext.png",new Vector3(-231,39,120),new Vector3(-170,0,172));
        RenderSettings.fog=false;TownFloristBatch.Render(scene,Pictures+"/CityContext.png",new Vector3(-172,225,-255),new Vector3(-90,0,100));
        File.WriteAllText(Pictures+"/complete.txt","PASS: six actual saved-scene Unity views of the construction depot; native pickup posed temporarily for loading scale; scene/player spawn not saved.\n");
    }
    private static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static string PrefabHash(){using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-235,32,120),new Vector3(-168,1,168));
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(-168,100,168),new Vector3(-168,0,168),48);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var lines=root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>p.b.max.x>-214&&p.b.min.x<-124&&p.b.max.z>148&&p.b.min.z<190)
                    .Select(p=>p.c.name+" | "+p.c.GetType().Name+" | "+p.b.min.ToString("F2")+".."+p.b.max.ToString("F2")).ToList();
                foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.position.x>-214&&t.position.x<-124&&t.position.z>148&&t.position.z<190&&t.childCount==0))lines.Add("NODE "+t.name+" | "+t.position.ToString("F2"));
                var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Construction);
                lines.Add("Existing Construction checkpoint="+checkpoint.transform.position+", kind="+checkpoint.kind+", rear="+checkpoint.requiresRearDocking);
                File.WriteAllLines(Report+"/site.txt",lines);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: empty block west of Equipment inspected without changing town.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
