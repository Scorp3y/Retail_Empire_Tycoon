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

/// <summary>Places the agreed dealership in the marked riverside strip; preserves showroom interaction.</summary>
internal sealed class TownShowroomPlacement
{
    public const string Root="Assets/Art/TownShowroom";
    public const string SiteName="Riverside car showroom";
    public static readonly Rect Site=new(278,116,34,40);
    // Wider permission includes complete crowns of only the trees intersecting the site and the two curb links.
    public static readonly Rect[] Permissions={new(270,110,50,56)};
    internal static readonly Rect ResidualBushArea=new(293,124,14,7);
    internal static readonly (string Name,float U,float V,float Yaw,float Height)[] RearLayout={
        ("GarageWorkbench",7.6f,20.56f,90,.09f),
        ("PartsCabinet",15.25f,28.06f,270,.09f),
        ("TireRack",7.6f,27.96f,270,.09f),
        ("AccessoryTrolley",9.95f,27.85f,270,.09f),
        ("HoseReel",15.25f,28.2f,270,2.8f),
        ("ServicePlanter",5.35f,20.57f,90,.09f),
        ("ServicePlanter near hall",12.4f,20.57f,90,.09f)};
    public static Vector3 Point(float u,float v,float y=.09f)=>new(278+v,y,116+u);
    public static Vector3 PublicBay=>Point(11.5f,2.8f,.085f);
    public static Vector3 PublicStreet=>Point(11.5f,-14,0);
    public static Vector3 ServiceBay=>Point(26.5f,25.0f,.085f);
    public static Vector3 ServiceStaging=>Point(26.5f,13,0);
    public static Vector3 ServiceStreet=>Point(26.5f,-14,0);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly List<string> audit=new();
    private Material surface,glass;
    public TownShowroomPlacement(GameObject town){this.town=town;}
    /// <summary>Removes only the five baked exhibition sedans, retaining the unrelated parked customer car.</summary>
    internal void RemoveExhibitionCars()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var site=town.transform.Find(SiteName);
        surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var templates=new[]{("Outdoor exhibition car","BlueSedan"),("Indoor exhibition WhiteSedan","WhiteSedan"),
            ("Indoor exhibition BlueSedan","BlueSedan"),("Indoor exhibition RedSedan","RedSedan"),("Service preparation car","GreenSedan")};
        RemovePlacedVisuals(site,templates.Select(t=>(site.Find(t.Item1),t.Item2)).ToArray(),2960,"FleetTrim");
        foreach(var template in templates)Object.DestroyImmediate(site.Find(template.Item1).gameObject);
    }
    /// <summary>Rearranges existing rear props and closes the service parapet; no new gameplay or interior changes.</summary>
    public void Arrange()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var site=town.transform.Find(SiteName);var details=site==null?null:site.Find("Showroom details v2");
        if(details==null||site.Find("Showroom layout v3")!=null)throw new InvalidOperationException("Expected enriched showroom without the courtyard revision.");
        surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        Transform Planter(float u,float v)=>site.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="ShowroomPlanter"&&Vector3.Distance(t.position,Point(u,v))<.001f);
        var protruding=Planter(1,6.5f);var south=new[]{6f,14f,22f,29.5f}.Select(v=>Planter(.65f,v)).ToArray();
        var templates=new List<(Transform Pose,string ModelId)>{(protruding,"ShowroomPlanter"),(site.Find("ServiceWing"),"ServiceWing")};
        templates.AddRange(south.Select(t=>(t,"ShowroomPlanter")));
        foreach(var pose in RearLayout)
        {
            var node=site.Find(pose.Name)??details.Find(pose.Name);
            if(node==null)throw new InvalidOperationException("Rear detail missing: "+pose.Name);
            templates.Add((node,pose.Name=="ServicePlanter near hall"?"ServicePlanter":pose.Name));
        }
        RemovePlacedVisuals(site,templates,3728,"LayoutTrim");
        Object.DestroyImmediate(protruding.gameObject);
        // These four boxes ran through the south railing by 15 cm. Keep the entire row on the courtyard side.
        foreach(var planter in south)planter.position+=Vector3.forward*.35f;
        foreach(var pose in RearLayout)
        {
            var node=site.Find(pose.Name)??details.Find(pose.Name);
            node.SetPositionAndRotation(Point(pose.U,pose.V,pose.Height),Quaternion.Euler(0,pose.Yaw,0));
        }
        var revision=TownShowroomModels.ServiceRoofRevision();string serviceMesh=SaveModelMesh(revision);
        var prototype=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/ServiceWing.prefab");
        try{prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(serviceMesh);PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/ServiceWing.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(prototype);}
        foreach(var item in templates.Where(t=>t.Pose!=protruding))AddVisual(item.Pose,AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+item.ModelId+".asset"),surface);
        TownBakeryPlacement.Child(site,"Showroom layout v3",Vector3.zero);
        audit.Add("Removed the front planter crossing the south railing; remaining south row shifted 35 cm inside the fence. Eleven exterior planter boxes remain.");
        audit.Add("Continuous service parapet closes the 85 cm junction gap without restoring the office roof loop.");
        audit.Add("Workbench faces into the courtyard with its back 5 cm from the showroom rear wall. Tire rack/trolley sit left of the service door; parts cabinet and elevated hose sit right. Both small planters follow the showroom wall. Door approach and vehicle corridor stay clear.");
        Combine(site);AssetDatabase.SaveAssets();File.WriteAllLines(TownShowroomBatch.Report+"/placement.txt",audit);
    }
    /// <summary>Repairs only the installed shells, then adds dressing without rebuilding typography or moving gameplay.</summary>
    public void Enrich()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var site=town.transform.Find(SiteName);
        if(site==null||site.Find("Showroom details v2")!=null)throw new InvalidOperationException("Expected original installed showroom; refusing duplicate dressing.");
        surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var shells=TownShowroomModels.RevisedShells();
        RemoveOriginalShells(site,shells);
        RemoveResidualBushes();
        foreach(var model in shells)
        {
            string path=SaveModelMesh(model);
            // The installed nodes retain the original colliders. Only their baked visual mesh is replaced.
            AddVisual(site.Find(model.Id),AssetDatabase.LoadAssetAtPath<Mesh>(path),surface);
            var prototype=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/"+model.Id+".prefab");
            try{prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab");}
            finally{PrefabUtility.UnloadPrefabContents(prototype);}
        }
        foreach(var model in TownShowroomModels.DressingModels())RegisterDressing(model);
        var details=TownBakeryPlacement.Child(site,"Showroom details v2",Vector3.zero);
        var hall=site.Find("ShowroomHall");
        var interior=Place(details,"InteriorDressing",11.5f,14);
        interior.SetPositionAndRotation(hall.position,hall.rotation);interior.localScale=hall.localScale;
        var ink=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Lettering.mat");
        foreach(float x in new[]{-3.4f,4.1f})
        {
            var label=TownBakeryPlacement.Child(interior,"CAR wall panel",new Vector3(x,2.83f,-5.685f));
            label.localScale=Vector3.one*.65f;
            AddVisual(label,AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/PylonCar.asset"),ink);
        }
        Place(details,"GarageWorkbench",6.5f,25.7f);
        Place(details,"PartsCabinet",13.5f,27.8f);
        var hose=Place(details,"HoseReel",0,0);
        var service=site.Find("ServiceWing");hose.SetPositionAndRotation(service.TransformPoint(new Vector3(-11,1.45f,2.8f)),service.rotation);
        Place(details,"ServicePlanter",5.3f,23);
        Place(details,"ServicePlanter",13.8f,21.8f).name="ServicePlanter near hall";
        int added=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/InteriorDressing.asset").triangles.Length/3+48+12;
        audit.Add("Interior: "+added+" added triangles against "+TownShowroomModels.OriginalInteriorTriangles+" original display/furniture triangles ("+(100f*added/TownShowroomModels.OriginalInteriorTriangles).ToString("F1")+"%). Detail density is approximate; shells excluded from this metric.");
        audit.Add("Coffee table has four grounded legs. Reception and armchair bases touch the tiled floor. Office roof uses one bare linking slab; service parapet has an open junction instead of a rectangular protruding loop.");
        audit.Add("Rear yard: workbench/tools, parts cabinet, wall-mounted hose and two planted boxes. Existing cars, checkpoint, driveway, suppliers and exterior context retained.");
        Combine(site);AssetDatabase.SaveAssets();File.WriteAllLines(TownShowroomBatch.Report+"/placement.txt",audit);
    }
    private void RemoveResidualBushes()
    {
        // Two baked bushes survived the original site clearance. Their geometry lives in a
        // translated city chunk, not the showroom batch; never rewrite the shared town asset.
        var filter=town.GetComponentsInChildren<MeshFilter>(true).Single(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==Root+"/Meshes/NatureTrim3.asset");
        var source=filter.sharedMesh;var world=source.vertices.Select(filter.transform.TransformPoint).ToArray();var uv=source.uv;var indices=source.triangles;
        string Vertex(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
        string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Vertex(a),Vertex(b),Vertex(c)}.OrderBy(p=>p,StringComparer.Ordinal));
        bool IsBush(int i){int color=Mathf.FloorToInt(uv[i].x*32);return color>=11&&color<=14&&ResidualBushArea.Contains(new Vector2(world[i].x,world[i].z));}
        var signatures=new HashSet<string>();int expected=0;
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=indices[i],b=indices[i+1],c=indices[i+2];if(!IsBush(a)||!IsBush(b)||!IsBush(c))continue;
            signatures.Add(Key(world[a],world[b],world[c]));expected++;
        }
        if(expected!=240)throw new InvalidOperationException("Residual bush baseline changed: expected 240 triangles, found "+expected);
        var copy=TownBakeryPlacement.CopyWithoutTriangles(source,filter.transform,(a,b,c)=>signatures.Contains(Key(a,b,c)),out int removed);
        if(copy==null||removed!=expected)throw new InvalidOperationException("Residual bush removal did not match its exact baseline.");
        string path=Root+"/Meshes/ResidualNatureTrim.asset";Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        audit.Add("Removed 240 remaining legacy bush triangles, including low caps, from the translated city chunk: one bush intersected reception, the other intersected the rear trolley. All other chunk attributes and shared original assets retained.");
    }
    private void RemoveOriginalShells(Transform site,IReadOnlyList<ShowroomModel> shells)
    {
        RemovePlacedVisuals(site,shells.Select(m=>(site.Find(m.Id),m.Id)).ToArray(),2052,"RepairTrim");
    }
    private void RemovePlacedVisuals(Transform site,IReadOnlyList<(Transform Pose,string ModelId)> models,int required,string trimName)
    {
        Vector3Int Bucket(Vector3 p)=>Vector3Int.FloorToInt(p*10);
        var templates=new Dictionary<Vector3Int,List<Vector3[]>>();int expected=0;
        bool Matches(Vector3 a,Vector3 b,Vector3 c)
        {
            var key=Bucket((a+b+c)/3);
            // Native CombineMeshes introduces tiny float differences. A 0.2 mm vertex tolerance
            // avoids rounding-boundary mismatches without accepting nearby decorative geometry.
            bool Near(Vector3 p,Vector3[] triangle)=>triangle.Any(v=>(v-p).sqrMagnitude<.00000004f);
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(templates.TryGetValue(key+new Vector3Int(x,y,z),out var candidates)&&candidates.Any(t=>Near(a,t)&&Near(b,t)&&Near(c,t)))return true;
            return false;
        }
        // Read old assets BEFORE overwriting them: combined site geometry contains these exact transformed templates.
        foreach(var model in models)
        {
            var pose=model.Pose;var source=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+model.ModelId+".asset");
            if(pose==null||source==null)throw new InvalidOperationException("Original placed model missing: "+model.ModelId);
            var vertices=source.vertices.Select(pose.TransformPoint).ToArray();var triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var triangle=new[]{vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]};
                var key=Bucket((triangle[0]+triangle[1]+triangle[2])/3);
                if(!templates.TryGetValue(key,out var list)){list=new List<Vector3[]>();templates.Add(key,list);}list.Add(triangle);
            }
            expected+=triangles.Length/3;
        }
        if(expected!=required)throw new InvalidOperationException("Placed models do not match supported showroom baseline: expected "+required+", found "+expected);
        int removed=0,index=0;
        foreach(var filter in site.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.GetComponent<MeshRenderer>().sharedMaterial!=surface)continue;
            var copy=TownBakeryPlacement.CopyWithoutTriangles(filter.sharedMesh,filter.transform,Matches,out int edits);
            if(copy==null)continue;
            removed+=edits;string path=Root+"/Meshes/"+trimName+(index++)+".asset";Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        if(removed!=expected)throw new InvalidOperationException("Scoped shell replacement failed: expected "+expected+", removed "+removed);
        audit.Add("Replaced exactly "+removed+" original placed-model triangles; all other showroom geometry retained.");
    }
    private string SaveModelMesh(ShowroomModel model)
    {
        var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;
        mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
        audit.Add(model.Id+": "+triangles+" opaque triangles, "+model.Draft.Solids.Count+" collision parts.");return path;
    }
    private void RegisterDressing(ShowroomModel model)
    {
        string path=SaveModelMesh(model);var prototype=new GameObject(model.Id);
        try
        {
            AddVisual(prototype.transform,AssetDatabase.LoadAssetAtPath<Mesh>(path),surface);
            foreach(var solid in model.Draft.Solids)
            {
                var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.localRotation=solid.Rotation;
                part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;
            }
            prefabs.Add(model.Id,PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab"));
        }
        finally{Object.DestroyImmediate(prototype);}
    }
    private static void AddVisual(Transform node,Mesh mesh,Material material)
    {
        if(mesh==null||material==null)throw new InvalidOperationException("Showroom visual asset missing.");
        node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Showroom already installed; refusing duplicate authoring.");
        var layout=town.GetComponent<TownWorldPlaces>().layout;
        if(Site.Overlaps(layout.reservedShopPlot))throw new InvalidOperationException("Showroom overlaps player land.");
        for(float z=Site.yMin;z<=Site.yMax;z+=1)if(Site.xMax>layout.RiverCenter(z)-24)throw new InvalidOperationException("Insufficient undisturbed riverbank margin.");
        Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
        ClearTrees();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        Place(site,"ShowroomHall",11.5f,14);
        Place(site,"OfficeLink",3,24.4f);
        Place(site,"ServiceWing",17,31);
        var display=Place(site,"DisplayPlinth",4.5f,3.4f,90);display.name="Outdoor display platform";
        Place(site,"BlueSedan",4.5f,3.4f,270,.395f).name="Outdoor exhibition car";
        Place(site,"WhiteSedan",18.0f,2.8f,90).name="Customer parked car";
        foreach(var p in new[]{("WhiteSedan",5.5f),("BlueSedan",11.5f),("RedSedan",17.5f)})
            Place(site,p.Item1,p.Item2,11.1f,270,.38f).name="Indoor exhibition "+p.Item1;
        Place(site,"GreenSedan",19.5f,25.1f,90).name="Service preparation car";
        Place(site,"TireRack",9.0f,27.2f);Place(site,"AccessoryTrolley",11.7f,27.2f);
        Place(site,"DealershipPylon",34.9f,1.4f);
        foreach(float v in new[]{6f,14f,22f,29.5f})Place(site,"ShowroomPlanter",.65f,v,0);
        foreach(float v in new[]{4f,12f,21f,29f})Place(site,"ShowroomPlanter",38.8f,v,0);
        foreach(float u in new[]{1.0f,7.0f,16.0f,22f})Place(site,"ShowroomPlanter",u,6.5f);
        foreach(float v in new[]{5f,16f,26f})Place(site,"SiteTree",35.3f,v);
        Place(site,"SiteTree",1.0f,3.6f);Place(site,"SiteTree",1.2f,32f);
        Place(site,"SiteLamp",.6f,1);Place(site,"SiteLamp",38.6f,6.5f);Place(site,"SiteLamp",33.0f,28.5f);
        Fence(site,Point(.3f,.7f),Point(.3f,33.85f));Fence(site,Point(39.4f,.7f),Point(39.4f,33.85f));
        Fence(site,Point(.3f,33.85f),Point(39.4f,33.85f));
        Paving(site);Combine(site);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
        if(checkpoint.kind!=CheckpointKind.Showroom||checkpoint.requiresRearDocking)throw new InvalidOperationException("Unexpected existing showroom contract; do not replace it with a depot.");
        checkpoint.transform.SetParent(site,false);checkpoint.transform.SetPositionAndRotation(PublicBay,Quaternion.Euler(0,90,0));
        checkpoint.name="Car showroom — public inspection";checkpoint.displayName="Car Showroom";
        audit.Add("34 x 40 m marked riverside strip: X278..312, Z116..156; west-facing facade. Water, river terrain and public road meshes unchanged.");
        audit.Add("Same existing Showroom checkpoint and kind; public stopped-car inspection retained. Rear court is vehicle preparation scenery, not a new depot or purchase mechanic. Old western showroom visual buildings retained.");
        File.WriteAllLines(TownShowroomBatch.Report+"/placement.txt",audit);
    }
    private void ClearTrees()
    {
        var signatures=new HashSet<string>();var selected=new List<Transform>();int expected=0;
        string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Vertex(a),Vertex(b),Vertex(c)}.OrderBy(p=>p,StringComparer.Ordinal));
        string Vertex(Vector3 p)=>Mathf.RoundToInt(p.x*100)+","+Mathf.RoundToInt(p.y*100)+","+Mathf.RoundToInt(p.z*100);
        bool Allowed(Vector3 p)=>Permissions[0].Contains(new Vector2(p.x,p.z));
        foreach(var node in town.GetComponentsInChildren<Transform>(true))
        {
            if(!new[]{"Pine","TreeWide","TreeRound","TreeTall","BushRound","BushWide"}.Contains(node.name))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+node.name+".prefab");
            if(source==null)throw new InvalidOperationException("Native vegetation template missing: "+node.name);
            var mesh=source.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices.Select(node.TransformPoint).ToArray();
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var p in vertices)bounds.Encapsulate(p);
            var projection=Rect.MinMaxRect(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z);
            if(!projection.Overlaps(Site))continue;
            if(!Allowed(bounds.min)||!Allowed(bounds.max))throw new InvalidOperationException("Intersecting tree crown extends outside approved clearance: "+node.position+" "+bounds);
            var triangles=mesh.triangles;for(int i=0;i<triangles.Length;i+=3)signatures.Add(Key(vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]));
            expected+=triangles.Length/3;selected.Add(node);
        }
        if(selected.Count==0)throw new InvalidOperationException("Marked forest site could not be identified.");
        int copies=0,removed=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var copy=TownBakeryPlacement.CopyWithoutTriangles(filter.sharedMesh,filter.transform,(a,b,c)=>Allowed(a)&&Allowed(b)&&Allowed(c)&&signatures.Contains(Key(a,b,c)),out int edits);
            if(copy==null)continue;removed+=edits;
            if(copy.vertexCount==0){Object.DestroyImmediate(copy);Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            string path=Root+"/Meshes/NatureTrim"+(copies++)+".asset";Save(copy,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        if(removed!=expected)throw new InvalidOperationException("Native tree template audit failed: expected "+expected+", removed "+removed+". No speculative forest trimming permitted.");
        foreach(var node in selected)foreach(var collider in node.GetComponents<Collider>())Object.DestroyImmediate(collider);
        audit.Add("Cleared "+selected.Count+" intersecting native trees/bushes, "+removed+" exact template triangles including low trunk caps; adjacent forest and shared nature assets preserved.");
    }
    private void Catalogue()
    {
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="ShowroomPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};palette.SetPixels32(TownShowroomModels.Atlas());palette.Apply();Save(palette,Root+"/Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="ShowroomSurface",enableInstancing=true};material.SetFloat("_PaletteIndex",-1);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));
        Save(material,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var glazing=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ShowroomGlass",renderQueue=3000,enableInstancing=true};
        glazing.SetFloat("_Surface",1);glazing.SetFloat("_Blend",0);glazing.SetFloat("_ZWrite",0);glazing.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glazing.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glazing.SetFloat("_Smoothness",.45f);glazing.SetColor("_BaseColor",new Color(.61f,.78f,.83f,.13f));glazing.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glazing.SetOverrideTag("RenderType","Transparent");glazing.SetShaderPassEnabled("ShadowCaster",false);
        Save(glazing,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        var letters=new TownStaticTypography(Root,"CAR SHOWROOM");
        foreach(var model in TownShowroomModels.Create())
        {
            var mesh=model.Draft.Mesh.Build(model.Id);int triangles=mesh.triangles.Length/3;
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+model.Id+".asset";Save(mesh,path);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.isStatic=true;prototype.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);prototype.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in model.Draft.Solids){var part=TownBakeryPlacement.Child(prototype.transform,solid.Id,solid.Center);part.localRotation=solid.Rotation;part.gameObject.AddComponent<BoxCollider>().size=solid.Size;part.gameObject.isStatic=true;}
                if(model.Id=="ShowroomHall"){letters.Add(prototype.transform,"FacadeSign","CAR SHOWROOM",new Vector3(0,4.76f,6.31f),18.8f,.75f);Glazing(prototype.transform);}
                if(model.Id=="DealershipPylon"){letters.Add(prototype.transform,"PylonCar","CAR",new Vector3(0,2.38f,.24f),2.0f,.48f);letters.Add(prototype.transform,"PylonShowroom","SHOWROOM",new Vector3(0,1.86f,.24f),2.05f,.25f);}
                prefabs.Add(model.Id,PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/"+model.Id+".prefab"));
                audit.Add(model.Id+": "+triangles+" opaque triangles, "+model.Draft.Solids.Count+" collision parts.");
            }
            finally{Object.DestroyImmediate(prototype);}
        }
        AssetDatabase.SaveAssets();
    }
    private void Glazing(Transform hall)
    {
        var builder=new TownMeshBuilder();
        builder.Quad(new Vector3(-9.25f,.48f,6.09f),new Vector3(9.88f,.48f,6.09f),new Vector3(9.88f,4.17f,6.09f),new Vector3(-9.25f,4.17f,6.09f),TownColor.White);
        builder.Quad(new Vector3(10.09f,.48f,5.9f),new Vector3(10.09f,.48f,-5.9f),new Vector3(10.09f,4.17f,-5.9f),new Vector3(10.09f,4.17f,5.9f),TownColor.White);
        string path=Root+"/Meshes/HallGlass.asset";Save(builder.Build("Showroom glazed frontage"),path);
        var pane=TownBakeryPlacement.Child(hall,"Glass",Vector3.zero);pane.gameObject.isStatic=true;pane.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var renderer=pane.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private Transform Place(Transform site,string id,float u,float v,float yaw=270,float y=.09f)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        item.transform.SetParent(site,false);item.transform.localPosition=Point(u,v,y);item.transform.localRotation=Quaternion.Euler(0,yaw,0);return item.transform;
    }
    private void Fence(Transform site,Vector3 a,Vector3 b)
    {
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);float step=Vector3.Distance(a,b)/count;
        for(int i=0;i<count;i++){var part=Place(site,"MeshFence",0,0,Mathf.Abs(a.x-b.x)<.01f?90:0);part.position=Vector3.Lerp(a,b,(i+.5f)/count);part.localScale=new Vector3(step/3,1,1);}
    }
    private void Paving(Transform site)
    {
        var mesh=new TownMeshBuilder();
        mesh.Ground(Site.center.x,Site.center.y,Site.width,Site.height,.07f,TownColor.Pavement);
        mesh.Ground(295,142.5f,34,7,.085f,TownColor.Asphalt);
        mesh.Ground(303.5f,131,11,24,.085f,TownColor.Asphalt);
        mesh.Ground(281.4f,128,6.8f,21,.085f,TownColor.Asphalt);
        mesh.Ground(295,151.3f,32,7,.076f,TownColor.Grass);
        foreach(float u in new[]{8.3f,14.7f,21.1f})mesh.Ground(281.3f,116+u,6.3f,.055f,.093f,TownColor.White);
        foreach(float u in new[]{24.35f,28.65f})mesh.Ground(ServiceBay.x,116+u,6.8f,.065f,.095f,TownColor.Yellow);
        foreach(float v in new[]{21.6f,28.4f})mesh.Ground(278+v,ServiceBay.z,.065f,4.35f,.095f,TownColor.Yellow);
        foreach(float u in new[]{11.5f,26.5f})
        {
            var a=Point(u-3.2f,-8,.04f);var b=Point(u+3.2f,-8,.04f);var c=Point(u+3.2f,0,.085f);var d=Point(u-3.2f,0,.085f);
            mesh.Quad(a,b,c,d,TownColor.Asphalt);
            Floor(site,"Entrance "+u,new Rect(270,116+u-3.2f,8,6.4f),.04f,.085f);
        }
        string path=Root+"/Meshes/SitePaving.asset";Save(mesh.Build("Showroom site paving"),path);
        var paving=TownBakeryPlacement.Child(site,"Paving",Vector3.zero);paving.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);paving.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;paving.gameObject.isStatic=true;
        Floor(site,"Site floor",Site,.07f,.07f);
        Floor(site,"Service lane floor",new Rect(278,139,34,7),.085f,.085f);
        Floor(site,"Rear service floor",new Rect(298,119,11,24),.085f,.085f);
        Floor(site,"Customer apron floor",new Rect(278,117.5f,6.8f,21),.085f,.085f);
    }
    private static void Floor(Transform site,string id,Rect area,float west,float east)
    {
        var mesh=new TownMeshBuilder();mesh.Quad(new Vector3(area.xMin,west,area.yMin),new Vector3(area.xMin,west,area.yMax),new Vector3(area.xMax,east,area.yMax),new Vector3(area.xMax,east,area.yMin),TownColor.Asphalt);
        string path=Root+"/Meshes/"+id.Replace(" ","")+".asset";Save(mesh.Build(id),path);TownBakeryPlacement.Child(site,id,Vector3.zero).gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>();int index=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Showroom visual group "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangles=mesh.triangles.Length/3;
            string path=Root+"/Meshes/Complex"+index+".asset";Save(mesh,path);
            var item=TownBakeryPlacement.Child(site,"Showroom visual group "+index,Vector3.zero);item.gameObject.isStatic=true;item.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=item.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key==glass||group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering")renderer.shadowCastingMode=ShadowCastingMode.Off;
            audit.Add("Visual batch "+index+": "+triangles+" triangles.");index++;
        }
        foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
    }
    private static void Save(Object value,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));TownBakeryPlacement.SaveAsset(value,path);}
}
