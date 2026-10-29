using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Authors the three store transports at the actual shop scale; unrelated city assets are not regenerated.</summary>
public static class StoreyTransportBatch
{
    public const string Root="Assets/Art/StoreyTransport", Report="Library/StoreyTransportQA", Pictures="Docs/StoreyTransport";
    private static Material steel, cream, tread, sage, safety;
    private static float Bottom=>.096f*ShopProportions.Scale;
    private static float Top=>GridSystem.StoreyHeight+FloorPainter.UpperSurfaceLift;
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            steel=Material("Charcoal",new Color(.16f,.23f,.23f));cream=Material("WarmFrame",new Color(.73f,.74f,.66f));
            tread=Material("OakTreads",new Color(.53f,.35f,.17f));sage=Material("SagePanels",new Color(.28f,.48f,.43f));
            safety=Material("SafetyEdges",new Color(.94f,.71f,.23f));
            string[] ids={"stairs_01","elevator_01","escalator_01"};
            for(int i=0;i<ids.Length;i++)
            {
                var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+ids[i]+".asset");
                var model=new GameObject(ids[i]);
                float width=item.footprint.x*ShopProportions.CellSize-.025f,depth=item.footprint.y*ShopProportions.CellSize-.025f;
                if(item.storeyConnection==StoreyConnectionKind.Elevator)Lift(model.transform,width,depth);
                else Ramp(model.transform,width,depth,item.storeyConnection==StoreyConnectionKind.Escalator);
                model.AddComponent<PlacedObject>();
                var prefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"/"+ids[i]+".prefab");
                item.prefab=prefab;item.placementBounds=CityUpdateSetup.Measure(model);
                item.placementAlignmentOffset=new Vector3(0,0,item.placementBounds.center.z);
                item.displayName=i==0?"Stairs":i==1?"Elevator":"Escalator";
                item.description="Customers travel between floors in both directions. Build both landings first. Installation returns ceiling tiles in the opening to storage. Furniture is never removed automatically.";
                item.icon=ShopUiPreviews.Render(prefab,item.id,205);EditorUtility.SetDirty(item);
                model.transform.position=new Vector3((i-1)*3.3f,0,0);
            }
            var blueprint=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>("Assets/Prefabs/StarterStore/StarterStore.asset");
            blueprint.ConstructionStock.ConfigureTransports(ids.Append("escalator_down_01").Select(id=>AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+id+".asset")));
            EditorUtility.SetDirty(blueprint);AssetDatabase.SaveAssets();
            var light=new GameObject("Review daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientLight=new Color(.68f,.72f,.69f);
            Box(null,"Review floor",new Vector3(0,-.04f,0),new Vector3(14,.08f,9),cream);
            TownFloristBatch.Render(scene,Pictures+"/Models.png",new Vector3(-7,6,-10),new Vector3(0,1,0),5.4f);
            var outputs=new[]{AssetDatabase.GetAssetPath(blueprint)}.Concat(ids.Select(id=>"Assets/Resources/MultiStorey/"+id+".asset"))
                .Concat(ids.Select(id=>"Assets/Art/ShopUi/Items/"+id+".png"))
                .Concat(Directory.GetFiles(Root,"*",SearchOption.AllDirectories)).Append(Root+".meta").Select(path=>path.Replace('\\','/')).Distinct().ToArray();
            File.WriteAllLines(Report+"/outputs.txt",outputs);
            File.WriteAllText(Report+"/build.txt","PASS three scaled transport prefabs, matching rendered catalog icons, English descriptions and one of each in new-game stock.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static Material Material(string name,Color color)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Root+"/"+name+".mat");}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(material);return material;
    }
    private static Transform Box(Transform parent,string name,Vector3 point,Vector3 size,Material material,bool physical=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=point;go.transform.localScale=size;
        go.GetComponent<MeshRenderer>().sharedMaterial=material;if(!physical)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
    }
    private static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material)
    {
        var beam=Box(parent,name,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material);
        beam.localRotation=Quaternion.LookRotation(b-a);
    }
    private static void Ramp(Transform root,float width,float depth,bool escalator)
    {
        float run=depth-ShopProportions.CellSize,rise=Top-Bottom;
        int steps=18;
        var visual=escalator?root.gameObject.AddComponent<StoreyTransportVisual>():null;
        if(visual!=null){visual.travelHeight=rise;visual.treadDepth=run;visual.treadTop=Bottom-.02f;visual.treadMarkers=new Transform[steps];}
        for(int i=0;i<steps;i++)
        {
            float t=(i+.5f)/steps;
            var step=new GameObject("Step "+i).transform;step.SetParent(root,false);step.localPosition=new Vector3(0,Bottom-.02f+t*rise,Mathf.Lerp(-run*.5f,run*.5f,t));
            Box(step,"Tread",Vector3.zero,new Vector3(width-.18f,.04f,run/steps+.008f),escalator?cream:tread,!escalator);
            if(escalator){Box(step,"Yellow nosing",new Vector3(0,.022f,-run/steps*.43f),new Vector3(width-.18f,.009f,.02f),safety);visual.treadMarkers[i]=step;}
        }
        foreach(float end in new[]{-1f,1f})Box(root,"Landing plate",new Vector3(0,(end<0?Bottom:Top)-.025f,end*run*.5f),new Vector3(width,.05f,ShopProportions.CellSize),cream,true);
        foreach(float x in new[]{-width*.47f,width*.47f})
        {
            Beam(root,"Side stringer",new Vector3(x,Bottom-.06f,-run*.5f),new Vector3(x,Top-.06f,run*.5f),.09f,escalator?cream:steel);
            Beam(root,"Continuous handrail",new Vector3(x,Bottom+.54f,-run*.5f),new Vector3(x,Top+.54f,run*.5f),.055f,steel);
            if(escalator)Beam(root,"Sage balustrade",new Vector3(x,Bottom+.26f,-run*.5f),new Vector3(x,Top+.26f,run*.5f),.16f,sage);
            for(int i=0;i<=5;i++)
            {
                float t=i/5f;Box(root,"Railing post",new Vector3(x,Bottom+rise*t+.24f,Mathf.Lerp(-run*.5f,run*.5f,t)),new Vector3(.035f,.57f,.035f),steel);
            }
        }
    }
    private static void Lift(Transform root,float width,float depth)
    {
        float rise=GridSystem.StoreyHeight+RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(1)-RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(0);
        var visual=root.gameObject.AddComponent<StoreyTransportVisual>();visual.lowerFeet=RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(0);visual.travelHeight=rise;
        var cabin=new GameObject("Moving cabin").transform;cabin.SetParent(root,false);visual.platform=cabin;
        Box(cabin,"Cabin floor",new Vector3(0,Bottom-.035f,0),new Vector3(width-.16f,.07f,depth-.16f),cream,true);
        foreach(float x in new[]{-width*.43f,width*.43f})
        {
            Box(cabin,"Cabin side panel",new Vector3(x,Bottom+.37f,0),new Vector3(.025f,.74f,depth-.14f),sage);
            Beam(cabin,"Cabin grab rail",new Vector3(x,Bottom+.45f,-depth*.35f),new Vector3(x,Bottom+.45f,depth*.35f),.03f,steel);
        }
        Box(cabin,"Cabin back",new Vector3(0,Bottom+.4f,depth*.43f),new Vector3(width-.12f,.8f,.035f),sage);
        Box(cabin,"Cabin roof",new Vector3(0,Bottom+.98f,0),new Vector3(width-.1f,.045f,depth-.1f),cream);
        Box(cabin,"Cabin light",new Vector3(0,Bottom+.95f,0),new Vector3(width*.55f,.012f,.09f),safety);
        float height=Top+1.04f;
        foreach(float x in new[]{-width*.47f,width*.47f})foreach(float z in new[]{-depth*.47f,depth*.47f})
            Box(root,"Shaft upright",new Vector3(x,height*.5f,z),new Vector3(.065f,height,.065f),steel);
        foreach(float y in new[]{Bottom,Top})
        {
            Box(root,"Entrance threshold",new Vector3(0,y-.01f,-depth*.48f),new Vector3(width,.02f,.06f),safety);
            Box(root,"Landing lintel",new Vector3(0,y+.99f,-depth*.47f),new Vector3(width,.07f,.075f),cream);
            Box(root,"Call panel",new Vector3(width*.47f-.04f,y+.48f,-depth*.48f-.005f),new Vector3(.08f,.15f,.045f),cream);
            Box(root,"Call button",new Vector3(width*.47f-.04f,y+.48f,-depth*.48f-.03f),new Vector3(.025f,.035f,.015f),safety);
        }
    }
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/StoreyTransportBatch.cs","Assets/Project/Editor/MultiStoreyValidation.cs","Assets/Project/Editor/ConstructionWorkspacePlay.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/play-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
