using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownDairyBatch
{
    internal const string Report="Library/TownDairyQA",Pictures="Docs/TownDairyReview";
    internal static readonly Vector3 Origin=new(525,0,122);
    internal static readonly Quaternion Rotation=Quaternion.Euler(0,-60,0);
    // Concept X increases to the right when viewed from the road; local +X faces screen-left.
    internal static Vector3 World(float x,float y,float z)=>Origin+Rotation*new Vector3(-x,y,z);
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownDairyPlacement.Permissions);string farm=TownFarmPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);string contracts=Contracts(root);var identities=root.GetComponent<TownWorldPlaces>().suppliers.Select(c=>c.GetInstanceID()).ToArray();
                new TownDairyPlacement(root).Build();Require(contracts==Contracts(root)&&identities.SequenceEqual(root.GetComponent<TownWorldPlaces>().suppliers.Select(c=>c.GetInstanceID())),"Original supplier contracts or identities changed.");homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownDairyPlacement.Permissions));Require(TownFarmPreservation.Capture()==farm,"Remote farm changed.");
            File.WriteAllText(Report+"/protection.txt","PASS: exact visual/material/collider fingerprint outside authorised dairy clearance unchanged; native trees cleared by complete template recognition only; farm, original supplier identities/contracts, residential buildings and doors unchanged.\n"+outside+"\n");
            Capture();Validate();File.WriteAllText(Report+"/result.txt","PASS: native dairy compound and separate road links generated, saved-scene placement and scope protection passed.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Failure("result.txt",e);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Capture();Validate();File.WriteAllText(Report+"/review.txt","PASS: native dairy reopened, captured and revalidated in fresh Unity process.\n");File.WriteAllText(Report+"/review-input.sha256",PrefabHash());EditorApplication.Exit(0);}
        catch(Exception e){Failure("review.txt",e);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>()).Single();var site=town.transform.Find(TownDairyPlacement.SiteName);Require(site!=null,"Dairy missing.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);Require(filters.Length==3&&triangles<120000,"Dairy geometry/material budget exceeded.");
        foreach(var f in filters)Require(f.sharedMesh.vertices.Select(f.transform.TransformPoint).All(p=>TownBakeryQuarterPlacement.Finite(p)&&TownDairyPlacement.Permissions.Any(r=>TownDairyPlacement.Inside(r,p))),"Dairy geometry outside marked clearance.");
        Require(site.GetComponentsInChildren<DeliveryCheckpoint>().Length==0&&town.suppliers.Length==7&&town.GetComponentsInChildren<SupplierLoadingCourt>().Length==6,"Duplicate gameplay supplier introduced.");
        var all=town.GetComponentsInChildren<Collider>();var solids=site.GetComponentsInChildren<BoxCollider>();Physics.SyncTransforms();
        foreach(var prop in solids)
        {
            var b=prop.bounds;Require(TownDairyPlacement.Permissions.Any(r=>TownDairyPlacement.Inside(r,b.min)&&TownDairyPlacement.Inside(r,b.max)),"Dairy prop outside parcel.");
            float expected=prop.name=="Cooling unit"?TownDairyPlacement.Floor+8.24f:prop.name=="Cold roof"?TownDairyPlacement.Floor+8:TownDairyPlacement.Floor;
            Require(Mathf.Abs(b.min.y-expected)<.003f,"Floating/buried "+prop.transform.parent.name+"/"+prop.name+" bottom="+b.min.y+" expected="+expected);
            Require(Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.03f,b.center.z),Vector3.down,.08f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider!=prop&&h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&Mathf.Abs(h.point.y-b.min.y)<.004f),"No physical support under "+prop.transform.parent.name+"/"+prop.name);
            foreach(var other in all)
            {
                if(other==prop||other.isTrigger||prop.transform.parent==other.transform.parent)continue;
                Require(!Physics.ComputePenetration(prop,prop.transform.position,prop.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<.005f,"Dairy prop "+prop.transform.parent.name+"/"+prop.name+" at "+prop.transform.parent.position+" intersects "+other.transform.parent.name+"/"+other.name+" by "+depth);
            }
        }
        Require(solids.Count(c=>c.name=="Milk silo")==3&&solids.Count(c=>c.name=="Parked milk tanker")==1,"Missing silo/tanker layout.");
        var placement=new TownDairyPlacement(town.gameObject);int samples=0;
        samples+=Route(town,new[]{placement.NearestStreet(World(-30,0,38)),World(-30,0,38),World(-30,0,17)});
        samples+=Route(town,new[]{placement.NearestStreet(World(30,0,38)),World(30,0,38),World(30,0,23),World(23,0,23),World(23,0,4.5f)});
        foreach(var pair in new[]{(World(0,0,38),World(0,0,13)),(World(-9,0,21),World(-9,0,18.5f))})
            for(int i=0;i<=Mathf.CeilToInt(Vector3.Distance(pair.Item1,pair.Item2)*2);i++){float t=i/(float)Mathf.CeilToInt(Vector3.Distance(pair.Item1,pair.Item2)*2);var p=Vector3.Lerp(pair.Item1,pair.Item2,t);Clear(town,p+Vector3.up*1,new Vector3(.65f,.65f,.65f),Rotation);}
        // Existing forest road cannot be obstructed by new fences, parked cars or retained invisible trunks.
        foreach(var p in town.layout.countryRoad.points.Where(p=>p.z>70&&p.z<195))Clear(town,p+Vector3.up*1.1f,new Vector3(1.35f,.65f,3.4f),Quaternion.LookRotation(new Vector3(.5f,0,.86f)));
        int residual=0;var leftovers=new System.Collections.Generic.List<string>();
        foreach(var f in town.GetComponentsInChildren<MeshFilter>().Where(f=>!f.transform.IsChildOf(site)))
        {
            var vertices=f.sharedMesh.vertices.Select(p=>site.InverseTransformPoint(f.transform.TransformPoint(p))).ToArray();var indices=f.sharedMesh.triangles;var core=new Rect(-42,-37,84,74);
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                if(TownDairyPlacement.Inside(core,a)&&TownDairyPlacement.Inside(core,b)&&TownDairyPlacement.Inside(core,c)&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.14f){residual++;if(leftovers.Count<6)leftovers.Add(f.transform.parent.name+"/"+f.name+" centre(local)="+((a+b+c)/3).ToString("F2"));}
            }
        }
        File.WriteAllLines(Report+"/residual.txt",leftovers);Require(residual==0,"Residual legacy woodland on the paved dairy core: "+residual+" triangles: "+string.Join(";",leftovers));
        int boundary=0;
        foreach(var span in new[]{(new Vector2(-43,-38),new Vector2(43,-38)),(new Vector2(-43,-38),new Vector2(-43,38)),(new Vector2(43,-38),new Vector2(43,38))})
        {
            int count=Mathf.CeilToInt(Vector2.Distance(span.Item1,span.Item2));for(int i=0;i<=count;i++){var p=Vector2.Lerp(span.Item1,span.Item2,i/(float)count);Require(Physics.OverlapBox(World(p.x,1,p.y),Vector3.one*.1f).Any(c=>c.transform.IsChildOf(site)&&c.name=="Perimeter fence"),"Dairy boundary gap.");boundary++;}
        }
        File.WriteAllText(Report+"/access.txt","PASS: "+samples+" design-envelope 2.7 x 6.8 m route/floor samples; existing country-road corridor, office doors and pedestrian approaches clear; all native props grounded, within marked clearance and free of physical intersections; "+boundary+" perimeter samples. Three silos, tanker, connected halls, two staff cars. "+triangles+" triangles / three visual batches. Dairy is scenery, no new supplier/economy.\n");
    }
    private static int Route(TownWorldPlaces town,Vector3[] points)
    {
        int total=0;for(int leg=1;leg<points.Length;leg++){var delta=Vector3.ProjectOnPlane(points[leg]-points[leg-1],Vector3.up);int count=Mathf.CeilToInt(delta.magnitude*2);Require(count>0,"Empty dairy route leg.");for(int i=0;i<=count;i++){var p=Vector3.Lerp(points[leg-1],points[leg],i/(float)count);total++;Clear(town,p+Vector3.up*1.1f,new Vector3(1.35f,.65f,3.4f),Quaternion.LookRotation(delta));Require(Physics.RaycastAll(p+Vector3.up*4,Vector3.down,5,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f),"Missing driveway floor at "+p);}}return total;
    }
    private static void Clear(TownWorldPlaces town,Vector3 p,Vector3 half,Quaternion rotation)
    {var hits=Physics.OverlapBox(p,half,rotation,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();Require(hits.Length==0,"Dairy access blocked at "+p+": "+string.Join(",",hits.Select(c=>c.transform.parent.name+"/"+c.name)));}
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);Directory.CreateDirectory(Pictures);RenderSettings.fog=false;
        var city=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>()).Single();var car=city.pickup.transform;car.SetPositionAndRotation(World(23,0,4.5f),Rotation);car.position+=Vector3.up*(TownDairyPlacement.Floor-car.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        TownFloristBatch.Render(scene,Pictures+"/Facade.png",World(58,34,90),World(0,3,-1));
        TownFloristBatch.Render(scene,Pictures+"/Top.png",Origin+Vector3.up*130,Origin,68);
        TownFloristBatch.Render(scene,Pictures+"/MilkIntake.png",World(-70,20,48),World(-28,4,-10));
        TownFloristBatch.Render(scene,Pictures+"/Dispatch.png",World(58,17,35),World(25,2,-6));
        TownFloristBatch.Render(scene,Pictures+"/Office.png",World(-28,10,51),World(-8,2,15));
        TownFloristBatch.Render(scene,Pictures+"/ForestContext.png",new Vector3(140,220,-140),new Vector3(475,0,130));
        File.WriteAllText(Pictures+"/complete.txt","PASS: six actual native saved-scene Unity camera views, not generated concept art; pickup temporarily posed for scale, scene and player spawn not saved.\n");
    }
    private static string Contracts(GameObject root)=>string.Join("\n",root.GetComponent<TownWorldPlaces>().suppliers.OrderBy(c=>c.supplier).Select(c=>JsonUtility.ToJson(c)+"|"+c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")))+"\n"+string.Join("\n",root.GetComponentsInChildren<SupplierLoadingCourt>().OrderBy(c=>c.checkpoint.supplier).Select(JsonUtility.ToJson));
    internal static string PrefabHash(){using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static void Failure(string report,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+report,"FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(825,265,-130),new Vector3(435,0,150));
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(515,220,145),new Vector3(515,0,145),115);
            var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>()).Single();
            File.WriteAllLines(Report+"/site.txt",town.GetComponentsInChildren<Collider>().Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c))).Where(p=>p.b.max.x>430&&p.b.min.x<610&&p.b.max.z>40&&p.b.min.z<200).Select(p=>p.c.transform.parent.name+"/"+p.c.name+" | "+p.c.transform.position.ToString("F2")+" | "+p.b.min.ToString("F2")+".."+p.b.max.ToString("F2")));
            var points=town.layout.countryRoad.points.Where(p=>p.z>80&&p.z<190).ToArray();File.WriteAllLines(Report+"/road.txt",points.Select(p=>p.ToString("F3")));
            // Temporary physical-layout outline is evidence only; no scene/assets are saved.
            foreach(var pair in new[]{(World(-44,.2f,-39),World(44,.2f,-39)),(World(44,.2f,-39),World(44,.2f,39)),(World(44,.2f,39),World(-44,.2f,39)),(World(-44,.2f,39),World(-44,.2f,-39))})
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Temporary dairy parcel outline";var delta=pair.Item2-pair.Item1;go.transform.position=(pair.Item1+pair.Item2)/2;go.transform.rotation=Quaternion.LookRotation(delta);go.transform.localScale=new Vector3(.6f,.6f,delta.magnitude);go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
            }
            TownFloristBatch.Render(scene,Pictures+"/MarkedSite.png",new Vector3(825,265,-130),new Vector3(435,0,150));
            File.WriteAllText(Report+"/inspect.txt","PASS: marked eastern woodland and country-road access surveyed without saving changes.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
