using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Owns generated art and references existing licensed project assets without modifying them.</summary>
internal sealed class TownRuralArtCatalogue
{
    internal const string Root="Assets/Art/TownRuralPolish";
    private readonly Dictionary<string,GameObject> models=new();
    private TownStaticTypography type;
    internal Material Surface{get;private set;}
    internal Material Glass{get;private set;}
    internal void Build()
    {
        foreach(string directory in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+directory);AssetDatabase.Refresh();
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,name="Rural art palette"};palette.SetPixels32(TownRuralDetailModels.Palette());palette.Apply();Save(palette,"Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Rural warm palette",enableInstancing=true};material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));material.SetFloat("_PaletteIndex",-1);Save(material,"Materials/Surface.mat");Surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var glass=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownGatewayPlacement.Root+"/Materials/Glass.mat")){name="Rural glazing"};Save(glass,"Materials/Glass.mat");Glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        type=new TownStaticTypography(Root,"FARM PICKUP FARM MARKET SHOP & COFFEE AIR & WATER FUEL FRESH PRODUCE WORKSHOP 1.99 2.49 3.99 95 98 DIESEL ENTRY EXIT PARKING COFFEE");
        foreach(var recipe in TownRuralDetailModels.Create())
        {
            var go=new GameObject(recipe.Id);go.isStatic=true;
            try
            {
                MeshPart(go.transform,recipe.Id,recipe.Draft.Mesh);
                foreach(var volume in recipe.Draft.Solids)Box(go.transform,volume.Id,volume.Center,volume.Size);
                switch(recipe.Id)
                {
                    case "PackingBarn":type.Add(go.transform,"Pickup fascia","FARM PICKUP",new Vector3(0,5.2f,13.98f),13,.7f);break;
                    case "Workshop":type.Add(go.transform,"Workshop sign","WORKSHOP",new Vector3(0,4.68f,7.63f),6,.35f);break;
                    case "FuelShopDetailed":type.Add(go.transform,"Coffee shop sign","SHOP & COFFEE",new Vector3(0,3.95f,5.72f),12,.45f);ShopGlazing(go.transform);break;
                    case "AirWaterStation":type.Add(go.transform,"Air water fascia","AIR & WATER",new Vector3(0,3.2f,-.18f),3.2f,.36f);break;
                    case "FuelPylonDetailed":type.Add(go.transform,"Fuel fascia","FUEL",new Vector3(0,3.65f,.29f),2,.5f);break;
                    case "CoffeeMenuBoard":type.Add(go.transform,"Coffee menu","COFFEE",new Vector3(0,1.25f,.16f),1,.18f);break;
                    case "MarketPriceBoard":type.Add(go.transform,"Market price","FRESH 2.49",new Vector3(0,1.12f,.075f),1.15f,.17f);break;
                }
                string path=Root+"/Prefabs/"+recipe.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);models.Add(recipe.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally{Object.DestroyImmediate(go);}
        }
        Import("DetailedTractor","Assets/Models/Car/Models/FBX format/tractor.fbx",4.5f,true);
        const string pack="Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/";
        foreach(var entry in new[]{("DetailedWheelbarrow","Prop_Wheelbarrow",2.2f),("SeedSack","Prop_FoodSack_01",.8f),("WoodBarrel","Prop_Berrel_02_A",1.0f),("WateringCan","Prop_WateringCan_01",.6f),("HandSpade","Prop_BigFarmingTool_01",1.6f),("HandRake","Prop_BigFarmingTool_03",1.7f),("HayBaleDetailed","Prop_Haystack_01",1.8f),("BucketDetailed","Prop_Bucket_01",.5f),("OrchardBasket","Prop_WoodenCrates_01",.9f)})Import(entry.Item1,pack+entry.Item2+".prefab",entry.Item3,false);
        AssetDatabase.SaveAssets();
    }
    private void ShopGlazing(Transform parent)
    {
        var panes=new TownMeshBuilder();foreach(float x in new[]{-4f,4f,0})
        {
            float w=x==0?2.4f:5.15f;float h=x==0?3.4f:3.1f,y=x==0?1.8f:1.9f,z=5.55f;
            var a=new Vector3(x-w/2,y-h/2,z);var b=new Vector3(x+w/2,y-h/2,z);var c=new Vector3(x+w/2,y+h/2,z);var d=new Vector3(x-w/2,y+h/2,z);panes.Quad(a,b,c,d,TownColor.White);panes.Quad(d,c,b,a,TownColor.White);
        }
        MeshPart(parent,"FuelShopGlazing",panes,false,Glass);
    }
    private void Import(string id,string sourcePath,float longest,bool solid)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);if(source==null)throw new InvalidOperationException("Missing licensed project asset "+sourcePath);
        var root=new GameObject(id);var instance=Object.Instantiate(source);instance.name="Authored source geometry";instance.transform.SetParent(root.transform,false);
        try
        {
            var filters=instance.GetComponentsInChildren<MeshFilter>(true);if(filters.Length==0)throw new InvalidOperationException("Source has no static geometry "+id);
            var points=filters.SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            float scale=longest/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);instance.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;instance.transform.localScale=Vector3.one*scale;
            foreach(var c in instance.GetComponentsInChildren<Component>(true).Where(c=>!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)).ToArray())Object.DestroyImmediate(c);
            foreach(var r in instance.GetComponentsInChildren<MeshRenderer>(true)){if(r.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.name.StartsWith("Universal Render Pipeline/")))throw new InvalidOperationException("Source material requires explicit conversion: "+id);}
            if(solid)Box(root.transform,"Parked tractor envelope",new Vector3(0,bounds.size.y*scale/2,0),bounds.size*scale);
            string path=Root+"/Prefabs/"+id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);models.Add(id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        finally{Object.DestroyImmediate(root);}
    }
    internal Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=TownRuralPolishPlacement.Floor,float scale=1)
    {
        if(!models.TryGetValue(id,out var prefab))prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownGatewayPlacement.Root+"/Prefabs/"+id+".prefab");
        if(prefab==null)throw new InvalidOperationException("Unknown rural model "+id);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true))if(AssetDatabase.GetAssetPath(r.sharedMaterial)==TownGatewayPlacement.Root+"/Materials/Surface.mat")r.sharedMaterial=Surface;
        return go.transform;
    }
    internal Transform MeshPart(Transform parent,string id,TownMeshBuilder builder,bool physical=false,Material material=null)
    {
        var mesh=builder.Build(id);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();string path="Meshes/"+id.Replace(" ","")+".asset";Save(mesh,path);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+path);
        var node=Child(parent,id,Vector3.zero);node.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material??Surface;if(material==Glass)node.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=saved;return node;
    }
    internal void PaintedPart(Transform parent,string id,TownMeshBuilder builder)=>MeshPart(parent,id,builder);
    internal void Sign(Transform parent,string id,string text,Vector3 p,float width,float height)=>type.Add(parent,id,text,p,width,height);
    internal static void Save(Object asset,string relative)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+relative);
    internal static Transform Child(Transform parent,string id,Vector3 p)=>TownBakeryPlacement.Child(parent,id,p);
    internal static void Box(Transform parent,string id,Vector3 center,Vector3 size){var t=Child(parent,id,Vector3.zero);var box=t.gameObject.AddComponent<BoxCollider>();box.center=center;box.size=size;}
    internal void Combine(Transform site,string prefix)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>(true);var entries=filters.SelectMany(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Select(i=>new{f,sub=i,material=f.GetComponent<MeshRenderer>().sharedMaterials[i]}));int number=0;
        foreach(var group in entries.GroupBy(e=>e.material))
        {
            var mesh=new Mesh{name=prefix+" visual batch "+number,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(e=>new CombineInstance{mesh=e.f.sharedMesh,subMeshIndex=e.sub,transform=site.worldToLocalMatrix*e.f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();string path="Meshes/"+prefix+"Batch"+number+".asset";Save(mesh,path);
            var node=Child(site,prefix+" visual batch "+number++,Vector3.zero);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            if(group.Key==Glass||group.Key.shader.name.Contains("Lettering"))node.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
}
