using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>English modelling brief: European right-hand-traffic street furniture, full-depth black signal
/// housings, white backplates, three recessed circular lenses with open sun hoods, galvanized mounting
/// brackets, pedestrian pictograms and clean double-sided priority signs. No flat floating discs.</summary>
internal sealed class TownJunctionArt
{
    internal const string Root="Assets/Art/TownJunctions";
    internal Material Surface { get; private set; }
    private readonly Dictionary<string,GameObject> models=new();
    private readonly Dictionary<string,Material> lights=new();
    private int meshNumber;

    internal void Build()
    {
        foreach(string dir in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();
        var surface=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Junction street furniture"};
        Save(surface,"Materials/Surface.mat");Surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        foreach(var entry in new[]{("Red",new Color(1,.035f,.018f)),("Amber",new Color(1,.58f,.025f)),("Green",new Color(.02f,1,.28f))})
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=entry.Item1+" signal lens"};
            material.SetColor("_BaseColor",entry.Item2);material.SetFloat("_Surface",0);Save(material,"Materials/"+entry.Item1+".mat");
            lights.Add(entry.Item1,AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+entry.Item1+".mat"));
        }
        Model("VehicleSignal",VehicleBody());Model("PedestrianSignal",PedestrianBody());
        Model("PriorityRoadSign",Priority(true));Model("YieldSign",Yield(true));
        Model("PriorityPlate",Priority(false));Model("YieldPlate",Yield(false));
        AssetDatabase.SaveAssets();
    }

    private void Model(string id,TownMeshBuilder geometry)
    {
        var node=new GameObject(id);
        try
        {
            Mesh(node.transform,id,geometry,Surface);
            string path=Root+"/Prefabs/"+id+".prefab";PrefabUtility.SaveAsPrefabAsset(node,path);
            models.Add(id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        finally{Object.DestroyImmediate(node);}
    }

    internal Transform Place(Transform parent,string id,Vector3 position,Quaternion rotation)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);
        PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
        if(id is "VehicleSignal" or "PedestrianSignal" or "PriorityRoadSign" or "YieldSign")
        {
            float h=id=="VehicleSignal"?4.9f:id=="PedestrianSignal"?2.9f:2.8f;
            var box=go.AddComponent<BoxCollider>();box.center=Vector3.up*h/2;box.size=new Vector3(.18f,h,.18f);
        }
        return go.transform;
    }

    internal GameObject Light(Transform parent,string id,TownMeshBuilder geometry,string color)=>Mesh(parent,id,geometry,lights[color]).gameObject;
    internal Transform Mesh(Transform parent,string id,TownMeshBuilder geometry,Material material)
    {
        var mesh=geometry.Build(id);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();
        string path="Meshes/"+id.Replace(" ","")+"_"+meshNumber+++".asset";Save(mesh,path);
        var go=new GameObject(id,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+path);go.GetComponent<MeshRenderer>().sharedMaterial=material;
        if(material!=Surface)go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    internal void CombineStatic(Transform parent)
    {
        var filters=parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().sharedMaterial==Surface).ToArray();
        var mesh=new Mesh{name="Junction static furniture",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
        Save(mesh,"Meshes/CombinedFurniture.asset");
        var go=new GameObject("Combined junction furniture",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/CombinedFurniture.asset");go.GetComponent<MeshRenderer>().sharedMaterial=Surface;
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }

    internal static void Save(Object asset,string path)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+path);
    internal static void Append(TownMeshBuilder target,TownMeshBuilder source,Vector3 position,Quaternion rotation)
    {
        var mesh=source.Build("Temporary signal geometry");target.Append(mesh,Matrix4x4.TRS(position,rotation,Vector3.one));Object.DestroyImmediate(mesh);
    }

    private static TownMeshBuilder VehicleBody()
    {
        var d=new TownMeshBuilder();Pole(d,4.9f);
        d.Box(new Vector3(0,4.1f,-.07f),new Vector3(.76f,1.82f,.10f),TownColor.White);
        d.Box(new Vector3(0,4.1f,-.005f),new Vector3(.69f,1.75f,.07f),TownColor.Charcoal);
        d.Box(new Vector3(0,4.1f,.13f),new Vector3(.52f,1.48f,.30f),TownColor.Frame);
        foreach(float y in new[]{3.7f,4.1f,4.5f})
        {
            d.Cylinder(new Vector3(0,y,.293f),.169f,.169f,.035f,TownColor.Metal,20,Quaternion.Euler(90,0,0));
            d.Cylinder(new Vector3(0,y,.317f),.15f,.15f,.025f,TownColor.Charcoal,20,Quaternion.Euler(90,0,0));
            Hood(d,new Vector3(0,y,.31f),.18f,.31f);
        }
        foreach(float y in new[]{3.47f,4.74f})d.Box(new Vector3(0,y,-.15f),new Vector3(.19f,.09f,.26f),TownColor.Metal);
        return d;
    }

    private static TownMeshBuilder PedestrianBody()
    {
        var d=new TownMeshBuilder();Pole(d,2.95f);
        d.Box(new Vector3(0,2.5f,.04f),new Vector3(.53f,1.03f,.09f),TownColor.White);
        d.Box(new Vector3(0,2.5f,.16f),new Vector3(.44f,.94f,.22f),TownColor.Charcoal);
        foreach(float y in new[]{2.28f,2.72f})Hood(d,new Vector3(0,y,.28f),.21f,.17f);
        d.Box(new Vector3(0,1.1f,.1f),new Vector3(.17f,.27f,.14f),TownColor.Yellow);
        d.Cylinder(new Vector3(0,1.1f,.18f),.052f,.052f,.025f,TownColor.Frame,12,Quaternion.Euler(90,0,0));
        return d;
    }

    private static void Pole(TownMeshBuilder d,float height)
    {
        d.Cylinder(new Vector3(0,height/2,0),.066f,.053f,height,TownColor.Metal,12);
        d.Box(new Vector3(0,.055f,0),new Vector3(.26f,.11f,.26f),TownColor.Stone);
        foreach(float x in new[]{-.09f,.09f})foreach(float z in new[]{-.09f,.09f})d.Cylinder(new Vector3(x,.115f,z),.016f,.016f,.02f,TownColor.Frame,6);
    }

    private static TownMeshBuilder Priority(bool pole)
    {
        var d=new TownMeshBuilder();if(pole)Pole(d,2.8f);
        var q=Quaternion.Euler(0,0,45);var p=new Vector3(0,2.6f,.1f);
        d.Box(p,new Vector3(.83f,.83f,.065f),TownColor.Metal,q);
        d.Box(p+Vector3.forward*.039f,new Vector3(.79f,.79f,.02f),TownColor.White,q);
        d.Box(p+Vector3.forward*.053f,new Vector3(.62f,.62f,.015f),TownColor.Yellow,q);
        return d;
    }

    private static TownMeshBuilder Yield(bool pole)
    {
        var d=new TownMeshBuilder();if(pole)Pole(d,2.8f);
        var a=new Vector3(-.49f,2.98f,.1f);var b=new Vector3(.49f,2.98f,.1f);var c=new Vector3(0,2.13f,.1f);
        d.Triangle(c,b,a,TownColor.Red);d.Triangle(a-Vector3.forward*.055f,b-Vector3.forward*.055f,c-Vector3.forward*.055f,TownColor.Metal);
        foreach(var edge in new[]{(a,b),(b,c),(c,a)})d.Quad(edge.Item1,edge.Item2,edge.Item2-Vector3.forward*.055f,edge.Item1-Vector3.forward*.055f,TownColor.Red);
        d.Triangle(new Vector3(0,2.32f,.105f),new Vector3(.34f,2.89f,.105f),new Vector3(-.34f,2.89f,.105f),TownColor.White);
        return d;
    }

    private static void Hood(TownMeshBuilder d,Vector3 p,float radius,float depth)
    {
        for(int i=0;i<10;i++)
        {
            float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;
            var x=p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;
            var y=p+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius;
            d.Quad(x,y,y+Vector3.forward*depth,x+Vector3.forward*depth,TownColor.Charcoal);
            d.Quad(x+Vector3.forward*depth,y+Vector3.forward*depth,y,x,TownColor.Charcoal);
        }
    }

    internal static TownMeshBuilder VehicleLens(int aspect)
    {
        var d=new TownMeshBuilder();d.Cylinder(new Vector3(0,4.5f-aspect*.4f,.344f),.135f,.135f,.014f,TownColor.White,24,Quaternion.Euler(90,0,0));return d;
    }

    internal static TownMeshBuilder PedestrianGlyph(bool walk)
    {
        var d=new TownMeshBuilder();float y=walk?2.28f:2.72f,z=.282f;
        d.Cylinder(new Vector3(0,y+.12f,z),.045f,.045f,.015f,TownColor.White,12,Quaternion.Euler(90,0,0));
        d.Box(new Vector3(0,y+.005f,z),new Vector3(.065f,.14f,.018f),TownColor.White);
        var draft=new EquipmentDraft();
        void Limb(float ax,float ay,float bx,float by)=>TownRuralDetailModels.Beam(draft,new Vector3(ax,y+ay,z),new Vector3(bx,y+by,z),.036f,TownColor.White);
        if(walk){Limb(0,-.05f,-.09f,-.16f);Limb(0,-.05f,.10f,-.13f);Limb(0,.05f,-.12f,-.01f);Limb(0,.05f,.10f,.10f);}
        else{Limb(-.02f,-.04f,-.04f,-.17f);Limb(.02f,-.04f,.04f,-.17f);Limb(-.04f,.05f,-.09f,-.05f);Limb(.04f,.05f,.09f,-.05f);}
        var mesh=draft.Mesh.Build("Pedestrian limbs");d.Append(mesh,Matrix4x4.identity);Object.DestroyImmediate(mesh);return d;
    }
}
