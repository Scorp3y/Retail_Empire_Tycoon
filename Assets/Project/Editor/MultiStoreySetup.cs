using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Builds only the new transport assets; never regenerates the city or modifies a player save.</summary>
public static class MultiStoreySetup
{
    public const string Folder="Assets/Prefabs/MultiStorey";
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        try
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/MultiStorey");AssetDatabase.Refresh();
            foreach(var kind in new[]{StoreyConnectionKind.Stairs,StoreyConnectionKind.Elevator,StoreyConnectionKind.Escalator})Create(kind);
            UpgradeParking();AssetDatabase.SaveAssets();
            MultiStoreyValidation.Run();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void RefreshIcons()
    {
        TownWorldSafety.RequireIsolatedEditor();
        try
        {
            foreach(var item in Resources.LoadAll<BuildItemData>("MultiStorey"))
            {
                item.icon=ShopUiPreviews.Render(item.prefab,item.id,205);
                EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Material Material(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(material);return material;
    }
    private static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material,bool collider=false)
    {
        var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(root,false);
        box.transform.localPosition=position;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=material;
        if(!collider)Object.DestroyImmediate(box.GetComponent<Collider>());return box;
    }
    private static void Beam(Transform root,string name,Vector3 from,Vector3 to,float width,Material material)
    {
        var beam=Box(root,name,(from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)),material);
        beam.transform.localRotation=Quaternion.LookRotation(to-from);
    }
    private static void Create(StoreyConnectionKind kind)
    {
        string id=kind==StoreyConnectionKind.Stairs?"stairs_01":kind==StoreyConnectionKind.Elevator?"elevator_01":"escalator_01";
        var steel=Material("Steel",new Color(.19f,.25f,.25f));var light=Material("Steps",new Color(.72f,.75f,.68f));
        var accent=Material("Safety",new Color(.89f,.65f,.17f));var glass=Material("LiftPanels",new Color(.31f,.49f,.44f));
        var root=new GameObject(id);
        try
        {
            float cell=.1667f,width=kind==StoreyConnectionKind.Escalator?6*cell:5*cell;
            float depth=kind==StoreyConnectionKind.Elevator?5*cell:10*cell;
            if(kind==StoreyConnectionKind.Elevator)
            {
                var animation=root.AddComponent<StoreyTransportVisual>();
                var cabin=new GameObject("Moving cabin").transform;cabin.SetParent(root.transform,false);animation.platform=cabin;
                Box(cabin,"Platform",new Vector3(0,.08f,0),new Vector3(width-.06f,.045f,depth-.06f),light);
                Box(cabin,"Back panel",new Vector3(0,.37f,depth*.46f),new Vector3(width-.08f,.55f,.025f),glass);
                foreach(float x in new[]{-width*.46f,width*.46f})
                {
                    Box(root.transform,"Guide rail",new Vector3(x,.79f,depth*.46f),new Vector3(.045f,1.58f,.045f),steel);
                    Box(cabin,"Cabin side",new Vector3(x,.37f,0),new Vector3(.025f,.55f,depth-.08f),glass);
                }
                foreach(float height in new[]{.03f,1.03f})
                    Box(root.transform,"Landing threshold",new Vector3(0,height,-depth*.5f),new Vector3(width,.025f,.05f),accent);
                Box(root.transform,"Lift header",new Vector3(0,1.58f,0),new Vector3(width,.06f,depth),steel);
            }
            else
            {
                int count=20;
                for(int i=0;i<count;i++)
                {
                    float t=(i+.5f)/count;
                    Box(root.transform,"Tread "+i,new Vector3(0,.105f+(i+1f)/count-.02f,Mathf.Lerp(-depth*.5f,depth*.5f,t)),new Vector3(width,.04f,depth/count+.001f),kind==StoreyConnectionKind.Stairs?light:steel);
                    if(kind==StoreyConnectionKind.Escalator)Box(root.transform,"Step edge "+i,new Vector3(0,.105f+(i+1f)/count+.002f,Mathf.Lerp(-depth*.5f,depth*.5f,t)-depth/count*.4f),new Vector3(width,.006f,.01f),accent);
                }
                foreach(float x in new[]{-width*.47f,width*.47f})
                {
                    Beam(root.transform,"Stringer",new Vector3(x,.09f,-depth*.5f),new Vector3(x,1.09f,depth*.5f),.065f,steel);
                    Beam(root.transform,"Handrail",new Vector3(x,.46f,-depth*.5f),new Vector3(x,1.46f,depth*.5f),.035f,steel);
                    for(int i=0;i<=4;i++)
                    {
                        float t=i/4f;
                        Box(root.transform,"Baluster",new Vector3(x,.27f+t,Mathf.Lerp(-depth*.5f,depth*.5f,t)),new Vector3(.02f,.38f,.02f),steel);
                    }
                }
                if(kind==StoreyConnectionKind.Escalator)
                {
                    var animation=root.AddComponent<StoreyTransportVisual>();
                    animation.treadMarkers=Enumerable.Range(0,6).Select(i=>Box(root.transform,"Moving belt marker",Vector3.zero,new Vector3(.08f,.008f,.025f),accent).transform).ToArray();
                }
            }
            root.AddComponent<PlacedObject>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+id+".prefab");
            string path="Assets/Resources/MultiStorey/"+id+".asset";
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            if(item==null){item=ScriptableObject.CreateInstance<BuildItemData>();AssetDatabase.CreateAsset(item,path);}
            item.id=id;item.displayName=kind==StoreyConnectionKind.Stairs?"Лестница":kind==StoreyConnectionKind.Elevator?"Лифт":"Эскалатор";
            item.description="Соединяет первый и второй этажи. Верхняя площадка должна иметь пол, над механизмом нужен проём.";
            item.category=BuildCategory.Structures;item.footprint=new Vector2Int(Mathf.RoundToInt(width/cell),Mathf.RoundToInt(depth/cell));
            item.ruleFlags=PlacementRuleFlags.InsidePurchasedArea|PlacementRuleFlags.NoOverlap;
            item.price=kind==StoreyConnectionKind.Stairs?750:kind==StoreyConnectionKind.Elevator?3000:2200;
            item.prefab=prefab;item.storeyConnection=kind;item.alignModelToFootprint=true;
            item.placementBounds=CityUpdateSetup.Measure(root);item.weightKg=100;item.cargoVolumeM3=.65f;
            item.icon=ShopUiPreviews.Render(prefab,id,205);EditorUtility.SetDirty(item);
        }
        finally{Object.DestroyImmediate(root);}
    }
    private static void UpgradeParking()
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        if(item.parkingGeometryVersion>=1)return;
        string old=Folder+"/ParkingBayLegacy.prefab";
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(item.prefab),old))throw new InvalidOperationException("Cannot preserve parking geometry.");
        item.previousParkingPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(old);
        item.previousParkingFootprint=item.footprint;item.previousParkingBounds=item.placementBounds;
        var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(item.prefab));
        try
        {
            float width=8*.1667f-.025f,depth=16*.1667f-.025f;
            root.GetComponent<ParkingBay>().Resize(width,depth,item.placementBounds);
            item.prefab=PrefabUtility.SaveAsPrefabAsset(root,AssetDatabase.GetAssetPath(item.prefab));
            item.footprint=new Vector2Int(8,16);item.placementBounds=CityUpdateSetup.Measure(root);item.parkingGeometryVersion=1;
            item.icon=ShopUiPreviews.Render(item.prefab,item.id,25);EditorUtility.SetDirty(item);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
