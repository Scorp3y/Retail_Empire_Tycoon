using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownBakeryQuarterBatch
{
    internal const string Report="Library/TownBakeryQuarterQA",Pictures="Docs/TownBakeryQuarterReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownBakeryQuarterPlacement.Permissions);
            var bakeryArea=new Rect(-92,-189,24,39);
            string bakery=TownFarmPreservation.Capture(bakeryArea,SupplierKind.Bakery),farm=TownFarmPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                string contracts=Contracts(root);var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                var identities=root.GetComponent<TownWorldPlaces>().suppliers.Select(c=>c.GetInstanceID()).ToArray();
                new TownBakeryQuarterPlacement(root).Build();
                Require(identities.SequenceEqual(root.GetComponent<TownWorldPlaces>().suppliers.Select(c=>c.GetInstanceID())),"Original supplier identity changed.");
                Require(contracts==Contracts(root),"Supplier checkpoint/loading contract changed.");homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownBakeryQuarterPlacement.Permissions));
            Require(TownFarmPreservation.Capture(bakeryArea,SupplierKind.Bakery)==bakery,"Original bakery geometry, collision or pickup changed.");Require(TownFarmPreservation.Capture()==farm,"Farm changed.");
            File.WriteAllText(Report+"/protection.txt","PASS: exact outside-parcel geometry/material/collision fingerprints unchanged. Original bakery geometry, collision, signage and rear pickup preserved; all original supplier identities/contracts and residential buildings/routes unchanged.\n"+outside+"\n");
            Capture();Validate();File.WriteAllText(Report+"/result.txt","PASS: bakery quarter native modular amenities generated; saved-scene geometry, access and scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Failure("result.txt",e);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Capture();Validate();File.WriteAllText(Report+"/review.txt","PASS: saved bakery quarter reopened, revalidated and captured in fresh Unity process.\n");File.WriteAllText(Report+"/review-input.sha256",PrefabHash());EditorApplication.Exit(0);}
        catch(Exception e){Failure("review.txt",e);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();var site=town.transform.Find(TownBakeryQuarterPlacement.SiteName);
        Require(site!=null,"Quarter missing.");var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(filters.Length==3&&triangles<120000,"Quarter geometry/material budget exceeded: "+triangles+" triangles / "+filters.Length+" batches.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.Select(f.transform.TransformPoint).All(p=>TownBakeryQuarterPlacement.Finite(p)&&TownBakeryQuarterPlacement.Permissions.Any(r=>TownBakeryQuarterPlacement.Inside(r,p))),"Quarter visual geometry extends over road or bakery parcel.");
        Require(site.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Tree trunk")==8,"Expected eight new shade trees.");
        Require(site.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Bench")==6,"Expected six benches.");
        Require(site.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Patio seating")==4,"Expected four tables.");
        Require(site.GetComponentsInChildren<DeliveryCheckpoint>().Length==0,"Decorative amenities must not create duplicate gameplay shops.");
        var bay=town.suppliers.Single(c=>c.supplier==SupplierKind.Bakery);var court=town.GetComponentsInChildren<SupplierLoadingCourt>().Single(c=>c.checkpoint==bay);
        Require(bay.requiresRearDocking&&Vector3.Dot(bay.transform.forward,Vector3.back)>.999f&&Vector3.Distance(bay.transform.position,new Vector3(-76,.075f,-170.4f))<.001f,"Original bakery bay changed.");
        Require(town.GetComponentsInChildren<SupplierLoadingCourt>().Length==6,"Original six collection courts not preserved.");
        Physics.SyncTransforms();var all=town.GetComponentsInChildren<Collider>();var solids=site.GetComponentsInChildren<BoxCollider>();
        foreach(var prop in solids)
        {
            var bounds=prop.bounds;
            Require(TownBakeryQuarterPlacement.Permissions.Any(r=>TownBakeryQuarterPlacement.Inside(r,bounds.min)&&TownBakeryQuarterPlacement.Inside(r,bounds.max)),"Physical prop outside public parcel: "+prop.name);
            float floor=prop.transform.parent.position.y;
            Require(Mathf.Abs(floor-TownBakeryQuarterPlacement.Floor)<.002f||Mathf.Abs(floor-TownBakeryQuarterPlacement.Soil)<.002f,"Unexpected prop support level.");
            Require(Mathf.Abs(bounds.min.y-floor)<.002f,"Floating/buried quarter prop "+prop.transform.parent.name+" at "+bounds.min.y);
            Require(Physics.RaycastAll(new Vector3(bounds.center.x,floor+.02f,bounds.center.z),Vector3.down,.06f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider!=prop&&h.normal.y>.8f&&Mathf.Abs(h.point.y-floor)<.005f),"Quarter prop lacks physical floor: "+prop.transform.parent.name);
            foreach(var other in all)
            {
                if(other==prop||other.isTrigger||other.transform.parent==prop.transform.parent)continue;
                Require(!Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<.005f,"Quarter prop "+prop.transform.parent.name+"/"+prop.name+" at "+prop.transform.parent.position+" intersects "+other.transform.parent.name+"/"+other.name+" at "+other.transform.parent.position+" by "+depth);
            }
        }
        Clear(town,new Vector3(-45,1.2f,-156),new Vector3(.8f,.8f,1.1f));Clear(town,new Vector3(-108,1.2f,-155.5f),new Vector3(.75f,.8f,.8f));
        // Continuous front promenade joins the existing sidewalk without crossing the protected service gate.
        foreach(var span in new[]{new Vector2(-66,-33),new Vector2(-110,-94)})for(float x=span.x;x<=span.y;x+=.5f)Clear(town,new Vector3(x,1,-152.8f),new Vector3(.30f,.65f,.30f));
        int samples=Route(town,new[]{court.streetAccess,court.entrance}.Concat(court.approachWaypoints).Concat(new[]{court.staging,bay.transform.position}).ToArray());
        File.WriteAllText(Report+"/access.txt","PASS: "+samples+" saved-scene bakery route/floor samples with 2.7 x 6.8 m envelope; coffee and kiosk approaches and front promenades clear; every new solid grounded, within parcel and free of physical intersections. Eight trees, six benches, four tables. "+triangles+" triangles / three visual material batches. Existing six goods courts retained.\n");
    }
    private static int Route(TownWorldPlaces town,Vector3[] points)
    {
        int samples=0;
        for(int leg=1;leg<points.Length;leg++)
        {
            var delta=Vector3.ProjectOnPlane(points[leg]-points[leg-1],Vector3.up);int count=Mathf.CeilToInt(delta.magnitude*2);
            Require(count>0,"Empty service route leg.");
            for(int i=0;i<=count;i++)
            {
                var p=Vector3.Lerp(points[leg-1],points[leg],(float)i/count);samples++;
                Clear(town,new Vector3(p.x,1.1f,p.z),new Vector3(1.35f,.65f,3.4f),Quaternion.LookRotation(delta));
                Require(Physics.RaycastAll(p+Vector3.up*4,Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"No service floor at "+p);
            }
        }
        return samples;
    }
    private static void Clear(TownWorldPlaces town,Vector3 p,Vector3 half,Quaternion? rotation=null)
    {
        var hits=Physics.OverlapBox(p,half,rotation??Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
        Require(hits.Length==0,"Approach blocked at "+p+": "+string.Join(",",hits.Select(c=>c.transform.parent.name+"/"+c.name)));
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);Directory.CreateDirectory(Pictures);
        var city=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();var car=city.pickup.transform;
        car.SetPositionAndRotation(new Vector3(-76,0,-170.4f),Quaternion.Euler(0,180,0));car.position+=Vector3.up*(.09f-car.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        TownFloristBatch.Render(scene,Pictures+"/Facade.png",new Vector3(-76,33,-96),new Vector3(-76,1,-168));
        TownFloristBatch.Render(scene,Pictures+"/Top.png",new Vector3(-74,110,-169),new Vector3(-74,0,-169),35);
        TownFloristBatch.Render(scene,Pictures+"/CoffeeTerrace.png",new Vector3(-60,13,-139),new Vector3(-52,1.8f,-165));
        TownFloristBatch.Render(scene,Pictures+"/FlowerGarden.png",new Vector3(-93,12,-142),new Vector3(-104,1.4f,-170));
        TownFloristBatch.Render(scene,Pictures+"/ServiceAccess.png",new Vector3(-82,14,-147),new Vector3(-82,1,-175));
        TownFloristBatch.Render(scene,Pictures+"/CityContext.png",new Vector3(-45,70,-81),new Vector3(-75,0,-171));
        File.WriteAllText(Pictures+"/complete.txt","PASS: six actual saved-scene Unity camera views, not AI concept art. Native pickup temporarily posed for scale; scene and spawn not saved.\n");
    }
    private static string Contracts(GameObject root)
    {
        return string.Join("\n",root.GetComponent<TownWorldPlaces>().suppliers.OrderBy(c=>c.supplier).Select(c=>JsonUtility.ToJson(c)+"|"+c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")))+"\n"+
            string.Join("\n",root.GetComponentsInChildren<SupplierLoadingCourt>().OrderBy(c=>c.checkpoint.supplier).Select(JsonUtility.ToJson));
    }
    private static string PrefabHash(){using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static void Failure(string report,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+report,"FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-76,32,-99),new Vector3(-76,0,-169));
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(-75,110,-170),new Vector3(-75,0,-170),34);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var lines=root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>p.b.max.x>-125&&p.b.min.x<-25&&p.b.max.z>-195&&p.b.min.z<-144)
                    .Select(p=>p.c.transform.parent.name+"/"+p.c.name+" | "+p.c.GetType().Name+" | "+p.b.min.ToString("F2")+".."+p.b.max.ToString("F2")).ToList();
                foreach(var court in root.GetComponentsInChildren<RetailEmpireTycoon.City.SupplierLoadingCourt>())lines.Add("COURT "+court.checkpoint.supplier+" | "+court.bounds+" | street "+court.streetAccess+" entrance "+court.entrance+" staging "+court.staging+" waypoint "+string.Join(",",court.approachWaypoints.Select(p=>p.ToString())));
                File.WriteAllLines(Report+"/site.txt",lines);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: existing bakery and both neighbouring public parcels inspected without modification.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
