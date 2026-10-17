using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Installs the approved Building Supplies complex in the marked empty block, preserving the original Construction checkpoint.</summary>
internal sealed class TownConstructionPlacement
{
    public const string Root="Assets/Art/TownConstruction",SiteName="Building supplies depot";
    public static readonly Rect ClearArea=new(-212,149,88,40),Site=new(-210,151,80,34);
    public static readonly Rect[] Permissions={new(-214,148,90,42),new(-159,138,8,13.2f)};
    public static readonly Vector3 ShopPosition=new(-182,.09f,168),WarehousePosition=new(-155,.09f,178),Bay=new(-155,.085f,168.0f),Staging=new(-155,0,155),Entrance=new(-155,0,151),Street=new(-155,0,144);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly List<string> audit=new();
    private Material surface,glass;
    public TownConstructionPlacement(GameObject town){this.town=town;}
    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Building supplies already installed; refusing duplication.");
        if(Site.Overlaps(town.GetComponent<TownWorldPlaces>().layout.reservedShopPlot))throw new InvalidOperationException("Site overlaps player reserve.");
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();ClearTrees();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        var shop=Place(site,"SupplyShop",ShopPosition);
        Place(site,"SupplyWarehouse",WarehousePosition);
        Place(site,"TimberCanopy",new Vector3(-202,.09f,177.5f),0);
        var interior=TownBakeryPlacement.Child(shop,"Supply shop displays",Vector3.zero);
        foreach(float x in new[]{-7.5f,7.5f})Local(interior,"PaintRack",new Vector3(x,.305f,5.8f),0);
        foreach(float x in new[]{-5.5f,5.5f})Local(interior,"ToolWall",new Vector3(x,.305f,-7.2f),0);
        Local(interior,"CheckoutCounter",new Vector3(0,.305f,-4.8f),0);
        foreach(float z in new[]{164.2f,167.4f})Place(site,"PaintRack",new Vector3(-168.7f,.09f,z),90);
        foreach(float z in new[]{173.7f,177.5f,181.3f})Place(site,"LumberStack",new Vector3(-202,.09f,z),0);
        Place(site,"SheetRack",new Vector3(-195.6f,.09f,180.0f),90);
        foreach(float x in new[]{-203f,-200f})foreach(float z in new[]{155.8f,160.1f,164.4f})
            Place(site,z==160.1f?"BlockPallet":z==164.4f?"CementPallet":"BrickPallet",new Vector3(x,.09f,z));
        foreach(float x in new[]{-188f,-183f,-178f})foreach(float z in new[]{179.2f,181.6f})
            Place(site,x==-183?"CementPallet":"BlockPallet",new Vector3(x,.09f,z));
        string[] stock={"BrickPallet","BlockPallet","CementPallet","BrickPallet","BlockPallet","CementPallet","BrickPallet"};
        foreach(float x in new[]{-134f,-136.7f})for(int i=0;i<stock.Length;i++)
            Place(site,stock[i],new Vector3(x,.09f,155.7f+i*4.3f),90);
        Place(site,"CementPallet",new Vector3(-139,.09f,158.5f));
        Place(site,"SheetRack",new Vector3(-139,.09f,163.5f),90);
        Place(site,"PipeCradle",new Vector3(-164.5f,.09f,168.4f),90);
        Place(site,"PalletJack",new Vector3(-164.4f,.09f,161.5f),180);
        Place(site,"HandTrolley",new Vector3(-144.0f,.09f,168.9f),270);
        foreach(float x in new[]{-158.25f,-151.75f})Place(site,"SafetyBollard",new Vector3(x,.09f,172.45f));
        foreach(float x in new[]{-159.7f,-150.3f})Place(site,"SafetyBollard",new Vector3(x,.09f,152.2f));
        foreach(float x in new[]{-162.7f,-147.3f})Place(site,"WallLamp",new Vector3(x,3.35f,172.80f));
        foreach(float x in new[]{-191.5f,-186.5f,-177.0f})Place(site,"EquipmentPlanter",new Vector3(x,.09f,159.4f),0);
        Place(site,"EquipmentPlanter",new Vector3(-207,.09f,162.7f),90);
        Place(site,"EquipmentPlanter",new Vector3(-132,.09f,174.7f),90);
        foreach(var p in new[]{new Vector2(-207,154),new Vector2(-131.8f,155),new Vector2(-131.8f,181.5f)})Place(site,"SiteTree",new Vector3(p.x,.09f,p.y));
        foreach(var p in new[]{new Vector2(-196,153),new Vector2(-143,154),new Vector2(-191,183)})Place(site,"SiteLamp",new Vector3(p.x,.09f,p.y));
        Place(site,"BlueCustomerCar",new Vector3(-191.2f,.09f,155.1f));
        Place(site,"GreenCustomerCar",new Vector3(-186.4f,.09f,155.1f));
        Fence(site,new Vector3(-209.5f,.09f,151.4f),new Vector3(-209.5f,.09f,184.5f));
        Fence(site,new Vector3(-130.5f,.09f,151.4f),new Vector3(-130.5f,.09f,184.5f));
        Fence(site,new Vector3(-209.5f,.09f,184.5f),new Vector3(-130.5f,.09f,184.5f));
        Fence(site,new Vector3(-207.5f,.09f,151.4f),new Vector3(-196,.09f,151.4f));
        Fence(site,new Vector3(-176,.09f,151.4f),new Vector3(-160,.09f,151.4f));
        Fence(site,new Vector3(-150,.09f,151.4f),new Vector3(-130.5f,.09f,151.4f));
        Paving(site);Combine(site);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Construction);
        if(checkpoint.kind!=CheckpointKind.Depot||!checkpoint.requiresRearDocking)throw new InvalidOperationException("Original Construction contract unexpectedly changed.");
        var oldCourt=town.GetComponentsInChildren<SupplierLoadingCourt>(true).Single(c=>c.checkpoint==checkpoint);Object.DestroyImmediate(oldCourt);
        checkpoint.transform.SetParent(site,false);checkpoint.transform.SetPositionAndRotation(Bay,Quaternion.Euler(0,180,0));checkpoint.radius=2.2f;checkpoint.displayName="Building Supplies";
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=checkpoint;court.bounds=new Rect(-165,151.4f,21,21.6f);
        court.streetAccess=Street;court.entrance=Entrance;court.staging=Staging;court.designVehicleLength=6.8f;
        audit.Add("Approved Building Supplies concept installed west of Equipment. Shop 24 x 16 m; warehouse 22 x 10 m; timber canopy, categorized materials at perimeter, three customer spaces and clear reverse-loading lane.");
        audit.Add("Same Construction checkpoint moved to "+Bay+", south-facing and stopped rear docking required. Original western Construction scenery retained outside scope; no second working supplier.");
        File.WriteAllLines(TownConstructionBatch.Report+"/placement.txt",audit);AssetDatabase.SaveAssets();
    }
    private static bool In(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private void ClearTrees()
    {
        var trees=town.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TreeTall"&&In(ClearArea,t.position)).ToArray();
        if(trees.Length!=6)throw new InvalidOperationException("Expected exactly six existing parcel trees, found "+trees.Length+". Inspect before clearing.");
        var footprints=trees.Select(t=>new Rect(t.position.x-2.8f,t.position.z-2.8f,5.6f,5.6f)).ToArray();
        int removed=0,copies=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var copy=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,(a,b,c)=>footprints.Any(r=>In(r,a)&&In(r,b)&&In(r,c)),out int edits);
            if(copy==null)continue;removed+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);continue;}
            string path=Root+"/Meshes/TreeTrim"+(copies++)+".asset";Save(copy,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        if(removed<100)throw new InvalidOperationException("Native parcel greenery was not identified.");
        foreach(var tree in trees)Object.DestroyImmediate(tree.gameObject);
        audit.Add("Six existing parcel trees and their small bases replaced with planned planting: "+removed+" local triangles removed, "+copies+" scoped mesh copies. No broad ground, building or street clearing.");
    }
    private void Catalogue()
    {
        var texture=new Texture2D(32,1,TextureFormat.RGBA32,false){name="ConstructionPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.SetPixels32(TownConstructionModels.Atlas());texture.Apply();Save(texture,Root+"/Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="ConstructionSurface",enableInstancing=true};material.SetFloat("_PaletteIndex",-1);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));Save(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var pane=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ConstructionGlass",renderQueue=3000,enableInstancing=true};
        pane.SetFloat("_Surface",1);pane.SetFloat("_Blend",0);pane.SetFloat("_ZWrite",0);pane.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);pane.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);pane.SetFloat("_Smoothness",.45f);
        pane.SetColor("_BaseColor",new Color(.64f,.82f,.85f,.13f));pane.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");pane.SetOverrideTag("RenderType","Transparent");pane.SetShaderPassEnabled("ShadowCaster",false);Save(pane,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var letters=new TownStaticTypography(Root,"BUILDING SUPPLIES PICKUP");
        foreach(var model in TownConstructionModels.Create())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids){var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                if(model.Id=="SupplyShop")letters.Add(prototype.transform,"SupplySign","BUILDING SUPPLIES",new Vector3(0,3.36f,8.34f),21.5f,.50f);
                if(model.Id=="SupplyWarehouse")letters.Add(prototype.transform,"PickupSign","PICKUP",new Vector3(0,4.82f,5.08f),4.6f,.49f);
                if(model.Id=="SupplyShop")Glazing(prototype.transform,model.Id);
                prefabs.Add(model.Id,PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab"));audit.Add(model.Id+": "+triangles+" opaque triangles; "+model.Draft.Solids.Count+" collision parts.");
            }
            finally{Object.DestroyImmediate(prototype);}
        }
        AssetDatabase.SaveAssets();
    }
    private void Glazing(Transform root,string id)
    {
        var b=new TownMeshBuilder();
        void Face(Vector3 p,float w,float h,float yaw=0)
        {
            var q=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float y)=>p+q*new Vector3(x,y,0);
            b.Quad(P(-w/2,-h/2),P(w/2,-h/2),P(w/2,h/2),P(-w/2,h/2),TownColor.White);
        }
        foreach(float x in new[]{-6.65f,6.65f})Face(new Vector3(x,1.72f,8.12f),9.5f,2.4f);
        Face(new Vector3(0,1.64f,8.12f),2.45f,2.56f);
        foreach(float z in new[]{-5f,0,5f})Face(new Vector3(-12.12f,1.72f,z),4.35f,2.4f,270);
        string path=Root+"/Meshes/"+id+"Glass.asset";Save(b.Build(id+" glass"),path);
        var node=TownBakeryPlacement.Child(root,"Glass",Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private Transform Place(Transform parent,string id,Vector3 p,float yaw=180)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        item.transform.SetParent(parent,false);item.transform.localPosition=p;item.transform.localRotation=Quaternion.Euler(0,yaw,0);return item.transform;
    }
    private Transform Local(Transform parent,string id,Vector3 p,float yaw)=>Place(parent,id,p,yaw);
    private void Fence(Transform parent,Vector3 a,Vector3 b)
    {
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);float length=Vector3.Distance(a,b)/count;
        for(int i=0;i<count;i++){var item=Place(parent,"EquipmentFence",Vector3.Lerp(a,b,(i+.5f)/count),Mathf.Abs(a.x-b.x)<.01f?90:0);item.localScale=new Vector3(length/3,1,1);}
        for(int i=0;i<=count;i+=3)Place(parent,"BrickPier",Vector3.Lerp(a,b,(float)i/count));
    }
    private void Paving(Transform site)
    {
        var b=new TownMeshBuilder();b.Ground(Site.center.x,Site.center.y,Site.width,Site.height,.075f,TownColor.Pavement);
        b.Ground(-169.5f,170.5f,78,26,.085f,TownColor.Asphalt);
        b.Ground(-155,154.3f,8,6.6f,.085f,TownColor.Asphalt);
        b.Ground(-186.4f,155.1f,14.4f,6.2f,.085f,TownColor.Asphalt);
        b.Ground(-207.0f,155.2f,4.0f,6.0f,.088f,TownColor.Grass);
        foreach(float x in new[]{-193.6f,-188.8f,-184.0f,-179.2f})b.Ground(x,155.1f,.055f,6.1f,.095f,TownColor.White);
        foreach(float x in new[]{-158.1f,-151.9f})b.Ground(x,168,.065f,7.8f,.095f,TownColor.Yellow);
        foreach(float z in new[]{164.1f,171.9f})b.Ground(-155,z,6.2f,.065f,.095f,TownColor.Yellow);
        // The old 14 cm sidewalk stays intact below a continuous low ramp; no hidden kerb sticks through the drive.
        Apron(b,new Rect(-159,138,8,10),.015f,.15f);
        Apron(b,new Rect(-159,148,8,2),.15f,.15f);
        Apron(b,new Rect(-159,150,8,1.1f),.15f,.086f);
        string path=Root+"/Meshes/SitePaving.asset";Save(b.Build("Construction paving"),path);var node=TownBakeryPlacement.Child(site,"Paving",Vector3.zero);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;node.gameObject.isStatic=true;
        Floor(site,"Site floor",Site,.075f,.075f);
        Floor(site,"Yard floor",new Rect(-208.5f,157.5f,78,26),.085f,.085f);
        // Even centimetre-high mesh edges can catch the arcade pickup. The whole driving lane shares one level.
        Floor(site,"Continuous pickup lane",new Rect(-159,151,8,22.5f),.085f,.085f);
        Floor(site,"Parking floor",new Rect(-193.6f,152,14.4f,6.2f),.085f,.085f);
        Floor(site,"Street ramp",new Rect(-159,138,8,10),.015f,.15f);
        Floor(site,"Sidewalk crossing",new Rect(-159,148,8,2),.15f,.15f);
        Floor(site,"Yard ramp",new Rect(-159,150,8,1.1f),.15f,.086f);
    }
    private static void Apron(TownMeshBuilder b,Rect r,float south,float north)
    {
        b.Quad(new Vector3(r.xMin,south,r.yMin),new Vector3(r.xMin,north,r.yMax),new Vector3(r.xMax,north,r.yMax),new Vector3(r.xMax,south,r.yMin),TownColor.Asphalt);
    }
    private static void Floor(Transform site,string name,Rect r,float south,float north)
    {
        var b=new TownMeshBuilder();b.Quad(new Vector3(r.xMin,south,r.yMin),new Vector3(r.xMin,north,r.yMax),new Vector3(r.xMax,north,r.yMax),new Vector3(r.xMax,south,r.yMin),TownColor.Asphalt);
        string path=Root+"/Meshes/"+name.Replace(" ","")+".asset";Save(b.Build(name),path);TownBakeryPlacement.Child(site,name,Vector3.zero).gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>();int index=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Construction visual group "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangles=mesh.triangles.Length/3;string path=Root+"/Meshes/Complex"+index+".asset";Save(mesh,path);
            var node=TownBakeryPlacement.Child(site,"Construction visual group "+index,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key==glass||group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering")renderer.shadowCastingMode=ShadowCastingMode.Off;
            audit.Add("Visual batch "+index+": "+triangles+" triangles.");index++;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    private static void Save(Object value,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));TownBakeryPlacement.SaveAsset(value,path);}
}
