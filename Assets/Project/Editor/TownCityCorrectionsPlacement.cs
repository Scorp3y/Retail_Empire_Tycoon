using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>One scoped repair pass: facade rules, joined paving and genuinely lowered drive crossings.
/// It never rebuilds the town, relocates a checkpoint, or writes an established shared art asset.</summary>
internal sealed class TownCityCorrectionsPlacement
{
    internal const string Root="Assets/Art/TownCityCorrections",SiteName="City facade and access corrections";
    internal sealed class Apron
    {
        internal readonly string Id;internal readonly Vector3 From,To;internal readonly float Width;internal readonly bool Dirt;
        internal Apron(string id,Vector3 from,Vector3 to,float width,bool dirt=false){Id=id;From=from;To=to;Width=width;Dirt=dirt;}
        internal Rect Cut {get{float h=Width/2+1;return Mathf.Abs(To.x-From.x)>.1f?Rect.MinMaxRect(Mathf.Min(From.x,To.x),From.z-h,Mathf.Max(From.x,To.x),From.z+h):Rect.MinMaxRect(From.x-h,Mathf.Min(From.z,To.z),From.x+h,Mathf.Max(From.z,To.z));}}
        internal Vector3 Point(float t,float side)=>Vector3.Lerp(From,To,t)+Vector3.Cross(Vector3.up,(To-From).normalized).normalized*side;
    }
    internal static readonly Apron[] Aprons={
        // Native street collision is at y=0, although its asphalt decal is slightly above it.
        // Even a 2 cm leading lip catches this arcade pickup's rigid, rotation-locked hull.
        new("Building supplies",new(-155,0,147.9f),new(-155,.085f,151.2f),8),
        new("Retail equipment",new(-64,0,147.9f),new(-64,.085f,151.2f),8),
        new("Bakery",new(-87,0,-147.9f),new(-87,.085f,-154.1f),6.2f),
        new("Wholesale",new(-219.9f,0,27.2f),new(-225.2f,.1f,27.2f),7),
        new("Decoration",new(75.9f,0,37.7f),new(82.1f,.085f,37.7f),7),
        new("Showroom customer",new(267.9f,0,127.5f),new(278.1f,.085f,127.5f),6.4f),
        new("Showroom service",new(267.9f,0,142.5f),new(278.1f,.085f,142.5f),6.4f),
        new("Workshop",new(144,0,-188.1f),new(144,.09f,-183.9f),6.2f),
        new("Construction site",new(123.9f,0,-52),new(134.1f,.08f,-52),5.8f,true)
    };
    private static readonly (string Site,Rect Cut)[] OldSupplierRamps={
        (TownConstructionPlacement.SiteName,new Rect(-159,138,8,13.2f)),
        (TownEquipmentPlacement.SiteName,new Rect(-68,138,8,13.2f))
    };
    private readonly GameObject town;
    private readonly List<string> audit=new();
    internal readonly HashSet<Object> Mutable=new();
    private TownCityCorrectionsGeometry geometry;
    private Transform repairs;
    private Material road;
    private Shader groundShader;
    private readonly Dictionary<Material,Material> groundMaterials=new();
    internal TownCityCorrectionsPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Corrections already installed; use the recorded original for revisions.");
        foreach(string folder in new[]{"Meshes","Materials","Shaders"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        repairs=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);CreateGroundShader();
        road=GroundMaterial(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat"));
        Architecture();geometry=new TownCityCorrectionsGeometry(town.transform);
        PerimeterHomes();LegacyStairs();FarmCars();ConstructionAndWorkshop();JoinedWalkways();Driveways();UnifiedRoads();FarmGroundShadows();
        foreach(var view in geometry.Views)if(view.Removed.Count>0||view.Added.Count>0)Mutable.Add(view.Filter);
        geometry.Save();AssetDatabase.SaveAssets();File.WriteAllLines(TownCityCorrectionsBatch.Report+"/placement.txt",audit);
    }
    private void CreateGroundShader()
    {
        string source=File.ReadAllText(TownWorldAssembler.Folder+"/TownSurface.shader");
        const string old="lighting.shadowCoord=input.shadowCoord;lighting.fogCoord=input.fog;";
        if(source.Split(new[]{old},StringSplitOptions.None).Length!=2)throw new InvalidOperationException("The existing town surface shadow contract changed.");
        // URP selects the cascade per pixel. Interpolating coordinates from different cascade
        // matrices at the three vertices creates false diagonal shadows on large floor triangles.
        const string corrected="\n#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)\nlighting.shadowCoord=input.shadowCoord;\n#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)\nlighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);\n#else\nlighting.shadowCoord=float4(0,0,0,0);\n#endif\nlighting.fogCoord=input.fog;";
        string path=Root+"/Shaders/CorrectedTownSurface.shader";
        File.WriteAllText(path,source.Replace("Universal Render Pipeline/Town Surface","Universal Render Pipeline/Town Corrected Ground").Replace(old,corrected));
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);groundShader=AssetDatabase.LoadAssetAtPath<Shader>(path);
        if(groundShader==null||ShaderUtil.ShaderHasError(groundShader))throw new InvalidOperationException("Corrected ground shader failed to compile.");
    }
    private Material GroundMaterial(Material source)
    {
        if(groundMaterials.TryGetValue(source,out var saved))return saved;
        var material=new Material(source){name=source.name+" corrected ground",shader=groundShader};
        string path=Root+"/Materials/Ground"+groundMaterials.Count+".mat";TownBakeryPlacement.SaveAsset(material,path);
        saved=AssetDatabase.LoadAssetAtPath<Material>(path);groundMaterials.Add(source,saved);return saved;
    }
    private void Architecture()
    {
        int count=0;
        foreach(var definition in TownArchitectureModels.Create())
        {
            var model=definition.Build();var mesh=model.Geometry.Build(definition.Id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            TownArchitectureValidation.Validate(definition,model,mesh,AssetDatabase.LoadAssetAtPath<Material>(TownArchitecturePalette.MaterialPath));
            string path=Root+"/Meshes/Architecture"+definition.Id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==TownArchitecturePalette.Root+"/Meshes/"+definition.Id+".asset"))
            {Mutable.Add(f);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);count++;}
        }
        audit.Add("FACADE: "+count+" architecture instances updated from nine corrected recipes; envelopes, roofs, entrances and colliders unchanged. Stair bays have separate half-landing windows and no balconies. Condensers use the solid inter-floor band.");
    }
    private void PerimeterHomes()
    {
        var site=town.transform.Find(TownShopPerimeterPlacement.SiteName);if(site==null)return;
        var recipes=TownShopPerimeterModels.Create().Where(m=>m.Id=="BrickWalkup"||m.Id=="StuccoHouse").ToDictionary(m=>m.Id);
        int count=0;
        foreach(var instance in site.GetComponentsInChildren<Transform>(true).Where(t=>t.parent!=null&&t.parent.parent==site&&recipes.ContainsKey(t.name)))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownShopPerimeterPlacement.Root+"/Prefabs/"+instance.name+".prefab");
            var old=prefab.GetComponentInChildren<MeshFilter>();geometry.MatchAndRemove(old.sharedMesh,instance.localToWorldMatrix*old.transform.localToWorldMatrix,site);
            var replacement=recipes[instance.name].Draft.Mesh.Build(instance.name+" staircase repair");
            try
            {
                var view=geometry.Views.Single(v=>v.Filter.transform.parent==instance.parent&&v.Filter.GetComponent<MeshRenderer>().sharedMaterial==old.GetComponent<MeshRenderer>().sharedMaterial);
                view.AppendWorld(replacement,instance.localToWorldMatrix);count++;
            }
            finally{Object.DestroyImmediate(replacement);}
        }
        audit.Add("FACADE: "+count+" recently added perimeter homes corrected inside their existing visual batches, without moving buildings or courtyard objects.");
    }
    private void LegacyStairs()
    {
        int count=0,removed=0;
        foreach(var body in town.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="Residential long block"&&c.transform.parent!=null&&c.transform.parent.name=="Urban buildings"))
        {
            float width=body.size.x,depth=body.size.z;int floors=Mathf.RoundToInt(body.size.y/3.1f);var entrances=new List<float>();for(float x=-width/2+5;x<width/2;x+=12)entrances.Add(x);
            var removal=new List<Bounds>();
            for(int floor=1;floor<floors;floor++)for(float x=-width/2+2;x<width/2-1;x+=3.4f)
                if(entrances.Any(e=>Mathf.Abs(x-e)<2.1f))removal.Add(new Bounds(new Vector3(x,1.9f+floor*3.1f,depth/2+.72f),new Vector3(2.2f,2.6f,1.5f)));
            TownCityCorrectionsGeometry.View append=null;
            var worldBody=TownResidentialPlacement.ColliderBounds(body);worldBody.Expand(5);
            foreach(var v in geometry.Views.Where(v=>v.Filter.name.StartsWith("TownChunk_",StringComparison.Ordinal)&&v.Bounds.Intersects(worldBody)))
            {
                for(int i=0;i<v.Indices.Length;i+=3)
                {
                    if(v.Removed.Contains(i))continue;var p=TownCityCorrectionsGeometry.Points(v,i);if(p.Min(a=>a.y)<3.5f||p.Max(a=>a.y)>body.size.y)continue;
                    var local=p.Select(body.transform.InverseTransformPoint).ToArray();
                    if(removal.Any(r=>local.All(r.Contains))){v.Removed.Add(i);removed++;append=v;}
                }
            }
            if(append==null)continue;
            var stairs=new TownMeshBuilder();foreach(float entrance in entrances)for(int landing=0;landing<floors-1;landing++)TownArchitecturalParts.Window(stairs,new Vector3(entrance,3.65f+landing*3.1f,depth/2+.06f),1,1);
            var mesh=stairs.Build("Legacy stair lights");try{append.AppendWorld(mesh,body.transform.localToWorldMatrix);}finally{Object.DestroyImmediate(mesh);}count++;
        }
        audit.Add("FACADE: "+count+" remaining legacy apartment blocks: "+removed+" apartment-window/balcony triangles removed from stair bays; dedicated landing windows added. No solid wall or roof replacement.");
    }
    private void FarmCars()
    {
        var farm=town.transform.Find(TownRuralPolishPlacement.Farm);int count=0;
        foreach(var car in farm.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==farm&&(t.name=="BlueCustomerCar"||t.name=="GreenCustomerCar")).ToArray())
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownRuralArtCatalogue.Root+"/Prefabs/"+car.name+".prefab")??AssetDatabase.LoadAssetAtPath<GameObject>(TownGatewayPlacement.Root+"/Prefabs/"+car.name+".prefab");
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>())geometry.MatchAndRemove(f.sharedMesh,car.localToWorldMatrix*f.transform.localToWorldMatrix,farm);
            foreach(var t in car.GetComponentsInChildren<Transform>(true)){Mutable.Add(t.gameObject);foreach(var component in t.GetComponents<Component>())Mutable.Add(component);}Mutable.Add(farm);
            Object.DestroyImmediate(car.gameObject);count++;
        }
        if(count!=3)throw new InvalidOperationException("Expected exactly three visitor cars at the farm market, found "+count);
        audit.Add("FARM: three market visitor cars and only their collision were removed. Farm work vehicles, market stalls, products and supplier routes retained.");
    }
    private void ConstructionAndWorkshop()
    {
        var construction=town.transform.Find(TownSpecialPlan.ConstructionRoot);var workshop=town.transform.Find(TownSpecialPlan.WorkshopRoot);
        int ground=0,markings=0;
        foreach(var v in geometry.Views)for(int i=0;i<v.Indices.Length;i+=3)
        {
            if(v.Removed.Contains(i))continue;var p=TownCityCorrectionsGeometry.Points(v,i);if(p.Max(a=>a.y)>.25f)continue;
            if(v.Filter.transform.IsChildOf(construction)&&TownCityCorrectionsGeometry.Color(v,i,TownColor.Asphalt))
            {
                v.Removed.Add(i);var m=new TownMeshBuilder();m.Triangle(p[0],p[1],p[2],TownColor.Soil);var mesh=m.Build("Compacted site earth");try{v.AppendWorld(mesh,Matrix4x4.identity);}finally{Object.DestroyImmediate(mesh);}ground++;
            }
            if(v.Filter.transform.IsChildOf(workshop)&&TownCityCorrectionsGeometry.Color(v,i,TownColor.White)&&p.All(a=>In(new Rect(140,-188.5f,8,9),a)))
            {v.Removed.Add(i);markings++;}
        }
        audit.Add("SITE: construction drive changed to the site's existing earth colour ("+ground+" triangles). Workshop entry arrow and misplaced crossing removed ("+markings+" triangles); a new crossing follows the lowered public sidewalk.");
    }
    private void JoinedWalkways()
    {
        int districts=0;
        foreach(var v in geometry.Views.Where(v=>v.Filter.name=="Residential ground and walkways"))
        {
            var rectangles=new List<Rect>();
            for(int i=0;i<v.Indices.Length;i+=3)
            {
                if(v.Removed.Contains(i))continue;
                if(TownCityCorrectionsGeometry.Color(v,i,TownColor.Stone)){v.Removed.Add(i);continue;}
                if(!TownCityCorrectionsGeometry.Color(v,i,TownColor.Pavement))continue;
                var p=TownCityCorrectionsGeometry.Points(v,i);v.Removed.Add(i);
                rectangles.Add(Rect.MinMaxRect(p.Min(a=>a.x),p.Min(a=>a.z),p.Max(a=>a.x),p.Max(a=>a.z)));
            }
            var mesh=UnionPaving(rectangles).Build("Joined district paving");try{v.AppendWorld(mesh,Matrix4x4.identity);}finally{Object.DestroyImmediate(mesh);}districts++;
        }
        // Legacy path edge strokes are removed where they cross another path or a street sidewalk.
        int lines=geometry.Remove((v,i)=>v.Filter.name.StartsWith("TownChunk_",StringComparison.Ordinal)&&TownCityCorrectionsGeometry.Color(v,i,TownColor.Stone)&&TownCityCorrectionsGeometry.Points(v,i).All(p=>p.y>=.14f&&p.y<=.149f));
        audit.Add("PAVING: "+districts+" residential path networks rebuilt as non-overlapping rectangle unions; "+lines+" old low edge-stroke triangles removed from legacy path intersections. Road crossings and kerbs handled separately.");
    }
    internal static TownMeshBuilder UnionPaving(IEnumerable<Rect> input)
    {
        var areas=input.Where(r=>r.width>.001f&&r.height>.001f).Distinct().ToArray();var m=new TownMeshBuilder();if(areas.Length==0)return m;
        var xs=areas.SelectMany(r=>new[]{r.xMin,r.xMax}).Distinct().OrderBy(x=>x).ToArray();var zs=areas.SelectMany(r=>new[]{r.yMin,r.yMax}).Distinct().OrderBy(z=>z).ToArray();
        for(int z=0;z<zs.Length-1;z++)
        {
            int run=-1;
            for(int x=0;x<xs.Length;x++)
            {
                bool filled=x<xs.Length-1&&areas.Any(r=>r.Contains(new Vector2((xs[x]+xs[x+1])/2,(zs[z]+zs[z+1])/2)));
                if(filled&&run<0)run=x;
                if(!filled&&run>=0){m.Ground((xs[run]+xs[x])/2,(zs[z]+zs[z+1])/2,xs[x]-xs[run],zs[z+1]-zs[z],.145f,TownColor.Pavement);run=-1;}
            }
        }
        return m;
    }
    private void Driveways()
    {
        // Remove the old private ramps that start in the opposite lane of the city road.
        foreach(var pair in OldSupplierRamps)
        {
            var scope=town.transform.Find(pair.Site);geometry.ClipGround(pair.Cut,.25f,(v,i)=>v.Filter.transform.IsChildOf(scope));
        }
        foreach(var apron in Aprons)
        {
            geometry.ClipGround(apron.Cut,.25f);
            var m=BuildApron(apron,true);AddPart(apron.Id+" lowered driveway",m,road,true);
            if(apron.Id=="Workshop")
            {
                var crossing=new TownMeshBuilder();for(float x=-apron.Width/2+.25f;x<apron.Width/2-.15f;x+=.65f)
                {
                    var a=apron.Point(.08f,x);var b=apron.Point(.08f,x+.35f);var c=apron.Point(.52f,x+.35f);var d=apron.Point(.52f,x);
                    a.y+=.002f;b.y+=.002f;c.y+=.002f;d.y+=.002f;crossing.Quad(b,a,d,c,TownColor.White);
                }
                AddPart("Workshop public pedestrian crossing",crossing,road);
            }
        }
        RepairPhysicalFloors();audit.Add("ACCESS: nine lowered driveway crossings. The street kerb is cut, not hidden under another decal. All six goods-court checkpoint/route contracts unchanged; two showroom entrances retained.");
    }
    internal static TownMeshBuilder BuildApron(Apron a,bool shoulders)
    {
        var m=new TownMeshBuilder();float h=a.Width/2;var color=a.Dirt?TownColor.Soil:TownColor.Asphalt;
        m.Quad(a.Point(0,-h),a.Point(1,-h),a.Point(1,h),a.Point(0,h),color);
        if(shoulders)foreach(float side in new[]{-1f,1f})
        {
            var p=a.Point(0,side*h);var q=a.Point(1,side*h);var r=a.Point(1,side*(h+1));var s=a.Point(0,side*(h+1));r.y=s.y=.145f;
            if(side>0)m.Quad(p,q,r,s,TownColor.Pavement);else m.Quad(s,r,q,p,TownColor.Pavement);
        }
        return m;
    }
    private void RepairPhysicalFloors()
    {
        int count=0;
        foreach(var c in town.GetComponentsInChildren<MeshCollider>(true).Where(c=>!c.transform.IsChildOf(repairs)&&c.sharedMesh!=null).ToArray())
        {
            var b=TownResidentialPlacement.ColliderBounds(c);if(b.max.y>.3f||b.max.y<.005f||!Aprons.Any(a=>Overlaps(a.Cut,b)))continue;
            var temporary=new GameObject("Temporary floor clipping");temporary.transform.SetPositionAndRotation(c.transform.position,c.transform.rotation);temporary.transform.localScale=c.transform.lossyScale;
            var filter=temporary.AddComponent<MeshFilter>();filter.sharedMesh=c.sharedMesh;
            try
            {
                var mesh=new TownCityCorrectionsGeometry(temporary.transform);foreach(var a in Aprons)mesh.ClipGround(a.Cut);
                foreach(var old in OldSupplierRamps)if(c.transform.IsChildOf(town.transform.Find(old.Site)))mesh.ClipGround(old.Cut);
                if(mesh.Views[0].Removed.Count==0)continue;
                mesh.Views[0].Save("PhysicalFloor"+count++);Mutable.Add(c);
                var floor=filter.sharedMesh;var vertices=floor.vertices;var indices=floor.triangles;float area=0;
                for(int i=0;i<indices.Length;i+=3)area+=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).magnitude*.5f;
                bool enabled=c.enabled;c.enabled=false;c.sharedMesh=null;
                if(area>.001f){c.sharedMesh=floor;c.enabled=enabled;}
            }
            finally{Object.DestroyImmediate(temporary);}
        }
        // Special-site slabs were boxes. Keeping their new vertical cut face can trap a rigid
        // bumper at the crest, even when both top heights match. Retain only their drivable tops.
        int slabs=0;
        foreach(var c in town.GetComponentsInChildren<BoxCollider>(true).Where(c=>!c.transform.IsChildOf(repairs)).ToArray())
        {
            var b=TownResidentialPlacement.ColliderBounds(c);if(b.max.y>.3f||b.max.y<.005f||!Aprons.Any(a=>Overlaps(a.Cut,b)))continue;
            var pieces=new List<Rect>{Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z)};
            foreach(var a in Aprons)pieces=pieces.SelectMany(r=>Subtract(r,a.Cut)).ToList();
            Mutable.Add(c);Mutable.Add(c.gameObject);Object.DestroyImmediate(c);
            if(pieces.Count==0)continue;
            var draft=new TownMeshBuilder();foreach(var r in pieces)draft.Ground(r.center.x,r.center.y,r.width,r.height,b.max.y,TownColor.Pavement);
            string id="RetainedBoxFloor"+slabs++;string path=Root+"/Meshes/"+id+".asset";TownBakeryPlacement.SaveAsset(draft.Build(id),path);
            var node=TownBakeryPlacement.Child(repairs,id,Vector3.zero);node.gameObject.isStatic=true;
            node.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        audit.Add("COLLISION: "+count+" old floor meshes clipped into private copies; "+slabs+" low slab boxes retained as top-only floor meshes, without false vertical cut faces at ramp crests.");
    }
    private void UnifiedRoads()
    {
        var scopes=new[]{TownConstructionPlacement.SiteName,TownEquipmentPlacement.SiteName,TownShowroomPlacement.SiteName,TownDairyPlacement.SiteName,TownRuralPolishPlacement.Farm,TownBakeryPlacement.SiteName};int count=0,batches=0;
        var farmApproach=new Rect(500,235,100,80);
        foreach(var v in geometry.Views.Where(v=>scopes.Any(s=>v.Filter.transform.IsChildOf(town.transform.Find(s)))
            ||v.Filter.name.StartsWith("TownChunk_",StringComparison.Ordinal)&&Overlaps(farmApproach,v.Bounds)))
        {
            var m=new TownMeshBuilder();int local=0;
            for(int i=0;i<v.Indices.Length;i+=3)
            {
                if(v.Removed.Contains(i)||!TownCityCorrectionsGeometry.Color(v,i,TownColor.Asphalt))continue;
                var p=TownCityCorrectionsGeometry.Points(v,i);if(p.Max(a=>a.y)>.25f||p.Min(a=>a.y)<-.01f)continue;
                v.Removed.Add(i);m.Triangle(p[0],p[1],p[2],TownColor.Asphalt);local++;
            }
            // Driveway cuts leave interpolated fragments in the same private material. Move those
            // too, otherwise a diagonal half of a courtyard retains its previous asphalt colour.
            var fragments=v.Added.ToArray();v.Added.Clear();
            for(int i=0;i<fragments.Length;i+=3)
            {
                bool asphalt=v.FixedPalette==(int)TownColor.Asphalt||v.FixedPalette<0&&v.HasPalette&&Enumerable.Range(0,3).All(k=>Mathf.FloorToInt(fragments[i+k].U.x*32)==(int)TownColor.Asphalt);
                var p=Enumerable.Range(0,3).Select(k=>v.Filter.transform.TransformPoint(fragments[i+k].P)).ToArray();
                if(asphalt&&p.Max(a=>a.y)<=.25f&&p.Min(a=>a.y)>=-.01f){m.Triangle(p[0],p[1],p[2],TownColor.Asphalt);local++;}
                else v.Added.AddRange(fragments.Skip(i).Take(3));
            }
            if(local>0){AddPart("Unified asphalt "+batches++,m,road);count+=local;}
        }
        audit.Add("COLOUR: "+count+" low asphalt triangles from "+batches+" site batches now use the exact city-road palette material and world-space detail coordinates. Concrete pedestrian areas and farm dirt tracks keep their own surfaces.");
    }
    private void FarmGroundShadows()
    {
        // Ground paint and terrain share batches with buildings. Separate their existing low
        // faces so the per-pixel cascade fix does not replace any building material or geometry.
        var area=new Rect(500,235,100,80);var drafts=new Dictionary<Material,TownMeshBuilder>();int count=0;
        var farm=town.transform.Find(TownRuralPolishPlacement.Farm);
        foreach(var v in geometry.Views.Where(v=>v.HasPalette&&Overlaps(area,v.Bounds)
            &&(v.Filter.name.StartsWith("TownChunk_",StringComparison.Ordinal)||v.Filter.transform.IsChildOf(farm))))
        for(int i=0;i<v.Indices.Length;i+=3)
        {
            if(v.Removed.Contains(i))continue;var p=TownCityCorrectionsGeometry.Points(v,i);
            if(p.Min(a=>a.y)<-.061f||p.Max(a=>a.y)>.18f||p.Max(a=>a.y)-p.Min(a=>a.y)>.0001f
                ||p.Max(a=>a.x)<area.xMin||p.Min(a=>a.x)>area.xMax||p.Max(a=>a.z)<area.yMin||p.Min(a=>a.z)>area.yMax)continue;
            int index=v.FixedPalette>=0?v.FixedPalette:Mathf.FloorToInt(v.Vertices[v.Indices[i]].U.x*32);var color=(TownColor)index;
            if(!TownCityCorrectionsGeometry.Color(v,i,color))continue;
            var material=v.Filter.GetComponent<MeshRenderer>().sharedMaterial;
            if(!drafts.TryGetValue(material,out var draft))drafts.Add(material,draft=new TownMeshBuilder());
            v.Removed.Add(i);draft.Triangle(p[0],p[1],p[2],color);count++;
        }
        int batch=0;foreach(var pair in drafts)AddPart("Farm approach flat ground "+batch++,pair.Value,GroundMaterial(pair.Key));
        audit.Add("SHADOW: "+count+" existing flat terrain, shoulder and ground-paint triangles separated around the farm approach. Geometry and palette parameters retained; cascade selection corrected per pixel on private ground materials. Real object shadows remain enabled.");
    }
    private void AddPart(string id,TownMeshBuilder draft,Material material,bool physical=false)
    {
        var mesh=draft.Build(id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+id.Replace(' ','_')+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
        var node=TownBakeryPlacement.Child(repairs,id,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        // Flat paving receives real building/tree shadows, but must not cast a second coplanar
        // shadow onto the terrain underneath it (large triangle-shaped dark artefacts).
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private static bool In(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private static bool Overlaps(Rect r,Bounds b)=>r.xMin<b.max.x&&r.xMax>b.min.x&&r.yMin<b.max.z&&r.yMax>b.min.z;
    private static IEnumerable<Rect> Subtract(Rect source,Rect cut)
    {
        if(!source.Overlaps(cut)){yield return source;yield break;}
        float x0=Mathf.Max(source.xMin,cut.xMin),x1=Mathf.Min(source.xMax,cut.xMax),z0=Mathf.Max(source.yMin,cut.yMin),z1=Mathf.Min(source.yMax,cut.yMax);
        if(x0>source.xMin+.001f)yield return Rect.MinMaxRect(source.xMin,source.yMin,x0,source.yMax);
        if(x1<source.xMax-.001f)yield return Rect.MinMaxRect(x1,source.yMin,source.xMax,source.yMax);
        if(z0>source.yMin+.001f)yield return Rect.MinMaxRect(x0,source.yMin,x1,z0);
        if(z1<source.yMax-.001f)yield return Rect.MinMaxRect(x0,z1,x1,source.yMax);
    }
}
