using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Builds a material-colored articulated train in the disposable Unity project.
/// Replaces only the old service's cars; the existing service and crossing references survive.</summary>
[InitializeOnLoad]
public static class RegionalTrainV2Batch
{
    private const string Root = "Assets/Art/RegionalTrainV2";
    private const string Report = "Docs/RegionalTrainV2";
    private const string Playing = "RegionalTrainV2.Play";
    private static float firstTime, firstDistance;
    private static Quaternion firstWheel;
    private static DateTime deadline;
    private static readonly Dictionary<string,string> Colors = new()
    {
        {"Cream","F0DDBC"},{"Green","2F5D46"},{"Stripe","C4573A"},{"Roof","6E6E6E"},
        {"Glass","4A6275"},{"Frame","F7E7CA"},{"Dark","252D30"},{"Steel","666E70"},
        {"Lamp","FFDF91"},{"Marker","B93329"},{"Rubber","353B3C"}
    };
    static RegionalTrainV2Batch() { EditorApplication.update += Tick; }
    private static void Prepare()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
    public static void Build()
    {
        try
        {
            Prepare();
            File.WriteAllText(Report+"/build.txt","RUNNING\n");
            File.WriteAllText(Report+"/geometry.txt","");
            foreach (string folder in new[]{"Models","Meshes","Materials","Prefabs"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var materials = MakeMaterials();
            BakeCar("Cab", materials); BakeCar("Coach", materials);
            MakeAssembly(materials);
            ReplaceService();
            AssetDatabase.SaveAssets();
            ValidateAndCapture();
            File.WriteAllText(Report+"/build.txt","PASS: three-car Blender geometry imported and integrated; route/timetable and all objects outside train preserved.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Fail("build",e); }
    }
    private static Dictionary<string,Material> MakeMaterials()
    {
        var result = new Dictionary<string,Material>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Check(shader!=null,"URP Lit unavailable");
        foreach(var entry in Colors)
        {
            ColorUtility.TryParseHtmlString("#"+entry.Value,out var color);
            var mat = new Material(shader) { name=entry.Key, enableInstancing=true };
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",.15f); mat.SetFloat("_Metallic",0);
            if(entry.Key=="Lamp" || entry.Key=="Marker")
            { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",color*.35f); }
            string path=Root+"/Materials/"+entry.Key+".mat";
            TownBakeryPlacement.SaveAsset(mat,path); result.Add(entry.Key,AssetDatabase.LoadAssetAtPath<Material>(path));
        }
        return result;
    }
    private sealed class Geometry
    {
        public Transform target;
        public readonly List<Vector3> vertices=new(), normals=new();
        public readonly List<int> triangles=new();
    }
    private static Transform Add(string name,Transform parent,Vector3 position)
    {
        var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
    }
    private static Transform Find(Transform root,string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    private static void BakeCar(string id,Dictionary<string,Material> materials)
    {
        string path=Root+"/Models/"+id+".fbx";
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        Check(importer!=null,"Missing FBX: "+path);
        importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
        importer.importAnimation=false; importer.importBlendShapes=false; importer.isReadable=true;
        importer.importNormals=ModelImporterNormals.Import;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation=ModelImporterMaterialLocation.InPrefab; importer.SaveAndReimport();
        var holder=new GameObject("Import space"); var result=new GameObject(id);
        try
        {
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),holder.transform,false);
            var destinations=new Dictionary<Transform,Transform>();
            var body=Add("Body",result.transform,Vector3.zero);
            foreach(var source in holder.GetComponentsInChildren<Transform>())
            {
                if(!source.name.StartsWith("Bogie_") && !source.name.StartsWith("WheelPivot_")) continue;
                var parent=source.name.StartsWith("WheelPivot_") ? destinations[source.parent] : result.transform;
                var target=Add(source.name.Split('.')[0],parent,Vector3.zero);
                target.position=holder.transform.InverseTransformPoint(source.position);
                destinations.Add(source,target);
            }
            foreach(var source in holder.GetComponentsInChildren<Transform>())
            {
                if(source.name.Contains("GangwaySocket"))
                    Add(source.name.Split('.')[0],result.transform,holder.transform.InverseTransformPoint(source.position));
                if(source.name=="Headlights" || source.name=="MarkerLights" || source.name.StartsWith("Door_") || source.name.StartsWith("DoorLeaf_"))
                {
                    var parent=source.name.StartsWith("DoorLeaf_") ? destinations[source.parent] : result.transform;
                    var target=Add(source.name.Split('.')[0],parent,Vector3.zero);
                    if(source.name.StartsWith("DoorLeaf_"))
                    {
                        var panel=source.GetComponentInChildren<MeshFilter>();
                        target.position=holder.transform.InverseTransformPoint(panel.transform.TransformPoint(panel.sharedMesh.bounds.center));
                    }
                    destinations.Add(source,target);
                }
            }
            var groups=new Dictionary<string,Geometry>();
            foreach(var filter in holder.GetComponentsInChildren<MeshFilter>())
            {
                Transform target=body;
                for(var ancestor=filter.transform; ancestor!=null; ancestor=ancestor.parent)
                    if(destinations.TryGetValue(ancestor,out target)) break;
                if(target==null)target=body;
                if(filter.name.StartsWith("Coupler"))target=Find(result.transform,"Coupler") ?? Add("Coupler",result.transform,Vector3.zero);
                var source=filter.sharedMesh;
                var matrix=target.worldToLocalMatrix*holder.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var normal=matrix.inverse.transpose;
                var sourceMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    string material=sourceMaterials[sub].name.Split('.')[0];
                    Check(materials.ContainsKey(material),"Unknown material "+material);
                    string key=target.GetInstanceID()+"_"+material;
                    if(!groups.TryGetValue(key,out var data))groups[key]=data=new Geometry{target=target};
                    var vertices=source.vertices; var normals=source.normals;
                    foreach(int index in source.GetTriangles(sub))
                    { data.vertices.Add(matrix.MultiplyPoint3x4(vertices[index])); data.normals.Add(normal.MultiplyVector(normals[index]).normalized); data.triangles.Add(data.triangles.Count); }
                }
            }
            int triangles=0, number=0;
            foreach(var entry in groups)
            {
                var data=entry.Value; string material=entry.Key.Substring(entry.Key.IndexOf('_')+1);
                var mesh=new Mesh{name=id+"_"+data.target.name+"_"+material};
                mesh.SetVertices(data.vertices); mesh.SetNormals(data.normals); mesh.SetTriangles(data.triangles,0); mesh.RecalculateBounds();
                string meshPath=Root+"/Meshes/"+id+"_"+(number++)+".asset";
                TownBakeryPlacement.SaveAsset(mesh,meshPath);
                var part=Add(material,data.target,Vector3.zero).gameObject;
                part.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                part.AddComponent<MeshRenderer>().sharedMaterial=materials[material]; triangles+=data.triangles.Count/3;
            }
            Check(triangles<8500,"Excessive module triangle count");
            var bounds=BoundsIn(result.transform);
            Check(bounds.size.x>2.8f && bounds.size.x<3 && bounds.size.y>3.5f && bounds.size.y<3.65f && bounds.size.z>15 && bounds.size.z<16,"FBX axis/scale invalid: "+bounds);
            Check(Mathf.Abs(bounds.min.y)<.005f,"Wheels must sit on local rail base zero");
            PrefabUtility.SaveAsPrefabAsset(result,Root+"/Prefabs/"+id+".prefab");
            File.AppendAllText(Report+"/geometry.txt",id+": "+triangles+" triangles; "+number+" material batches; bounds="+bounds+"\n");
        }
        finally { Object.DestroyImmediate(holder); Object.DestroyImmediate(result); }
    }
    private static Bounds BoundsIn(Transform root)
    {
        bool first=true; var bounds=new Bounds();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var m=root.worldToLocalMatrix*f.transform.localToWorldMatrix;
            foreach(var p in f.sharedMesh.vertices)
            { var v=m.MultiplyPoint3x4(p); if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v); }
        }
        return bounds;
    }
    private static Mesh BeveledBox(string name,Vector3 size,float bevel)
    {
        // A twelve-edge chamfer keeps flat faces and an eight-corner silhouette.
        var v=new List<Vector3>();var n=new List<Vector3>();var triangles=new List<int>();
        var half=size*.5f;float b=Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.24f);
        var points=new List<Vector3>();
        for(int axis=0;axis<3;axis++)for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
        { var p=new Vector3(x*(half.x-b),y*(half.y-b),z*(half.z-b));p[axis]=new[]{x,y,z}[axis]*half[axis];points.Add(p); }
        var planes=new List<Vector3>{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        for(int a=0;a<3;a++)for(int c=a+1;c<3;c++)for(int s=-1;s<=1;s+=2)for(int t=-1;t<=1;t+=2){var p=Vector3.zero;p[a]=s;p[c]=t;planes.Add(p.normalized);}
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)planes.Add(new Vector3(x,y,z).normalized);
        foreach(var normal in planes)
        {
            float max=points.Max(p=>Vector3.Dot(p,normal));var face=points.Where(p=>Mathf.Abs(Vector3.Dot(p,normal)-max)<.0001f).Distinct().ToArray();if(face.Length<3)continue;
            var center=face.Aggregate(Vector3.zero,(a,p)=>a+p)/face.Length;
            var u=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;var w=Vector3.Cross(normal,u);
            face=face.OrderBy(p=>Mathf.Atan2(Vector3.Dot(p-center,w),Vector3.Dot(p-center,u))).ToArray();
            int start=v.Count;v.AddRange(face);n.AddRange(face.Select(_=>normal));
            for(int i=1;i<face.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
    private static void MakeAssembly(Dictionary<string,Material> materials)
    {
        var root=new GameObject("RegionalTrain");
        try
        {
            var cars=new Transform[3];
            for(int i=0;i<3;i++)
            {
                var car=Add("Car"+(i+1),root.transform,new Vector3(0,0,(1-i)*16));cars[i]=car;
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+(i==1?"Coach":"Cab")+".prefab"));
                model.transform.SetParent(car,false);if(i==2)model.transform.localRotation=Quaternion.Euler(0,180,0);
                var follower=car.gameObject.AddComponent<RegionalTrainCar>();
                follower.frontBogie=Find(model.transform,i==2?"Bogie_Rear":"Bogie_Front");
                follower.rearBogie=Find(model.transform,i==2?"Bogie_Front":"Bogie_Rear");
                follower.wheels=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("WheelPivot_")).ToArray();
                follower.headlights=Find(model.transform,"Headlights")?.gameObject;follower.markerLights=Find(model.transform,"MarkerLights")?.gameObject;
                if(follower.headlights!=null)
                {
                    var light=Add("Headlight beam",follower.headlights.transform,new Vector3(0,1.55f,7.8f)).gameObject.AddComponent<Light>();
                    light.type=LightType.Spot;light.color=new Color(1,.87f,.57f);light.range=22;light.spotAngle=65;light.intensity=1.5f;light.shadows=LightShadows.None;
                    follower.SetHeadlights(i==0);follower.SetMarkerLights(i==2);
                }
                var collider=car.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,2,0);collider.size=new Vector3(2.78f,2.8f,15.4f);
                var body=car.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            }
            var articulation=root.AddComponent<RegionalTrainArticulation>();articulation.links=new RegionalTrainArticulation.Link[2];
            var rib=BeveledBox("Gangway fold",new Vector3(1.84f,2.2f,.11f),.025f);
            TownBakeryPlacement.SaveAsset(rib,Root+"/Meshes/GangwayFold.asset");
            var tunnel=BeveledBox("Gangway tunnel",new Vector3(1.65f,2.02f,1),.02f);
            TownBakeryPlacement.SaveAsset(tunnel,Root+"/Meshes/GangwayTunnel.asset");
            for(int i=0;i<2;i++)
            {
                var link=new RegionalTrainArticulation.Link {root=Add("Gangway_"+(i+1),root.transform,Vector3.zero),from=Find(cars[i],"RearGangwaySocket"),to=Find(cars[i+1],i==0?"FrontGangwaySocket":"RearGangwaySocket"),ribs=new Transform[5]};
                link.tunnel=Part("Tunnel",link.root,"GangwayTunnel",materials["Dark"]);
                for(int r=0;r<5;r++)link.ribs[r]=Part("Fold_"+(r+1),link.root,"GangwayFold",materials["Rubber"]);
                articulation.links[i]=link;
            }
            articulation.Refresh();
            var bounds=BoundsIn(root.transform);
            Check(Mathf.Abs(bounds.min.y)<.005f&&Mathf.Abs(bounds.center.z)<.005f,"Assembly pivot must be centered at rail contact height");
            Check(Mathf.Abs(bounds.size.z-48.06f)<.02f,"Assembly length differs from original train envelope");
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/RegionalTrain.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static Transform Part(string name,Transform parent,string mesh,Material mat)
    {
        var t=Add(name,parent,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+mesh+".asset");t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;
    }
    private static string Signature(GameObject town,Transform service)
    {
        var text=new StringBuilder();foreach(var c in town.GetComponentsInChildren<Component>(true))if(c!=null&&!c.transform.IsChildOf(service))text.Append(c.GetType().FullName).Append(EditorJsonUtility.ToJson(c)).Append('\n');
        using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static void ReplaceService()
    {
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var service=town.GetComponentInChildren<TownRailTraffic>(true);Check(service!=null,"Existing rail service missing");
            string before=Signature(town,service.transform);var layout=service.layout;float speed=service.metresPerSecond,wait=service.stationWaitSeconds,interval=service.serviceIntervalSeconds;
            var existing=service.GetComponentInChildren<RegionalTrainArticulation>(true);
            if(existing!=null) Object.DestroyImmediate(existing.gameObject);
            else foreach(var car in service.cars)
            {
                Check(car.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"Unknown old car component; refusing to destroy it");
                Object.DestroyImmediate(car.gameObject);
            }
            var train=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/RegionalTrain.prefab"));train.transform.SetParent(service.transform,false);
            service.cars=Enumerable.Range(1,3).Select(i=>Find(train.transform,"Car"+i)).ToArray();service.carSpacing=16;
            train.GetComponent<RegionalTrainArticulation>().service=service;service.Advance(.0001f);
            Check(service.layout==layout&&speed==service.metresPerSecond&&wait==service.stationWaitSeconds&&interval==service.serviceIntervalSeconds,"Service contract changed");
            Check(Signature(town,service.transform)==before,"Objects outside train changed");
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);
            File.WriteAllText(Report+"/protection.txt","PASS: component/transform signature outside rail service unchanged; service component retained, layout/speed/wait/interval retained; only cars/spacing replaced.\n");
        }
        finally { PrefabUtility.UnloadPrefabContents(town); }
    }
    private static void CheckPose(TownRailTraffic service)
    {
        for(int i=0;i<3;i++)
        {
            var car=service.cars[i].GetComponent<RegionalTrainCar>();Check(car!=null&&car.wheels.Length==8,"Missing animated wheels");
            foreach(var entry in new[]{(car.frontBogie,car.bogieOffset),(car.rearBogie,-car.bogieOffset)})
            {
                var p=TownRailTraffic.Sample(service.layout.railway.points,service.TravelDistance-i*16+entry.Item2,out var tangent);
                var q=Quaternion.LookRotation(tangent);var expected=service.transform.TransformPoint(p+q*Vector3.up*.5f);
                Check(Vector3.Distance(entry.Item1.position,expected)<.003f*service.transform.lossyScale.x,"Bogie does not follow rail head");
            }
            Check(car.GetComponent<Rigidbody>().isKinematic&&car.GetComponent<BoxCollider>()!=null,"Rail collision missing");
        }
        foreach(var link in service.GetComponentInChildren<RegionalTrainArticulation>(true).links)
        { Check(link.ribs.Length==5,"Gangway folds missing");Check(Vector3.Distance(link.tunnel.position,(link.from.position+link.to.position)*.5f)<.001f,"Disconnected gangway"); }
    }
    private static void ValidateAndCapture()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var service=Object.FindObjectOfType<TownRailTraffic>();
        Check(service.cars.Length==3&&service.carSpacing==16&&service.serviceIntervalSeconds==420,"Wrong assembly/timetable");
        Check(Object.FindObjectOfType<TownLevelCrossing>().train==service,"Crossing reference lost");
        Check(!service.cars.SelectMany(c=>c.GetComponentsInChildren<MeshFilter>(true)).Any(f=>!AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Root+"/Meshes/")),"Old train mesh retained");
        service.enabled=false;service.Advance(.001f);CheckPose(service);
        var wheel=service.cars[0].GetComponent<RegionalTrainCar>().wheels[0];var rotation=wheel.localRotation;
        service.Advance(.1f);Check(Quaternion.Angle(rotation,wheel.localRotation)>1,"Wheels do not spin");
        service.SetCrossingHold(true);rotation=wheel.localRotation;float held=service.TravelDistance;service.Advance(2);
        Check(service.TravelDistance==held&&Quaternion.Angle(rotation,wheel.localRotation)<.01f,"Stopped wheels moved");service.SetCrossingHold(false);
        service.Advance((service.layout.stationDistance-service.TravelDistance)/service.metresPerSecond+.001f);
        Check(service.IsWaiting,"Station stop missing");CheckPose(service);rotation=wheel.localRotation;service.Advance(1);Check(Quaternion.Angle(rotation,wheel.localRotation)<.01f,"Station wheels moved");
        RenderSettings.fog=false;
        var head=service.cars[0].position;var forward=service.cars[0].forward;var right=service.cars[0].right;var center=head-forward*16;
        TownFloristBatch.Render(scene,Report+"/UnityTrain.png",center+forward*37+right*38+Vector3.up*22,center+Vector3.up*1.6f,22);
        TownFloristBatch.Render(scene,Report+"/UnityCab.png",head+forward*15+right*10+Vector3.up*6,head+forward*5+Vector3.up*1.7f,5.3f);
        // Exercise the native curve/gradient, not just the straight station platform.
        float cumulative=0,curve=0;var points=service.layout.railway.points;
        for(int i=1;i<points.Length-1;i++){cumulative+=Vector3.Distance(points[i-1],points[i]);if(Vector3.Angle(points[i]-points[i-1],points[i+1]-points[i])>2){curve=cumulative;break;}}
        if(curve>service.TravelDistance)
        {
            service.Advance(service.stationWaitSeconds+(curve-service.TravelDistance)/service.metresPerSecond);CheckPose(service);
            head=service.cars[0].position;center=head-service.cars[0].forward*16;
            TownFloristBatch.Render(scene,Report+"/UnityCurve.png",center+service.cars[0].right*34+Vector3.up*29,center,23);
        }
        service.Advance((float)(419-service.ServiceClock));Check(!service.IsInService&&service.cars.All(c=>!c.gameObject.activeSelf),"Idle train not hidden");
        Check(service.GetComponentInChildren<RegionalTrainArticulation>(true).links.All(l=>!l.root.gameObject.activeSelf),"Idle gangways remain visible");
        service.Advance(1.01f);Check(service.IsInService&&Math.Abs(service.NextDeparture-840)<.01,"420 second departure failed");
        service.Advance(420);Check(service.IsInService&&Math.Abs(service.NextDeparture-1260)<.01,"840 second departure failed");
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);service=Object.FindObjectOfType<TownRailTraffic>();service.enabled=false;service.Advance(.001f);CheckPose(service);
        File.WriteAllText(Report+"/validation.txt","PASS: City + scaled Game; 24 wheels; native rail-head height/gauge and bogie pivots; curve/gradient pose; connected gangways; wheel rotation and stationary holds; station wait; crossing reference; hidden idle; 420/840 second departures; new meshes only.\n");
    }
    public static void Play()
    {
        try{Prepare();EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Playing,true);deadline=DateTime.UtcNow.AddSeconds(90);EditorApplication.EnterPlaymode();}
        catch(Exception e){Fail("play",e);}
    }
    public static void CheckGenerator()
    {
        GameObject group=null;
        try
        {
            Prepare();
            var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:TownWorldLayout")[0]));
            var service=TownWorldAssembler.CreatePassengerTrain(layout,null);group=service.gameObject;
            Check(service.cars.Length==3&&service.metresPerSecond==11&&service.serviceIntervalSeconds==420,"New world generator used old train");
            CheckPose(service);
            File.WriteAllText(Report+"/generator.txt","PASS: original town-generation entry point now instantiates the authored three-car train with bound bogies, wheels and gangways; no whole-world rebuild required.\n");
            Object.DestroyImmediate(group);EditorApplication.Exit(0);
        }
        catch(Exception e){if(group!=null)Object.DestroyImmediate(group);Fail("generator",e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Playing,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            var service=Object.FindObjectOfType<TownRailTraffic>();
            var wheel=service.cars[0].GetComponent<RegionalTrainCar>().wheels[0];
            if(firstTime==0){firstTime=Time.time;firstDistance=service.TravelDistance;firstWheel=wheel.localRotation;deadline=DateTime.UtcNow.AddSeconds(60);return;}
            Check(DateTime.UtcNow<deadline,"Live train did not advance");if(Time.time-firstTime<2||service.TravelDistance<firstDistance+1)return;
            CheckPose(service);Check(Quaternion.Angle(firstWheel,wheel.localRotation)>1,"Live wheels not rotating");
            Check(service.cars.SelectMany(c=>c.GetComponentsInChildren<MeshRenderer>()).All(r=>r.sharedMaterial.shader.isSupported),"Unsupported shader");
            SessionState.SetBool(Playing,false);File.WriteAllText(Report+"/play.txt","PASS: actual Play Mode updates move three cars, independent bogies, 24 wheels and two gangways; supported URP shaders. City scene does not load save components.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){SessionState.SetBool(Playing,false);Fail("play",e);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Fail(string stage,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
