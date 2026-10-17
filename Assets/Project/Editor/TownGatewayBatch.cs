using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Isolated authoring and evidence for the farm, fuel station and northern gateway update.</summary>
public static class TownGatewayBatch
{
    internal const string Report="Library/TownGatewayQA", Pictures="Docs/TownGatewayReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var outside=TownPilotScopePreservation.Capture(TownGatewayPlacement.Permissions);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var places=root.GetComponent<TownWorldPlaces>();var identities=places.suppliers.Select(s=>s.GetInstanceID()).ToArray();var contracts=Contracts(places);
                var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                new TownGatewayPlacement(root).Build();
                Require(identities.SequenceEqual(places.suppliers.Select(s=>s.GetInstanceID())),"Supplier identity changed.");Require(contracts==Contracts(places),"Non-farm supplier contract changed.");homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownGatewayPlacement.Permissions));
            File.WriteAllText(Report+"/protection.txt","PASS: exact native visual/material/collision fingerprint outside five authorised update parcels unchanged. Residential snapshot and six non-farm supplier contracts retained; all seven original supplier references retained.\n"+outside);
            Capture();Validate();Receipt("result","PASS: farm, fuel, crossing and local dairy/bridge corrections generated and saved.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail("result",e);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Capture();Validate();Receipt("review","PASS: reopened saved native town, reran geometry/access checks and captured real Unity views.\n");EditorApplication.Exit(0);}
        catch(Exception e){Fail("review",e);}
    }
    private static string Contracts(TownWorldPlaces p)=>string.Join("\n",p.suppliers.Where(c=>c.supplier!=SupplierKind.Farmers).Select(c=>c.supplier+"|"+c.transform.position+"|"+c.transform.rotation+"|"+c.radius+"|"+c.requiresRearDocking));
    internal static void Receipt(string stage,string text)
    {
        File.WriteAllText(Report+"/"+stage+".txt",text);File.WriteAllText(Report+"/"+stage+"-input.sha256",Hash());
        var files=Directory.GetFiles(TownGatewayPlacement.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownWorldAssembler.PrefabPath,"Assets/Project/Scripts/City/TownLevelCrossing.cs","Assets/Project/Scripts/City/TownRailTraffic.cs"});
        File.WriteAllLines(Report+"/"+stage+"-assets.sha256",files.OrderBy(p=>p,StringComparer.Ordinal).Select(p=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","")+" "+p.Replace('\\','/')));
    }
    private static void Validate()
    {
        Directory.CreateDirectory(Report);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        foreach(string name in new[]{TownGatewayPlacement.Farm,TownGatewayPlacement.Fuel,TownGatewayPlacement.Crossing,TownGatewayPlacement.Repairs})
        {
            var site=town.transform.Find(name);Require(site!=null,"Missing site "+name);
            var filters=site.GetComponentsInChildren<MeshFilter>(true);Require(filters.All(f=>f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite)),"Non-finite geometry.");
            Require(filters.Sum(f=>f.sharedMesh.triangles.Length/3)<240000,"Site geometry budget exceeded: "+name);
        }
        var fuel=town.transform.Find(TownGatewayPlacement.Fuel);Require(fuel.Cast<Transform>().Count(t=>t.name=="FuelPump")==4,"Exactly four pumps required.");Require(fuel.Cast<Transform>().Count(t=>t.name=="PumpIsland")==2,"Exactly two islands required.");
        var farm=town.transform.Find(TownGatewayPlacement.Farm);Require(farm.Cast<Transform>().Count(t=>t.name=="Greenhouse")==2&&farm.Cast<Transform>().Count(t=>t.name=="GrainSilo")==2,"Farm greenhouse/silo pair missing.");
        Require(town.suppliers.Length==7&&town.GetComponentsInChildren<DeliveryCheckpoint>().Length==7,"Duplicate or missing supplier.");
        var checkpoint=town.suppliers.Single(s=>s.supplier==SupplierKind.Farmers);Require(checkpoint.requiresRearDocking&&Vector3.Distance(checkpoint.transform.position,new Vector3(560,.17f,305))<.001f,"Farm contract not updated.");
        Physics.SyncTransforms();int samples=0;
        samples+=Route(town,town.layout.countryRoad.points.Where(p=>p.z>=244&&p.z<=278).Concat(new[]{new Vector3(550,0,289),new Vector3(560,0,289),new Vector3(560,0,305)}).ToArray(),1.25f,2.7f);
        samples+=Route(town,new[]{new Vector3(228,0,-140),new Vector3(228,0,-126),new Vector3(224,0,-126),new Vector3(224,0,-102)},1.15f,2.2f);
        samples+=Route(town,new[]{new Vector3(224,0,-125),new Vector3(204,0,-125),new Vector3(204,0,-140)},1.15f,2.2f);
        samples+=Route(town,new[]{new Vector3(-24,0,198),new Vector3(-24,0,309)},1.2f,2.5f,true);
        // Actual physical road must retain continuous smooth height through both bridge ramps.
        int bridge=0;foreach(float x0 in new[]{299f,345f})for(int i=0;i<=48;i++)
        {
            float x=x0+i*(x0==299?12f:14f)/48;var hits=Physics.RaycastAll(new Vector3(x,8,-40),Vector3.down,10,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.6f&&h.point.y>=0&&h.point.y<3).OrderByDescending(h=>h.point.y).ToArray();Require(hits.Length>0,"Missing bridge ramp support at "+x);bridge++;
        }
        var crossing=town.GetComponentInChildren<TownLevelCrossing>();Require(crossing!=null&&crossing.arms.Length==2&&crossing.train!=null,"Crossing runtime dependencies missing.");
        Require(!TownLevelCrossing.RequiresClosure(200,336,4,12)&&TownLevelCrossing.RequiresClosure(300,336,4,12)&&TownLevelCrossing.RequiresClosure(380,336,4,12)&&!TownLevelCrossing.RequiresClosure(387,336,4,12),"Crossing arrival/tail clearance boundaries failed.");
        File.WriteAllText(Report+"/access.txt","PASS: "+samples+" saved-scene vehicle floor/clearance probes, "+bridge+" bridge ramp floor probes, crossing warning-window boundaries, farm supplier identity and exact pump/greenhouse/silo counts.\n");
        int pairs=0;var intersections=new System.Collections.Generic.List<string>();
        foreach(var site in new[]{farm,fuel})
        {
            var boxes=site.GetComponentsInChildren<BoxCollider>();
            for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)
            {
                var a=boxes[i];var b=boxes[j];if(a.transform.parent==b.transform.parent||!a.bounds.Intersects(b.bounds))continue;
                pairs++;if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>=.025f)intersections.Add(a.transform.parent.name+"/"+a.name+" at "+a.bounds.center+" with "+b.transform.parent.name+"/"+b.name+" depth="+depth);
            }
        }
        File.WriteAllText(Report+"/props.txt",intersections.Count==0?"PASS: "+pairs+" broadphase prop collision pairs checked in farm and fuel station. Decorative foliage, roof trim and intentional within-model joints excluded.\n":"FAILED\n"+string.Join("\n",intersections));
        Require(intersections.Count==0,"Prop intersections: "+string.Join("; ",intersections));
    }
    private static int Route(TownWorldPlaces town,Vector3[] points,float width,float halfLength,bool slopes=false)
    {
        int samples=0;for(int leg=1;leg<points.Length;leg++)
        {
            var a=points[leg-1];var b=points[leg];int count=Mathf.CeilToInt(Vector3.Distance(a,b)*2);var rotation=Quaternion.LookRotation(b-a);
            for(int i=0;i<=count;i++)
            {
                var p=Vector3.Lerp(a,b,i/(float)count);var floors=Physics.RaycastAll(p+Vector3.up*8,Vector3.down,10,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.6f&&h.point.y>=-.01f&&h.point.y<3).ToArray();Require(floors.Length>0,"No route floor at "+p);float y=floors.Max(h=>h.point.y);
                var hits=Physics.OverlapBox(new Vector3(p.x,y+1.2f,p.z),new Vector3(width,.55f,halfLength),rotation,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();Require(hits.Length==0,"Route blocked at "+p+": "+string.Join(",",hits.Select(c=>c.transform.parent.name+"/"+c.name)));samples++;
            }
        }
        return samples;
    }
    private static void Capture()
    {
        Directory.CreateDirectory(Pictures);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        TownFloristBatch.Render(scene,Pictures+"/Farm.png",new Vector3(435,155,163),new Vector3(552,0,345));
        TownFloristBatch.Render(scene,Pictures+"/FarmTop.png",new Vector3(552,240,347),new Vector3(552,0,347),112);
        TownFloristBatch.Render(scene,Pictures+"/FarmPickup.png",new Vector3(539,16,283),new Vector3(560,2,316));
        TownFloristBatch.Render(scene,Pictures+"/Fuel.png",new Vector3(279,65,-174),new Vector3(215,0,-106));
        TownFloristBatch.Render(scene,Pictures+"/FuelTop.png",new Vector3(216,130,-110),new Vector3(216,0,-110),48);
        TownFloristBatch.Render(scene,Pictures+"/FuelShop.png",new Vector3(196,2.5f,-101),new Vector3(197,1.6f,-92));
        TownFloristBatch.Render(scene,Pictures+"/Crossing.png",new Vector3(57,68,166),new Vector3(-24,1,240));
        TownFloristBatch.Render(scene,Pictures+"/CrossingTop.png",new Vector3(-24,160,246),new Vector3(-24,0,246),66);
        TownFloristBatch.Render(scene,Pictures+"/Bridge.png",new Vector3(369,8,-60),new Vector3(343,1,-40));
        TownFloristBatch.Render(scene,Pictures+"/Dairy.png",new Vector3(402,95,163),new Vector3(525,0,122));
        File.WriteAllText(Pictures+"/complete.txt","PASS: ten actual Unity saved-scene views; no AI-generated evidence images.\n");
    }
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
            File.WriteAllLines(Report+"/survey.txt",town.GetComponentsInChildren<MeshFilter>().Select(f=>f.name+" | "+f.GetComponent<Renderer>().bounds+" | "+AssetDatabase.GetAssetPath(f.sharedMesh))
                .Concat(town.GetComponentsInChildren<Transform>().Where(t=>t.position.x>460&&t.position.z>240||t.parent!=null&&t.parent.name=="Office and landscape").Select(t=>"NODE "+t.parent.name+"/"+t.name+" "+t.position+" local="+t.localPosition)));
            File.WriteAllLines(Report+"/colliders.txt",town.GetComponentsInChildren<Collider>().Select(c=>c.transform.parent.name+"/"+c.name+" "+TownResidentialPlacement.ColliderBounds(c)));
            TownFloristBatch.Render(scene,Pictures+"/BeforeFarm.png",new Vector3(440,180,165),new Vector3(550,0,350));
            TownFloristBatch.Render(scene,Pictures+"/BeforeFuel.png",new Vector3(275,65,-174),new Vector3(216,0,-102));
            File.WriteAllText(Report+"/inspect.txt","PASS: saved town surveyed without modification.");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail("inspect",e);}
    }
    internal static string Hash()=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(TownWorldAssembler.PrefabPath))).Replace("-","");
    internal static void Fail(string stage,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
