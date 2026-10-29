using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Authors the approved two-stop shop lift and wires its cabin and landing doors.</summary>
public static class ElevatorModelBatch
{
    public const string Root="Assets/Art/ElevatorModel",Report="Library/ElevatorModelQA",Pictures="Docs/ElevatorModel";
    private const int Ivory=0,Sage=1,Trim=2,Metal=3,Yellow=4,Glass=5;
    private static float Bottom=>.096f*ShopProportions.Scale;
    private static float Top=>GridSystem.StoreyHeight+FloorPainter.UpperSurfaceLift;
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var materials=new[]{Shared("IvoryHousing"),Shared("SagePanels"),Shared("RubberHandrails"),Shared("GreySteps"),Shared("SafetyYellow"),Glazing()};
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/elevator_01.asset");
            float width=item.footprint.x*ShopProportions.CellSize-.025f,depth=item.footprint.y*ShopProportions.CellSize-.025f;
            float shaftTop=Top+1.35f,doorZ=-depth*.5f+.1f,doorHeight=1.08f,opening=.74f;
            var root=new GameObject("elevator_01");var frame=new LowPolyPropMesh(Root);var glass=new LowPolyPropMesh(Root);
            foreach(float x in new[]{-width*.5f+.075f,width*.5f-.075f})foreach(float z in new[]{-depth*.5f+.075f,depth*.5f-.075f})
            {
                frame.BeveledBox(new Vector3(x,shaftTop*.5f,z),new Vector3(.12f,shaftTop,.12f),.016f,Ivory);
                foreach(float y in new[]{.09f,Top-.015f,shaftTop-.055f})frame.BeveledBox(new Vector3(x,y,z),new Vector3(.17f,.18f,.17f),.017f,Ivory);
            }
            foreach(float y in new[]{Bottom-.07f,Top-.07f,shaftTop-.06f})
            {
                foreach(float z in new[]{-depth*.5f+.075f,depth*.5f-.075f})frame.BeveledBox(new Vector3(0,y,z),new Vector3(width-.12f,.12f,.115f),.012f,Ivory);
                foreach(float x in new[]{-width*.5f+.075f,width*.5f-.075f})frame.BeveledBox(new Vector3(x,y,0),new Vector3(.115f,.12f,depth-.12f),.012f,Ivory);
            }
            frame.BeveledBox(new Vector3(0,shaftTop+.075f,0),new Vector3(width*.80f,.15f,depth*.80f),.018f,Ivory);
            frame.Box(new Vector3(0,shaftTop-.09f,0),new Vector3(width-.19f,.18f,depth-.19f),Sage);
            // Quiet rear wall and two visible guide rails keep the shaft readable through the side glazing.
            frame.Box(new Vector3(0,shaftTop*.5f,depth*.5f-.10f),new Vector3(width-.2f,shaftTop-.12f,.035f),Sage);
            foreach(float x in new[]{-width*.30f,width*.30f})
            {
                frame.Box(new Vector3(x,shaftTop*.5f,depth*.5f-.17f),new Vector3(.045f,shaftTop-.1f,.065f),Trim);
                foreach(float y in new[]{.55f,1.25f,2.05f,2.65f})frame.BeveledBox(new Vector3(x,y,depth*.5f-.17f),new Vector3(.08f,.06f,.09f),.007f,Metal);
            }
            var doorMesh=new LowPolyPropMesh(Root);doorMesh.BeveledBox(Vector3.zero,new Vector3(opening*.5f-.003f,doorHeight,.028f),.006f,Metal);
            var leafMesh=doorMesh.Save("DoorLeaf");var pairs=new LiftLandingDoors.Landing[2];
            for(int level=0;level<2;level++)
            {
                float y=level==0?Bottom:Top;
                frame.BeveledBox(new Vector3(0,y-.013f,doorZ-.032f),new Vector3(opening+.11f,.05f,.12f),.008f,Ivory);
                frame.Box(new Vector3(0,y+.013f,doorZ-.06f),new Vector3(opening+.01f,.003f,.024f),Yellow);
                foreach(float x in new[]{-1f,1f})
                {
                    frame.Box(new Vector3(x*(opening*.5f+.032f),y+doorHeight*.5f,doorZ-.025f),new Vector3(.065f,doorHeight+.04f,.07f),Trim);
                    frame.Box(new Vector3(x*(width*.5f-.17f),y+.6f,doorZ-.03f),new Vector3(.15f,1.25f,.045f),Sage);
                }
                frame.BeveledBox(new Vector3(0,y+doorHeight+.055f,doorZ-.025f),new Vector3(opening+.13f,.115f,.075f),.008f,Trim);
                frame.Box(new Vector3(0,y+doorHeight+.2f,doorZ+.005f),new Vector3(width-.2f,.24f,.065f),Sage);
                float buttonX=opening*.5f+.10f,buttonZ=doorZ-.075f;
                frame.BeveledBox(new Vector3(buttonX,y+.53f,buttonZ),new Vector3(.085f,.16f,.02f),.005f,Trim);
                frame.Triangle(new Vector3(buttonX-.017f,y+.54f,buttonZ-.011f),new Vector3(buttonX,y+.574f,buttonZ-.011f),new Vector3(buttonX+.017f,y+.54f,buttonZ-.011f),Yellow);
                frame.Triangle(new Vector3(buttonX-.017f,y+.52f,buttonZ-.011f),new Vector3(buttonX+.017f,y+.52f,buttonZ-.011f),new Vector3(buttonX,y+.486f,buttonZ-.011f),Yellow);
                var left=Leaf(root.transform,"Floor "+(level+1)+" left door",leafMesh,materials,new Vector3(-opening*.25f,y+doorHeight*.5f,doorZ));
                var right=Leaf(root.transform,"Floor "+(level+1)+" right door",leafMesh,materials,new Vector3(opening*.25f,y+doorHeight*.5f,doorZ));
                pairs[level]=new LiftLandingDoors.Landing(left,right);
                float windowBottom=y+.31f,windowTop=level==0?Top-.14f:shaftTop-.16f;
                foreach(float side in new[]{-1f,1f})
                {
                    float x=side*(width*.5f-.075f),near=-depth*.5f+.15f,far=depth*.5f-.15f;
                    frame.Box(new Vector3(x,y+.135f,0),new Vector3(.045f,.27f,depth-.2f),Sage);
                    foreach(float z in new[]{near,far})frame.Box(new Vector3(x,(windowTop+windowBottom)*.5f,z),new Vector3(.035f,windowTop-windowBottom,.028f),Metal);
                    foreach(float height in new[]{windowBottom,windowTop})frame.Box(new Vector3(x,height,0),new Vector3(.035f,.028f,far-near),Metal);
                    glass.Quad(new Vector3(x,windowBottom,near),new Vector3(x,windowTop,near),new Vector3(x,windowTop,far),new Vector3(x,windowBottom,far),Glass);
                }
            }
            // The pit hides the painted tile below; cabin and sill have distinct heights to prevent coplanar flicker.
            frame.Box(new Vector3(0,Bottom-.018f,0),new Vector3(width-.20f,.04f,depth-.20f),Trim);
            var shell=MeshObject(root.transform,"Ivory frame, green panels and guide rails",frame,"Shaft",materials);
            shell.AddComponent<MeshCollider>().sharedMesh=shell.GetComponent<MeshFilter>().sharedMesh;
            var panes=MeshObject(root.transform,"Tinted shaft glazing",glass,"Glazing",materials);panes.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var cabin=new GameObject("Moving cabin").transform;cabin.SetParent(root.transform,false);
            var cabinMesh=new LowPolyPropMesh(Root);float cabinWidth=width-.27f,cabinDepth=depth-.27f;
            cabinMesh.BeveledBox(new Vector3(0,Bottom-.026f,0),new Vector3(cabinWidth,.07f,cabinDepth),.008f,Metal);
            cabinMesh.Box(new Vector3(0,Bottom+.54f,cabinDepth*.5f-.025f),new Vector3(cabinWidth,1.08f,.04f),Sage);
            foreach(float x in new[]{-cabinWidth*.5f+.02f,cabinWidth*.5f-.02f})
            {
                cabinMesh.BeveledBox(new Vector3(x,Bottom+.14f,0),new Vector3(.035f,.28f,cabinDepth),.007f,Sage);
                cabinMesh.Box(new Vector3(x,Bottom+.55f,0),new Vector3(.025f,.025f,cabinDepth-.12f),Metal);
                cabinMesh.Box(new Vector3(x,Bottom+.55f,cabinDepth*.5f-.03f),new Vector3(.035f,1.1f,.035f),Trim);
            }
            cabinMesh.Box(new Vector3(0,Bottom+.55f,cabinDepth*.5f-.065f),new Vector3(cabinWidth-.09f,.025f,.025f),Metal);
            cabinMesh.BeveledBox(new Vector3(0,Bottom+1.14f,0),new Vector3(cabinWidth,.045f,cabinDepth),.008f,Ivory);
            cabinMesh.Box(new Vector3(0,Bottom+1.11f,0),new Vector3(cabinWidth*.55f,.012f,.075f),Yellow);
            MeshObject(cabin,"Cabin interior",cabinMesh,"Cabin",materials);
            var floorCollider=cabin.gameObject.AddComponent<BoxCollider>();floorCollider.center=new Vector3(0,Bottom-.026f,0);floorCollider.size=new Vector3(cabinWidth,.07f,cabinDepth);
            var doors=root.AddComponent<LiftLandingDoors>();doors.Configure(pairs,.27f,doorZ);
            var visual=root.AddComponent<StoreyTransportVisual>();visual.platform=cabin;visual.liftDoors=doors;visual.lowerFeet=RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(0);
            visual.travelHeight=GridSystem.StoreyHeight+RetailEmpireTycoon.StoreOperations.StoreNavigation.FloorOffset(1)-visual.lowerFeet;
            root.AddComponent<PlacedObject>();
            var bounds=CityUpdateSetup.Measure(root);
            Require(bounds.size.x<=item.footprint.x*ShopProportions.CellSize+.001f && bounds.size.z<=item.footprint.y*ShopProportions.CellSize+.001f,"Lift exceeds its existing 5x5 footprint.");
            int triangles=root.GetComponentsInChildren<MeshFilter>().Sum(filter=>filter.sharedMesh.triangles.Length/3);Require(triangles<6500,"Lift geometry exceeds low-poly budget.");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Elevator.prefab");item.prefab=prefab;item.placementBounds=bounds;item.placementAlignmentOffset=new Vector3(0,0,bounds.center.z);
            item.description="Two-stop passenger lift with moving cabin and automatic landing doors. Place on a ground-floor surface, then add flooring at the marked upper exit. Ceiling tiles in its opening return to storage.";
            item.icon=ShopUiPreviews.Render(prefab,item.id,205);EditorUtility.SetDirty(item);AssetDatabase.SaveAssets();
            // Gallery pose matches the approved concept. The saved prefab starts with both landing doors closed.
            pairs[0].Left.localPosition+=Vector3.left*.25f;pairs[0].Right.localPosition+=Vector3.right*.25f;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Review floor";ground.transform.position=new Vector3(0,-.04f,0);ground.transform.localScale=new Vector3(10,.08f,10);ground.GetComponent<Renderer>().sharedMaterial=materials[Ivory];
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.66f,.68f,.64f);
            var sun=new GameObject("Soft studio daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(40,-35,0);
            TownFloristBatch.Render(scene,Pictures+"/Model.png",new Vector3(4.5f,3.1f,-6),new Vector3(0,1.52f,0),2f);
            TownFloristBatch.Render(scene,Pictures+"/Front.png",new Vector3(0,2,-7),new Vector3(0,1.5f,0),1.85f);
            File.WriteAllLines(Report+"/outputs.txt",Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Append(Root+".meta")
                .Concat(new[]{AssetDatabase.GetAssetPath(item),"Assets/Art/ShopUi/Items/elevator_01.png"}).Select(path=>path.Replace('\\','/')).Distinct());
            File.WriteAllText(Report+"/build.txt",$"PASS approved two-stop elevator model: ivory bevelled frame, green panels, transparent side glazing, guide rails, one moving cabin, two sliding landing door pairs and call buttons; {triangles} triangles; unchanged elevator_01 ID and 5x5 footprint.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static Material Shared(string name)=>AssetDatabase.LoadAssetAtPath<Material>(EscalatorModelBatch.Root+"/"+name+".mat")??throw new InvalidOperationException("Missing shared transport palette: "+name);
    private static Material Glazing()
    {
        string path=Root+"/Glass.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",new Color(.42f,.55f,.46f,.22f));material.SetFloat("_Smoothness",.32f);material.SetFloat("_Surface",1);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;EditorUtility.SetDirty(material);return material;
    }
    private static GameObject MeshObject(Transform parent,string name,LowPolyPropMesh mesh,string asset,Material[] materials)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh.Save(asset);go.GetComponent<MeshRenderer>().sharedMaterials=materials;return go;
    }
    private static Transform Leaf(Transform parent,string name,Mesh mesh,Material[] materials,Vector3 position)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));go.transform.SetParent(parent,false);go.transform.localPosition=position;
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=materials;go.GetComponent<BoxCollider>().center=mesh.bounds.center;go.GetComponent<BoxCollider>().size=mesh.bounds.size;return go.transform;
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/ElevatorModelBatch.cs","Assets/Project/Editor/LowPolyPropMesh.cs","Assets/Project/Editor/EscalatorModelBatch.cs","Assets/Project/Editor/MultiStoreyValidation.cs","Assets/Project/Editor/ConstructionWorkspacePlay.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/play-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
