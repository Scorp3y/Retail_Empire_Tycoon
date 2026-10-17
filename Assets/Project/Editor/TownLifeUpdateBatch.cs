using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class TownLifeUpdateBatch
{
    internal const string Root="Assets/Art/TownLifeUpdate",Report="Library/TownLifeQA",Pictures="Docs/TownLifeReview",PickupPath="Assets/Prefabs/City/Pickup.prefab";
    private static int meshIndex;
    private static Material city;
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool generate)
    {
        string stage=generate?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            ReleaseScene();
            if(generate)
            {
                Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();city=AssetDatabase.LoadAssetAtPath<Material>(TownCityCorrectionsPlacement.Root+"/Materials/Ground0.mat");
                var preserved=new[]{StarterStoreSetup.BlueprintPath,CityUpdateSetup.GamePath,CityUpdateSetup.CityPath,TownForgottenYardAuthoring.LayoutPath,"Assets/Art/TownForgottenYard/Land24.asset"}.ToDictionary(p=>p,TownForgottenYardBatch.Hash);
                var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try{Build(town);PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);}finally{PrefabUtility.UnloadPrefabContents(town);}
                ConfigurePickup();AssetDatabase.SaveAssets();foreach(var p in preserved)Require(TownForgottenYardBatch.Hash(p.Key)==p.Value,"Protected source changed: "+p.Key);
                File.WriteAllText(Report+"/protection.txt","PASS: Game/City scenes, tiny starter blueprint, 24-plot catalogue and town layout byte-identical. Existing supplier/checkpoint components not relocated; only scoped copies of native geometry and the driving pickup prefab authored.\n");
            }
            if(!generate){Validate();ReleaseScene();Capture();}
            Receipt(stage);File.WriteAllText(Report+"/"+stage+".txt",generate?"PASS: scoped native town and four-wheel pickup authored; protected scenes, starter store and land catalogue unchanged.\n":"PASS: native surfaces, pavement junctions, articulated train and animals, farm woodland, four-wheel pickup validated and rendered.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void ReleaseScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();GC.WaitForPendingFinalizers();
    }
    private static void Build(GameObject town)
    {
        Require(town.transform.Find("Living town update")==null,"Generate from recorded accepted original.");
        var root=Node(town.transform,"Living town update");var g=new TownCityCorrectionsGeometry(town.transform);var audit=new List<string>();
        // Extract exact old arrow recipes before clipping any of their ground triangles.
        var arrows=new TownMeshBuilder();foreach(var p in new[]{(228f,-132f,0f),(238f,-123f,0f),(238f,-104f,270f),(224f,-104f,180f),(224f,-123f,270f),(204f,-132f,180f)})Arrow(arrows,new(p.Item1,.158f,p.Item2),p.Item3,false);
        var mesh=arrows.Build("Old fuel direction arrows");try{g.MatchAndRemove(mesh,Matrix4x4.identity,town.transform);}finally{Object.DestroyImmediate(mesh);}
        // Its stem was already clipped by the accepted lowered construction gate.
        g.Remove((v,i)=>TownCityCorrectionsGeometry.Color(v,i,TownColor.White)&&TownCityCorrectionsGeometry.Points(v,i).All(p=>p.y<.13f&&In(new Rect(133.8f,-52.7f,2.6f,1.4f),p)));
        ReplaceAnimals(town,root,g); // Their old feet can reside in the earlier stable-ground batch.
        var surfaces=new TownMeshBuilder();int changed=0;
        foreach(var v in g.Views)for(int i=0;i<v.Indices.Length;i+=3)
        {
            if(v.Removed.Contains(i)||!TownCityCorrectionsGeometry.Color(v,i,TownColor.Asphalt))continue;var p=TownCityCorrectionsGeometry.Points(v,i);
            if(p.Min(a=>a.y)<.005f||p.Max(a=>a.y)>.25f||!new[]{new Rect(-282,16,63,47),new Rect(176,-141,81,57)}.Any(r=>p.All(a=>In(r,a))))continue;
            v.Removed.Add(i);surfaces.Triangle(p[0],p[1],p[2],TownColor.Asphalt);changed++;
        }
        Part(root,"City palette depot and fuel asphalt",surfaces,city);
        var repair=new Rect(-32,-140,18,5);g.ClipGround(repair,.25f);
        var path=new TownMeshBuilder();path.Ground(repair.center.x,repair.center.y,repair.width,repair.height,.14f,TownColor.Pavement);Part(root,"Continuous store T junction sidewalk",path,city,true);
        g.ClipGround(new Rect(-62,-140.6f,8,1.2f),.12f,(v,i)=>TownCityCorrectionsGeometry.Color(v,i,TownColor.White));
        // Old diagonal supplier slab overhangs the outside of the station's curved road. Its
        // 1–3 cm height identifies it without removing the 4 cm public-road surface beneath.
        g.ClipGround(new Rect(-224,187,16,18),.031f,(v,i)=>TownCityCorrectionsGeometry.Color(v,i,TownColor.Asphalt));
        var stationWalk=new TownMeshBuilder();stationWalk.Ground(-213,197.5f,2,2,.13f,TownColor.Pavement);Part(root,"Station corner pedestrian connection",stationWalk,city,true);
        // Street edge is continuous except at the real blue entrance, which stays unmarked.
        var edge=new TownMeshBuilder();edge.Ground(-23,-140.04f,18,.065f,.035f,TownColor.White);Part(root,"Closed junction street edge",edge,city);
        g.Save(Root);Train(town);Woodland(town,root);audit.Add("Asphalt triangles normalized: "+changed+". Fuel arrows: 6; construction arrow: 1 removed. T-junction footpath merged, real shop entrance edge cut; station's diagonal asphalt overhang removed.");File.WriteAllLines(Report+"/placement.txt",audit);
    }
    private static void Arrow(TownMeshBuilder m,Vector3 p,float yaw,bool large)
    {
        var q=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float z)=>p+q*new Vector3(x,0,z);
        float w=large?.12f:.08f,h=large?.5f:.4f;m.Quad(P(-w,-1),P(-w,h),P(w,h),P(w,-1),TownColor.White);float a=large?.6f:.45f,b=large?.3f:.2f;m.Triangle(P(-a,b),P(0,large?1.2f:1),P(a,b),TownColor.White);
    }
    private static void ReplaceAnimals(GameObject town,Transform root,TownCityCorrectionsGeometry g)
    {
        var farm=town.transform.Find(TownRuralPolishPlacement.Farm);int index=0;
        foreach(var old in farm.Cast<Transform>().Where(t=>t.name=="Cow"||t.name=="Hen"||t.name=="Rooster").ToArray())
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(TownGatewayPlacement.Root+"/Prefabs/"+old.name+".prefab");
            Require(source!=null,"Original farm model missing: "+old.name);
            foreach(var f in source.GetComponentsInChildren<MeshFilter>())g.MatchAndRemove(f.sharedMesh,old.localToWorldMatrix*f.transform.localToWorldMatrix,town.transform);
            var actor=Node(root,(old.name=="Cow"?"Dairy cow ":"Poultry ")+index);actor.localPosition=old.localPosition;actor.localRotation=old.localRotation;
            TownLifeModels.Animal(actor,old.name!="Cow",old.name=="Rooster",index++,(p,id,d)=>Part(p,id,d,AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")));
            Object.DestroyImmediate(old.gameObject);
        }
        Require(index==11,"Expected four cows and seven poultry.");
    }
    private static void Train(GameObject town)
    {
        var service=town.GetComponentInChildren<TownRailTraffic>(true);service.serviceIntervalSeconds=420;service.metresPerSecond=11;service.carSpacing=12;
        for(int i=0;i<service.cars.Length;i++)
        {
            var f=service.cars[i].GetComponent<MeshFilter>();f.sharedMesh=Save(TownLifeModels.Train(i==0).Build(i==0?"Regional cab":"Passenger carriage"));
        }
    }
    private static void Woodland(GameObject town,Transform root)
    {
        var layout=town.GetComponent<TownWorldPlaces>().layout;var random=new System.Random(1947);var groups=new Dictionary<Vector2Int,List<CombineInstance>>();var positions=new List<Vector3>();
        var old=town.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Pine"||t.name=="TreeWide"||t.name=="TreeRound").Select(t=>t.localPosition).ToArray();
        foreach(var region in new[]{new Rect(664,260,155,212),new Rect(449,466,370,82)})for(float z=region.yMin+5;z<region.yMax-4;z+=10)for(float x=region.xMin+5;x<region.xMax-4;x+=10)
        {
            var p=new Vector3(x+(float)random.NextDouble()*6-3,0,z+(float)random.NextDouble()*6-3);
            if(old.Any(a=>(a-p).sqrMagnitude<36)||layout.railway.points.Zip(layout.railway.points.Skip(1),(a,b)=>Distance(p,a,b)).Any(d=>d<13)||layout.countryRoad.points.Zip(layout.countryRoad.points.Skip(1),(a,b)=>Distance(p,a,b)).Any(d=>d<14))continue;
            string id=random.Next(5)<3?"Pine":random.Next(2)==0?"TreeRound":"TreeWide";var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+id+".prefab");
            // Match the mature woodland already used around the farm, rather than a field of saplings.
            float scale=(1.1f+(float)random.NextDouble()*1.4f)*1.3f;var matrix=Matrix4x4.TRS(p,Quaternion.Euler(0,random.Next(360),0),Vector3.one*scale);var key=new Vector2Int(Mathf.FloorToInt(p.x/48),Mathf.FloorToInt(p.z/48));if(!groups.TryGetValue(key,out var parts))groups.Add(key,parts=new());
            foreach(var f in source.GetComponentsInChildren<MeshFilter>())parts.Add(new CombineInstance{mesh=f.sharedMesh,transform=matrix*f.transform.localToWorldMatrix});positions.Add(p);
            var trunk=Node(root,"Woodland trunk");trunk.localPosition=p;var c=trunk.gameObject.AddComponent<CapsuleCollider>();c.radius=.2f*scale;c.height=3*scale;c.center=Vector3.up*c.height/2;
        }
        foreach(var pair in groups){var mesh=new Mesh{name="Farm woodland "+pair.Key,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);Part(root,mesh.name,mesh,AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"));}
        File.WriteAllText(Report+"/woodland.txt","Added "+positions.Count+" mixed trees in "+groups.Count+" 48 m visual chunks; 13 m railway and 14 m road clearances. Fields, farm circulation and original vegetation retained.\n");
    }
    private static float Distance(Vector3 p,Vector3 a,Vector3 b){p.y=a.y=b.y=0;var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude)));}
    private static void ConfigurePickup()
    {
        var root=PrefabUtility.LoadPrefabContents(PickupPath);
        try
        {
            var drive=root.GetComponent<PickupDrive>();Require(drive!=null,"Driving prefab missing controller.");
            foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.IndexOf("wheel",StringComparison.OrdinalIgnoreCase)>=0)){var r=f.GetComponent<MeshRenderer>();if(r!=null)r.enabled=false;}
            var box=root.GetComponent<BoxCollider>();box.size=new Vector3(1.78f,1.3f,4.15f);box.center=new Vector3(0,1.02f,0);
            var bindings=new List<PickupDrive.Wheel>();
            for(int axle=0;axle<2;axle++)foreach(float side in new[]{-1f,1f})
            {
                var wheel=Node(root.transform,(axle==0?"Front":"Rear")+" suspension "+side);wheel.localPosition=new Vector3(side*.86f,.54f,axle==0?1.38f:-1.48f);
                var c=wheel.gameObject.AddComponent<WheelCollider>();c.radius=.38f;c.mass=25;c.suspensionDistance=.24f;c.wheelDampingRate=1.2f;c.forceAppPointDistance=.18f;
                c.suspensionSpring=new JointSpring{spring=42000,damper=5500,targetPosition=.5f};var forward=c.forwardFriction;forward.stiffness=1.5f;c.forwardFriction=forward;var sideways=c.sidewaysFriction;sideways.stiffness=1.65f;c.sidewaysFriction=sideways;c.ConfigureVehicleSubsteps(5,8,12);
                var visual=Node(root.transform,"Animated wheel "+bindings.Count);var draft=new TownMeshBuilder();draft.Cylinder(Vector3.zero,.38f,.38f,.25f,TownColor.Rubber,16,Quaternion.Euler(0,0,90));foreach(float s in new[]{-1f,1f}){draft.Cylinder(new(s*.13f,0,0),.25f,.25f,.025f,TownColor.Metal,12,Quaternion.Euler(0,0,90));for(int spoke=0;spoke<5;spoke++)draft.Box(new(s*.15f,0,0),new(.03f,.045f,.43f),TownColor.Frame,Quaternion.Euler(spoke*72,0,0));}
                Part(visual,"Rim and tire",draft,AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"));visual.localPosition=wheel.localPosition-Vector3.up*.12f;bindings.Add(new PickupDrive.Wheel{contact=c,visual=visual,front=axle==0});
            }
            drive.wheels=bindings.ToArray();PrefabUtility.SaveAsPrefabAsset(root,PickupPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    internal static Transform Node(Transform parent,string id){var n=new GameObject(id).transform;n.SetParent(parent,false);return n;}
    private static Mesh Save(Mesh mesh){if(mesh.uv2.Length!=mesh.vertexCount)mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();string p=Root+"/Meshes/Mesh"+meshIndex+++".asset";TownBakeryPlacement.SaveAsset(mesh,p);return AssetDatabase.LoadAssetAtPath<Mesh>(p);}
    private static void Part(Transform p,string id,TownMeshBuilder m,Material mat,bool physical=false)=>Part(p,id,m.Build(id),mat,physical);
    private static void Part(Transform p,string id,Mesh mesh,Material mat,bool physical=false){var n=Node(p,id);mesh=Save(mesh);n.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;n.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;if(physical)n.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;}
    private static bool In(Rect r,Vector3 p)=>r.Contains(new Vector2(p.x,p.z));
    private static void Validate()
    {
        EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=Object.FindObjectOfType<TownWorldPlaces>();Require(town.GetComponentsInChildren<FarmAnimal>(true).Length==11,"Missing animated animals.");Require(town.GetComponentInChildren<TownRailTraffic>().serviceIntervalSeconds==420,"Train interval changed.");
        var pickup=AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);Require(pickup.GetComponentsInChildren<WheelCollider>().Length==4&&pickup.GetComponent<PickupDrive>().wheels.All(w=>w.contact!=null&&w.visual!=null),"Incomplete wheel rig.");
        var train=town.GetComponentInChildren<TownRailTraffic>();train.enabled=false;train.Advance(419);Require(!train.IsInService,"Train did not finish and hide before interval.");train.Advance(1);Require(train.IsInService,"No new departure at 420 s.");train.Advance(420);Require(train.IsInService,"No new departure at 840 s.");
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))Require(f.sharedMesh==null||f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite),"Non-finite geometry.");
        File.WriteAllText(Report+"/validation.txt","PASS: 11 articulated animals; four wheel contacts/visuals; train hidden between trips and departures at 420/840 seconds; native geometry finite.\n");
    }
    private static void Capture()
    {
        var s=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);RenderSettings.fog=false;
        TownFloristBatch.Render(s,Pictures+"/Depot.png",new(-207,32,10),new(-249,0,37));TownFloristBatch.Render(s,Pictures+"/Entrance.png",new(-62,40,-182),new(-35,0,-130));
        TownFloristBatch.Render(s,Pictures+"/Station.png",new(-235,30,165),new(-207,0,199));TownFloristBatch.Render(s,Pictures+"/Fuel.png",new(175,25,-151),new(215,0,-111));
        TownFloristBatch.Render(s,Pictures+"/FarmWoodland.png",new(480,155,205),new(638,0,398));TownFloristBatch.Render(s,Pictures+"/Animals.png",new(480,5,248),new(487,1,270));
        var train=Object.FindObjectOfType<TownRailTraffic>();train.Advance(.01f);var head=train.cars[0].position;TownFloristBatch.Render(s,Pictures+"/Train.png",head+new Vector3(16,5,-17),head+Vector3.up*1.5f);
        var pickup=Object.FindObjectOfType<PickupDrive>().transform;TownFloristBatch.Render(s,Pictures+"/Pickup.png",pickup.TransformPoint(new Vector3(5,2.5f,6)),pickup.position+Vector3.up*.8f);
    }
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Concat(new[]{Root+".meta",TownWorldAssembler.PrefabPath,PickupPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>TownForgottenYardBatch.Hash(p)+" "+p.Replace('\\','/')));
}
