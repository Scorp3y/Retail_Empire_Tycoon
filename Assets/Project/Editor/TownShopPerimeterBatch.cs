using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownShopPerimeterBatch
{
    internal const string Report="Library/TownShopPerimeterQA",Pictures="Docs/TownShopPerimeterReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool generate)
    {
        string stage=generate?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            if(generate)
            {
                var before=SavedRecords();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);
                string rootTransform=Id(prefab.transform);before.Remove(rootTransform);
                var resources=AssetDatabase.GetDependencies(TownWorldAssembler.PrefabPath,true).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p!=TownWorldAssembler.PrefabPath)
                    .Concat(Directory.GetFiles(TownForgottenYardAuthoring.Root,"*",SearchOption.AllDirectories))
                    .Concat(new[]{CityUpdateSetup.GamePath,StarterStoreSetup.BlueprintPath,TownForgottenYardAuthoring.LayoutPath})
                    .Distinct().ToDictionary(p=>p,TownForgottenYardBatch.Hash);
                var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try{new TownShopPerimeterPlacement(root).Build();PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();}
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var after=SavedRecords();foreach(var p in before)Require(after.TryGetValue(p.Key,out var text)&&text==p.Value,"Pre-existing town record changed: "+p.Key);
                prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);var site=prefab.transform.Find(TownShopPerimeterPlacement.SiteName);
                var owned=site.GetComponentsInChildren<Transform>(true).SelectMany(t=>new UnityEngine.Object[]{t.gameObject}.Concat(t.GetComponents<Component>())).Select(Id).ToHashSet();owned.Add(rootTransform);
                foreach(string id in after.Keys)Require(before.ContainsKey(id)||owned.Contains(id),"Unexpected addition outside owned perimeter: "+id);
                foreach(var p in resources)Require(File.Exists(p.Key)&&TownForgottenYardBatch.Hash(p.Key)==p.Value,"Protected resource changed: "+p.Key);
                File.WriteAllText(Report+"/protection.txt",$"PASS: all {before.Count} existing saved town records byte-identical except the root child list; {resources.Count} established resource files unchanged by SHA256. Game, tiny starter store, 24-parcel catalogue, detailed abandoned yard, city roads, suppliers and buildings preserved. Only an additive perimeter subtree was added.\n");
            }
            Capture();Validate();Receipt(stage);
            File.WriteAllText(Report+"/"+stage+".txt","PASS: additive perimeter, protected land, native model budgets, physical access and reopened saved-prefab checks.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var site=town.transform.Find(TownShopPerimeterPlacement.SiteName);Require(site!=null,"Missing perimeter section.");
        var filters=site.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(filters.Length<=16&&triangles<220000,"Perimeter render budget exceeded: "+filters.Length+" batches / "+triangles+" triangles.");
        foreach(var f in filters)
        {
            var mesh=f.sharedMesh;var vertices=mesh.vertices.Select(f.transform.TransformPoint).ToArray();
            Require(mesh.normals.Length==vertices.Length&&mesh.uv.Length==vertices.Length&&mesh.uv2.Length==vertices.Length,"Incomplete mesh channels: "+f.name);
            var outside=vertices.Where(v=>!TownShopPerimeterPlacement.Permissions.Any(r=>TownBakeryQuarterPlacement.Inside(r,v))).ToArray();
            Require(vertices.All(TownBakeryQuarterPlacement.Finite)&&outside.Length==0,"Perimeter visual outside the permitted band: "+f.transform.parent.name+"/"+f.name+" at "+outside.FirstOrDefault());
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
                foreach(var excluded in TownShopPerimeterPlacement.ShopExclusions)
                    Require(!TriangleEnters(excluded,vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]),"New geometry enters shop/gameplay land: "+f.name);
        }
        Require(site.GetComponentsInChildren<DeliveryCheckpoint>(true).Length==0,"Background buildings must not register new gameplay shops.");
        Require(site.GetComponentsInChildren<TownExpansionScenery>(true).Length==0,"Perimeter may not own purchasable scenery.");
        var nodes=site.GetComponentsInChildren<Transform>(true);
        var instances=nodes.Where(t=>t.parent!=null&&t.parent.parent==site).ToArray();
        int buildings=instances.Count(t=>new[]{"BrickWalkup","StuccoHouse","TownhouseA","TownhouseB","CornerDeli","CoffeeRoom","CycleWorkshop","Newsstand"}.Contains(t.name));
        Require(buildings==9,"Expected nine background buildings, found "+buildings);
        Physics.SyncTransforms();var all=town.GetComponentsInChildren<Collider>(true);var solids=site.GetComponentsInChildren<BoxCollider>(true);
        foreach(var c in solids)
        {
            var b=TownResidentialPlacement.ColliderBounds(c);
            Require(TownShopPerimeterPlacement.Permissions.Any(r=>TownBakeryQuarterPlacement.Inside(r,b.min)&&TownBakeryQuarterPlacement.Inside(r,b.max)),"Collider outside permitted band: "+c.transform.parent.name+"/"+c.name);
            foreach(var other in all)
            {
                if(other==c||other.isTrigger||other.transform.parent==c.transform.parent||other.transform.IsChildOf(c.transform.parent)||other.transform.IsChildOf(site)&&other is MeshCollider)continue;
                // Native terrain and path planes are support, not obstacles.
                var ob=TownResidentialPlacement.ColliderBounds(other);if(ob.max.y<.3f||!b.Intersects(ob))continue;
                Require(!Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<.006f,
                    "Physical intersection: "+c.transform.parent.name+"/"+c.name+" at "+c.transform.parent.position+" and "+other.transform.parent.name+"/"+other.name+" by "+depth);
            }
            Require(Physics.RaycastAll(new Vector3(b.center.x,.32f,b.center.z),Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider!=c&&h.normal.y>.8f&&h.point.y>=-.01f&&h.point.y<=.2f),"No physical support at "+c.transform.parent.name+"/"+c.name);
        }
        int samples=0;
        // Wider than the original 2.7 x 6.8 m pickup envelope; shop forecourt stays unaltered.
        for(float z=-143;z<=-118;z+=.5f){Clear(town,new Vector3(-24,1.5f,z),new Vector3(1.5f,.9f,3.4f));samples++;}
        for(float x=-40;x<=-24;x+=.5f){Clear(town,new Vector3(x,1.5f,-120),new Vector3(3.4f,.9f,1.5f));samples++;}
        foreach(var route in new[]{new[]{new Vector3(-112.5f,0,-136),new Vector3(-112.5f,0,-12)},new[]{new Vector3(-88.5f,0,-135),new Vector3(-88.5f,0,-13)},new[]{new Vector3(-15,0,-136),new Vector3(92,0,-136)},new[]{new Vector3(-69,0,-10),new Vector3(92,0,-10)},
            new[]{new Vector3(-112.5f,0,-28),new Vector3(-107.2f,0,-28)},new[]{new Vector3(-112.5f,0,-58.325f),new Vector3(-107.2f,0,-58.325f)},new[]{new Vector3(-112.5f,0,-91),new Vector3(-107.2f,0,-91)},
            new[]{new Vector3(-88.5f,0,-35.8f),new Vector3(-83.15f,0,-35.8f)},new[]{new Vector3(-83.15f,0,-35.8f),new Vector3(-83.15f,0,-34.2f)},
            new[]{new Vector3(-88.5f,0,-69.6f),new Vector3(-78.85f,0,-69.6f)},new[]{new Vector3(-97,0,-135.8f),new Vector3(-97,0,-125.4f)},
            new[]{new Vector3(15,0,-136),new Vector3(15,0,-134.2f)},new[]{new Vector3(65,0,-136),new Vector3(65,0,-134.2f)},new[]{new Vector3(82,0,-136),new Vector3(82,0,-132.8f)},
            new[]{new Vector3(-16,0,-10),new Vector3(-16,0,5)},new[]{new Vector3(31,0,-10),new Vector3(31,0,5)},new[]{new Vector3(82,0,-10),new Vector3(82,0,5)}})
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(route[0],route[1])*2);
            for(int i=0;i<=steps;i++)
            {
                var p=Vector3.Lerp(route[0],route[1],(float)i/steps);Clear(town,p+Vector3.up,new Vector3(.32f,.75f,.32f));samples++;
                Require(Physics.RaycastAll(p+Vector3.up*.3f,Vector3.down,.4f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(site)&&h.normal.y>.8f&&Mathf.Abs(h.point.y-TownShopPerimeterPlacement.Floor)<.005f),"Public walk lacks continuous paved support: "+p);
            }
        }
        File.WriteAllText(Report+"/access.txt",$"PASS: {samples} collision-free samples on the shop pickup approach, forecourt connection and public perimeter walks. Every new physical prop is in the permitted band, grounded and free from other solid obstacles. No land/city-road changes.\n");
        File.WriteAllText(Report+"/metrics.txt",$"Nine buildings (five homes, deli, coffee room, cycle workshop and newsstand), {instances.Count(t=>t.name=="UrbanTree")} new trees, {instances.Count(t=>t.name=="QuarterLamp")} lamps. Nine new reusable native model recipes plus existing neighbourhood furniture. {triangles} triangles, {filters.Length} visual batches. Buildings are decorative closed exteriors, not duplicate functional shops.\n");
    }
    private static void Clear(TownWorldPlaces town,Vector3 p,Vector3 half)
    {
        var hits=Physics.OverlapBox(p,half,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
        Require(hits.Length==0,"Route blocked at "+p+": "+string.Join(", ",hits.Select(c=>c.transform.parent.name+"/"+c.name)));
    }
    private static bool TriangleEnters(Rect r,Vector3 a,Vector3 b,Vector3 c)
    {
        // Clip projected triangles against the forbidden rectangle, including walls projected to a line.
        var polygon=new List<Vector2>{new(a.x,a.z),new(b.x,b.z),new(c.x,c.z)};
        const float margin=.0001f;r=Rect.MinMaxRect(r.xMin+margin,r.yMin+margin,r.xMax-margin,r.yMax-margin);
        if(Mathf.Max(a.x,b.x,c.x)<r.xMin||Mathf.Min(a.x,b.x,c.x)>r.xMax||Mathf.Max(a.z,b.z,c.z)<r.yMin||Mathf.Min(a.z,b.z,c.z)>r.yMax)return false;
        foreach(int edge in Enumerable.Range(0,4))
        {
            var input=polygon;polygon=new List<Vector2>();if(input.Count==0)return false;
            float D(Vector2 p)=>edge==0?p.x-r.xMin:edge==1?r.xMax-p.x:edge==2?p.y-r.yMin:r.yMax-p.y;
            var prev=input[input.Count-1];float dp=D(prev);
            foreach(var p in input){float d=D(p);if((d>=0)!=(dp>=0))polygon.Add(Vector2.Lerp(prev,p,dp/(dp-d)));if(d>=0)polygon.Add(p);prev=p;dp=d;}
        }
        return polygon.Count>0;
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);RenderSettings.fog=false;
        TownFloristBatch.Render(scene,Pictures+"/Overview.png",new Vector3(-154,112,-217),new Vector3(-24,0,-72),88);
        TownFloristBatch.Render(scene,Pictures+"/Top.png",new Vector3(-20,190,-75),new Vector3(-20,0,-75),98);
        TownFloristBatch.Render(scene,Pictures+"/ResidentialStreet.png",new Vector3(-140,28,-116),new Vector3(-94,4,-57));
        TownFloristBatch.Render(scene,Pictures+"/DeliCourtyard.png",new Vector3(-125,17,-153),new Vector3(-95,2,-123));
        TownFloristBatch.Render(scene,Pictures+"/HighStreet.png",new Vector3(91,21,-170),new Vector3(43,2,-129));
        TownFloristBatch.Render(scene,Pictures+"/GardenWalk.png",new Vector3(86,20,-45),new Vector3(20,1,-12));
    }
    private static string Id(UnityEngine.Object obj){if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id))throw new InvalidOperationException("Nonpersistent record: "+obj.name);return id.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    private static Dictionary<string,string> SavedRecords()=>Regex.Matches(File.ReadAllText(TownWorldAssembler.PrefabPath),@"(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value.Replace("\r\n","\n"));
    private static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownShopPerimeterPlacement.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownShopPerimeterPlacement.Root+".meta",TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>TownForgottenYardBatch.Hash(p)+" "+p.Replace('\\','/')));
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);RenderSettings.fog=false;
            TownFloristBatch.Render(scene,Pictures+"/BeforeTop.png",new Vector3(-20,190,-75),new Vector3(-20,0,-75),98);
            TownFloristBatch.Render(scene,Pictures+"/Before.png",new Vector3(-184,145,-235),new Vector3(-25,0,-73));
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                File.WriteAllLines(Report+"/site.txt",root.GetComponentsInChildren<Collider>(true).Select(c=>(c,b:TownResidentialPlacement.ColliderBounds(c)))
                    .Where(p=>p.b.max.x>-130&&p.b.min.x<100&&p.b.max.z>-150&&p.b.min.z<10)
                    .Select(p=>p.c.transform.parent.name+"/"+p.c.name+" | "+p.b.min.ToString("F2")+".."+p.b.max.ToString("F2")));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(Report+"/inspect.txt","PASS: native site surveyed without changes.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
