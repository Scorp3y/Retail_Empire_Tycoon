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

/// <summary>Places the Grocery Depot on the confirmed fourth western parcel and retains the original Wholesale contract.</summary>
internal sealed class TownGroceryPlacement
{
    public const string Root="Assets/Art/TownGrocery",SiteName="Grocery depot";
    public static readonly Rect ClearArea=new(-282,19.2f,60,40.5f),Site=new(-279,17,54,45);
    // Includes the obsolete western yards authorised for removal, but excludes the public road.
    public static readonly Rect LegacyArea=new(-294,-184,72,361);
    private static readonly float[] FormerDriveways={-144,-84,-24,36,96,156};
    public static readonly Rect[] Permissions=new[]{new Rect(-390,-184,168,361),new Rect(-282,14,60,48),new Rect(-225.2f,23.2f,15.2f,8)}
        .Concat(FormerDriveways.Select(z=>new Rect(-226.01f,z-4.01f,12.02f,8.02f))).ToArray();
    public static readonly Vector3 ShopPosition=new(-242,.105f,48),WarehousePosition=new(-267,.105f,33.6f),Bay=new(-255,.10f,27.2f),FreeBay=new(-255,.10f,40),Staging=new(-237,0,27.2f),Entrance=new(-225,0,27.2f),Street=new(-216,0,27.2f);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly HashSet<Vector3> pierPositions=new();
    private readonly List<string> audit=new();
    private Material surface,glass;
    public TownGroceryPlacement(GameObject town){this.town=town;}
    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var previous=town.transform.Find(SiteName);
        bool updating=previous!=null;
        if(updating)
        {
            // Keep the actual gameplay component and every supplier reference, not a replacement checkpoint.
            var retained=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Wholesale);
            retained.transform.SetParent(town.transform,true);
            Object.DestroyImmediate(previous.gameObject);
        }
        if(Site.Overlaps(town.GetComponent<TownWorldPlaces>().layout.reservedShopPlot))throw new InvalidOperationException("Grocery site overlaps player reserve.");
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();if(!updating)ClearGarden();ClearLegacyDistrict();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        var shop=Place(site,"GroceryShop",ShopPosition);
        var warehouse=Place(site,"GroceryWarehouse",WarehousePosition);
        Place(site,"GroceryLink",new Vector3(-255,.105f,47.5f));
        Place(site,"SortingCanopy",new Vector3(-242,.105f,34));
        var inside=TownBakeryPlacement.Child(shop,"Grocery trade interior",Vector3.zero);
        foreach(float x in new[]{-7.6f,-4.0f,4.0f,7.6f})Local(inside,"GroceryShelf",new Vector3(x,.305f,6.5f),0);
        foreach(float x in new[]{-7f,-3.5f,3.5f,7f})Local(inside,"GroceryShelf",new Vector3(x,.305f,-5.8f),0);
        Local(inside,"CheckoutCounter",new Vector3(-2.2f,.305f,1.5f),0);
        foreach(var p in new[]{new Vector2(-6,-3),new Vector2(7,4)})Local(shop,"CoolingUnit",new Vector3(p.x,4.64f,p.y),0);
        foreach(float x in new[]{-9f,0,9f})Local(warehouse,"CoolingUnit",new Vector3(x,6.44f,-3),0);
        foreach(float x in new[]{-245.6f,-239.1f})Place(site,"GroceryRack",new Vector3(x,.105f,34));
        Place(site,"TransportCrates",new Vector3(-242,.105f,34));
        Place(site,"GroceryPallet",new Vector3(-232.9f,.105f,34));
        string[] goods={"GroceryPallet","CoveredPallet","GroceryPallet","TransportCrates","GroceryPallet","CoveredPallet","GroceryPallet"};
        var storage=TownBakeryPlacement.Child(site,"Second bay pallet storage",Vector3.zero);
        var relocated=goods.Concat(new[]{"CoveredPallet","CoveredPallet","CoveredPallet"}).ToArray();
        for(int i=0;i<relocated.Length;i++)Place(storage,relocated[i],new Vector3(-257.7f+(i%3)*2.2f,.105f,34.6f+(i/3)*2.5f));
        Place(site,"DrinkPallet",new Vector3(-246,.105f,20.5f));
        Place(site,"CoveredPallet",new Vector3(-242,.105f,20.5f));
        Place(site,"GroceryPallet",new Vector3(-238,.105f,20.5f));
        Place(site,"PalletJack",new Vector3(-248.8f,.105f,31.6f),90);
        Place(site,"HandTrolley",new Vector3(-259.0f,.105f,22.8f),90);
        Place(site,"LoadedDolly",new Vector3(-256.5f,.105f,32.0f));
        // Waste is screened off in the back corner, not mixed with grocery pallets.
        Place(site,"WasteBin",new Vector3(-275.7f,.105f,55.8f));
        Place(site,"WasteScreen",new Vector3(-272.0f,.105f,55.8f),90);
        Place(site,"WasteScreen",new Vector3(-273.5f,.105f,53.0f),0);
        foreach(float z in new[]{24.05f,30.35f,36.85f,43.15f})Place(site,"SafetyBollard",new Vector3(-259.2f,.105f,z));
        foreach(float z in new[]{22.25f,32.15f})Place(site,"SafetyBollard",new Vector3(-225.1f,.105f,z));
        foreach(float z in new[]{20.9f,33.6f,46.3f})Place(site,"WallLamp",new Vector3(-259.8f,3.7f,z));
        foreach(float z in new[]{40.0f,45.0f,54.5f})Place(site,"EquipmentPlanter",new Vector3(-232.9f,.105f,z),90);
        Place(site,"EquipmentPlanter",new Vector3(-269.7f,.105f,55.8f),0);
        Place(site,"EquipmentPlanter",new Vector3(-232,.105f,20.5f),90);
        foreach(var p in new[]{new Vector2(-228,58),new Vector2(-228,20),new Vector2(-276.5f,51.2f)})Place(site,"SiteTree",new Vector3(p.x,.105f,p.y));
        foreach(var p in new[]{new Vector2(-229,38.5f),new Vector2(-251.5f,54),new Vector2(-233,31.7f)})Place(site,"SiteLamp",new Vector3(p.x,.105f,p.y));
        Place(site,"GreenCustomerCar",new Vector3(-228,.105f,54.7f));
        Place(site,"BlueCustomerCar",new Vector3(-228,.105f,49.9f));
        // Complete private enclosure, with separate openings for deliveries and public parking.
        Fence(site,new Vector3(-278.5f,.105f,17.5f),new Vector3(-225.5f,.105f,17.5f));
        Fence(site,new Vector3(-278.5f,.105f,17.5f),new Vector3(-278.5f,.105f,61.5f));
        Fence(site,new Vector3(-278.5f,.105f,61.5f),new Vector3(-225.5f,.105f,61.5f));
        Fence(site,new Vector3(-225.5f,.105f,17.5f),new Vector3(-225.5f,.105f,22.1f));
        Fence(site,new Vector3(-225.5f,.105f,32.3f),new Vector3(-225.5f,.105f,41.6f));
        Landscaping(site);
        // Check actual roof/step vertices, not just nominal building centres or collision envelopes.
        foreach(var shell in new[]{shop,warehouse,site.Find("GroceryLink")})
            foreach(var mesh in shell.GetComponentsInChildren<MeshFilter>())
                if(!mesh.sharedMesh.vertices.Select(mesh.transform.TransformPoint).All(p=>In(Site,p)))throw new InvalidOperationException("Building geometry protrudes beyond paving: "+shell.name);
        Paving(site);Combine(site);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Wholesale);
        if(checkpoint.kind!=CheckpointKind.Depot||!checkpoint.requiresRearDocking)throw new InvalidOperationException("Original Wholesale contract changed.");
        foreach(var oldCourt in town.GetComponentsInChildren<SupplierLoadingCourt>(true).Where(c=>c.checkpoint==checkpoint).ToArray())Object.DestroyImmediate(oldCourt);
        checkpoint.transform.SetParent(site,false);checkpoint.transform.SetPositionAndRotation(Bay,Quaternion.Euler(0,90,0));checkpoint.radius=2.2f;checkpoint.displayName="Grocery Depot";
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=checkpoint;court.bounds=new Rect(-260,22.1f,35.5f,22);
        court.streetAccess=Street;court.entrance=Entrance;court.staging=Staging;court.designVehicleLength=6.8f;
        audit.Add("Refined perimeter inside site paving; full western/northern fence; road apron ends at X -220, before road centre. Ten rear pallets retained on ground in the second bay storage pocket, not another supplier.");
        audit.Add("Same Wholesale checkpoint at "+Bay+", facing east, stopped rear loading required. Obsolete western complexes removed under explicit user approval; actual road, railway, residential neighbourhood and active suppliers retained.");
        File.WriteAllLines(TownGroceryBatch.Report+"/placement.txt",audit);AssetDatabase.SaveAssets();
    }
    private static bool In(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private void ClearLegacyDistrict()
    {
        int triangles=0,copies=0,colliders=0;
        var retainedGreenery=town.transform.Find(TownWesternGreenery.RootName);
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            if(retainedGreenery!=null&&f.transform.IsChildOf(retainedGreenery))continue;
            // Keep the terrain ribbon: its vertices intentionally span the whole town.
            if(f.name=="River-cut town terrain"||f.name=="River surface")continue;
            var copy=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,
                (a,b,c)=>In(LegacyArea,a)&&In(LegacyArea,b)&&In(LegacyArea,c)||FormerDriveways.Any(z=>OldApronCorner(a,z)&&OldApronCorner(b,z)&&OldApronCorner(c,z)),out int edits);
            if(copy==null)continue;
            triangles+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);continue;}
            string path=Root+"/Meshes/LegacyTrim"+(copies++)+".asset";Save(copy,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        foreach(var c in town.GetComponentsInChildren<Collider>(true))
        {
            if(retainedGreenery!=null&&c.transform.IsChildOf(retainedGreenery))continue;
            var b=TownResidentialPlacement.ColliderBounds(c);
            if(In(LegacyArea,b.min)&&In(LegacyArea,b.max)){Object.DestroyImmediate(c);colliders++;}
        }
        // Never delete checkpoints or route contracts merely because their empty transform lies in a cleared plot.
        foreach(var t in town.GetComponentsInChildren<Transform>(true).Reverse().ToArray())
            if(t!=town.transform&&(retainedGreenery==null||!t.IsChildOf(retainedGreenery))&&t.childCount==0&&In(LegacyArea,t.position)&&t.GetComponents<Component>().All(c=>c is Transform))Object.DestroyImmediate(t.gameObject);
        audit.Add("Authorised obsolete district clearing: "+triangles+" triangles; "+colliders+" colliders; "+copies+" copied meshes. Shared source geometry untouched.");
    }
    private static bool OldApronCorner(Vector3 p,float z)
    {
        // Exact old box corners distinguish the obsolete asphalt overlays from the actual road mesh.
        return (Mathf.Abs(p.x+226)<.001f||Mathf.Abs(p.x+214)<.001f)&&Mathf.Abs(Mathf.Abs(p.z-z)-4)<.001f&&
            (Mathf.Abs(p.y-.01f)<.001f||Mathf.Abs(p.y-.03f)<.001f);
    }
    private void Landscaping(Transform site)
    {
        // The site's rear planting remains independent of the wider district's natural groves.
        var planting=TownBakeryPlacement.Child(site,"Rear green buffer",Vector3.zero);
        foreach(float z in new[]{24f,32f,40f,47f})Place(planting,"EquipmentPlanter",new Vector3(-276.4f,.105f,z),90);
        foreach(float x in new[]{-268f,-260f,-252f,-244f,-236f})Place(planting,"EquipmentPlanter",new Vector3(x,.105f,60.2f),0);
        foreach(var p in new[]{new Vector2(-282,24),new Vector2(-282,36),new Vector2(-282,48),new Vector2(-271,63),new Vector2(-259,63),new Vector2(-245,63)})Place(planting,"SiteTree",new Vector3(p.x,0,p.y));
    }
    private void ClearGarden()
    {
        var formerGarden=town.transform.Find("Former bakery — pocket garden");
        if(formerGarden==null)throw new InvalidOperationException("Confirmed former-bakery parcel not found.");
        var frontFence=new Rect(-224.2f,14,.4f,46.1f);
        int removed=0,copies=0,collisions=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            bool gardenMesh=f.transform.IsChildOf(formerGarden);
            var copy=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,(a,b,c)=>
                gardenMesh&&In(ClearArea,a)&&In(ClearArea,b)&&In(ClearArea,c) ||
                In(frontFence,a)&&In(frontFence,b)&&In(frontFence,c)&&(a.z+b.z+c.z)/3<59.92f,out int edits);
            if(copy==null)continue;removed+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);continue;}
            string path=Root+"/Meshes/GardenTrim"+(copies++)+".asset";Save(copy,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        foreach(var c in town.GetComponentsInChildren<Collider>(true))
        {
            var b=TownResidentialPlacement.ColliderBounds(c);
            if(!(In(ClearArea,b.min)&&In(ClearArea,b.max))&&!(In(frontFence,b.min)&&In(frontFence,b.max)))continue;
            Object.DestroyImmediate(c);collisions++;
        }
        // Batched street/boundary nodes have no renderer but still own retained physical walls.
        foreach(var t in town.GetComponentsInChildren<Transform>(true).Where(t=>t!=town.transform&&t.childCount==0&&In(ClearArea,t.position)&&t.GetComponent<MeshFilter>()==null&&t.GetComponent<Collider>()==null).ToArray())Object.DestroyImmediate(t.gameObject);
        if(removed<1000)throw new InvalidOperationException("Former garden geometry not identified.");
        audit.Add("Scoped clearing: "+removed+" garden/east-fence triangles and "+collisions+" colliders; "+copies+" copied meshes. Original mesh assets, cafe, south lantern, northern/western boundary and neighbours not overwritten.");
    }
    private void Catalogue()
    {
        var texture=new Texture2D(32,1,TextureFormat.RGBA32,false){name="GroceryPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.SetPixels32(TownGroceryModels.Atlas());texture.Apply();Save(texture,Root+"/Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="GrocerySurface",enableInstancing=true};material.SetFloat("_PaletteIndex",-1);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));Save(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var pane=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="GroceryGlass",renderQueue=3000,enableInstancing=true};
        pane.SetFloat("_Surface",1);pane.SetFloat("_Blend",0);pane.SetFloat("_ZWrite",0);pane.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);pane.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);pane.SetFloat("_Smoothness",.45f);
        pane.SetColor("_BaseColor",new Color(.64f,.82f,.85f,.13f));pane.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");pane.SetOverrideTag("RenderType","Transparent");pane.SetShaderPassEnabled("ShadowCaster",false);Save(pane,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var letters=new TownStaticTypography(Root,"GROCERY DEPOT PICKUP");
        foreach(var model in TownGroceryModels.Create())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids){var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                if(model.Id=="GroceryShop")letters.Add(prototype.transform,"GrocerySign","GROCERY DEPOT",new Vector3(0,3.42f,8.34f),20.5f,.52f);
                if(model.Id=="GroceryWarehouse")letters.Add(prototype.transform,"PickupSign","PICKUP",new Vector3(0,5.42f,7.14f),5.6f,.50f);
                if(model.Id=="GroceryShop")Glazing(prototype.transform,model.Id);
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
        foreach(float x in new[]{-6.2f,6.2f})Face(new Vector3(x,1.76f,8.11f),8.6f,2.25f);
        Face(new Vector3(0,1.68f,8.11f),2.5f,2.62f);
        foreach(float z in new[]{-5f,0,5f})Face(new Vector3(-11.11f,1.76f,z),4.35f,2.25f,270);
        string path=Root+"/Meshes/"+id+"Glass.asset";Save(b.Build(id+" glass"),path);
        var node=TownBakeryPlacement.Child(root,"Glass",Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private Transform Place(Transform parent,string id,Vector3 p,float yaw=90)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        item.transform.SetParent(parent,false);item.transform.localPosition=p;item.transform.localRotation=Quaternion.Euler(0,yaw,0);return item.transform;
    }
    private Transform Local(Transform parent,string id,Vector3 p,float yaw)=>Place(parent,id,p,yaw);
    private void Fence(Transform parent,Vector3 a,Vector3 b)
    {
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);float length=Vector3.Distance(a,b)/count;
        for(int i=0;i<count;i++){var item=Place(parent,"EquipmentFence",Vector3.Lerp(a,b,(i+.5f)/count),Mathf.Abs(a.x-b.x)<.01f?90:0);item.localScale=new Vector3(length/3,1,1);}
        for(int i=0;i<count;i+=3)Pier(Vector3.Lerp(a,b,(float)i/count));
        Pier(b);
        void Pier(Vector3 p){if(pierPositions.Add(p))Place(parent,"BrickPier",p);}
    }
    private void Paving(Transform site)
    {
        var b=new TownMeshBuilder();b.Ground(Site.center.x,Site.center.y,Site.width,Site.height,.09f,TownColor.Pavement);
        b.Ground(-252.4f,32.5f,52.8f,26.0f,.10f,TownColor.Asphalt);
        b.Ground(-225.5f,27.2f,1,8,.10f,TownColor.Asphalt);
        b.Ground(-228,49.9f,6.2f,14.4f,.10f,TownColor.Asphalt);
        foreach(float z in new[]{42.7f,47.5f,52.3f,57.1f})b.Ground(-228,z,6.1f,.055f,.112f,TownColor.White);
        foreach(float z in new[]{24.3f,30.1f})b.Ground(-255,z,7.8f,.065f,.112f,TownColor.Yellow);
        foreach(float x in new[]{-258.9f,-251.1f})b.Ground(x,27.2f,.065f,5.8f,.112f,TownColor.Yellow);
        foreach(float z in new[]{33.4f,43.4f})b.Ground(-255.5f,z,7.2f,.065f,.112f,TownColor.White);
        foreach(float x in new[]{-259.1f,-251.9f})b.Ground(x,38.4f,.065f,10,.112f,TownColor.White);
        Apron(b,new Rect(-225.1f,23.2f,1.1f,8),.10f,.15f);
        Apron(b,new Rect(-224,23.2f,2,8),.15f,.15f);
        Apron(b,new Rect(-222,23.2f,2,8),.15f,.015f);
        string path=Root+"/Meshes/SitePaving.asset";Save(b.Build("Grocery paving"),path);var node=TownBakeryPlacement.Child(site,"Paving",Vector3.zero);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;node.gameObject.isStatic=true;
        Floor(site,"Site floor",Site,.09f,.09f);
        Floor(site,"Continuous yard floor",new Rect(-278.8f,19.5f,52.8f,26),.10f,.10f);
        Floor(site,"Continuous pickup lane",new Rect(-260,23.2f,35,8),.10f,.10f);
        Floor(site,"Parking floor",new Rect(-231.1f,42.7f,6.2f,14.4f),.10f,.10f);
        Floor(site,"Yard ramp",new Rect(-225.1f,23.2f,1.1f,8),.10f,.15f);
        Floor(site,"Sidewalk crossing",new Rect(-224,23.2f,2,8),.15f,.15f);
        Floor(site,"Street ramp",new Rect(-222,23.2f,2,8),.15f,.015f);
    }
    private static void Apron(TownMeshBuilder b,Rect r,float west,float east)
    {
        b.Quad(new Vector3(r.xMin,west,r.yMin),new Vector3(r.xMin,west,r.yMax),new Vector3(r.xMax,east,r.yMax),new Vector3(r.xMax,east,r.yMin),TownColor.Asphalt);
    }
    private static void Floor(Transform site,string name,Rect r,float south,float north)
    {
        var b=new TownMeshBuilder();Apron(b,r,south,north);
        string path=Root+"/Meshes/"+name.Replace(" ","")+".asset";Save(b.Build(name),path);TownBakeryPlacement.Child(site,name,Vector3.zero).gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>();int index=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Grocery visual group "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangles=mesh.triangles.Length/3;string path=Root+"/Meshes/Complex"+index+".asset";Save(mesh,path);
            var node=TownBakeryPlacement.Child(site,"Grocery visual group "+index,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key==glass||group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering")renderer.shadowCastingMode=ShadowCastingMode.Off;
            audit.Add("Visual batch "+index+": "+triangles+" triangles.");index++;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    private static void Save(Object value,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));TownBakeryPlacement.SaveAsset(value,path);}
}
