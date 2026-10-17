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
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Blender FBX import, compact wheel rig bake and additive shared-town traffic installation.</summary>
public static class TrafficFleetBatch
{
    internal const string Root="Assets/Art/TrafficFleet",Report="Library/TrafficFleetQA",Pictures="Docs/TrafficFleetReview";
    [Serializable] private sealed class FleetReport { public Design[] vehicles; }
    [Serializable] private sealed class Design { public string id,paint;public float width,length,height,wheelbase,radius;public WheelData[] wheels; }
    [Serializable] private sealed class WheelData { public string name;public float[] position;public bool front; }
    private static readonly string[] ColorNames={"Yellow","Brick","Green","Blue","Beige","Orange","Cream","Teal","White","Olive","Red","Cargo","Sky","Burgundy","Mint","Navy","Terracotta","Silver","Glass","Rubber","Trim","Rim","Lamp","Tail","Indicator"};
    private static readonly Color[] Colors={new(.93f,.69f,.12f),new(.68f,.24f,.16f),new(.30f,.43f,.24f),new(.25f,.43f,.57f),
        new(.77f,.70f,.52f),new(.88f,.38f,.12f),new(.88f,.83f,.67f),new(.16f,.53f,.52f),new(.88f,.88f,.84f),
        new(.43f,.46f,.24f),new(.72f,.24f,.16f),new(.64f,.66f,.65f),new(.43f,.59f,.70f),new(.39f,.18f,.21f),
        new(.53f,.71f,.59f),new(.19f,.29f,.43f),new(.69f,.34f,.21f),new(.61f,.65f,.67f),new(.15f,.205f,.26f),
        new(.14f,.15f,.16f),new(.17f,.20f,.20f),new(.54f,.57f,.58f),new(.96f,.92f,.71f),new(.65f,.08f,.05f),new(.95f,.49f,.08f)};
    public static void Build()=>Run(true);
    public static void Review()=>Run(false);
    /// <summary>Model-only revision: stable prefab/mesh GUIDs, no town or network re-authoring.</summary>
    public static void RebuildModels()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/remake.txt","RUNNING\n");
            string townBefore=Hash(TownWorldAssembler.PrefabPath);
            string networkBefore=Hash(Root+"/Data/RoadNetwork.asset");
            var guids=Directory.GetFiles(Root+"/Prefabs","*.prefab").ToDictionary(p=>p,AssetDatabase.AssetPathToGUID);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.Refresh();
            var report=JsonUtility.FromJson<FleetReport>(File.ReadAllText("ArtSource/TrafficFleet-v2/fleet-report.json"));
            Require(report.vehicles.Length==20,"Twenty reference models required.");
            File.WriteAllText(Report+"/geometry.txt",string.Empty);
            var palette=CreateMaterial();foreach(var design in report.vehicles)Bake(design,palette);
            AssetDatabase.SaveAssets();
            foreach(var entry in guids)Require(AssetDatabase.AssetPathToGUID(entry.Key)==entry.Value,"Existing traffic prefab GUID changed.");
            Require(Hash(TownWorldAssembler.PrefabPath)==townBefore&&Hash(Root+"/Data/RoadNetwork.asset")==networkBefore,"Model revision altered town/traffic network.");
            Validate();TrafficFleetReferenceReview.Capture();Receipt("remake");
            File.WriteAllText(Report+"/remake.txt","PASS: twenty reference-shaped Blender models reimported; original prefab GUIDs, town and road network retained.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Report+"/remake.txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void Run(bool build)
    {
        string stage=build ? "build" : "review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(build)
            {
                foreach(string folder in new[]{"Materials","Meshes","Prefabs","Data"})Directory.CreateDirectory(Root+"/"+folder);
                AssetDatabase.Refresh();
                var report=JsonUtility.FromJson<FleetReport>(File.ReadAllText("ArtSource/TrafficFleet/fleet-report.json"));
                Require(report.vehicles.Length==20,"Twenty Blender exports required.");
                var palette=CreateMaterial();var fleet=report.vehicles.Select(d=>Bake(d,palette)).ToArray();
                var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
                try
                {
                    Require(town.transform.Find("Public road traffic")==null,"Traffic already installed: rebuild from snapshot.");
                    var before=ExistingSignature(town);
                    var signals=town.GetComponentsInChildren<TownJunctionSignals>(true);
                    Require(signals.Length==6,"Existing signal bindings changed.");
                    Save(TrafficNetworkAuthoring.Build(town,signals),Root+"/Data/RoadNetwork.asset");
                    var root=new GameObject("Public road traffic");root.transform.SetParent(town.transform,false);
                    root.AddComponent<TownRoadTraffic>().Configure(AssetDatabase.LoadAssetAtPath<TrafficRoadNetwork>(Root+"/Data/RoadNetwork.asset"),fleet,signals,town.GetComponentInChildren<TownLevelCrossing>());
                    Require(ExistingSignature(town)==before,"Existing town components changed.");
                    PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);
                    File.WriteAllText(Report+"/protection.txt","PASS: every pre-existing component except the root child list unchanged; additive traffic child only. Shop, scenery, train, signal bindings and all original colliders retained.\n");
                }
                finally{PrefabUtility.UnloadPrefabContents(town);}
                AssetDatabase.SaveAssets();
            }
            Validate();Capture();Receipt(stage);
            File.WriteAllText(Report+"/"+stage+".txt","PASS: 20 Blender vehicle imports, opaque glass, independent wheel rigs, connected right-hand network, fleet render and shared saved-scene bindings.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static Material CreateMaterial()
    {
        var texture=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Traffic palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var pixels=Enumerable.Repeat(Color.black,32).ToArray();Array.Copy(Colors,pixels,Colors.Length);texture.SetPixels(pixels);texture.Apply();
        Save(texture,Root+"/Materials/Palette.asset");
        var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader!=null,"URP Lit unavailable.");
        var material=new Material(shader){name="Traffic opaque palette",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));
        material.SetFloat("_Surface",0);material.SetFloat("_Smoothness",.13f);material.SetFloat("_Metallic",0);
        Save(material,Root+"/Materials/Fleet.mat");return AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Fleet.mat");
    }
    private static TrafficVehicle Bake(Design design,Material material)
    {
        string path=Root+"/Models/"+design.id+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
        importer.importAnimation=false;importer.importBlendShapes=false;importer.isReadable=true;
        importer.importNormals=ModelImporterNormals.Import;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation=ModelImporterMaterialLocation.InPrefab;importer.SaveAndReimport();
        var holder=new GameObject("Bake");var output=new GameObject(design.id);
        try
        {
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),holder.transform,false);
            var filters=holder.GetComponentsInChildren<MeshFilter>();
            Part("Body",filters.Where(f=>!f.name.StartsWith("WheelFL_")&&!f.name.StartsWith("WheelFR_")&&!f.name.StartsWith("WheelRL_")&&!f.name.StartsWith("WheelRR_")),Vector3.zero);
            var bindings=new List<TrafficVehicle.Wheel>();
            foreach(var wheel in design.wheels)
            {
                var pivot=new Vector3(wheel.position[0],wheel.position[1],wheel.position[2]);
                var visual=Part("Wheel"+wheel.name,filters.Where(f=>f.name.StartsWith("Wheel"+wheel.name+"_")),pivot);
                bindings.Add(new TrafficVehicle.Wheel{visual=visual,front=wheel.front});
            }
            var body=output.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var collider=output.AddComponent<BoxCollider>();collider.center=new Vector3(0,design.height*.47f+.10f,0);
            collider.size=new Vector3(design.width,design.height*.82f,design.length);
            output.AddComponent<TrafficVehicle>().Configure(design.id,design.length,design.width,design.wheelbase,design.radius,bindings.ToArray());
            var saved=PrefabUtility.SaveAsPrefabAsset(output,Root+"/Prefabs/"+design.id+".prefab");
            File.AppendAllText(Report+"/geometry.txt",design.id+": "+output.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+" triangles; four wheel pivots, five draw meshes, shared opaque palette.\n");
            return saved.GetComponent<TrafficVehicle>();
        }
        finally{Object.DestroyImmediate(holder);Object.DestroyImmediate(output);}

        Transform Part(string id,IEnumerable<MeshFilter> sources,Vector3 pivot)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            foreach(var filter in sources)
            {
                var matrix=holder.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var nm=matrix.inverse.transpose;
                var mesh=filter.sharedMesh;var v=mesh.vertices;var n=mesh.normals;var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    int color=Array.IndexOf(ColorNames,materials[sub].name.Split('.')[0]);Require(color>=0,"Unknown Blender color "+materials[sub].name);
                    foreach(int index in mesh.GetTriangles(sub))
                    {vertices.Add(matrix.MultiplyPoint3x4(v[index])-pivot);normals.Add(nm.MultiplyVector(n[index]).normalized);uv.Add(new Vector2((color+.5f)/32,.5f));indices.Add(indices.Count);}
                }
            }
            Require(vertices.Count>0,"Missing body/wheel group "+design.id+" "+id);
            if(id=="Body")
            {
                var lights=Enumerable.Range(0,vertices.Count).Where(i=>Mathf.Abs(uv[i].x-22.5f/32)<.0001f).ToArray();
                Require(lights.Length>0&&lights.Average(i=>vertices[i].z)>design.length*.35f,"Headlights must face local +Z for traffic: "+design.id);
            }
            var baked=new Mesh{name=design.id+"_"+id,indexFormat=IndexFormat.UInt32};baked.SetVertices(vertices);baked.SetNormals(normals);baked.SetUVs(0,uv);baked.SetTriangles(indices,0);baked.RecalculateBounds();
            string meshPath=Root+"/Meshes/"+baked.name+".asset";Save(baked,meshPath);
            var go=new GameObject(id,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(output.transform,false);go.transform.localPosition=pivot;
            go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
    }
    internal static void Validate()
    {
        var designs=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Require(designs.Length==20,"Fleet size changed.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Fleet.mat");
        Require(material.GetFloat("_Surface")==0 && material.renderQueue<3000,"Glass must be opaque.");
        var palette=(Texture2D)material.GetTexture("_BaseMap");Require(palette.GetPixel(18,0).a==1 && palette.GetPixel(18,0).grayscale<.28f,"Glass color is not dark opaque graphite.");
        foreach(var design in designs)
        {
            var filters=design.GetComponentsInChildren<MeshFilter>();
            Require(filters.Length==5 && filters.All(f=>f.GetComponent<MeshRenderer>().sharedMaterial==material),"Incomplete compact rig.");
            var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var f in filters)foreach(var v in f.sharedMesh.vertices)bounds.Encapsulate(f.transform.localPosition+v);
            Require(bounds.size.x>1.4f && bounds.size.x<2.8f && bounds.size.z>2.8f && bounds.size.z<7 && bounds.min.y>-.02f,"Incorrect FBX scale/axes: "+design.name+" "+bounds);
            Require(filters.Sum(f=>f.sharedMesh.triangles.Length/3)<4000,"Triangle budget exceeded.");
            foreach(var f in filters.Where(f=>f.name.StartsWith("Wheel")))
                Require(f.sharedMesh.bounds.size.x<.5f && f.sharedMesh.bounds.size.y>.5f && f.sharedMesh.bounds.size.z>.5f && f.sharedMesh.bounds.center.magnitude<.08f,"Wheel pivot/axis wrong: "+design.name+" "+f.name+" "+f.sharedMesh.bounds);
        }
        foreach(string path in new[]{CityUpdateSetup.CityPath,CityUpdateSetup.GamePath})
        {
            var scene=EditorSceneManager.OpenScene(path);
            var traffic=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownRoadTraffic>(true)).ToArray();
            Require(traffic.Length==1 && traffic[0].Network!=null,"Saved scene missing shared traffic: "+path);
        }
    }
    private static void Capture()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var light=new GameObject("Fleet studio sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(48,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.70f,.73f);RenderSettings.fog=false;
        var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        for(int i=0;i<prefabs.Length;i++)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]));
            Object.DestroyImmediate(go.GetComponent<TrafficVehicle>());Object.DestroyImmediate(go.GetComponent<Rigidbody>());
            go.transform.position=new Vector3(i%5*7.3f,0,-(i/5)*8);
            go.transform.rotation=Quaternion.Euler(0,-25,0);
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(15,-.15f,-12);floor.transform.localScale=new Vector3(44,.25f,38);
        var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.color=new Color(.68f,.69f,.68f);floor.GetComponent<MeshRenderer>().sharedMaterial=floorMat;
        TownFloristBatch.Render(scene,Pictures+"/Fleet.png",new Vector3(42,35,24),new Vector3(14,0,-12),23);
        TownFloristBatch.Render(scene,Pictures+"/CarsClose.png",new Vector3(7,3.6f,7),new Vector3(2.8f,.7f,0));
        Object.DestroyImmediate(floorMat);
        var city=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var traffic=Object.FindObjectOfType<TownRoadTraffic>();traffic.enabled=false;
        traffic.Simulate(.02f,0,traffic.transform.TransformPoint(new Vector3(-105,0,40)),false);
        TownFloristBatch.Render(city,Pictures+"/TownTraffic.png",new Vector3(-163,24,-12),new Vector3(-115,0,43));
        TownFloristBatch.Render(city,Pictures+"/TrafficOverview.png",new Vector3(-145,100,10),new Vector3(-115,0,48),65);
        TownFloristBatch.Render(city,Pictures+"/TrafficStreet.png",new Vector3(-160,4,48),new Vector3(-95,1,48));
        TrafficFleetReferenceReview.Capture();
    }
    private static string ExistingSignature(GameObject town)
    {
        var text=new StringBuilder();
        foreach(var c in town.GetComponentsInChildren<Component>(true))
        {if(c==null || c==town.transform || c.transform.name=="Public road traffic" || c.transform.parent!=null && c.transform.parent.name=="Public road traffic")continue;text.Append(c.GetType().FullName).Append(EditorJsonUtility.ToJson(c)).Append('\n');}
        using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
    internal static void Save(Object asset,string path)
    {var old=AssetDatabase.LoadMainAssetAtPath(path);if(old==null)AssetDatabase.CreateAsset(asset,path);else{EditorUtility.CopySerialized(asset,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(asset);}}
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static string Hash(string path){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    internal static void Receipt(string stage)
    {
        File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Concat(new[]{Root+".meta",TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
        File.WriteAllLines(Report+"/"+stage+"-code.sha256",Directory.GetFiles("Assets/Project/Scripts/City","*Traffic*.cs")
            .Concat(Directory.GetFiles("Assets/Project/Editor","*Traffic*.cs")).Concat(new[]{"Assets/Project/Scripts/City/TownLevelCrossing.cs"}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
    }
}
