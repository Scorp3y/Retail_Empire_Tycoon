using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Builds the approved enclosed escalator without changing its saved ID, footprint or navigation contract.</summary>
public static class EscalatorModelBatch
{
    public const string Root="Assets/Art/EscalatorModel",Report="Library/EscalatorModelQA",Pictures="Docs/EscalatorModel";
    private const int Cream=0,Sage=1,Rubber=2,Steel=3,Yellow=4,Groove=5;
    private static float Bottom=>.096f*ShopProportions.Scale;
    private static float Top=>GridSystem.StoreyHeight+FloorPainter.UpperSurfaceLift;
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var materials=new[]{Material("IvoryHousing",new Color(.77f,.77f,.69f)),Material("SagePanels",new Color(.40f,.51f,.36f)),
                Material("RubberHandrails",new Color(.13f,.15f,.15f)),Material("GreySteps",new Color(.45f,.48f,.47f)),
                Material("SafetyYellow",new Color(.98f,.70f,.08f)),Material("TreadGrooves",new Color(.34f,.37f,.36f))};
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/escalator_01.asset");
            float width=item.footprint.x*ShopProportions.CellSize-.025f,depth=item.footprint.y*ShopProportions.CellSize-.025f;
            float half=depth*.5f,run=depth-ShopProportions.CellSize,rise=Top-Bottom,clearWidth=width-.49f;
            var model=new GameObject("escalator_01");
            var draft=new LowPolyPropMesh(Root);
            var body=new[]{new Vector2(-half+.015f,.045f),new Vector2(-half+.015f,Bottom+.12f),new Vector2(-half+.06f,Bottom+.25f),
                new Vector2(-half+.25f,Bottom+.28f),new Vector2(half-.24f,Top+.28f),new Vector2(half-.035f,Top+.28f),
                new Vector2(half-.015f,Top+.20f),new Vector2(half-.015f,.045f)};
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*(width*.5f-.12f);
                draft.Profile(body,x,.24f,.017f,Cream);
                var insert=new[]{new Vector2(-half+.40f,.12f),new Vector2(-half+.40f,Bottom+.11f),new Vector2(half-.11f,Top+.08f),new Vector2(half-.11f,.12f)};
                draft.Profile(insert,side*(width*.5f+.001f),.004f,.001f,Sage);
                var a=new Vector2(-half+.25f,Bottom+.42f);var b=new Vector2(half-.25f,Top+.42f);
                var rail=RoundedLoop(a,b,.19f,10);
                draft.Profile(rail,x,.065f,.008f,Sage);
                draft.Handrail(rail,x,.10f,.033f,Rubber);
            }
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*(half-.12f),y=side<0?Bottom:Top;
                draft.Box(new Vector3(0,y-.025f,z),new Vector3(clearWidth,.05f,.25f),Steel);
                for(int i=0;i<20;i++)draft.TopRect(new Vector3(Mathf.Lerp(-clearWidth*.48f,clearWidth*.48f,i/19f),y+.001f,z),.004f,.225f,Groove);
                draft.Box(new Vector3(0,y+.003f,z-side*.107f),new Vector3(clearWidth,.008f,.023f),Yellow);
            }
            var housing=CreateMeshObject(model.transform,"Enclosed housing and continuous handrails",draft,"Housing",materials);
            housing.AddComponent<MeshCollider>().sharedMesh=housing.GetComponent<MeshFilter>().sharedMesh;
            // The existing navigation routes own passenger motion; colliders support the physical landing surfaces only.
            foreach(float side in new[]{-1f,1f})
            {
                var landing=new GameObject("Landing support").AddComponent<BoxCollider>();landing.transform.SetParent(model.transform,false);
                landing.center=new Vector3(0,(side<0?Bottom:Top)-.025f,side*(half-.12f));landing.size=new Vector3(clearWidth,.05f,.25f);
            }
            const int count=18;float stepDepth=run/count,stepHeight=rise/count+.003f;
            var tread=new LowPolyPropMesh(Root);tread.Box(Vector3.zero,new Vector3(clearWidth-.012f,stepHeight,stepDepth+.002f),Steel);
            float top=stepHeight*.5f;
            tread.Box(new Vector3(0,top+.002f,-stepDepth*.5f+.01f),new Vector3(clearWidth-.012f,.009f,.02f),Yellow);
            foreach(float side in new[]{-1f,1f})tread.Box(new Vector3(side*(clearWidth*.5f-.012f),top+.002f,0),new Vector3(.009f,.006f,stepDepth),Yellow);
            for(int i=0;i<18;i++)tread.TopRect(new Vector3(Mathf.Lerp(-clearWidth*.46f,clearWidth*.46f,i/17f),top+.0015f,.008f),.003f,stepDepth-.035f,Groove);
            var stepMesh=tread.Save("Step");
            var visual=model.AddComponent<StoreyTransportVisual>();visual.travelHeight=rise;visual.treadDepth=run;visual.treadTop=Bottom-stepHeight*.5f;visual.treadMarkers=new Transform[count];
            for(int i=0;i<count;i++)
            {
                float t=(i+.5f)/count;
                var step=new GameObject("Moving step "+i,typeof(MeshFilter),typeof(MeshRenderer));step.transform.SetParent(model.transform,false);
                step.transform.localPosition=new Vector3(0,visual.treadTop+t*rise,Mathf.Lerp(-run*.5f,run*.5f,t));
                step.GetComponent<MeshFilter>().sharedMesh=stepMesh;step.GetComponent<MeshRenderer>().sharedMaterials=materials;visual.treadMarkers[i]=step.transform;
            }
            model.AddComponent<PlacedObject>();
            var bounds=CityUpdateSetup.Measure(model);
            Require(bounds.size.x<=item.footprint.x*ShopProportions.CellSize+.001f && bounds.size.z<=item.footprint.y*ShopProportions.CellSize+.001f,"Escalator extends beyond its existing placement footprint.");
            int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(filter=>filter.sharedMesh.triangles.Length/3);
            Require(triangles<6000,"Escalator geometry exceeds the low-poly budget.");
            var prefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"/Escalator.prefab");
            item.prefab=prefab;item.placementBounds=bounds;item.placementAlignmentOffset=new Vector3(0,0,bounds.center.z);
            item.description="Reversible escalator between floors 1 and 2. Place on a ground-floor surface; add flooring at the marked upper exit to enable customer access. Ceiling tiles in its opening return to storage.";
            item.icon=ShopUiPreviews.Render(prefab,item.id,205);EditorUtility.SetDirty(item);AssetDatabase.SaveAssets();
            var backdrop=new LowPolyPropMesh(Root);backdrop.Box(new Vector3(0,-.03f,0),new Vector3(12,.06f,12),Cream);
            CreateMeshObject(null,"Review ground",backdrop,"ReviewGround",materials);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.67f,.69f,.66f);
            var sun=new GameObject("Soft review daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            TownFloristBatch.Render(scene,Pictures+"/Model.png",new Vector3(-4.4f,3.7f,-5.8f),new Vector3(0,1.05f,0),2.1f);
            TownFloristBatch.Render(scene,Pictures+"/Side.png",new Vector3(-6,2.5f,0),new Vector3(0,1,0),1.8f);
            Object.DestroyImmediate(model);
            var outputs=Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Append(Root+".meta")
                .Concat(new[]{AssetDatabase.GetAssetPath(item),"Assets/Art/ShopUi/Items/escalator_01.png"}).Select(path=>path.Replace('\\','/')).Distinct().ToArray();
            File.WriteAllLines(Report+"/outputs.txt",outputs);
            File.WriteAllText(Report+"/build.txt",$"PASS approved concept rebuilt as an enclosed low-poly escalator: bevelled ivory body, sage panels, rounded continuous handrails, grey ribbed steps and yellow edges. {triangles} triangles; shared animated tread mesh; unchanged 6x10 footprint, ID, stock and price.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static Material Material(string name,Color color)
    {
        string path=Root+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",name=="RubberHandrails"?.15f:.22f);EditorUtility.SetDirty(material);return material;
    }
    private static GameObject CreateMeshObject(Transform parent,string name,LowPolyPropMesh geometry,string asset,Material[] materials)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=geometry.Save(asset);go.GetComponent<MeshRenderer>().sharedMaterials=materials;return go;
    }
    private static Vector2[] RoundedLoop(Vector2 a,Vector2 b,float radius,int segments)
    {
        var direction=(b-a).normalized;var normal=new Vector2(-direction.y,direction.x);var points=new List<Vector2>{a+normal*radius,b+normal*radius};
        for(int i=1;i<=segments;i++){float t=i*Mathf.PI/segments;points.Add(b+(normal*Mathf.Cos(t)+direction*Mathf.Sin(t))*radius);}
        points.Add(a-normal*radius);
        for(int i=1;i<segments;i++){float t=i*Mathf.PI/segments;points.Add(a+(-normal*Mathf.Cos(t)-direction*Mathf.Sin(t))*radius);}
        return points.ToArray();
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/EscalatorModelBatch.cs","Assets/Project/Editor/LowPolyPropMesh.cs","Assets/Project/Editor/MultiStoreyValidation.cs","Assets/Project/Editor/ConstructionWorkspacePlay.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/play-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}

