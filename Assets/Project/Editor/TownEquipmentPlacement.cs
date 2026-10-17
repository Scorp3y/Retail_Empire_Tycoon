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

/// <summary>Replaces the marked postal-square scenery and moves the existing Equipment pickup contract.</summary>
internal sealed class TownEquipmentPlacement
{
    public const string Root="Assets/Art/TownEquipment",SiteName="Retail equipment depot";
    public static readonly Rect ClearArea=new(-114,150,84,36),Site=new(-112,151,80,34);
    public static readonly Rect[] Permissions={new(-116,148,88,40),new(-68,138,8,13.2f)};
    public static readonly Vector3 ShopPosition=new(-94,.09f,169),WarehousePosition=new(-64,.09f,178),Bay=new(-64,.085f,168.8f),Staging=new(-64,0,155),Entrance=new(-64,0,151),Street=new(-64,0,144);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly List<string> audit=new();
    private Material surface,glass;
    public TownEquipmentPlacement(GameObject town){this.town=town;}
    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Equipment depot already installed; refusing duplicate placement.");
        if(Site.Overlaps(town.GetComponent<TownWorldPlaces>().layout.reservedShopPlot))throw new InvalidOperationException("Equipment depot overlaps player expansion land.");
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        ClearOldSquare();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        var shop=Place(site,"EquipmentShop",ShopPosition);
        Place(site,"EquipmentWarehouse",WarehousePosition);Place(site,"EquipmentCanopy",new Vector3(-77.8f,.09f,166.15f),0);
        var interior=TownBakeryPlacement.Child(shop,"Equipment display interior",Vector3.zero);
        foreach(float x in new[]{-10f,-8.25f,8.25f,10f})Local(interior,"DisplayFridge",new Vector3(x,.415f,4.3f),0);
        foreach(float x in new[]{-7.8f,7.8f})Local(interior,"RefrigeratedCounter",new Vector3(x,.415f,5.6f),0);
        foreach(float x in new[]{-6f,6f})Local(interior,"RetailShelf",new Vector3(x,.415f,-3.6f),0);
        Local(interior,"CheckoutCounter",new Vector3(0,.415f,-3.7f),0);
        // Outdoor exhibits are on the wall side of the canopy, outside the six-metre loading corridor.
        foreach(float z in new[]{162.2f,164.0f,165.8f})Place(site,"DisplayFridge",new Vector3(-80.2f,.09f,z),90);
        Place(site,"RefrigeratedCounter",new Vector3(-80.2f,.09f,168.75f),90);
        Place(site,"CheckoutCounter",new Vector3(-80.2f,.09f,171.55f),90);
        Place(site,"RetailShelf",new Vector3(-76.2f,.09f,170.7f),270);
        Place(site,"LoadedDolly",new Vector3(-72.5f,.09f,165.3f));
        foreach(float z in new[]{157.7f,164.4f,171.1f})Place(site,"PalletRack",new Vector3(-47,.09f,z),270);
        foreach(var p in new[]{new Vector2(-50.9f,179.5f),new Vector2(-49.1f,179.5f),new Vector2(-49.1f,177.8f),new Vector2(-47,177.8f)})Place(site,"WrappedPallet",new Vector3(p.x,.09f,p.y));
        Place(site,"PalletStack",new Vector3(-47,.09f,181.1f));
        foreach(float x in new[]{-67.05f,-60.95f})Place(site,"SafetyBollard",new Vector3(x,.09f,172.45f));
        foreach(float x in new[]{-68.35f,-59.65f})Place(site,"SafetyBollard",new Vector3(x,.09f,151.6f));
        foreach(float x in new[]{-70.8f,-58.2f})Place(site,"WallLamp",new Vector3(x,3.5f,172.80f));
        foreach(float x in new[]{-105.5f,-101.5f,-86.5f,-83.5f})Place(site,"EquipmentPlanter",new Vector3(x,.09f,160.15f),0);
        foreach(float z in new[]{157f,166f,177f})Place(site,"EquipmentPlanter",new Vector3(-110.8f,.09f,z),90);
        foreach(float z in new[]{155f,166f,181f})Place(site,"EquipmentPlanter",new Vector3(-33.8f,.09f,z),90);
        foreach(var p in new[]{new Vector2(-110,181),new Vector2(-38,157),new Vector2(-38,172),new Vector2(-38,181)})Place(site,"SiteTree",new Vector3(p.x,.09f,p.y));
        foreach(var p in new[]{new Vector2(-110.2f,153),new Vector2(-43,154),new Vector2(-43,181)})Place(site,"SiteLamp",new Vector3(p.x,.09f,p.y));
        Place(site,"BlueCustomerCar",new Vector3(-105,.09f,155.2f));Place(site,"GreenCustomerCar",new Vector3(-100.2f,.09f,155.2f));
        Fence(site,new Vector3(-111.6f,.09f,151.4f),new Vector3(-111.6f,.09f,184.5f));
        Fence(site,new Vector3(-32.5f,.09f,151.4f),new Vector3(-32.5f,.09f,184.5f));
        Fence(site,new Vector3(-111.6f,.09f,184.5f),new Vector3(-32.5f,.09f,184.5f));
        Fence(site,new Vector3(-88,.09f,151.4f),new Vector3(-69,.09f,151.4f));
        Fence(site,new Vector3(-59,.09f,151.4f),new Vector3(-32.5f,.09f,151.4f));
        Paving(site);Combine(site);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Equipment);
        if(checkpoint.kind!=CheckpointKind.Depot||!checkpoint.requiresRearDocking)throw new InvalidOperationException("Existing equipment collection contract changed unexpectedly.");
        var oldCourt=town.GetComponentsInChildren<SupplierLoadingCourt>(true).Single(c=>c.checkpoint==checkpoint);Object.DestroyImmediate(oldCourt);
        checkpoint.transform.SetParent(site,false);checkpoint.transform.SetPositionAndRotation(Bay,Quaternion.Euler(0,180,0));checkpoint.radius=2.2f;checkpoint.displayName="Retail Equipment";
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=checkpoint;court.bounds=new Rect(-74,151.4f,22,22);
        court.streetAccess=Street;court.entrance=Entrance;court.staging=Staging;court.designVehicleLength=6.8f;
        audit.Add("Marked postal square replaced: X-114..-30, Z150..186. New 26 x 14 m brick shop faces south; 24 x 10 m warehouse behind/right, wall-side canopy and open pickup apron. East strip is a planted buffer beside the existing park.");
        audit.Add("Same existing Equipment checkpoint moved to "+Bay+"; stopped rear docking retained, heading south (180 degrees). Exactly six goods courts remain. Former western equipment buildings retained as scenery, not a second working supplier.");
        File.WriteAllLines(TownEquipmentBatch.Report+"/placement.txt",audit);AssetDatabase.SaveAssets();
    }
    private static bool In(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private void ClearOldSquare()
    {
        var targets=town.GetComponentsInChildren<Transform>(true).Where(t=>(t.name=="PostOffice"||t.name=="CornerCafe")&&In(ClearArea,t.position)).ToArray();
        if(targets.Length!=2)throw new InvalidOperationException("Exactly the marked post office and cafe must be identified before clearing.");
        int copies=0,removed=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var copy=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,(a,b,c)=>In(ClearArea,a)&&In(ClearArea,b)&&In(ClearArea,c),out int edits);
            if(copy==null)continue;removed+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);continue;}
            string path=Root+"/Meshes/LegacyTrim"+(copies++)+".asset";Save(copy,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        if(removed<1000)throw new InvalidOperationException("Marked square geometry was not found; refusing speculative replacement.");
        int collisions=0;
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {
            var b=TownResidentialPlacement.ColliderBounds(collider);
            if(!In(ClearArea,b.min)||!In(ClearArea,b.max))continue;Object.DestroyImmediate(collider);collisions++;
        }
        foreach(var node in town.GetComponentsInChildren<Transform>(true).Where(t=>t!=town.transform&&t.childCount==0&&In(ClearArea,t.position)&&t.GetComponent<MeshFilter>()==null).ToArray())
            Object.DestroyImmediate(node.gameObject);
        audit.Add("Removed marked post office, cafe, terrace, clock, benches, noticeboard, old planting and low ground details: "+removed+" complete local triangles and "+collisions+" colliders. Original shared mesh assets preserved in "+copies+" scoped copies; streets and neighbouring park not cleared.");
    }
    private void Catalogue()
    {
        var texture=new Texture2D(32,1,TextureFormat.RGBA32,false){name="EquipmentPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.SetPixels32(TownEquipmentModels.Atlas());texture.Apply();Save(texture,Root+"/Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="EquipmentSurface",enableInstancing=true};material.SetFloat("_PaletteIndex",-1);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));Save(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var pane=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="EquipmentGlass",renderQueue=3000,enableInstancing=true};
        pane.SetFloat("_Surface",1);pane.SetFloat("_Blend",0);pane.SetFloat("_ZWrite",0);pane.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);pane.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);pane.SetFloat("_Smoothness",.45f);
        pane.SetColor("_BaseColor",new Color(.64f,.82f,.85f,.13f));pane.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");pane.SetOverrideTag("RenderType","Transparent");pane.SetShaderPassEnabled("ShadowCaster",false);Save(pane,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var letters=new TownStaticTypography(Root,"EQUIPMENT PICKUP");
        foreach(var model in TownEquipmentModels.Create())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids){var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                if(model.Id=="EquipmentShop")letters.Add(prototype.transform,"EquipmentSign","EQUIPMENT",new Vector3(0,3.71f,7.42f),23.5f,.54f);
                if(model.Id=="EquipmentWarehouse")letters.Add(prototype.transform,"PickupSign","PICKUP",new Vector3(0,4.77f,5.07f),4.6f,.49f);
                if(model.Id=="EquipmentShop"||model.Id=="DisplayFridge"||model.Id=="RefrigeratedCounter")Glazing(prototype.transform,model.Id);
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
        if(id=="EquipmentShop")
        {
            foreach(float x in new[]{-7.12f,7.12f})Face(new Vector3(x,1.88f,7.19f),10.55f,2.8f);
            Face(new Vector3(0,1.82f,7.20f),2.5f,2.85f);
            foreach(float z in new[]{-4.5f,0,4.5f})Face(new Vector3(-13.18f,1.88f,z),3.95f,2.8f,270);
        }
        else if(id=="DisplayFridge")Face(new Vector3(0,1.25f,.425f),1.03f,2.09f);
        else
        {
            Face(new Vector3(0,.97f,.505f),2.66f,.37f);b.Quad(new Vector3(-1.3f,1.14f,.48f),new Vector3(1.3f,1.14f,.48f),new Vector3(1.3f,1.14f,-.45f),new Vector3(-1.3f,1.14f,-.45f),TownColor.White);
        }
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
        b.Ground(-62,162.25f,22,22.5f,.085f,TownColor.Asphalt);b.Ground(-100,155.1f,20,6.2f,.085f,TownColor.Asphalt);
        b.Ground(-37.7f,168,10.6f,30,.08f,TownColor.Grass);
        foreach(float x in new[]{-107.4f,-102.6f,-97.8f,-93})b.Ground(x,155.1f,.055f,6.1f,.094f,TownColor.White);
        foreach(float x in new[]{-67.1f,-60.9f})b.Ground(x,168.8f,.065f,7.8f,.095f,TownColor.Yellow);
        foreach(float z in new[]{164.9f,172.7f})b.Ground(-64,z,6.2f,.065f,.095f,TownColor.Yellow);
        b.Quad(new Vector3(-68,.015f,138),new Vector3(-68,.086f,151.1f),new Vector3(-60,.086f,151.1f),new Vector3(-60,.015f,138),TownColor.Asphalt);
        string path=Root+"/Meshes/SitePaving.asset";Save(b.Build("Equipment paving"),path);var node=TownBakeryPlacement.Child(site,"Paving",Vector3.zero);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;node.gameObject.isStatic=true;
        Floor(site,"Site floor",Site,.075f,.075f);Floor(site,"Drive floor",new Rect(-73,151,22,22.5f),.085f,.085f);Floor(site,"Parking floor",new Rect(-110,152,20,6.2f),.085f,.085f);
        Floor(site,"Street apron",new Rect(-68,138,8,13.1f),.015f,.086f);
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
            var mesh=new Mesh{name="Equipment visual group "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangles=mesh.triangles.Length/3;string path=Root+"/Meshes/Complex"+index+".asset";Save(mesh,path);
            var node=TownBakeryPlacement.Child(site,"Equipment visual group "+index,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key==glass||group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering")renderer.shadowCastingMode=ShadowCastingMode.Off;
            audit.Add("Visual batch "+index+": "+triangles+" triangles.");index++;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    private static void Save(Object value,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));TownBakeryPlacement.SaveAsset(value,path);}
}
