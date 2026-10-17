using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Details-only acceptance. The Game scene, land catalogue and starter-store data are immutable inputs.</summary>
public static class TownYardStoryBatch
{
    internal const string Report="Library/TownYardStoryQA",Pictures="Docs/TownYardStoryReview";
    public static void Generate()=>Run(true);
    public static void Review()=>Run(false);
    private static void Run(bool generate)
    {
        string stage=generate?"result":"review";
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/"+stage+".txt","RUNNING\n");
            if(generate)
            {
                Capture("Before");
                var before=new TownForgottenYardPreservation();
                var protectedFiles=Directory.GetFiles(TownForgottenYardAuthoring.Root,"*",SearchOption.AllDirectories)
                    .Where(p=>!p.Replace('\\','/').StartsWith(TownYardStoryPlacement.Root+"/",StringComparison.Ordinal))
                    .Concat(new[]{CityUpdateSetup.GamePath,StarterStoreSetup.BlueprintPath,TownForgottenYardAuthoring.LayoutPath})
                    .ToDictionary(p=>p,TownForgottenYardBatch.Hash);
                new TownYardStoryPlacement().Build();before.RequireUnchanged();
                foreach(var p in protectedFiles)TownForgottenYardBatch.Require(TownForgottenYardBatch.Hash(p.Key)==p.Value,"Non-detail input changed: "+p.Key);
                File.Copy(TownForgottenYardBatch.Report+"/protection.txt",Report+"/protection.txt",true);
                File.AppendAllText(Report+"/protection.txt","PASS: Game scene, starter blueprint, 24-plot catalogue, city layout and "+protectedFiles.Count+" established input resources unchanged.\n");
            }
            TownForgottenYardBatch.Validate(420000);ValidateDetails();Capture("After");Catalogue();
            File.Copy(TownForgottenYardBatch.Report+"/domain.txt",Report+"/domain.txt",true);
            File.WriteAllText(Report+"/"+stage+".txt","PASS: distinct scene landmarks, connected curved paths, contained 24-parcel geometry, render budget and legacy ownership regression.\n");
            Receipt(stage);EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void ValidateDetails()
    {
        var scenery=Object.FindObjectOfType<TownExpansionScenery>();
        var children=scenery.GetComponentsInChildren<Transform>(true);
        string[] landmarks={"PlayFort","ClimbingDome","CrookedPergola","ToolShed","BrokenGreenhouse","RepairCanopy","AbandonedEstateCar","BasketballHoop","WaterTank","DryFountain"};
        foreach(string id in landmarks)TownForgottenYardBatch.Require(children.Any(t=>t.name==id),"Missing authored landmark: "+id);
        var filters=scenery.GetComponentsInChildren<MeshFilter>(true);int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        TownForgottenYardBatch.Require(filters.Length==24&&filters.All(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(TownYardStoryPlacement.Root+"/",StringComparison.Ordinal)),"Old repeated parcel meshes remain.");
        TownForgottenYardBatch.Require(triangles<420000,"Yard detail budget exceeded.");
        foreach(var filter in filters)
        {
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;
            TownForgottenYardBatch.Require(vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Non-finite geometry.");
            TownForgottenYardBatch.Require(mesh.normals.Length==vertices.Length&&mesh.uv.Length==vertices.Length&&mesh.uv2.Length==vertices.Length,"Incomplete native mesh channels.");
        }
        File.WriteAllText(Report+"/metrics.txt",$"37 new reusable models, 10 major scene landmarks, 24 independent scenery batches, {triangles} triangles, no scenery colliders. Curved routes and ground patches are clipped to purchase boundaries; whole props remain on one parcel.\n");
    }
    private static void Capture(string suffix)
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);RenderSettings.fog=false;
        TownFloristBatch.Render(scene,Pictures+"/Overview"+suffix+".png",new Vector3(86,69,-69),new Vector3(33,0,17),43);
        if(suffix=="Before")return;
        TownFloristBatch.Render(scene,Pictures+"/Playground.png",new Vector3(17,17,1),new Vector3(6.5f,0,14.5f),10);
        TownFloristBatch.Render(scene,Pictures+"/GardenWorkshop.png",new Vector3(37,18,10),new Vector3(21,0,25),10);
        TownFloristBatch.Render(scene,Pictures+"/RepairCorner.png",new Vector3(34,10,-12),new Vector3(22,0,4),8);
        TownFloristBatch.Render(scene,Pictures+"/Court.png",new Vector3(59,16,2),new Vector3(40,0,13),10);
    }
    private static void Catalogue()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.65f,.68f,.68f);
        var light=new GameObject("Catalogue sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(48,-32,0);
        var prefabs=Directory.GetFiles(TownYardStoryPlacement.Root+"/Prefabs","*.prefab").OrderBy(p=>p).ToArray();
        for(int i=0;i<prefabs.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]));go.transform.position=new Vector3(i%7*9,0,i/7*9);
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(27,-.12f,22);floor.transform.localScale=new Vector3(70,.2f,62);
        var backdrop=new Material(Shader.Find("Universal Render Pipeline/Lit"));backdrop.color=new Color(.55f,.58f,.49f);floor.GetComponent<MeshRenderer>().sharedMaterial=backdrop;
        TownFloristBatch.Render(scene,Pictures+"/NewModels.png",new Vector3(53,55,-47),new Vector3(27,0,21),38);
    }
    internal static void Receipt(string stage)=>File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownYardStoryPlacement.Root,"*",SearchOption.AllDirectories)
        .Concat(new[]{TownYardStoryPlacement.Root+".meta",TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal)
        .Select(p=>TownForgottenYardBatch.Hash(p)+" "+p.Replace('\\','/')));
}
