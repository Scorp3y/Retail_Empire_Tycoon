using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Additive background neighbourhood. It owns no gameplay land, entrances or old city assets.</summary>
internal sealed class TownShopPerimeterPlacement
{
    internal const string Root="Assets/Art/TownShopPerimeter",SiteName="Shop perimeter neighbourhood";
    internal const float Floor=.14f;
    internal static readonly Rect[] Permissions={
        Rect.MinMaxRect(-114,-137.5f,-71,-9),
        Rect.MinMaxRect(-71,-137.5f,-33,-124.1f),
        Rect.MinMaxRect(-16,-137.5f,93,-124.1f),
        Rect.MinMaxRect(-70,-17.7f,93,-8.5f),
        Rect.MinMaxRect(-21,-17.7f,-10,6),
        Rect.MinMaxRect(26,-17.7f,37,6),
        Rect.MinMaxRect(76,-17.7f,93,6)
    };
    internal static readonly Rect[] ShopExclusions={
        Rect.MinMaxRect(-70.0016f,-124.003204f,86.02962f,-17.981994f),
        new Rect(-28,-144,8,28),new Rect(-32,-124,12,8)
    };
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> models=new();
    private readonly List<string> audit=new();
    private readonly List<Rect> paving=new();
    private Material surface;
    private int patchIndex;
    internal TownShopPerimeterPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Perimeter already exists. Regenerate from the recorded original.");
        Catalogue();var site=Child(town.transform,SiteName);
        WestNorth(Child(site,"Residential north"));WestMiddle(Child(site,"Residential middle"));WestSouth(Child(site,"Residential south"));
        SouthShops(Child(site,"Neighbourhood high street"));RearGardens(Child(site,"Existing homes garden walk"));
        foreach(Transform group in site)Combine(group);
        File.WriteAllLines(TownShopPerimeterBatch.Report+"/placement.txt",audit);
    }
    private void WestNorth(Transform g)
    {
        Paving(g,-112.5f,-29,2,38);Paving(g,-88.5f,-28,2.5f,36);Paving(g,-100,-44,27,2.5f);Paving(g,-93,-13,38,2.5f);
        Pad(g,-101,-28,13,21);Place(g,"BrickWalkup",-101,-28,270);
        Paving(g,-109.5f,-28,6,2.2f);
        Pad(g,-81,-28,10,13);Place(g,"TownhouseA",-81,-28,180);
        Paving(g,-85.8f,-35.8f,7,2.2f);Paving(g,-83.15f,-34.5f,2.2f,3.5f);
        Place(g,"Mailboxes",-109,-35,270);Place(g,"CycleShelter",-80,-40,180,.14f,.85f);
        Place(g,"TimberBench",-80,-15,180);Place(g,"StreetBin",-83,-15);
        Garden(g,-76,-16,1);Garden(g,-93,-16,.85f);Garden(g,-76,-36,.85f);
        Garden(g,-78,-46,1);Garden(g,-106,-42,.75f);
        Place(g,"GardenFence",-81,-20);Place(g,"Clothesline",-80,-18,0,0);
        foreach(float z in new[]{-18f,-40f})Place(g,"QuarterLamp",-110,z,90);
        Plant(g,-74.5f,-23,90);Plant(g,-74.5f,-32,90);Plant(g,-95,-12);
    }
    private void WestMiddle(Transform g)
    {
        Paving(g,-112.5f,-65,2,34);Paving(g,-88.5f,-63,2.5f,34);Paving(g,-101,-77,25,2.5f);
        Pad(g,-101,-60,13,18);Place(g,"StuccoHouse",-101,-60,270);
        Paving(g,-109.5f,-58.325f,6,2.2f);
        Pad(g,-81,-76,10,13);Place(g,"TownhouseB",-81,-76);
        Paving(g,-83.7f,-69.6f,10.5f,2.2f);Paving(g,-78.85f,-70,2.2f,2.2f);
        Patch(g,"Garden terrace",-79.5f,-57,11,10,TownColor.Pavement);
        Place(g,"Pergola",-79.5f,-57,90);Place(g,"CafeTable",-79.5f,-57,90);
        Place(g,"TimberBench",-81,-63,180);Place(g,"GardenFence",-75,-63);
        Place(g,"Mailboxes",-109,-64,270);Place(g,"StreetBin",-109,-66);
        Place(g,"FlowerStrip",-84,-50);Place(g,"FlowerStrip",-75,-50);
        Garden(g,-94,-48,.95f);Garden(g,-77,-67,.8f);Garden(g,-95,-73,.8f);
        Plant(g,-74.5f,-55,90);Plant(g,-74.5f,-60,90);Plant(g,-84,-67);
        foreach(float z in new[]{-52f,-73f})Place(g,"QuarterLamp",-110,z,90);
    }
    private void WestSouth(Transform g)
    {
        Paving(g,-112.5f,-109.25f,2,54.5f);Paving(g,-88.5f,-109,2.5f,57);
        Paving(g,-101,-107,25,2.5f);Paving(g,-91.5f,-135.8f,43,2.4f);
        Pad(g,-101,-91,13,21);Place(g,"BrickWalkup",-101,-91,270);
        Paving(g,-109.5f,-91,6,2.2f);
        Pad(g,-97,-121,15,12);Place(g,"CornerDeli",-97,-121,180);
        Patch(g,"Deli terrace",-97,-130.2f,15,5,TownColor.Pavement);
        Paving(g,-97,-127.2f,2.5f,3);Paving(g,-97,-134.2f,2.5f,3.5f);
        foreach(float x in new[]{-101f,-94f}){Place(g,"CafeTable",x,-130.5f);Place(g,"CafeUmbrella",x,-130.5f);}
        Place(g,"CafeMenu",-104,-126,180);Place(g,"BikeRack",-85,-131,90);Place(g,"StreetBin",-86.5f,-133);
        Place(g,"RepairDisplay",-76,-111,90);Place(g,"BinEnclosure",-79,-119,90,.14f,.7f);
        Place(g,"Clothesline",-80,-100,90,0);Place(g,"GardenFence",-76,-103,90);
        Garden(g,-79,-86,1);Garden(g,-78,-95,.85f);Garden(g,-83,-107,.85f);Garden(g,-108,-108,.85f);
        Garden(g,-77,-129,.9f);Garden(g,-60,-131,1.1f);Garden(g,-49,-129,.75f);
        // The old trolley corral at x=-43..-39, z=-135..-133 remains untouched and reachable.
        Paving(g,-52,-136,38,2);Place(g,"TimberBench",-55,-128,180);Place(g,"StreetBin",-52,-127.5f);
        foreach(float z in new[]{-86f,-104f,-130f})Place(g,"QuarterLamp",-110,z,90);
        Plant(g,-74.5f,-89,90);Plant(g,-74.5f,-99,90);Plant(g,-84,-125);
    }
    private void SouthShops(Transform g)
    {
        Paving(g,38.5f,-136,109,2.2f);
        Pad(g,15,-130,13.5f,10);Place(g,"CoffeeRoom",15,-130,180);
        Pad(g,65,-130,13.5f,10);Place(g,"CycleWorkshop",65,-130,180);
        Pad(g,82,-130,7,8);Place(g,"Newsstand",82,-130,180);
        Paving(g,82,-134.5f,2.2f,3);
        Patch(g,"Pocket terrace",34,-130,12,10,TownColor.Pavement);
        Place(g,"Pergola",34,-130);Place(g,"CafeTable",32,-130,90);Place(g,"CafeTable",36,-130,90);
        Pad(g,-9,-131,5,10);Place(g,"ParcelLocker",-9,-129,180);Place(g,"BikeRack",55,-130,90);Place(g,"RepairDisplay",73,-130,90);
        Place(g,"CafeMenu",6.5f,-130,180);Place(g,"StreetBin",24,-133);Place(g,"StreetBin",88,-130);
        Place(g,"TimberBench",43,-128,180);Place(g,"TimberBench",-2.5f,-128,180);
        // Existing city trees at x=0 and x=48, z=-132.5 are retained, not duplicated.
        Garden(g,-13,-132,0.8f);Garden(g,26,-128,.75f);Garden(g,53,-127,.8f);Garden(g,90,-127,.75f);
        foreach(float x in new[]{-7f,24f,45f,76f,90f})Place(g,"QuarterLamp",x,-134.4f,180,.14f,.75f);
        foreach(float x in new[]{-4f,4f,45f,50f,57f,88f})Place(g,"FlowerStrip",x,-125.2f,0,0,.7f);
    }
    private void RearGardens(Transform g)
    {
        Paving(g,11.5f,-10,163,2.2f);
        foreach(float x in new[]{-16f,31f,82f})Paving(g,x,-2.5f,2.2f,17);
        // A low planted strip keeps existing rear facades and the purchasable yard visible.
        foreach(float x in new[]{-65f,-48f,-31f,-5f,13f,42f,60f,89f})Garden(g,x,-14,.72f);
        foreach(float x in new[]{-57f,-39f,4f,22f,51f,70f})Plant(g,x,-15);
        foreach(float x in new[]{-53f,-1f,47f}){Place(g,"TimberBench",x,-12.1f,180);Place(g,"StreetBin",x+2,-12.1f);}
        foreach(float x in new[]{-68f,-20f,28f,78f})Place(g,"QuarterLamp",x,-11.7f,90,.14f,.75f);
    }
    private void Garden(Transform g,float x,float z,float scale)
    {
        Place(g,"TreePlanter",x,z,0,0,scale);Place(g,"UrbanTree",x,z,0,.03f,scale*1.08f);
        audit.Add($"TREE {g.name}: ({x}, {z}), scale {scale}");
    }
    private void Plant(Transform g,float x,float z,float yaw=0)=>Place(g,"MixedPlanting",x,z,yaw,0,.7f);
    private void Pad(Transform g,float x,float z,float width,float depth)=>Patch(g,"Building apron",x,z,width,depth,TownColor.Pavement);
    private void Paving(Transform g,float x,float z,float width,float depth)=>Patch(g,"Public walkway",x,z,width,depth,TownColor.Pavement);
    private void Patch(Transform g,string id,float x,float z,float width,float depth,TownColor color)
    {
        paving.Add(new Rect(x-width/2,z-depth/2,width,depth));
        var m=new TownMeshBuilder();m.Ground(x,z,width,depth,Floor,color);
        // Large subtle paving modules, not a repeated parcel grid.
        if(width>depth)for(float p=x-width/2+3;p<x+width/2;p+=3)m.Ground(p,z,.018f,depth,Floor+.0005f,TownColor.Stone);
        else for(float p=z-depth/2+3;p<z+depth/2;p+=3)m.Ground(x,p,width,.018f,Floor+.0005f,TownColor.Stone);
        string name="Paving"+patchIndex++;var node=Part(g,name,m,surface);
        var physical=new TownMeshBuilder();physical.Ground(x,z,width,depth,Floor,color);var floor=physical.Build(name+" floor");
        string path=Root+"/Meshes/"+name+"Floor.asset";TownBakeryPlacement.SaveAsset(floor,path);
        node.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private void Catalogue()
    {
        foreach(string dir in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Perimeter warm brick palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        palette.SetPixels32(TownShopPerimeterModels.Palette());palette.Apply();TownBakeryPlacement.SaveAsset(palette,Root+"/Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Perimeter warm brick and slate",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));material.SetFloat("_PaletteIndex",-1);
        TownBakeryPlacement.SaveAsset(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var type=new TownStaticTypography(Root,"DELI COFFEE CYCLE WORKS NEWS");
        foreach(var recipe in TownShopPerimeterModels.Create())
        {
            var go=new GameObject(recipe.Id){isStatic=true};
            try
            {
                Part(go.transform,recipe.Id,recipe.Draft.Mesh,surface);
                foreach(var solid in recipe.Draft.Solids){var node=Child(go.transform,solid.Id);var box=node.gameObject.AddComponent<BoxCollider>();box.center=solid.Center;box.size=solid.Size;}
                if(recipe.Id=="CornerDeli")type.Add(go.transform,"DeliLabel","DELI",new Vector3(0,3.15f,3.28f),7,.42f);
                if(recipe.Id=="CoffeeRoom")type.Add(go.transform,"CoffeeLabel","COFFEE",new Vector3(0,3.15f,3.28f),7,.42f);
                if(recipe.Id=="CycleWorkshop")type.Add(go.transform,"CycleLabel","CYCLE WORKS",new Vector3(0,3.15f,3.28f),8,.42f);
                if(recipe.Id=="Newsstand")type.Add(go.transform,"NewsLabel","NEWS",new Vector3(0,2.48f,1.84f),3.5f,.30f);
                string path=Root+"/Prefabs/"+recipe.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);models.Add(recipe.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally{Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=Floor,float scale=1)
    {
        // Furniture on lawns uses the actual terrain level; paving is a separate 14 cm surface.
        if(y==Floor&&!paving.Any(r=>r.Contains(new Vector2(x,z))))y=0;
        if(!models.TryGetValue(id,out var prefab)){prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownForestArt.Root+"/Prefabs/"+id+".prefab");if(prefab==null)throw new InvalidOperationException("Unknown perimeter model "+id);}
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        go.name=id;go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        audit.Add($"MODEL {parent.name}/{id}: ({x}, {y}, {z}), yaw {yaw}, scale {scale}");return go.transform;
    }
    private Transform Part(Transform parent,string id,TownMeshBuilder draft,Material material)
    {
        var mesh=draft.Build(id);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();string path=Root+"/Meshes/"+id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
        var node=Child(parent,id);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return node;
    }
    private void Combine(Transform group)
    {
        var filters=group.GetComponentsInChildren<MeshFilter>();int count=0;
        foreach(var set in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name=group.name+" batch "+count,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(set.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();string path=Root+"/Meshes/"+group.name.Replace(' ','_')+"Batch"+count+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            var node=Child(group,"Visual batch "+count++);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=set.Key;if(set.Key.shader.name.Contains("Lettering"))renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    private static Transform Child(Transform parent,string id){var t=TownBakeryPlacement.Child(parent,id,Vector3.zero);t.gameObject.isStatic=true;return t;}
}
