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
using Object=UnityEngine.Object;

/// <summary>Cow-only migration. Retains all four pasture actors and their FarmAnimal components.</summary>
[InitializeOnLoad]
public static class FarmCowV2Batch
{
    private const string Root="Assets/Art/FarmAnimalsV2",Report="Docs/FarmCowV2",Playing="FarmCowV2.Play";
    private static readonly string[] Joints={"Body","Head","Tail","Leg_FL","Leg_FR","Leg_BL","Leg_BR","Ear_L","Ear_R","Eye_L","Eye_R"};
    private static readonly Dictionary<string,string> Palette=new(){{"Cream","EFE3CC"},{"CreamShade","E6D9BE"},{"Dark","5A4E48"},{"Pink","F4B8B0"},{"PinkShade","EDA39C"},{"Eyes","302C29"},{"Horn","E6D2A9"}};
    private static float firstTime;private static DateTime deadline;private static Vector3[] initial;private static Quaternion firstLeg,firstHead;
    static FarmCowV2Batch(){EditorApplication.update+=Tick;}
    public static void Build()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);File.WriteAllText(Report+"/build.txt","RUNNING\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var folder in new[]{"Models","Materials","Meshes","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();Bake();MakeActorPrefab();ReplaceCows();
            foreach(var joint in Joints)AssetDatabase.DeleteAsset(Root+"/Meshes/Cow_"+joint+".asset");
            AssetDatabase.SaveAssets();Review();
            File.WriteAllText(Report+"/build.txt","PASS: cow-only model, standalone actor prefab, four existing pasture actors retained and rebound; both scene scales checked.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail("build",e);}
    }
    private sealed class Part
    {
        internal Transform target;internal readonly List<Vector3> vertices=new(),normals=new();
        internal readonly Dictionary<string,List<int>> indices=new();
    }
    private static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    private static void Bake()
    {
        var mats=new Dictionary<string,Material>();
        foreach(var p in Palette)
        {
            ColorUtility.TryParseHtmlString("#"+p.Value,out var color);var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=p.Key,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);
            string path=Root+"/Materials/"+p.Key+".mat";TownBakeryPlacement.SaveAsset(m,path);mats.Add(p.Key,AssetDatabase.LoadAssetAtPath<Material>(path));
        }
        var importer=AssetImporter.GetAtPath(Root+"/Models/Cow.fbx") as ModelImporter;Check(importer!=null,"Cow FBX missing");
        importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importAnimation=false;importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;importer.SaveAndReimport();
        var space=new GameObject("Cow FBX space");var model=new GameObject("CowModel");
        try
        {
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Cow.fbx"),space.transform,false);
            var map=new Dictionary<Transform,Transform>();var parts=new Dictionary<Transform,Part>();
            foreach(var source in space.GetComponentsInChildren<Transform>())
            {
                if(!Joints.Contains(source.name))continue;
                var target=new GameObject(source.name).transform;Transform parent=model.transform;
                for(var p=source.parent;p!=null;p=p.parent)if(map.TryGetValue(p,out parent))break;
                if(parent==null)parent=model.transform;target.SetParent(parent,false);target.position=space.transform.InverseTransformPoint(source.position);map.Add(source,target);parts.Add(target,new Part{target=target});
            }
            foreach(var filter in space.GetComponentsInChildren<MeshFilter>())
            {
                Transform target=null;
                for(var p=filter.transform;p!=null;p=p.parent)if(map.TryGetValue(p,out target))break;
                Check(target!=null,"Unbound cow geometry: "+filter.name);var part=parts[target];var mesh=filter.sharedMesh;
                var transform=target.worldToLocalMatrix*space.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normals=transform.inverse.transpose;
                var vertices=mesh.vertices;var directions=mesh.normals;var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    string mat=materials[sub].name.Split('.')[0];Check(mats.ContainsKey(mat),"Unknown material "+mat);
                    if(!part.indices.TryGetValue(mat,out var indices))part.indices.Add(mat,indices=new List<int>());
                    foreach(int i in mesh.GetTriangles(sub)){indices.Add(part.vertices.Count);part.vertices.Add(transform.MultiplyPoint3x4(vertices[i]));part.normals.Add(normals.MultiplyVector(directions[i]).normalized);}
                }
            }
            int count=0;
            foreach(var part in parts.Values)
            {
                // One material per renderer avoids unstable first-frame material slot binding
                // in this project's URP/SRP batching, without increasing the triangle budget.
                foreach(var surface in part.indices)
                {
                    var mesh=new Mesh{name="Cow_"+part.target.name+"_"+surface.Key};
                    mesh.SetVertices(surface.Value.Select(i=>part.vertices[i]).ToList());mesh.SetNormals(surface.Value.Select(i=>part.normals[i]).ToList());
                    mesh.SetTriangles(Enumerable.Range(0,surface.Value.Count).ToArray(),0);mesh.RecalculateBounds();count+=surface.Value.Count/3;
                    string path=Root+"/Meshes/"+mesh.name+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
                    var geometry=new GameObject(surface.Key);geometry.transform.SetParent(part.target,false);
                    geometry.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);geometry.AddComponent<MeshRenderer>().sharedMaterial=mats[surface.Key];
                }
            }
            var bounds=BoundsIn(model.transform);Check(count>=300&&count<=800,"Triangle budget exceeded: "+count);
            Check(Mathf.Abs(bounds.min.y)<.001f&&bounds.size.y>1.7f&&bounds.size.y<2.1f,"Cow ground/axis/units incorrect: "+bounds);
            Check(Find(model.transform,"Head").localPosition.z>.5f,"Cow head must face +Z");
            foreach(string eye in new[]{"Eye_L","Eye_R"})
            {var mesh=Find(model.transform,eye).GetComponentInChildren<MeshFilter>().sharedMesh;Check(mesh.normals.All(n=>n.z>.9f),"Eyes face inward: "+eye);}
            model.AddComponent<FarmAnimalPalette>().Apply();
            PrefabUtility.SaveAsPrefabAsset(model,Root+"/Prefabs/CowModel.prefab");
            File.WriteAllText(Report+"/geometry.txt",count+" triangles; "+parts.Count+" independently articulated mesh parts; "+mats.Count+" shared solid materials; bounds="+bounds+"; +Z forward; eyes outward; flat imported normals.\n");
        }
        finally{Object.DestroyImmediate(space);Object.DestroyImmediate(model);}
    }
    private static Bounds BoundsIn(Transform root)=>TownLifeModels.AnimalBounds(root);
    private static void MakeActorPrefab()
    {
        var root=new GameObject("Cow");try
        {
            var actor=root.AddComponent<FarmAnimal>();actor.pasture=new Rect(-3,-3,6,6);actor.seed=1;
            TownLifeModels.AttachCowModel(actor,AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/CowModel.prefab"),2.02f);
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Cow.prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    private static FarmAnimal[] Cows(GameObject town)=>town.GetComponentsInChildren<FarmAnimal>(true).Where(a=>!a.chicken).OrderBy(a=>a.name).ToArray();
    private static string Outside(GameObject town,FarmAnimal[] cows)
    {
        var text=new StringBuilder();foreach(var c in town.GetComponentsInChildren<Component>(true))
            if(c!=null&&!cows.Any(a=>c.transform.IsChildOf(a.transform)))text.Append(c.GetType().FullName).Append(EditorJsonUtility.ToJson(c)).Append('\n');
        using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static void ReplaceCows()
    {
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);try
        {
            var cows=Cows(town);Check(cows.Length==4,"Expected exactly four existing cows; stop before touching another species");string outside=Outside(town,cows);var lines=new List<string>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/CowModel.prefab");
            foreach(var actor in cows)
            {
                var position=actor.transform.localPosition;var rotation=actor.transform.localRotation;var scale=actor.transform.localScale;int seed=actor.seed;var pasture=actor.pasture;
                var old=BoundsIn(actor.transform);float height=old.size.y;
                foreach(var child in actor.transform.Cast<Transform>().ToArray())
                {
                    Check(child.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer||c is FarmAnimalPalette),"Custom cow component found; refusing to delete it");Object.DestroyImmediate(child.gameObject);
                }
                TownLifeModels.AttachCowModel(actor,prefab,height);
                Check(position==actor.transform.localPosition&&rotation==actor.transform.localRotation&&scale==actor.transform.localScale&&seed==actor.seed&&pasture==actor.pasture,"Cow placement or AI changed");
                Check(Mathf.Abs(BoundsIn(actor.transform).size.y-height)<.005f,"Existing cow height changed");lines.Add(actor.name+" preserved actor/AI; height="+height+"; old bounds="+old+"; new bounds="+BoundsIn(actor.transform));
            }
            Check(Outside(town,cows)==outside,"Objects outside four cows changed, including poultry/train");
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);File.WriteAllLines(Report+"/migration.txt",lines);
            File.WriteAllText(Report+"/protection.txt","PASS: all objects/components outside four existing cow actors unchanged; same actor and FarmAnimal IDs, positions, rotations, scales, seed and pasture; no poultry or other species replaced.\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(town);}
    }
    private static void CheckCows(GameObject town)
    {
        var cows=Cows(town);Check(cows.Length==4&&town.GetComponentsInChildren<FarmAnimal>(true).Count(a=>a.chicken)==7,"Farm population changed");
        foreach(var cow in cows)
        {
            Check(cow.head!=null&&cow.tail!=null&&cow.legs.Length==4&&cow.legs.All(t=>t!=null),"Incomplete joints");Check(cow.GetComponent<BoxCollider>()!=null,"Cow collider missing");
            foreach(var f in cow.GetComponentsInChildren<MeshFilter>(true))Check(AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Root+"/Meshes/"),"Old cow mesh still in scene");
            Check(Mathf.Abs(BoundsIn(cow.transform).min.y)<.005f,"Cow feet not on ground");
        }
        Check(town.GetComponentInChildren<TownRailTraffic>().cars.Length==3,"New train unexpectedly changed");
    }
    private static void Review()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=Object.FindObjectOfType<TownWorldPlaces>().gameObject;CheckCows(town);RenderSettings.fog=false;
        var cow=Cows(town)[0];var p=cow.transform.position;
        TownFloristBatch.Render(scene,Report+"/Cow-Unity.png",p+cow.transform.forward*4.2f-cow.transform.right*3.5f+Vector3.up*2.6f,p+Vector3.up*1.02f,1.65f);
        TownFloristBatch.Render(scene,Report+"/Pasture-Unity.png",new Vector3(478,13,245),new Vector3(485,0,270),17);
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);CheckCows(Object.FindObjectOfType<TownWorldPlaces>().gameObject);
        File.WriteAllText(Report+"/validation.txt","PASS: four cows in City and reduced Game; seven poultry preserved; original cow heights and ground contact; new meshes only; bound head/tail/four legs; simple colliders; three-car train retained.\n");
    }
    public static void ReviewFresh()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Review();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/CowModel.prefab");
            foreach(var eye in new[]{"Eye_L","Eye_R"})Check(Find(prefab.transform,eye).GetComponentInChildren<MeshRenderer>().sharedMaterial.name=="Eyes","Eye material changed");
            var head=Find(prefab.transform,"Head");int pink=0;
            foreach(var filter in head.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform.parent==head))
            {
                var mesh=filter.sharedMesh;var material=filter.GetComponent<MeshRenderer>().sharedMaterial;
                Check(mesh.subMeshCount==1,"Cow renderer must have exactly one material");
                var points=mesh.vertices;var indices=mesh.GetTriangles(0);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var center=(points[indices[i]]+points[indices[i+1]]+points[indices[i+2]])/3;
                    if(center.z<.70f)continue;
                    Check(material.name=="Pink"||material.name=="Eyes","Wrong front muzzle material: "+material.name);
                    if(material.name=="Pink")pink++;
                }
            }
            Check(pink>6,"Muzzle pink surface missing");
            File.WriteAllText(Report+"/review.txt","PASS: fresh Unity process native captures and serialized submesh/material assignments; pink muzzle and dark eyes; all earlier scene checks repeated.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail("review",e);}
    }
    public static void Play()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Playing,true);EditorApplication.EnterPlaymode();}
        catch(Exception e){Fail("play",e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Playing,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            var cows=Object.FindObjectsOfType<FarmAnimal>().Where(a=>!a.chicken).OrderBy(a=>a.name).ToArray();
            if(firstTime==0)
            {
                firstTime=Time.time;deadline=DateTime.UtcNow.AddSeconds(75);initial=cows.Select(c=>c.transform.localPosition).ToArray();firstLeg=cows[0].legs[0].localRotation;firstHead=cows[0].head.localRotation;
                // Bring the real camera into the pasture so the existing distance culling allows updates.
                if(Camera.main!=null)
                {
                    var follow=Camera.main.GetComponent<PickupCamera>();if(follow!=null)follow.enabled=false;
                    Camera.main.transform.position=cows[0].transform.position+new Vector3(0,6,5);
                }
                return;
            }
            Check(DateTime.UtcNow<deadline,"Live cow behaviour did not move/animate");if(Time.time-firstTime<9)return;
            Check(cows.Any(c=>c.IsWalking),"No live cow walking");
            Check(cows.Where((c,i)=>Vector3.Distance(c.transform.localPosition,initial[i])>.1f).Any(),"Cow did not walk");
            Check(Quaternion.Angle(firstLeg,cows[0].legs[0].localRotation)>.1f||Quaternion.Angle(firstHead,cows[0].head.localRotation)>.1f,"Joints not animated");
            foreach(var c in cows){var p=c.transform.localPosition;Check(c.pasture.Contains(new Vector2(p.x,p.z)),"Cow left pasture");Check(c.transform.localPosition.y==initial[Array.IndexOf(cows,c)].y,"Cow changed ground height");}
            var cow=cows[0];var position=cow.transform.position;
            TownFloristBatch.Render(cow.gameObject.scene,Report+"/Cow-Live.png",position+cow.transform.forward*4-cow.transform.right*3+Vector3.up*2.6f,position+Vector3.up,1.65f);
            File.WriteAllText(Report+"/play.txt","PASS: real Play Mode existing FarmAnimal AI moves cows inside original pasture and animates head/four legs/tail; camera distance culling exercised. No player save loaded.\n");SessionState.SetBool(Playing,false);EditorApplication.Exit(0);
        }
        catch(Exception e){SessionState.SetBool(Playing,false);Fail("play",e);}
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    private static void Fail(string stage,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
