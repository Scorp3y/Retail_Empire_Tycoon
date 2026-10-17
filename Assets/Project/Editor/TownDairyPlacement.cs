using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Authors only the user-marked eastern woodland dairy parcel and its three road links.</summary>
internal sealed class TownDairyPlacement
{
    internal const string Root="Assets/Art/TownDairy",SiteName="Woodland dairy plant";
    internal const float Floor=.13f;
    internal static readonly Rect[] Permissions={new Rect(435,45,165,165)};
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> models=new();
    private readonly List<string> audit=new();
    private readonly List<Vector3> streets=new();
    private Material surface;
    internal TownDairyPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Dairy already installed. Regenerate from original backup.");
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        foreach(float x in new[]{-30f,0,30})streets.Add(NearestStreet(TownDairyBatch.World(x,0,38)));
        ClearTrees();Catalogue();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,TownDairyBatch.Origin);site.rotation=TownDairyBatch.Rotation;
        Paving(site);var buildings=TownBakeryPlacement.Child(site,"Connected dairy buildings",Vector3.zero);
        Place(buildings,"DairyProduction",0,-2);Place(buildings,"DairyColdStore",0,-21);Place(buildings,"DairyDispatch",23,-18);Place(buildings,"DairyOffice",-11,13);
        foreach(float x in new[]{-9f,0,9})Place(buildings,"CoolingUnit",x,-23,0,Floor+8.24f);
        var intake=TownBakeryPlacement.Child(site,"Raw milk intake",Vector3.zero);
        foreach(float z in new[]{-12f,-19,-26}){Place(intake,"MilkSilo",-32,z);Place(intake,"MilkPipe",-24,z);}
        Place(intake,"IntakeCanopy",-30,4);Place(intake,"MilkTanker",-30,3);Place(intake,"HoseStation",-22,4,270);
        foreach(float x in new[]{-36.8f,-23.2f})foreach(float z in new[]{-2f,10})Place(intake,"SafetyBollard",x,z);
        var dispatch=TownBakeryPlacement.Child(site,"Finished goods dispatch",Vector3.zero);
        Place(dispatch,"DispatchCanopy",23,3.8f);
        foreach(float z in new[]{-25f,-18,-11,-4}){Place(dispatch,"GroceryPallet",34,z);Place(dispatch,"TransportCrates",38,z);}
        foreach(float x in new[]{18.8f,27.2f})Place(dispatch,"SafetyBollard",x,-.5f);
        Place(dispatch,"WasteBin",38,-32);
        TownBakeryPlacement.Child(site,"Dairy dispatch bay",new Vector3(-23,Floor,4.5f));TownBakeryPlacement.Child(site,"Dairy staging",new Vector3(-23,Floor,23));
        var publicArea=TownBakeryPlacement.Child(site,"Office and landscape",Vector3.zero);
        Place(publicArea,"BlueCustomerCar",6,25,180);Place(publicArea,"GreenCustomerCar",11,25,180);
        foreach(var p in new[]{new Vector2(-18,29),new Vector2(-9,30),new Vector2(18,30),new Vector2(-39,-31),new Vector2(-39,18),new Vector2(36,-34.8f)})Place(publicArea,"SquareTree",p.x,p.y);
        foreach(float x in new[]{-20f,-15,-10,-5,18,23})Place(publicArea,"LowHedge",x,34);
        foreach(var p in new[]{new Vector2(-16,23),new Vector2(-6,22)})Place(publicArea,"SquareBench",p.x,p.y);
        foreach(var p in new[]{new Vector2(-11,26),new Vector2(18,24),new Vector2(-38,-36)})Place(publicArea,"FlowerBed",p.x,p.y);
        foreach(float x in new[]{-15f,-5.5f})Place(publicArea,"EquipmentPlanter",x,18.6f);
        Place(publicArea,"BicycleRack",-21,24,90);
        foreach(var p in new[]{new Vector2(-41,28),new Vector2(-20,14),new Vector2(15,34),new Vector2(40,28),new Vector2(40,-28)})Place(publicArea,"SiteLamp",p.x,p.y);
        Boundary(site);Combine(site);
        audit.Add("Marked eastern woodland parcel: centre (525,122), yaw -60 degrees; compact 88 x 78 m compound. Exactly original seven supplier checkpoints retained; dairy is environment, not a new ordering/processing mechanic.");
        audit.Add("Three separate road connections: milk intake, office walk and finished-goods driveway. Every original country-road triangle/collider retained. Physical dispatch bay and staging markers are not duplicate supplier components.");
        File.WriteAllLines(TownDairyBatch.Report+"/placement.txt",audit);
    }
    internal Vector3 NearestStreet(Vector3 point)
    {
        var points=town.GetComponent<TownWorldPlaces>().layout.countryRoad.points;float best=float.MaxValue;var result=Vector3.zero;
        for(int i=1;i<points.Length;i++){var delta=points[i]-points[i-1];float t=Mathf.Clamp01(Vector3.Dot(point-points[i-1],delta)/delta.sqrMagnitude);var p=points[i-1]+delta*t;if((p-point).sqrMagnitude<best){best=(p-point).sqrMagnitude;result=p;}}
        return result;
    }
    private void ClearTrees()
    {
        var keys=new HashSet<string>();var selected=new List<Transform>();int expected=0;
        string Vertex(Vector3 p)=>Mathf.RoundToInt(p.x*100)+","+Mathf.RoundToInt(p.y*100)+","+Mathf.RoundToInt(p.z*100);
        string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Vertex(a),Vertex(b),Vertex(c)}.OrderBy(s=>s,StringComparer.Ordinal));
        var inverse=Matrix4x4.TRS(TownDairyBatch.Origin,TownDairyBatch.Rotation,Vector3.one).inverse;
        var shrubKeys=new Dictionary<string,int>();var shrubHits=new List<int>();var shrubSizes=new List<int>();
        // Original town batching discarded collider-free shrub nodes. Recover their exact template
        // candidates from the historical world-offset rule, accepting only complete matches or absence.
        var shrubSource=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/BushRound.prefab").GetComponent<MeshFilter>().sharedMesh;
        foreach(var tree in town.GetComponentsInChildren<Transform>(true).Where(t=>t.parent!=null&&t.parent.name=="Forest and river corridor"&&Inside(Permissions[0],t.position)))
        {
            var pose=Matrix4x4.TRS(tree.position+new Vector3(2,0,-2),Quaternion.identity,Vector3.one*1.2f);var p=shrubSource.vertices.Select(pose.MultiplyPoint3x4).ToArray();var local=p.Select(inverse.MultiplyPoint3x4).ToArray();
            var box=Rect.MinMaxRect(local.Min(v=>v.x),local.Min(v=>v.z),local.Max(v=>v.x),local.Max(v=>v.z));
            bool yard=box.Overlaps(new Rect(-45,-40,90,80));bool link=streets.Select((street,i)=>(street,gate:TownDairyBatch.World(new[]{-30f,0,30}[i],0,38))).Any(pair=>SegmentDistance(pose.MultiplyPoint3x4(Vector3.zero),pair.street,pair.gate)<4);
            if(!yard&&!link)continue;if(p.Any(v=>!Inside(Permissions[0],v)))throw new InvalidOperationException("Shrub candidate outside clearance.");
            int candidate=shrubHits.Count;shrubHits.Add(0);var t=shrubSource.triangles;shrubSizes.Add(t.Length/3);
            for(int i=0;i<t.Length;i+=3)shrubKeys.Add(Key(p[t[i]],p[t[i+1]],p[t[i+2]]),candidate);
        }
        foreach(var node in town.GetComponentsInChildren<Transform>(true))
        {
            if(!Inside(Permissions[0],node.position)||!new[]{"Pine","TreeWide","TreeRound","TreeTall","BushRound","BushWide","GrassClump","RockGroup"}.Contains(node.name))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+node.name+".prefab");if(source==null)throw new InvalidOperationException("Missing native woodland source "+node.name);
            var mesh=source.GetComponent<MeshFilter>().sharedMesh;var p=mesh.vertices.Select(node.TransformPoint).ToArray();var local=p.Select(inverse.MultiplyPoint3x4).ToArray();
            var box=Rect.MinMaxRect(local.Min(v=>v.x),local.Min(v=>v.z),local.Max(v=>v.x),local.Max(v=>v.z));
            bool yard=box.Overlaps(new Rect(-45,-40,90,80));
            bool link=streets.Select((street,i)=>(street,gate:TownDairyBatch.World(new[]{-30f,0,30}[i],0,38))).Any(pair=>SegmentDistance(node.position,pair.street,pair.gate)<(node.name.StartsWith("Bush")?4:8));
            if(!yard&&!link)continue;
            if(p.Any(v=>!Inside(Permissions[0],v)))throw new InvalidOperationException("Selected woodland crown extends beyond clearance permission: "+node.position);
            var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)keys.Add(Key(p[t[i]],p[t[i+1]],p[t[i+2]]));expected+=t.Length/3;selected.Add(node);
        }
        int removed=0,copies=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            bool Remove(Vector3 a,Vector3 b,Vector3 c)
            {
                if(!Inside(Permissions[0],a)||!Inside(Permissions[0],b)||!Inside(Permissions[0],c))return false;string key=Key(a,b,c);
                if(keys.Contains(key))return true;if(!shrubKeys.TryGetValue(key,out int index))return false;shrubHits[index]++;return true;
            }
            var mesh=TownBakeryPlacement.CopyWithoutTriangles(filter.sharedMesh,filter.transform,Remove,out int count);
            if(count==0){if(mesh!=null)Object.DestroyImmediate(mesh);continue;}
            removed+=count;string path=Root+"/Meshes/WoodlandTrim"+(copies++)+".asset";TownBakeryPlacement.SaveAsset(mesh,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        for(int i=0;i<shrubHits.Count;i++)if(shrubHits[i]!=0&&shrubHits[i]!=shrubSizes[i])throw new InvalidOperationException("Partial orphan shrub template: "+shrubHits[i]+" / "+shrubSizes[i]);
        expected+=shrubHits.Sum();if(removed!=expected||selected.Count==0)throw new InvalidOperationException("Exact native woodland match failed: "+removed+" / "+expected+" triangles.");
        foreach(var node in selected)Object.DestroyImmediate(node.gameObject);
        audit.Add("Cleared "+selected.Count+" complete native tree/plant nodes and "+shrubHits.Count(n=>n>0)+" complete collider-free shrub templates; "+removed+" exact triangles, including trunk caps and original colliders. No rectangular forest wipe; shared source vegetation and all other woodland untouched.");
    }
    internal static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b){p.y=a.y=b.y=0;var delta=b-a;return Vector3.Distance(p,a+delta*Mathf.Clamp01(Vector3.Dot(p-a,delta)/delta.sqrMagnitude));}
    private void Catalogue()
    {
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Dairy palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};palette.SetPixels32(TownDairyModels.Atlas());palette.Apply();TownBakeryPlacement.SaveAsset(palette,Root+"/Materials/Palette.asset");
        var paint=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Dairy surface",enableInstancing=true};paint.SetFloat("_PaletteIndex",-1);paint.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));TownBakeryPlacement.SaveAsset(paint,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var type=new TownStaticTypography(Root,"DAIRY COLD STORE PICKUP OFFICE MILK INTAKE");
        foreach(var recipe in TownDairyModels.Create())
        {
            var mesh=recipe.Draft.Mesh.Build(recipe.Id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();int triangles=mesh.triangles.Length/3;
            if(triangles==0||mesh.vertices.Any(p=>!TownBakeryQuarterPlacement.Finite(p)))throw new InvalidOperationException("Invalid dairy recipe "+recipe.Id);
            string path=Root+"/Meshes/"+recipe.Id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);var model=new GameObject(recipe.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                model.isStatic=true;model.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);model.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var s in recipe.Draft.Solids)
                {
                    var child=TownBakeryPlacement.Child(model.transform,s.Id,Vector3.zero);child.gameObject.isStatic=true;var box=child.gameObject.AddComponent<BoxCollider>();box.center=s.Center;box.size=s.Size;
                    // The inherited bin body omits its wheels. This local envelope includes actual ground contact.
                    if(recipe.Id=="WasteBin"){box.center=model.GetComponent<MeshFilter>().sharedMesh.bounds.center;box.size=model.GetComponent<MeshFilter>().sharedMesh.bounds.size;}
                }
                if(recipe.Id=="SiteLamp"){var child=TownBakeryPlacement.Child(model.transform,"Lamp stem",Vector3.zero);child.gameObject.isStatic=true;var box=child.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,1.86f,0);box.size=new Vector3(.1f,3.72f,.1f);}
                switch(recipe.Id)
                {
                    case "DairyProduction":type.Add(model.transform,"Dairy sign","DAIRY",new Vector3(0,5,11.40f),11,.8f);Glazing(model.transform);break;
                    case "DairyColdStore":type.Add(model.transform,"Cold store sign","COLD STORE",new Vector3(0,6.4f,8.25f),11,.72f);break;
                    case "DairyDispatch":type.Add(model.transform,"Dispatch sign","PICKUP",new Vector3(0,5.15f,16.31f),8,.55f);break;
                    case "DairyOffice":type.Add(model.transform,"Office sign","OFFICE",new Vector3(0,3.3f,4.24f),5.2f,.35f);break;
                    case "IntakeCanopy":type.Add(model.transform,"Intake sign","MILK INTAKE",new Vector3(0,4.45f,6.15f),10.6f,.55f);break;
                }
                path=Root+"/Prefabs/"+recipe.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(model,path);models.Add(recipe.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));audit.Add(recipe.Id+": "+triangles+" triangles / "+recipe.Draft.Solids.Count+" collision parts.");
            }
            finally{Object.DestroyImmediate(model);}
        }
        AssetDatabase.SaveAssets();
    }
    private static void Glazing(Transform hall)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Missing dairy glazing shader.");var glass=new Material(shader){name="Dairy glazing",renderQueue=3000};glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetColor("_BaseColor",new Color(.60f,.78f,.84f,.10f));glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetShaderPassEnabled("ShadowCaster",false);TownBakeryPlacement.SaveAsset(glass,Root+"/Materials/Glass.mat");
        var draft=new TownMeshBuilder();foreach(float x in new[]{-9f,9})Pane(x,2.75f,12.3f,3);Pane(0,2.25f,3.85f,3.7f);
        void Pane(float x,float y,float w,float h)=>draft.Quad(new Vector3(x-w/2,y-h/2,11.15f),new Vector3(x+w/2,y-h/2,11.15f),new Vector3(x+w/2,y+h/2,11.15f),new Vector3(x-w/2,y+h/2,11.15f),TownColor.White);
        TownBakeryPlacement.SaveAsset(draft.Build("Dairy glazing"),Root+"/Meshes/Glazing.asset");var node=TownBakeryPlacement.Child(hall,"Transparent production windows",Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Glazing.asset");var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private void Paving(Transform site)
    {
        var d=new TownMeshBuilder();d.Ground(0,0,86.4f,76.4f,Floor,TownColor.Asphalt);d.Ground(2,25,40,24,Floor+.001f,TownColor.Pavement);
        foreach(float x in new[]{2.8f,7.3f,11.8f,16.3f})d.Ground(-x,25,.065f,6,Floor+.005f,TownColor.White);
        foreach(float x in new[]{19.2f,26.8f})d.Ground(-x,4.5f,.06f,7,Floor+.005f,TownColor.Yellow);foreach(float z in new[]{1f,8f})d.Ground(-23,z,7.6f,.06f,Floor+.005f,TownColor.Yellow);
        foreach(var p in new[]{new Vector2(-18,29),new Vector2(-9,30),new Vector2(18,30),new Vector2(-39,-31),new Vector2(-39,18),new Vector2(36,-34.8f)})d.Ground(-p.x,p.y,7,5,Floor+.002f,TownColor.Grass);
        MeshPart(site,"Site paving",d,true);
        // Real drive links end inside the existing asphalt edge, never across its centreline.
        for(int i=0;i<streets.Count;i++)
        {
            float x=new[]{-30f,0,30}[i];var gate=TownDairyBatch.World(x,Floor,38);var street=streets[i];var edge=street+(gate-street).normalized*3.75f;edge.y=.12f;
            var a=site.InverseTransformPoint(edge);var b=new Vector3(-x,Floor,37.8f);var side=Vector3.Cross(Vector3.up,b-a).normalized*(i==1?1.0f:4.4f);
            var lane=new TownMeshBuilder();lane.Quad(b-side,b+side,a+side,a-side,i==1?TownColor.Pavement:TownColor.Asphalt);MeshPart(site,i==1?"Office pedestrian link":"Drive link "+i,lane,true);
            TownBakeryPlacement.Child(site,"Road connection "+i,site.InverseTransformPoint(street));
        }
    }
    private void Boundary(Transform site)
    {
        var d=new EquipmentDraft();
        void Segment(Vector3 a,Vector3 b)
        {
            var delta=b-a;var centre=(a+b)/2;bool horizontal=Mathf.Abs(delta.x)>.1f;
            var size=horizontal?new Vector3(delta.magnitude,1.9f,.08f):new Vector3(.08f,1.9f,delta.magnitude);d.Collision("Perimeter fence",centre+Vector3.up*.95f,size);
            foreach(float y in new[]{.3f,1.55f})d.Box(centre+Vector3.up*y,horizontal?new Vector3(delta.magnitude,.06f,.07f):new Vector3(.07f,.06f,delta.magnitude),TownColor.Frame);
            int count=Mathf.CeilToInt(delta.magnitude/.28f);for(int i=0;i<=count;i++)d.Box(Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.98f,new Vector3(.032f,1.82f,.032f),TownColor.Frame);
        }
        Segment(new Vector3(-43,0,-38),new Vector3(43,0,-38));Segment(new Vector3(-43,0,-38),new Vector3(-43,0,38));Segment(new Vector3(43,0,-38),new Vector3(43,0,38));
        foreach(var span in new[]{new Vector2(-43,-35.5f),new Vector2(-24.5f,-2.5f),new Vector2(2.5f,24.5f),new Vector2(35.5f,43)})Segment(new Vector3(span.x,0,38),new Vector3(span.y,0,38));
        MeshPart(site,"Physical boundary",d.Mesh,false);var node=site.Find("Physical boundary");node.localPosition=Vector3.up*Floor;
        foreach(var s in d.Solids){var child=TownBakeryPlacement.Child(node,s.Id,Vector3.zero);child.gameObject.isStatic=true;var box=child.gameObject.AddComponent<BoxCollider>();box.center=s.Center;box.size=s.Size;}
    }
    private void MeshPart(Transform site,string id,TownMeshBuilder draft,bool physical)
    {
        string path=Root+"/Meshes/"+id.Replace(" ","")+".asset";var mesh=draft.Build(id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();TownBakeryPlacement.SaveAsset(mesh,path);var node=TownBakeryPlacement.Child(site,id,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=Floor)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);item.transform.SetParent(parent,false);
        if(id.EndsWith("CustomerCar"))y-=models[id].GetComponent<MeshFilter>().sharedMesh.bounds.min.y;
        item.transform.localPosition=new Vector3(-x,y,z);item.transform.localRotation=Quaternion.Euler(0,-yaw,0);return item.transform;
    }
    private static void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>();int index=0;foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Dairy batch "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/DairyBatch"+index+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            var node=TownBakeryPlacement.Child(site,"Dairy visual batch "+index++,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;if(group.Key.shader.name.Contains("Lettering")||group.Key.HasProperty("_Surface")&&group.Key.GetFloat("_Surface")>.5f)renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    internal static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
}
