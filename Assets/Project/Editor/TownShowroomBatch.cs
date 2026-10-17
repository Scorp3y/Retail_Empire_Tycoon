using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Native authoring and saved-scene acceptance for the riverside car showroom.</summary>
public static class TownShowroomBatch
{
    internal const string Report="Library/TownShowroomQA";
    internal const string Pictures="Docs/TownShowroomReview";
    public static void Enrich()=>UpdateInstalled(false);
    public static void Arrange()=>UpdateInstalled(true);
    private static void UpdateInstalled(bool arrange)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions);
            string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
                int identity=checkpoint.GetInstanceID();Vector3 position=checkpoint.transform.position;Quaternion rotation=checkpoint.transform.rotation;
                var placement=new TownShowroomPlacement(root);
                if(arrange)placement.Arrange();else placement.Enrich();
                Require(checkpoint.GetInstanceID()==identity&&checkpoint.transform.position==position&&checkpoint.transform.rotation==rotation,"Showroom interaction moved or replaced.");
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions));
            Require(TownFarmPreservation.Capture()==farm,"Farm changed.");var after=TownSupplierPreservation.Capture();
            foreach(var supplier in suppliers)Require(after[supplier.Key]==supplier.Value,"Supplier changed: "+supplier.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: exact geometry/materials/colliders outside showroom scope unchanged; farm, all supplier contracts, western suppliers and residential buildings/routes preserved.\n"+outside+"\n");
            Validate();Capture();File.WriteAllText(Report+"/result.txt",arrange?
                "SHOWROOM PASSED: protruding planter removed; service parapet closed; rear props wall-aligned; door, pickup access and outside-scope protection passed.\n":
                "SHOWROOM PASSED: enriched interior and service court; grounded furniture; repaired roof junction; unchanged interaction, access and outside-scope protection.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void Revise()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var site=root.transform.Find(TownShowroomPlacement.SiteName);Require(site!=null,"Installed showroom absent.");
                var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
                foreach(var court in site.GetComponentsInChildren<SupplierLoadingCourt>(true))
                {Require(court.checkpoint==checkpoint,"Revision encountered another supplier court.");Object.DestroyImmediate(court);}
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions));
            Validate();Capture();File.WriteAllText(Report+"/result.txt","SHOWROOM PASSED: public inspection retained without a seventh loading-court marker; unchanged models and access; outside-scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions);
            string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                var original=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);int identity=original.GetInstanceID();
                new TownShowroomPlacement(root).Build();
                Require(root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom).GetInstanceID()==identity,"Original showroom interaction reference replaced.");
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownShowroomPlacement.Permissions));
            Require(TownFarmPreservation.Capture()==farm,"Farm changed.");var after=TownSupplierPreservation.Capture();
            foreach(var supplier in suppliers.Where(p=>p.Key!=SupplierKind.Showroom))Require(after[supplier.Key]==supplier.Value,"Other supplier changed: "+supplier.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: exact visual geometry/materials/colliders outside marked riverside site unchanged; farm, bakery, florist, other suppliers and residential buildings/routes preserved.\n"+outside+"\n");
            Validate();Capture();File.WriteAllText(Report+"/result.txt","SHOWROOM PASSED: native showroom and details, correct riverside placement, unchanged public inspection contract, public/service access and outside-scope protection.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Validate();Capture();File.WriteAllText(Report+"/review.txt","PASS: saved showroom reopened and captured in a fresh native Unity process.\n");File.WriteAllText(Report+"/review-input.sha256",PrefabHash());EditorApplication.Exit(0);}
        catch(Exception error){File.WriteAllText(Report+"/review.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var site=town.transform.Find(TownShowroomPlacement.SiteName);Require(site!=null,"Riverside showroom absent.");
        var hall=site.Find("ShowroomHall");Require(hall!=null&&Vector3.Dot(hall.forward,Vector3.left)>.999f&&Vector3.Distance(hall.position,TownShowroomPlacement.Point(11.5f,14))<.001f,"Wrong showroom position or heading.");
        var checkpoint=town.suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
        Require(checkpoint.kind==CheckpointKind.Showroom&&!checkpoint.requiresRearDocking&&Vector3.Distance(checkpoint.transform.position,TownShowroomPlacement.PublicBay)<.001f,"Showroom public inspection contract changed.");
        Require(town.GetComponentsInChildren<DeliveryCheckpoint>(true).Count(c=>c.supplier==SupplierKind.Showroom)==1,"Duplicate showroom checkpoint.");
        var courts=town.GetComponentsInChildren<SupplierLoadingCourt>(true);
        Require(courts.Length==6&&courts.All(c=>c.checkpoint.supplier!=SupplierKind.Showroom&&c.checkpoint.requiresRearDocking),"Public showroom must not become a seventh goods loading court.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(triangles<65000&&site.GetComponentsInChildren<MeshRenderer>().Length==3,"Showroom geometry/material budget exceeded.");
        foreach(var filter in filters)Require(filter.sharedMesh.vertices.All(p=>!float.IsNaN(p.x)&&!float.IsInfinity(p.y)),"Invalid model vertices.");
        Physics.SyncTransforms();
        if(site.Find("Showroom details v2")!=null)ValidateDressing(site,hall,town);
        if(site.Find("Showroom layout v3")!=null)ValidateLayout(site,town);
        var cars=site.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Vehicle body"&&!c.transform.parent.name.StartsWith("Indoor exhibition",StringComparison.Ordinal)).ToArray();
        Require(cars.Length==3,"Outdoor exhibition, customer and preparation cars required.");
        foreach(var car in cars)foreach(var obstacle in town.GetComponentsInChildren<Collider>())
        {
            if(car==obstacle||obstacle.isTrigger)continue;
            bool overlap=Physics.ComputePenetration(car,car.transform.position,car.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out var direction,out float depth);
            Require(!overlap||depth<.005f,"Showroom parked car intersects "+obstacle.name+" by "+depth+" m.");
        }
        int probes=CheckRoute(town,new[]{TownShowroomPlacement.PublicStreet,TownShowroomPlacement.Point(11.5f,-2,0),TownShowroomPlacement.PublicBay});
        probes+=CheckRoute(town,new[]{TownShowroomPlacement.ServiceStreet,TownShowroomPlacement.Point(26.5f,-2,0),TownShowroomPlacement.ServiceStaging,TownShowroomPlacement.ServiceBay});
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" corridor/floor samples; three exterior cars without physical intersections. "+triangles+" triangles, three material batches. Public inspection remains Showroom, not a depot; rear preparation yard has no duplicate checkpoint.\n");
    }
    private static void ValidateLayout(Transform site,TownWorldPlaces town)
    {
        var details=site.Find("Showroom details v2");
        var planters=site.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Concrete planter").ToArray();
        Require(planters.Length==11,"Expected eleven exterior planter boxes after removing the protruding one.");
        foreach(var planter in planters)
            Require(planter.bounds.min.z>=116.4f&&planter.bounds.max.z<=155.4f,"Planter protrudes through the site perimeter.");
        foreach(var pose in TownShowroomPlacement.RearLayout)
        {
            var node=site.Find(pose.Name)??details.Find(pose.Name);
            Require(node!=null&&Vector3.Distance(node.position,TownShowroomPlacement.Point(pose.U,pose.V,pose.Height))<.001f,"Rear prop not placed: "+pose.Name);
            Require(Quaternion.Angle(node.rotation,Quaternion.Euler(0,pose.Yaw,0))<.01f,"Rear prop heading incorrect: "+pose.Name);
            foreach(var prop in node.GetComponentsInChildren<BoxCollider>())foreach(var other in town.GetComponentsInChildren<Collider>())
            {
                if(prop==other||other.isTrigger)continue;
                bool overlap=Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth);
                Require(!overlap||depth<.005f,"Rearranged prop "+pose.Name+" intersects "+other.name+" by "+depth+" m.");
            }
        }
        var bench=details.Find("GarageWorkbench");
        Require(Vector3.Dot(bench.forward,Vector3.right)>.999f,"Workbench does not face into the court from the showroom wall.");
        var benchBounds=bench.GetComponentInChildren<BoxCollider>().bounds;
        Require(benchBounds.min.x>298.01f&&benchBounds.min.x<298.10f,"Workbench back should be close to the wall without clipping.");
        // Width 2.5 m, depth 3.35 m in front of the pedestrian service door, separate from vehicle docking.
        var doorHits=Physics.OverlapBox(new Vector3(304.675f,1.2f,128.1f),new Vector3(1.675f,1,1.25f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
            .Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
        Require(doorHits.Length==0,"Service door approach blocked: "+string.Join(",",doorHits.Select(c=>c.name)));
        var service=site.Find("ServiceWing");
        var opaque=AssetDatabase.LoadAssetAtPath<Material>(TownShowroomPlacement.Root+"/Materials/Surface.mat");
        var vertices=site.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().sharedMaterial==opaque)
            .SelectMany(f=>f.sharedMesh.vertices.Select(p=>service.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
        foreach(float x in new[]{-13.2f,13.2f})foreach(float z in new[]{2.6f,2.8f})
            Require(vertices.Any(v=>(v-new Vector3(x,4.42f,z)).sqrMagnitude<.00000004f),"Continuous service parapet edge missing.");
        File.WriteAllText(Report+"/yard-layout.txt","PASS: protruding planter removed; eleven boxes remain inside perimeter; full service parapet corners checked; seven rear props placed/oriented; workbench back close to showroom wall; 2.5 x 3.35 m pedestrian door approach clear; rear props have no physical intersections.\n");
    }
    private static void ValidateDressing(Transform site,Transform hall,TownWorldPlaces town)
    {
        var details=site.Find("Showroom details v2");
        foreach(string id in new[]{"InteriorDressing","GarageWorkbench","PartsCabinet","HoseReel","ServicePlanter","ServicePlanter near hall"})
            Require(details.Find(id)!=null,"Missing showroom dressing: "+id);
        var interior=AssetDatabase.LoadAssetAtPath<Mesh>(TownShowroomPlacement.Root+"/Meshes/InteriorDressing.asset");
        int added=interior.triangles.Length/3+48+12;float ratio=(float)added/TownShowroomModels.OriginalInteriorTriangles;
        Require(ratio>=.4f&&ratio<=.5f,"Interior detail addition should remain approximately 40%: "+ratio);
        var surface=AssetDatabase.LoadAssetAtPath<Material>(TownShowroomPlacement.Root+"/Materials/Surface.mat");
        var vertices=site.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().sharedMaterial==surface)
            .SelectMany(f=>f.sharedMesh.vertices.Select(p=>hall.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
        bool Has(Vector3 p)=>vertices.Any(v=>(v-p).sqrMagnitude<.00000004f);
        foreach(float x in new[]{-7.06f,-6.14f})foreach(float z in new[]{-3.04f,-2.56f})
            foreach(float y in new[]{TownShowroomModels.InteriorFloor,.68f})
                Require(Has(new Vector3(x-.05f,y,z-.05f))&&Has(new Vector3(x+.05f,y,z+.05f)),"Coffee table support absent or above floor.");
        Require(Has(new Vector3(-1.6f,TownShowroomModels.InteriorFloor,-4.5f)),"Reception base does not touch tiled floor.");
        foreach(float x in new[]{-7.4f,-5.8f})Require(Has(new Vector3(x-.6f,TownShowroomModels.InteriorFloor,-4.6f)),"Armchair floats above floor.");
        var office=AssetDatabase.LoadAssetAtPath<Mesh>(TownShowroomPlacement.Root+"/Meshes/OfficeLink.asset");
        Require(office.triangles.Length/3==24&&office.bounds.max.y<=4.031f,"Protruding office parapet loop remains.");
        var cleared=town.GetComponentsInChildren<MeshFilter>().Single(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==TownShowroomPlacement.Root+"/Meshes/ResidualNatureTrim.asset");
        var oldNatureVertices=cleared.sharedMesh.vertices;var oldNatureUV=cleared.sharedMesh.uv;
        for(int i=0;i<oldNatureVertices.Length;i++)
        {
            var p=cleared.transform.TransformPoint(oldNatureVertices[i]);int color=Mathf.FloorToInt(oldNatureUV[i].x*32);
            Require(color<11||color>14||!TownShowroomPlacement.ResidualBushArea.Contains(new Vector2(p.x,p.z)),"Legacy bush still intersects the showroom or service props.");
        }
        foreach(var prop in details.GetComponentsInChildren<BoxCollider>())foreach(var other in town.GetComponentsInChildren<Collider>())
        {
            if(prop==other||other.isTrigger)continue;
            bool overlap=Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth);
            Require(!overlap||depth<.005f,"New rear detail intersects "+other.name+" by "+depth+" m.");
        }
        File.WriteAllText(Report+"/dressing.txt","PASS: "+added+" extra interior detail triangles ("+(ratio*100).ToString("F1")+"% of original display/furniture geometry); all four coffee-table legs grounded; reception and armchairs grounded; office parapet loop removed; two intersecting legacy bushes removed; six dressing placements and rear colliders checked.\n");
    }
    private static int CheckRoute(TownWorldPlaces town,Vector3[] route)
    {
        int probes=0;
        for(int leg=1;leg<route.Length;leg++)
        {
            var a=route[leg-1];var b=route[leg];var direction=b-a;int steps=Mathf.CeilToInt(direction.magnitude*2);
            for(int sample=0;sample<=steps;sample++)
            {
                var p=Vector3.Lerp(a,b,(float)sample/steps);probes++;
                var hits=Physics.OverlapBox(new Vector3(p.x,1.1f,p.z),new Vector3(1.35f,.65f,2.5f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                Require(hits.Length==0,"Showroom route blocked at "+p+": "+string.Join(",",hits.Select(c=>c.name)));
                Require(Physics.RaycastAll(new Vector3(p.x,4,p.z),Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"No physical showroom route floor at "+p);
            }
        }
        return probes;
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);Directory.CreateDirectory(Pictures);
        var city=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();
        var pickup=city.pickup.transform;pickup.SetPositionAndRotation(TownShowroomPlacement.ServiceBay,Quaternion.Euler(0,270,0));
        pickup.position+=Vector3.up*(.087f-pickup.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        TownFloristBatch.Render(scene,Pictures+"/Facade.png",new Vector3(248,15,103),new Vector3(291,2,131));
        TownFloristBatch.Render(scene,Pictures+"/ServiceCourt.png",new Vector3(294,18,169),new Vector3(303,2,139));
        TownFloristBatch.Render(scene,Pictures+"/Top.png",new Vector3(294,77,136),new Vector3(294,0,136),26);
        TownFloristBatch.Render(scene,Pictures+"/ThroughGlazing.png",TownShowroomPlacement.Point(11.5f,5.2f,2.1f),TownShowroomPlacement.Point(11.5f,13.5f,1.4f));
        var places=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var hall=places.transform.Find(TownShowroomPlacement.SiteName).Find("ShowroomHall");
        TownFloristBatch.Render(scene,Pictures+"/Interior.png",hall.TransformPoint(new Vector3(-9.2f,3.8f,5.15f)),hall.TransformPoint(new Vector3(0,1.1f,-2.8f)));
        TownFloristBatch.Render(scene,Pictures+"/RearDetails.png",new Vector3(302.5f,6.5f,135),new Vector3(302.4f,1.2f,122));
        TownFloristBatch.Render(scene,Pictures+"/RoofJunction.png",new Vector3(305,12,123),new Vector3(306.3f,4.03f,120),4);
        TownFloristBatch.Render(scene,Pictures+"/Boundary.png",new Vector3(288,9,109),new Vector3(284.5f,.5f,117));
        TownFloristBatch.Render(scene,Pictures+"/RiverContext.png",new Vector3(367,42,77),new Vector3(291,1,134));
        RenderSettings.fog=false;TownFloristBatch.Render(scene,Pictures+"/CityContext.png",new Vector3(146,240,-330),new Vector3(50,0,30));
        File.WriteAllText(Pictures+"/complete.txt","PASS: ten actual saved-scene Unity views, including interior, courtyard arrangement, perimeter and roof junction. Pickup posed temporarily for rear-yard scale; player spawn and scene not saved.\n");
    }
    private static string PrefabHash(){using(var input=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            RenderSettings.fog=false;
            TownFloristBatch.Render(scene,Pictures+"/SiteContextBefore.png",new Vector3(146,240,-330),new Vector3(50,0,30));
            TownFloristBatch.Render(scene,Pictures+"/SiteTopBefore.png",new Vector3(293,135,133),new Vector3(293,0,133),80);
            TownFloristBatch.Render(scene,Pictures+"/SiteBefore.png",new Vector3(369,55,34),new Vector3(292,0,102));
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                bool In(Vector3 p)=>p.x>=272&&p.x<=319&&p.z>=48&&p.z<=170;
                var lines=root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>In(p.b.center)).Select(p=>p.c.name+" | "+p.c.GetType().Name+" | min="+p.b.min.ToString("F2")+" | max="+p.b.max.ToString("F2")).ToList();
                var layout=root.GetComponent<TownWorldPlaces>().layout;
                foreach(float z in new[]{48f,72f,96f,120f,144f,168f})lines.Add("River at z="+z+": center="+layout.RiverCenter(z)+", west water edge="+(layout.RiverCenter(z)-14)+", west promenade="+(layout.RiverCenter(z)-23));
                var checkpoint=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
                lines.Add("Existing Showroom checkpoint="+checkpoint.transform.position+"; kind="+checkpoint.kind+"; requiresRearDocking="+checkpoint.requiresRearDocking);
                File.WriteAllLines(Report+"/site.txt",lines);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: read-only native riverside inspection.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt",error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
