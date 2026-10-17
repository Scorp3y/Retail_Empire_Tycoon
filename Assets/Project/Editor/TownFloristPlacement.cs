using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Installs a compact garden boutique in the approved vacant block, without rebuilding the city.</summary>
internal sealed class TownFloristPlacement
{
    public const string Root="Assets/Art/TownFlorist";
    public const string SiteName="Flowers and Decor garden boutique";
    public static readonly Rect Site=new(78,18,36,24);
    public static readonly Rect[] Permissions={new(76,16,40,28)};
    // u is frontage width (south to north); v is site depth (west to east).
    public static Vector3 Point(float u,float v,float y=.07f)=>new(78+v,y,18+u);
    public static Vector3 ShopPosition=>Point(8.5f,12.2f,.09f);
    public static Vector3 Bay=>Point(19.7f,25.3f,.075f);
    public static Vector3 Staging=>Point(19.7f,13,0);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly List<string> audit=new();
    private Material surface;
    private Material glass;
    public TownFloristPlacement(GameObject town){this.town=town;}
    public void Enrich()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var site=town.transform.Find(SiteName);
        if(site==null)throw new InvalidOperationException("Install the florist before enriching it.");
        if(site.Find("Garden details v2")!=null)throw new InvalidOperationException("Garden details v2 already installed; refusing duplicate plants.");
        surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        if(surface==null||glass==null)throw new InvalidOperationException("Installed florist materials missing.");
        RemoveOrphanLampBases(site);
        GardenCatalogue();
        var details=TownBakeryPlacement.Child(site,"Garden details v2",Vector3.zero);
        var greenhouse=site.Find("Greenhouse");
        var rails=Place(details,"GreenhousePlantRails",0,0);rails.SetPositionAndRotation(greenhouse.position,greenhouse.rotation);
        // Six existing benches keep their pots; foliage grows from those exact pot rims, not through the floor.
        foreach(float z in new[]{-2.2f,2.2f})foreach(float x in new[]{-3.5f,0,3.5f})for(int i=0;i<5;i++)
        {
            var item=Place(details,"NurseryFoliage",0,0);
            item.position=greenhouse.TransformPoint(new Vector3(x+(i-2)*.48f*.82f,1.179f,z));
            item.rotation=greenhouse.rotation*Quaternion.Euler(0,i*47,0);
        }
        foreach(float z in new[]{-1.4f,1.4f})for(int i=0;i<5;i++)
        {
            var item=Place(details,i%2==0?"BroadleafPot":"FernPot",0,0);
            item.position=greenhouse.TransformPoint(new Vector3(-4.1f+i*2.05f,.2f,z));
            item.rotation=greenhouse.rotation*Quaternion.Euler(0,i*29,0);
        }
        foreach(float x in new[]{-1.75f,1.75f})foreach(float z in new[]{-2.15f,2.15f})
        {
            var item=Place(details,"HangingBasket",0,0);item.position=greenhouse.TransformPoint(new Vector3(x,1.85f,z));
        }
        var shop=site.Find("GardenShop");
        foreach(float x in new[]{-4.7f,-2.4f,2.4f,4.7f})
        {
            var item=Place(details,"HangingBasket",0,0);item.position=shop.TransformPoint(new Vector3(x,1.79f,5.75f));
        }
        foreach(float z in new[]{-2.4f,0f,2.4f})
        {
            var item=Place(details,"HangingBasket",0,0);item.position=shop.TransformPoint(new Vector3(7.0f,1.69f,z));
        }
        // Small planted islands occupy pedestrian edges, never the six-metre goods lane.
        foreach(float v in new[]{8.4f,17.7f,26.4f,32.7f})Place(details,"FlowerBed",1.8f,v,0);
        foreach(float v in new[]{4.5f,12.6f,21.4f})Place(details,"FlowerBed",22.8f,v,0);
        Place(details,"FlowerBed",8.0f,34.1f,90);
        foreach(var p in new[]{new Vector2(1.6f,6.1f),new Vector2(1.6f,29.5f),new Vector2(22.6f,4.4f),new Vector2(22.6f,24.4f),new Vector2(13.7f,19.7f)})
            Place(details,"PottedTree",p.x,p.y);
        foreach(var p in new[]{new Vector2(3.0f,18.2f),new Vector2(12.9f,18.5f),new Vector2(14.5f,26f),new Vector2(14.5f,32.5f)})
        {Place(details,"BroadleafPot",p.x,p.y);Place(details,"FernPot",p.x+1.1f,p.y+.3f);}
        Place(details,"PottingWorkbench",4.5f,34.05f,90);
        var last=Place(details,"FernPot",2.5f,34.05f);last.localScale=Vector3.one*1.15f;
        // Glass is still present, but the pale tint no longer washes out the now sizeable plants.
        glass.SetColor("_BaseColor",new Color(.63f,.83f,.85f,.16f));EditorUtility.SetDirty(glass);
        int objects=details.childCount;
        Combine(site);AssetDatabase.SaveAssets();
        audit.Add("Additive garden revision: "+objects+" modules; 30 bench crowns, 10 floor plants and 4 suspended greenhouse baskets. Central greenhouse aisle and pickup lane retained.");
        File.WriteAllLines(TownFloristBatch.Report+"/placement.txt",audit);
    }
    private void GardenCatalogue()
    {
        foreach(var model in TownFloristModels.CreateGardenDetails())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids)
                {var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.localRotation=solid.Rotation;part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                prefabs.Add(model.Id,PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab"));
                audit.Add(model.Id+": "+triangles+" triangles, "+model.Draft.Solids.Count+" collision parts.");
            }
            finally{Object.DestroyImmediate(prototype);}
        }
    }
    private void RemoveOrphanLampBases(Transform site)
    {
        int copies=0,total=0;
        var oldMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        var uv=TownPalette.UV(TownColor.Frame);
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.transform.IsChildOf(site)&&f.GetComponent<MeshRenderer>()?.sharedMaterial==oldMaterial))
        {
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var tex=mesh.uv;var centers=new List<Vector3>();
            // Native StreetLamp foot (city scale 1/1.3): top fan survived the former >25 cm clearance.
            for(int i=0;i<triangles.Length;i+=3)
            {
                int index=triangles[i];if((tex[index]-uv).sqrMagnitude>.0000001f)continue;
                var a=filter.transform.TransformPoint(vertices[index]);var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                if(!Site.Contains(new Vector2(a.x,a.z))||(Mathf.Abs(a.y-.24f)>.001f&&Mathf.Abs(a.y-.24f/1.3f)>.001f)||Mathf.Abs(b.y-a.y)>.001f||Mathf.Abs(c.y-a.y)>.001f)continue;
                float radius=.22f*a.y/.24f;
                if(Mathf.Abs(Vector3.Distance(a,b)-radius)>.001f||Mathf.Abs(Vector3.Distance(a,c)-radius)>.001f)continue;
                if(!centers.Any(p=>Vector3.Distance(p,a)<.01f))centers.Add(a);
            }
            if(centers.Count==0)continue;
            bool OnFoot(Vector3 p,Vector3 center)=>p.y>=-.001f&&p.y<=center.y+.001f&&new Vector2(p.x-center.x,p.z-center.z).sqrMagnitude<=Mathf.Pow(.251f*center.y/.24f,2);
            var copy=TownBakeryPlacement.CopyWithoutTriangles(mesh,filter.transform,(a,b,c)=>centers.Any(p=>OnFoot(a,p)&&OnFoot(b,p)&&OnFoot(c,p)),out int edits);
            if(copy==null)throw new InvalidOperationException("Identified orphan foot could not be removed.");
            string path=Root+"/Meshes/OrphanFootCleanup"+(copies++)+".asset";Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);total+=edits;
            audit.Add("Removed orphan lamp foot geometry at "+string.Join("; ",centers.Select(p=>p.ToString("F2")))+"; "+edits+" triangles. Road surfaces and drains untouched.");
        }
        if(total==0)throw new InvalidOperationException("Marked asphalt object was not identified as an orphan StreetLamp foot; refusing speculative road edits.");
    }
    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Florist already installed; use its original recorded prefab for regeneration.");
        if(Site.Overlaps(town.GetComponent<TownWorldPlaces>().layout.reservedShopPlot))throw new InvalidOperationException("Florist would occupy player expansion land.");
        ClearPlot();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        Place(site,"GardenShop",8.5f,12.2f);
        Place(site,"Greenhouse",8.2f,28);
        Place(site,"GardenStore",19.7f,31.2f);
        foreach(float u in new[]{1.9f,14.5f})Place(site,"HedgePlanter",u,3.1f,270);
        foreach(float u in new[]{2.9f,6.7f,10.3f,14f})Place(site,"TerracottaFlowers",u,5.65f,0);
        foreach(float u in new[]{4.7f,12.3f})Place(site,"FlowerStand",u,6.65f,270);
        Place(site,"ShopMenu",12.0f,5.6f);
        foreach(float v in new[]{9.0f,12.2f,15.6f})Place(site,"FlowerStand",15.6f,v,0);
        Place(site,"DecorDisplay",8.1f,18.3f);
        Place(site,"FlowerStand",3.4f,20.0f);
        Place(site,"FlowerStand",14.3f,23.7f);
        Place(site,"DecorDisplay",15.4f,29.0f);
        Place(site,"PlantTrolley",15.2f,30.7f);
        foreach(float v in new[]{30.4f,32.2f})Place(site,"DecorCrates",23.0f,v,0);
        foreach(float v in new[]{4.5f,13.5f,22.8f,32.7f})Place(site,"HedgePlanter",.65f,v,0);
        foreach(float v in new[]{8f,18f,27f})Place(site,"PurpleFlowers",22.95f,v);
        Place(site,"GardenLantern",.9f,1.0f);Place(site,"GardenLantern",23.0f,1.0f);Place(site,"GardenLantern",1.1f,33.5f);
        Fence(site,Point(.3f,.8f),Point(.3f,35.3f));
        Fence(site,Point(23.7f,.8f),Point(23.7f,35.3f));
        Fence(site,Point(.3f,35.3f),Point(23.7f,35.3f));
        // No front fence across customer parking or either driveway connection.
        Paving(site);ParkedCar(site,4.5f,2.7f);ParkedCar(site,10.4f,2.7f);
        Combine(site);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Decoration);
        var oldCourt=town.GetComponentsInChildren<SupplierLoadingCourt>(true).Single(c=>c.checkpoint==checkpoint);Object.DestroyImmediate(oldCourt);
        checkpoint.transform.SetParent(site,false);checkpoint.transform.SetPositionAndRotation(Bay,Quaternion.Euler(0,270,0));
        checkpoint.name="Flowers and Decor — rear collection";checkpoint.displayName="Flowers & Decor";checkpoint.requiresRearDocking=true;checkpoint.radius=2.2f;
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=checkpoint;court.bounds=new Rect(87,31.8f,25,10);
        court.streetAccess=Point(19.7f,-4,0);court.entrance=Point(19.7f,4,0);court.staging=Staging;court.designVehicleLength=6.8f;
        audit.Add("Actual vacant block: 36 x 24 m. Facade faces west. One existing Decoration checkpoint moved; the former western buildings are retained, not demolished.");
        File.WriteAllLines(TownFloristBatch.Report+"/placement.txt",audit);
    }
    private void ClearPlot()
    {
        int copies=0,removed=0;
        bool In(Vector3 p)=>p.x>=76&&p.x<=116&&p.z>=16&&p.z<=44;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var copy=TownBakeryPlacement.CopyWithoutTriangles(filter.sharedMesh,filter.transform,(a,b,c)=>Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.25f&&In(a)&&In(b)&&In(c),out int edits);
            if(copy==null)continue;removed+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            string path=Root+"/Meshes/PlotTrim"+(copies++)+".asset";Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {var b=TownResidentialPlacement.ColliderBounds(collider);if(b.max.y>.25f&&In(b.min)&&In(b.max))Object.DestroyImmediate(collider);}
        audit.Add("Vacant parcel clearance: "+removed+" local detail triangles removed in "+copies+" copied meshes; original shared mesh assets unchanged.");
    }
    private void Catalogue()
    {
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        var palette=new Texture2D(32,2,TextureFormat.RGBA32,false){name="FloristPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};palette.SetPixels32(TownFloristModels.Atlas());palette.Apply();
        Save(palette,Root+"/Materials/Palette.asset");
        var source=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");if(source==null)throw new InvalidOperationException("Native town surface missing.");
        var material=new Material(source){name="FloristSurface",enableInstancing=true};material.SetFloat("_PaletteIndex",-1);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));
        Save(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var glassMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="FloristGlass",renderQueue=3000,enableInstancing=true};
        glassMaterial.SetFloat("_Surface",1);glassMaterial.SetFloat("_Blend",0);glassMaterial.SetFloat("_ZWrite",0);
        glassMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glassMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glassMaterial.SetFloat("_Smoothness",.55f);glassMaterial.SetColor("_BaseColor",new Color(.63f,.83f,.85f,.22f));glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glassMaterial.SetOverrideTag("RenderType","Transparent");glassMaterial.SetShaderPassEnabled("ShadowCaster",false);
        Save(glassMaterial,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var lettering=new TownStaticTypography(Root,"FLOWERS & DECOR");
        foreach(var model in TownFloristModels.Create())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            if(mesh.vertexCount==0||mesh.bounds.min.y<-.02f)throw new InvalidOperationException("Invalid florist model "+model.Id+": "+mesh.bounds);
            int triangles=mesh.triangles.Length/3;string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids)
                {var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.localRotation=solid.Rotation;part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                if(model.Id=="GardenShop")lettering.Add(prototype.transform,"ShopSign","FLOWERS & DECOR",new Vector3(0,3.53f,4.89f),7.6f,.50f);
                if(model.Id=="Greenhouse"||model.Id=="GardenShop")Glazing(prototype.transform,model.Id);
                prefabs.Add(model.Id,PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab"));
                audit.Add(model.Id+": "+triangles+" opaque triangles; "+model.Draft.Solids.Count+" collision parts.");
            }
            finally{Object.DestroyImmediate(prototype);}
        }
        AssetDatabase.SaveAssets();
    }
    private void Glazing(Transform parent,string id)
    {
        var builder=new TownMeshBuilder(TownFloristModels.UV);
        void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {builder.Quad(a,b,c,d,TownFloristModels.Color(TownFloristModels.Paint.White));}
        if(id=="GardenShop")
        {
            foreach(float x in new[]{-3.8f,3.8f})Q(new Vector3(x-1.7f,.25f,4.94f),new Vector3(x+1.7f,.25f,4.94f),new Vector3(x+1.7f,2.75f,4.94f),new Vector3(x-1.7f,2.75f,4.94f));
        }
        else
        {
            foreach(float z in new[]{-3.26f,3.26f})
            {
                var a=new Vector3(-5.24f,.22f,z);var b=new Vector3(5.24f,.22f,z);var c=new Vector3(5.24f,3.19f,z);var d=new Vector3(-5.24f,3.19f,z);
                if(z>0)Q(a,b,c,d);else Q(b,a,d,c);
                var e=new Vector3(-5.24f,4.69f,0);var f=new Vector3(5.24f,4.69f,0);
                if(z>0)Q(d,c,f,e);else Q(c,d,e,f);
            }
            foreach(float x in new[]{-5.26f,5.26f})
            {
                var a=new Vector3(x,.22f,-3.24f);var b=new Vector3(x,.22f,3.24f);var c=new Vector3(x,3.19f,3.24f);var d=new Vector3(x,3.19f,-3.24f);var tip=new Vector3(x,4.69f,0);
                if(x<0){Q(a,b,c,d);builder.Triangle(d,c,tip,TownFloristModels.Color(TownFloristModels.Paint.White));}
                else{Q(b,a,d,c);builder.Triangle(c,d,tip,TownFloristModels.Color(TownFloristModels.Paint.White));}
            }
        }
        string path=Root+"/Meshes/"+id+"Glass.asset";Save(builder.Build(id+" glazing"),path);
        var pane=TownBakeryPlacement.Child(parent,"Glass",Vector3.zero);pane.gameObject.isStatic=true;
        pane.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var renderer=pane.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private Transform Place(Transform site,string id,float u,float v,float yaw=270)
    {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        instance.transform.SetParent(site,false);instance.transform.localPosition=Point(u,v,.09f);instance.transform.localRotation=Quaternion.Euler(0,yaw,0);return instance.transform;
    }
    private void Fence(Transform parent,Vector3 a,Vector3 b)
    {
        float length=Vector3.Distance(a,b);int parts=Mathf.CeilToInt(length/3);float step=length/parts;float yaw=Mathf.Abs(a.x-b.x)<.01f?90:0;
        for(int i=0;i<parts;i++)
        {var item=Place(parent,"GardenFence",0,0,yaw);item.position=Vector3.Lerp(a,b,(i+.5f)/parts);item.localScale=new Vector3(step/3,1,1);}
        for(int i=0;i<=parts;i+=3){var item=Place(parent,"GardenPier",0,0);item.position=Vector3.Lerp(a,b,(float)i/parts);}
    }
    private void Paving(Transform site)
    {
        var d=new FloristDraft();
        d.Mesh.Ground(96,30,36,24,.072f,TownFloristModels.Color(TownFloristModels.Paint.Pavement));
        d.Mesh.Ground(95,37.7f,34,6,.085f,TownFloristModels.Color(TownFloristModels.Paint.Asphalt));
        d.Mesh.Ground(80.7f,26.2f,5.4f,13,.085f,TownFloristModels.Color(TownFloristModels.Paint.Asphalt));
        d.Mesh.Ground(101.4f,35.5f,10.7f,11.8f,.085f,TownFloristModels.Color(TownFloristModels.Paint.Asphalt));
        var a=Point(16.7f,-2,.04f);var b=Point(22.7f,-2,.04f);var c=Point(22.7f,0,.085f);var e=Point(16.7f,0,.085f);
        d.Mesh.Quad(a,b,c,e,TownFloristModels.Color(TownFloristModels.Paint.Asphalt));
        foreach(float u in new[]{2.2f,7.6f,13.2f})d.Mesh.Ground(80.7f,18+u,5.1f,.055f,.093f,TownFloristModels.Color(TownFloristModels.Paint.White));
        foreach(float u in new[]{17.55f,21.85f})d.Mesh.Ground(Bay.x,18+u,6.8f,.07f,.095f,TownFloristModels.Color(TownFloristModels.Paint.Yellow));
        foreach(float v in new[]{21.9f,28.7f})d.Mesh.Ground(78+v,Bay.z,.07f,4.37f,.095f,TownFloristModels.Color(TownFloristModels.Paint.Yellow));
        var mesh=d.Mesh.Build("Florist paving");mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/SitePaving.asset";Save(mesh,path);
        var paving=TownBakeryPlacement.Child(site,"Paving",Vector3.zero);paving.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);paving.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;paving.gameObject.isStatic=true;
        Floor(site,"Lane floor",new Rect(78,34.7f,34,6),.085f,.085f);
        Floor(site,"Court floor",new Rect(96.05f,29.6f,10.7f,11.8f),.085f,.085f);
        Floor(site,"Entrance floor",new Rect(76,34.7f,2,6),.04f,.085f);
    }
    private static void Floor(Transform site,string id,Rect area,float west,float east)
    {
        var mesh=new TownMeshBuilder();mesh.Quad(new Vector3(area.xMin,west,area.yMin),new Vector3(area.xMin,west,area.yMax),new Vector3(area.xMax,east,area.yMax),new Vector3(area.xMax,east,area.yMin),TownColor.Asphalt);
        string path=Root+"/Meshes/"+id.Replace(" ","")+".asset";Save(mesh.Build(id),path);
        TownBakeryPlacement.Child(site,id,Vector3.zero).gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private void ParkedCar(Transform site,float u,float v)
    {
        string path="Assets/Art/TownKit/Prefabs/Vehicles/VisitorSedan.prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new InvalidOperationException("Existing parked car missing: "+path);
        var car=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(car,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        car.transform.SetParent(site,false);car.transform.localPosition=Point(u,v,.09f);car.transform.localRotation=Quaternion.Euler(0,90,0);
        var original=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");foreach(var renderer in car.GetComponentsInChildren<MeshRenderer>()){renderer.sharedMaterial=original;renderer.gameObject.isStatic=true;}
        var bounds=car.GetComponent<MeshFilter>().sharedMesh.bounds;var box=car.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
    }
    private void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>();int groupIndex=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Florist visual group "+groupIndex,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangleCount=mesh.triangles.Length/3;
            string path=Root+"/Meshes/Complex"+groupIndex+".asset";Save(mesh,path);
            var item=TownBakeryPlacement.Child(site,"Florist visual group "+groupIndex,Vector3.zero);item.gameObject.isStatic=true;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var renderer=item.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key==glass||group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering")renderer.shadowCastingMode=ShadowCastingMode.Off;
            audit.Add("Visual batch "+groupIndex+": "+triangleCount+" triangles.");groupIndex++;
        }
        foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
    }
    private static void Save(Object value,string path)
    {Directory.CreateDirectory(Path.GetDirectoryName(path));TownBakeryPlacement.SaveAsset(value,path);}
}
