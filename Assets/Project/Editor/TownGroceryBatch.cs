using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownGroceryBatch
{
    internal const string Report="Library/TownGroceryQA",Pictures="Docs/TownGroceryReview";
    public static void Generate()=>Author(false);
    public static void Green()=>Author(true);
    private static void Author(bool greeneryOnly)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownGroceryPlacement.Permissions);string farm=TownFarmPreservation.Capture();
            var groceryArea=new Rect(-280,16,60,47);
            string retainedGrocery=greeneryOnly?TownFarmPreservation.Capture(groceryArea,SupplierKind.Wholesale):null;
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);string contracts=Contracts(root);var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Wholesale);int identity=checkpoint.GetInstanceID();
                if(greeneryOnly)new TownWesternGreenery(root).Build();else new TownGroceryPlacement(root).Build();
                Require(root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Wholesale).GetInstanceID()==identity,"Original Grocery checkpoint replaced.");
                Require(Contracts(root)==contracts,"Another supplier contract changed.");homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownGroceryPlacement.Permissions));Require(TownFarmPreservation.Capture()==farm,"Farm changed.");
            if(greeneryOnly)Require(TownFarmPreservation.Capture(groceryArea,SupplierKind.Wholesale)==retainedGrocery,"Existing grocery geometry, collision or loading contract changed during landscaping.");
            File.WriteAllText(Report+"/protection.txt","PASS: exact geometry/materials/colliders outside authorised western district and former street apron unchanged; residential buildings/routes, railway, farm, bakery, florist, showroom and other supplier contracts preserved.\n"+outside+"\n");
            Validate();Capture();File.WriteAllText(Report+"/result.txt",greeneryOnly?
                "GROCERY PASSED: marked western district landscaped; existing grocery geometry/collisions and pickup contract unchanged; grounded trees, clear roads and outside-scope protection passed.\n":
                "GROCERY PASSED: existing grocery site refined; obsolete western complexes removed with user approval; original grocery checkpoint retained; doors, relocated pallets, exterior props, pickup corridor and outside-scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Validate();Capture();File.WriteAllText(Report+"/review.txt","PASS: saved grocery depot reopened and captured in a fresh Unity process.\n");File.WriteAllText(Report+"/review-input.sha256",PrefabHash());EditorApplication.Exit(0);}
        catch(Exception e){File.WriteAllText(Report+"/review.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();var site=town.transform.Find(TownGroceryPlacement.SiteName);Require(site!=null,"Grocery depot missing.");
        var checkpoint=town.suppliers.Single(c=>c.supplier==SupplierKind.Wholesale);
        Require(checkpoint.kind==CheckpointKind.Depot&&checkpoint.requiresRearDocking&&Vector3.Distance(checkpoint.transform.position,TownGroceryPlacement.Bay)<.001f&&Vector3.Dot(checkpoint.transform.forward,Vector3.right)>.999f,"Grocery rear collection contract or heading invalid.");
        Require(town.GetComponentsInChildren<DeliveryCheckpoint>(true).Count(c=>c.supplier==SupplierKind.Wholesale)==1,"Duplicate Grocery pickup checkpoint.");
        var courts=town.GetComponentsInChildren<SupplierLoadingCourt>(true);Require(courts.Length==6&&courts.All(c=>c.checkpoint.requiresRearDocking&&c.checkpoint.supplier!=SupplierKind.Showroom),"Expected six rear goods collection courts.");
        var court=site.GetComponent<SupplierLoadingCourt>();Require(court!=null&&court.checkpoint==checkpoint&&court.designVehicleLength==6.8f,"Grocery compact-pickup court missing.");
        Require(!town.GetComponentsInChildren<Transform>(true).Any(t=>(t.name=="TreeRound")&&Inside(TownGroceryPlacement.ClearArea,t.position)),"Old garden tree metadata remains.");
        var inner=new Rect(-276,20,49.5f,38);int residual=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.transform.IsChildOf(site)))
        {
            var p=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray();var triangles=f.sharedMesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=p[triangles[i]];var b=p[triangles[i+1]];var c=p[triangles[i+2]];
                if(Inside(inner,a)&&Inside(inner,b)&&Inside(inner,c)&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.095f)residual++;
            }
        }
        Require(residual==0,"Old square still has "+residual+" raised local triangles.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int total=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(total<90000&&site.GetComponentsInChildren<MeshRenderer>().Length==3,"Grocery geometry/material budget exceeded.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.All(p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)),"Non-finite grocery mesh coordinates.");
        Physics.SyncTransforms();
        ValidateDistrictGreenery(town);
        var all=town.GetComponentsInChildren<Collider>();
        var solids=site.GetComponentsInChildren<BoxCollider>().Where(c=>!new[]{"GroceryShop","GroceryWarehouse","GroceryLink"}.Any(id=>c.transform.IsChildOf(site.Find(id)))&&c.name!="Fence"&&c.name!="Brick pier"&&c.name!="Tree trunk").ToArray();
        foreach(var prop in solids)foreach(var other in all)
        {
            if(prop==other||other.isTrigger||prop.transform.parent==other.transform.parent)continue;
            bool overlap=Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth);
            Require(!overlap||depth<.005f,"Grocery prop "+prop.transform.parent.name+" intersects "+other.transform.parent.name+"/"+other.name+" by "+depth+" m.");
        }
        Require(site.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Customer car")==2,"Expected two customer cars and one free parking space.");
        int probes=CheckRoute(town,new[]{TownGroceryPlacement.Street,TownGroceryPlacement.Entrance,TownGroceryPlacement.Staging,TownGroceryPlacement.Bay});
        // The facade and personnel door each have an independent clear pedestrian approach.
        Door(town,new Vector3(-232,1.2f,48),new Vector3(.8f,.8f,1.4f));
        Door(town,new Vector3(-258.5f,1.2f,20.9f),new Vector3(1.0f,.8f,.9f));
        var storage=site.Find("Second bay pallet storage");Require(storage!=null&&storage.childCount==10,"Ten original rear pallets must remain in the second-bay storage pocket.");
        foreach(var pallet in storage.GetComponentsInChildren<BoxCollider>())Require(pallet.bounds.min.x>-259.1f&&pallet.bounds.max.x<-251.9f&&pallet.bounds.min.z>33.4f&&pallet.bounds.max.z<43.4f&&Mathf.Abs(pallet.bounds.min.y-.105f)<.001f,"Relocated pallet outside storage or floating.");
        foreach(var planter in site.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Planter"))Require(planter.bounds.min.x>-278.3f&&planter.bounds.max.x<-225.7f&&planter.bounds.min.z>17.6f&&planter.bounds.max.z<61.3f,"Planter crosses the fence.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.Select(f.transform.TransformPoint).All(p=>p.x<=-219.999f),"Grocery asphalt/props still protrude across the road.");
        // Continuous physical boundary is sampled separately from its decorative piers.
        int fenceSamples=0;
        foreach(var segment in new[]{(new Vector3(-278.5f,1,17.5f),new Vector3(-278.5f,1,61.5f)),(new Vector3(-278.5f,1,61.5f),new Vector3(-225.5f,1,61.5f)),(new Vector3(-278.5f,1,17.5f),new Vector3(-225.5f,1,17.5f))})
            for(int i=0;i<=Mathf.CeilToInt(Vector3.Distance(segment.Item1,segment.Item2));i++){float t=i/(float)Mathf.CeilToInt(Vector3.Distance(segment.Item1,segment.Item2));var p=Vector3.Lerp(segment.Item1,segment.Item2,t);Require(Physics.OverlapBox(p,Vector3.one*.12f).Any(c=>c.transform.IsChildOf(site)&&c.name=="Fence"),"Perimeter has a physical gap at "+p);fenceSamples++;}
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" actual corridor/floor samples; 2.7 x 6.8 m design envelope; shop/personnel doors clear; two customer cars; exterior props have no physical intersections; 10 rear pallets grounded within second bay; "+fenceSamples+" rear/side boundary samples; no site geometry across road centre. "+total+" triangles, three material batches. Grocery rear pickup retained; exactly six goods courts.\n");
    }
    private static void ValidateDistrictGreenery(TownWorldPlaces town)
    {
        var district=town.transform.Find(TownWesternGreenery.RootName);if(district==null)return;
        var trees=district.GetComponentsInChildren<CapsuleCollider>();Require(trees.Length>=400,"Marked district still lacks dense forest vegetation.");
        Require(trees.Average(c=>c.transform.lossyScale.x)>=2.0f,"Forest trees are still undersized relative to the reference.");
        var filters=district.GetComponentsInChildren<MeshFilter>();
        Require(filters.Sum(f=>f.sharedMesh.triangles.Length/3)<180000&&filters.Length<=40,"District vegetation budget exceeded.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.Select(f.transform.TransformPoint).All(p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Inside(TownWesternGreenery.Area,p)),"District greenery outside marked bounds or non-finite.");
        var existing=town.GetComponentsInChildren<Collider>().Where(c=>!c.transform.IsChildOf(district)&&!c.isTrigger).ToArray();
        foreach(var tree in trees)
        {
            var p=tree.transform.position;Require(Mathf.Abs(p.y)<.001f,"Floating district tree.");
            Require(Physics.RaycastAll(p+Vector3.up*10,Vector3.down,11).Any(h=>h.collider.transform.IsChildOf(town.transform)&&!h.collider.transform.IsChildOf(district)&&h.normal.y>.8f&&Mathf.Abs(h.point.y)<.01f),"Tree has no ground support at "+p);
            foreach(var other in existing)Require(!Physics.ComputePenetration(tree,tree.transform.position,tree.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<.005f,"District tree intersects existing object at "+p);
        }
        File.AppendAllText(Report+"/greenery.txt","Native saved-scene PASS: "+trees.Length+" grounded tree trunks; complete planting geometry within marked area; "+filters.Sum(f=>f.sharedMesh.triangles.Length/3)+" triangles in "+filters.Length+" spatial batches; existing colliders not intersected.\n");
    }
    private static void Door(TownWorldPlaces town,Vector3 p,Vector3 half)
    {var hits=Physics.OverlapBox(p,half,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();Require(hits.Length==0,"Grocery door approach blocked: "+string.Join(",",hits.Select(c=>c.name)));}
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
                Require(hits.Length==0,"Grocery loading route blocked at "+p+": "+string.Join(",",hits.Select(c=>c.name)));
                Require(Physics.RaycastAll(new Vector3(p.x,4,p.z),Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"No physical floor at grocery route "+p);
            }
        }
        return probes;
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);Directory.CreateDirectory(Pictures);
        var city=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();var pickup=city.pickup.transform;
        pickup.SetPositionAndRotation(TownGroceryPlacement.Bay,Quaternion.Euler(0,90,0));pickup.position+=Vector3.up*(.102f-pickup.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        TownFloristBatch.Render(scene,Pictures+"/Facade.png",new Vector3(-194,22,-8),new Vector3(-249,2.3f,37));
        TownFloristBatch.Render(scene,Pictures+"/Yard.png",new Vector3(-226,12,26),new Vector3(-262,2.7f,34));
        TownFloristBatch.Render(scene,Pictures+"/Top.png",new Vector3(-252,90,38),new Vector3(-252,0,38),35);
        TownFloristBatch.Render(scene,Pictures+"/Sorting.png",new Vector3(-221,8,22),new Vector3(-242,1.4f,34));
        TownFloristBatch.Render(scene,Pictures+"/Rear.png",new Vector3(-310,27,81),new Vector3(-260,1,39));
        TownFloristBatch.Render(scene,Pictures+"/Storage.png",new Vector3(-233,14,29),new Vector3(-255,1,39));
        TownFloristBatch.Render(scene,Pictures+"/StreetContext.png",new Vector3(-185,39,-15),new Vector3(-252,0,38));
        RenderSettings.fog=false;TownFloristBatch.Render(scene,Pictures+"/CityContext.png",new Vector3(-415,260,-120),new Vector3(-155,0,65));
        TownFloristBatch.Render(scene,Pictures+"/GreenDistrict.png",new Vector3(-440,200,-160),new Vector3(-302,0,-4));
        TownFloristBatch.Render(scene,Pictures+"/GreenDistrictTop.png",new Vector3(-305,260,-4),new Vector3(-305,0,-4),194);
        File.WriteAllText(Pictures+"/complete.txt","PASS: ten actual saved-scene Unity views of the grocery depot and marked western district; native pickup posed temporarily for loading scale; scene/player spawn not saved.\n");
    }
    private static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static string PrefabHash(){using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    private static string Contracts(GameObject root)
    {
        return string.Join("\n",root.GetComponent<TownWorldPlaces>().suppliers.Where(c=>c.supplier!=SupplierKind.Wholesale).OrderBy(c=>c.supplier).Select(c=>JsonUtility.ToJson(c)+"|"+c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")));
    }

    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(-252,180,65),new Vector3(-252,0,65),105);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-145,85,-45),new Vector3(-252,0,65));
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var lines=root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>p.b.max.x>-310&&p.b.min.x<-216&&p.b.max.z>-70&&p.b.min.z<180)
                    .Select(p=>p.c.transform.parent.name+"/"+p.c.name+" | "+p.c.GetType().Name+" | "+p.b.min.ToString("F2")+".."+p.b.max.ToString("F2")).ToList();
                foreach(var court in root.GetComponentsInChildren<SupplierLoadingCourt>(true))lines.Add("COURT "+court.checkpoint.supplier+" | "+court.bounds+" | "+court.checkpoint.transform.position);
                File.WriteAllLines(Report+"/site.txt",lines);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: western vacant land inspected without changing town.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
