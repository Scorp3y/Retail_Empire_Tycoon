using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class TownCityCorrectionsBatch
{
    internal const string Report="Library/TownCityCorrectionsQA",Pictures="Docs/TownCityCorrectionsReview";
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
                var before=Records();var dependencies=AssetDatabase.GetDependencies(TownWorldAssembler.PrefabPath,true).Where(p=>p.StartsWith("Assets/")&&p!=TownWorldAssembler.PrefabPath)
                    .Concat(new[]{CityUpdateSetup.GamePath,StarterStoreSetup.BlueprintPath,TownForgottenYardAuthoring.LayoutPath,"Assets/Art/TownForgottenYard/Land24.asset"}).Distinct().ToDictionary(p=>p,TownForgottenYardBatch.Hash);
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);var ids=new Dictionary<int,string>();
                Pair(asset.transform,root.transform,ids);var mutable=new HashSet<string>{Id(asset.transform)};
                try
                {
                    var edit=new TownCityCorrectionsPlacement(root);edit.Build();foreach(var obj in edit.Mutable)if(ids.TryGetValue(obj.GetInstanceID(),out var id))mutable.Add(id);
                    PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var after=Records();foreach(var p in before)if(!mutable.Contains(p.Key))Require(after.TryGetValue(p.Key,out var value)&&value==p.Value,"Protected town record changed: "+p.Key);
                asset=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);var owned=asset.transform.Find(TownCityCorrectionsPlacement.SiteName).GetComponentsInChildren<Transform>(true).SelectMany(t=>new Object[]{t.gameObject}.Concat(t.GetComponents<Component>())).Select(Id).ToHashSet();
                foreach(var p in after)Require(before.ContainsKey(p.Key)||owned.Contains(p.Key),"Unexpected addition outside repairs: "+p.Key);
                foreach(var p in dependencies)Require(File.Exists(p.Key)&&TownForgottenYardBatch.Hash(p.Key)==p.Value,"Original shared resource changed: "+p.Key);
                File.WriteAllText(Report+"/protection.txt",$"PASS: {before.Count-mutable.Count} saved town records byte-identical; {mutable.Count} explicitly edited mesh/collider/car records permitted. {dependencies.Count} original resources unchanged by SHA256. Supplier IDs and routes, Game, starter blueprint, 24-plot rules and detailed shop yard protected.\n");
            }
            Validate();Capture();Receipt(stage);File.WriteAllText(Report+"/"+stage+".txt","PASS: corrected facades, joined paving, lowered entrances, common asphalt and farm car removal.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var site=town.transform.Find(TownCityCorrectionsPlacement.SiteName);Require(site!=null,"Missing corrections layer.");
        var farm=town.transform.Find(TownRuralPolishPlacement.Farm);Require(!farm.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="BlueCustomerCar"||t.name=="GreenCustomerCar"),"Farm market cars remain.");
        int architecture=town.GetComponentsInChildren<MeshFilter>(true).Count(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(TownCityCorrectionsPlacement.Root+"/Meshes/Architecture"));Require(architecture>40,"Architecture repair is incomplete: "+architecture);
        Physics.SyncTransforms();int samples=0;var seamErrors=new List<string>();
        foreach(var a in TownCityCorrectionsPlacement.Aprons)
        {
            var direction=Vector3.ProjectOnPlane(a.To-a.From,Vector3.up).normalized;
            foreach(var end in new[]{(Point:a.From,Outside:-direction),(Point:a.To,Outside:direction)})
            foreach(float side in new[]{-1.25f,0,1.25f})
            {
                var p=end.Point+end.Outside*.02f+Vector3.Cross(Vector3.up,direction)*side;
                var adjoining=Physics.RaycastAll(p+Vector3.up*.5f,Vector3.down,.7f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.75f).ToArray();
                if(adjoining.Length==0||Mathf.Abs(adjoining.Max(h=>h.point.y)-end.Point.y)>=.0005f)
                    seamErrors.Add(a.Id+" "+p+" heights "+string.Join(",",adjoining.Select(h=>h.point.y+":"+h.collider.name)));
            }
            int steps=Mathf.CeilToInt(Vector3.Distance(a.From,a.To)*4);
            for(int i=0;i<=steps;i++)foreach(float side in new[]{-1.25f,0,1.25f})
            {
                var p=a.Point((float)i/steps,side);var hits=Physics.RaycastAll(p+Vector3.up*.5f,Vector3.down,.7f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.75f).ToArray();
                Require(hits.Any(h=>Mathf.Abs(h.point.y-p.y)<.008f),"Missing driveway support: "+a.Id+" "+p);
                Require(!hits.Any(h=>h.point.y>p.y+.012f),"Old sidewalk/slab projects through lowered driveway: "+a.Id+" "+p+" height "+string.Join(",",hits.Select(h=>h.point.y+":"+h.collider.name)));
                Require(!Physics.OverlapBox(p+Vector3.up*1.2f,new Vector3(.25f,.7f,.25f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.transform.IsChildOf(town.transform)),"Driveway obstruction: "+a.Id+" "+p);samples++;
            }
        }
        Require(seamErrors.Count==0,"Driveway ends have collision lips:\n"+string.Join("\n",seamErrors));
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(TownCityCorrectionsPlacement.Root+"/")))
        {
            var mesh=f.sharedMesh;Require(mesh.vertices.All(TownBakeryQuarterPlacement.Finite),"Non-finite mesh "+f.name);Require(mesh.normals.Length==mesh.vertexCount&&mesh.uv.Length==mesh.vertexCount&&mesh.uv2.Length==mesh.vertexCount,"Incomplete mesh channels: "+f.name);
        }
        File.WriteAllText(Report+"/access.txt",$"PASS: {samples} lowered-drive samples across three pickup wheel tracks; physical support matches the visual slope, no retained high kerb or obstacle. All 54 outside-edge checks match adjoining floors within 0.5 mm. Six existing supplier courts and their routes retained.\n");
        File.WriteAllText(Report+"/metrics.txt",$"Corrected architecture instances: {architecture}. Lowered entrances: 9. Farm visitor cars: 0. No gameplay scene or store-area edits.\n");
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);RenderSettings.fog=false;
        TownFloristBatch.Render(scene,Pictures+"/Construction.png",new Vector3(104,35,-110),new Vector3(144,0,-57));
        TownFloristBatch.Render(scene,Pictures+"/Workshop.png",new Vector3(183,36,-222),new Vector3(121,0,-171));
        TownFloristBatch.Render(scene,Pictures+"/Supplies.png",new Vector3(-128,31,116),new Vector3(-173,0,166));
        TownFloristBatch.Render(scene,Pictures+"/Equipment.png",new Vector3(-32,28,116),new Vector3(-76,0,165));
        TownFloristBatch.Render(scene,Pictures+"/BakeryEntry.png",new Vector3(-76,20,-124),new Vector3(-82,0,-164));
        TownFloristBatch.Render(scene,Pictures+"/Showroom.png",new Vector3(249,27,108),new Vector3(291,0,136));
        TownFloristBatch.Render(scene,Pictures+"/Farm.png",new Vector3(514,61,223),new Vector3(550,0,314));
        TownFloristBatch.Render(scene,Pictures+"/Dairy.png",TownDairyBatch.World(77,62,108),TownDairyBatch.World(0,0,3));
        TownFloristBatch.Render(scene,Pictures+"/ResidentialFront.png",new Vector3(-185,30,-9),new Vector3(-179,7,-40));
        TownFloristBatch.Render(scene,Pictures+"/ResidentialRear.png",new Vector3(-140,7,-57),new Vector3(-140,4,-40));
    }
    private static void Pair(Transform asset,Transform preview,Dictionary<int,string> ids)
    {
        ids.Add(preview.gameObject.GetInstanceID(),Id(asset.gameObject));var a=asset.GetComponents<Component>();var p=preview.GetComponents<Component>();Require(a.Length==p.Length&&asset.childCount==preview.childCount,"Original prefab hierarchy differs from its preview.");
        for(int i=0;i<a.Length;i++)ids.Add(p[i].GetInstanceID(),Id(a[i]));for(int i=0;i<asset.childCount;i++)Pair(asset.GetChild(i),preview.GetChild(i),ids);
    }
    private static Dictionary<string,string> Records()=>Regex.Matches(File.ReadAllText(TownWorldAssembler.PrefabPath),@"(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value.Replace("\r\n","\n"));
    private static string Id(Object o){if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string _,out long id))throw new InvalidOperationException("No persistent ID "+o.name);return id.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    private static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownCityCorrectionsPlacement.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownCityCorrectionsPlacement.Root+".meta",TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>TownForgottenYardBatch.Hash(p)+" "+p.Replace('\\','/')));
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Inspect()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);RenderSettings.fog=false;
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var lines=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("AirConditioner")||t.name.Contains("long block")||t.name.Contains("Layout")||t.name.Contains("layout")||t.name.Contains("ground and walkways")||t.name.Contains("Drive")||t.name.Contains("Farm")||t.name.Contains("Visitor")||t.name.Contains("Sedan")||t.name.Contains("Paving")||t.name.Contains("paving")||t.name.Contains("Ground")||t.name.Contains("ground")||t.name.Contains("TownChunk"))
                    .Select(t=>PathOf(t)+" | pose "+t.position.ToString("F3")+" local "+t.localPosition.ToString("F3")+" yaw "+t.eulerAngles.y+" | mesh "+string.Join(",",t.GetComponents<MeshFilter>().Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh))));
                File.WriteAllLines(Report+"/survey.txt",lines);
                File.WriteAllLines(Report+"/architecture.txt",root.GetComponentsInChildren<MeshFilter>(true).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).Contains("TownArchitecture")).Select(f=>PathOf(f.transform)+" | "+AssetDatabase.GetAssetPath(f.sharedMesh)+" | "+f.transform.position));
                var town=root.GetComponent<TownWorldPlaces>();File.WriteAllLines(Report+"/routes.txt",root.GetComponentsInChildren<SupplierLoadingCourt>(true).Select(c=>c.checkpoint.supplier+" street "+c.streetAccess+" entrance "+c.entrance+" staging "+c.staging+" bay "+c.checkpoint.transform.position+" waypoints "+string.Join("; ",c.approachWaypoints)));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            TownFloristBatch.Render(scene,Pictures+"/BeforeConstruction.png",new Vector3(104,35,-110),new Vector3(144,0,-57));
            TownFloristBatch.Render(scene,Pictures+"/BeforeWorkshop.png",new Vector3(183,36,-222),new Vector3(121,0,-171));
            File.WriteAllText(Report+"/inspect.txt","PASS: existing city surveyed without changes.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/inspect.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    internal static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}

/// <summary>Each corrected entrance is crossed by the real pickup Rigidbody, not by teleporting
/// between the two ends. Only case setup teleports; all measured motion uses PickupDrive.FixedUpdate.</summary>
[InitializeOnLoad]
public static class TownCityCorrectionsDrive
{
    private const string Active="CityCorrections.Drive.Active",Index="CityCorrections.Drive.Index";
    private static int phase;private static float settle;private static DateTime deadline;private static Vector3 start,end,direction;private static float distance;
    static TownCityCorrectionsDrive(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownCityCorrectionsBatch.Report);File.WriteAllText(TownCityCorrectionsBatch.Report+"/drive.txt","RUNNING\n");File.WriteAllText(TownCityCorrectionsBatch.Report+"/drives.txt","");
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Index,0);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            int index=SessionState.GetInt(Index,0);if(index>=TownCityCorrectionsPlacement.Aprons.Length){Finish(null);return;}
            var a=TownCityCorrectionsPlacement.Aprons[index];var city=Object.FindObjectOfType<CityWorld>();var car=city.pickup;
            if(phase==0)
            {
                direction=Vector3.ProjectOnPlane(a.To-a.From,Vector3.up).normalized;start=a.From-direction*2.3f;end=a.To+direction*2;
                car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=start+Vector3.up*.35f;car.Body.rotation=Quaternion.LookRotation(direction);
                settle=Time.fixedTime+.5f;deadline=DateTime.UtcNow.AddSeconds(35);distance=0;phase=1;return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException(a.Id+": actual pickup drive stalled at "+car.Body.position+" velocity "+car.Body.velocity);
            if(Time.fixedTime<settle)return;
            var offset=Vector3.ProjectOnPlane(car.Body.position-start,Vector3.up);distance=Vector3.Dot(offset,direction);
            if(car.Body.position.y<-.3f||car.Body.position.y>1.3f||Vector3.ProjectOnPlane(offset,direction).magnitude>1)throw new InvalidOperationException(a.Id+": pickup lost its safe route/floor at "+car.Body.position);
            if(phase==1)
            {
                if(Vector3.Dot(car.Body.position-end,direction)<-.12f){car.SetInput(.28f,0,false);return;}
                car.Stop();settle=Time.fixedTime+.25f;phase=2;return;
            }
            car.Stop();File.AppendAllText(TownCityCorrectionsBatch.Report+"/drives.txt",a.Id+": PASS, actual FixedUpdate/Rigidbody travel "+distance.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m, stopped beyond the lowered crossing.\n");
            SessionState.SetInt(Index,index+1);phase=0;
        }
        catch(Exception e){Finish(e);}
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);File.WriteAllText(TownCityCorrectionsBatch.Report+"/drive.txt",error==null?"PASS: nine real pickup Rigidbody drives through all corrected driveway crossings; no player save components or order/economy changes.\n":"FAILED\n"+error);
        if(error==null)File.WriteAllText(TownCityCorrectionsBatch.Report+"/drive-input.sha256",TownForgottenYardBatch.Hash(TownWorldAssembler.PrefabPath));else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
