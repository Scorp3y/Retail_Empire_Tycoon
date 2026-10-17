using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Adds independent public amenities; never rebuilds the accepted bakery or its loading court.</summary>
internal sealed class TownBakeryQuarterPlacement
{
    internal const string Root="Assets/Art/TownBakeryQuarter",SiteName="Bakery quarter public amenities";
    // Road sidewalks end at x=-114/-30 and z=-186/-150. The service parcel is excluded entirely.
    internal static readonly Rect[] Permissions={Rect.MinMaxRect(-68,-186,-30,-150),Rect.MinMaxRect(-114,-186,-93,-150)};
    internal const float Floor=.17f,Soil=.24f;
    private static readonly Rect[] OldProps={new Rect(-114,-171,5,6),new Rect(-106,-178,4,4)};
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> models=new();
    private readonly List<string> audit=new();
    private Material surface;
    internal TownBakeryQuarterPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Quarter already installed; regenerate from recorded original, not incrementally.");
        if(town.transform.Find(TownBakeryPlacement.SiteName)==null)throw new InvalidOperationException("Original bakery required.");
        Catalogue();RemoveReplacedPublicProps();
        var site=TownBakeryPlacement.Child(town.transform,SiteName,Vector3.zero);
        Paving(site);
        var coffee=TownBakeryPlacement.Child(site,"Coffee and terrace",Vector3.zero);
        Place(coffee,"CoffeeHouse",-45,-163);
        Place(coffee,"TerracePergola",-56.5f,-163);
        foreach(float x in new[]{-58f,-55f})foreach(float z in new[]{-161f,-165f})Place(coffee,x==-58?"UmbrellaTableSet":"PatioTableSet",x,z);
        Place(coffee,"CoffeeMenu",-42.7f,-156);
        foreach(float x in new[]{-48.6f,-41.4f})Place(coffee,"FlowerPot",x,-157.8f);
        Place(coffee,"LitterBin",-52,-157.5f);
        var east=TownBakeryPlacement.Child(site,"Pocket square",Vector3.zero);
        Garden(east,-64,-174);Garden(east,-64.4f,-159,.9f);Garden(east,-54,-179);Garden(east,-42,-179);Garden(east,-36,-171);
        Bench(east,-59,-175,90);Bench(east,-63,-164,0);Bench(east,-49,-177,270);Bench(east,-37,-178,270);
        foreach(var p in new[]{new Vector2(-64,-168),new Vector2(-54,-183.8f),new Vector2(-42,-183.8f),new Vector2(-36,-166)})Place(east,"FlowerBed",p.x,p.y,p.x==-64||p.x==-36?90:0);
        foreach(var p in new[]{new Vector2(-63,-154.5f),new Vector2(-51,-172),new Vector2(-34,-181)})Place(east,"SquareLantern",p.x,p.y);
        Place(east,"LitterBin",-59,-178);Place(east,"BicycleRack",-35,-154.5f,90);
        var west=TownBakeryPlacement.Child(site,"Flower kiosk and garden",Vector3.zero);
        Place(west,"FlowerKiosk",-108,-162);
        Place(west,"FlowerStand",-110,-157.8f);Place(west,"FlowerStand",-106.5f,-157.8f);
        Garden(west,-100,-168);Garden(west,-100,-179);Garden(west,-110,-173,.65f);
        Bench(west,-100,-173.4f,180);Bench(west,-105.2f,-177,90);
        Place(west,"FlowerBed",-108,-183.5f);Place(west,"FlowerBed",-95,-174,90);
        Place(west,"BicycleRack",-99,-154.5f);Place(west,"LitterBin",-96,-156);
        foreach(var p in new[]{new Vector2(-95,-160),new Vector2(-107,-170),new Vector2(-96,-182)})Place(west,"SquareLantern",p.x,p.y);
        Combine(site);
        audit.Add("16 reusable native model recipes. 2 small buildings, 4 terrace tables, 8 garden islands/trees, 6 benches, 6 lamps, 2 bicycle racks. Existing bakery, both street trees, traffic signal and service yard preserved.");
        File.WriteAllLines(TownBakeryQuarterBatch.Report+"/placement.txt",audit);
    }
    private void Garden(Transform parent,float x,float z,float widthScale=1)
    {
        var island=Place(parent,"GardenIsland",x,z);island.localScale=new Vector3(widthScale,1,1);
        Place(parent,"SquareTree",x,z,0,Soil,widthScale<1?.8f:1);
        Place(parent,"FlowerBed",x,z+2,0,Soil,widthScale);
        foreach(float offset in new[]{-2f,2f})Place(parent,"LowHedge",x+offset*widthScale,z-2,0,Soil,widthScale);
    }
    private void Bench(Transform parent,float x,float z,float yaw)=>Place(parent,"SquareBench",x,z,yaw);
    private void Catalogue()
    {
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        var atlas=new Texture2D(32,1,TextureFormat.RGBA32,false,false){name="Quarter palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        atlas.SetPixels32(TownBakeryQuarterModels.Atlas());atlas.Apply();TownBakeryPlacement.SaveAsset(atlas,Root+"/Materials/Palette.asset");
        var original=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(original==null)throw new InvalidOperationException("Native town material missing.");
        var paint=new Material(original){name="Quarter surface",enableInstancing=true};paint.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));paint.SetFloat("_PaletteIndex",-1);
        TownBakeryPlacement.SaveAsset(paint,Root+"/Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var type=new TownStaticTypography(Root,"COFFEE FLOWERS");
        foreach(var recipe in TownBakeryQuarterModels.Create())
        {
            var mesh=recipe.Draft.Mesh.Build(recipe.Id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            if(mesh.vertexCount==0||mesh.vertices.Any(p=>!Finite(p)))throw new InvalidOperationException("Invalid native quarter recipe "+recipe.Id);
            int triangles=mesh.triangles.Length/3;string path=Root+"/Meshes/"+recipe.Id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            var model=new GameObject(recipe.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                model.isStatic=true;model.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);model.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var solid in recipe.Draft.Solids)
                {
                    var child=TownBakeryPlacement.Child(model.transform,solid.Id,Vector3.zero);child.gameObject.isStatic=true;
                    var collider=child.gameObject.AddComponent<BoxCollider>();collider.center=solid.Center;collider.size=solid.Size;
                }
                if(recipe.Id=="CoffeeHouse"){type.Add(model.transform,"Coffee sign","COFFEE",new Vector3(0,3.3f,4.2f),6.2f,.45f);Glazing(model.transform);}
                if(recipe.Id=="FlowerKiosk")type.Add(model.transform,"Flower sign","FLOWERS",new Vector3(0,2.52f,2.68f),5.1f,.43f);
                path=Root+"/Prefabs/"+recipe.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(model,path);models.Add(recipe.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
                audit.Add(recipe.Id+": "+triangles+" triangles; "+recipe.Draft.Solids.Count+" physical volumes.");
            }
            finally{Object.DestroyImmediate(model);}
        }
        AssetDatabase.SaveAssets();
    }
    private static void Glazing(Transform house)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP glass shader missing.");
        var glass=new Material(shader){name="Coffee glazing",renderQueue=(int)RenderQueue.Transparent,enableInstancing=true};
        glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_ZWrite",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_Smoothness",.7f);glass.SetColor("_BaseColor",new Color(.62f,.76f,.8f,.12f));glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetShaderPassEnabled("ShadowCaster",false);
        TownBakeryPlacement.SaveAsset(glass,Root+"/Materials/Glass.mat");
        var draft=new TownMeshBuilder();
        foreach(float x in new[]{-3.05f,3.05f})Pane(x,1.63f,3,2.35f);
        Pane(0,1.6f,1.95f,2.7f);
        void Pane(float x,float y,float width,float height){draft.Quad(new Vector3(x-width/2,y-height/2,4.08f),new Vector3(x+width/2,y-height/2,4.08f),new Vector3(x+width/2,y+height/2,4.08f),new Vector3(x-width/2,y+height/2,4.08f),TownColor.White);}
        TownBakeryPlacement.SaveAsset(draft.Build("Coffee glazing"),Root+"/Meshes/Glazing.asset");
        var panes=TownBakeryPlacement.Child(house,"Transparent glazing",Vector3.zero);panes.gameObject.isStatic=true;
        panes.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Glazing.asset");var renderer=panes.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private void Paving(Transform site)
    {
        var draft=new TownMeshBuilder();draft.Ground(-50,-168.5f,36,33,Floor,TownColor.Pavement);draft.Ground(-103,-168.5f,18,33,Floor,TownColor.Pavement);
        // Fine joints articulate the public plaza without introducing steps in pedestrian approaches.
        for(float z=-183;z<-153;z+=3){draft.Ground(-50,z,36,.025f,Floor+.001f,TownColor.Stone);draft.Ground(-103,z,18,.025f,Floor+.001f,TownColor.Stone);}
        string path=Root+"/Meshes/PublicPaving.asset";TownBakeryPlacement.SaveAsset(draft.Build("Public paving"),path);
        var paving=TownBakeryPlacement.Child(site,"Public paving",Vector3.zero);paving.gameObject.isStatic=true;paving.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);paving.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;
        // Separate physical quads do not bridge the untouched middle service parcel.
        foreach(var area in new[]{new Rect(-68,-185,36,33),new Rect(-112,-185,18,33)})
        {
            var plane=new TownMeshBuilder();plane.Ground(area.center.x,area.center.y,area.width,area.height,Floor,TownColor.Pavement);
            string floorPath=Root+"/Meshes/Floor"+area.xMin+".asset";TownBakeryPlacement.SaveAsset(plane.Build("Public physical floor"),floorPath);
            var node=TownBakeryPlacement.Child(site,"Public floor "+area.xMin,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(floorPath);
        }
    }
    private void RemoveReplacedPublicProps()
    {
        int copies=0,total=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,(a,b,c)=>OldProps.Any(r=>Inside(r,a)&&Inside(r,b)&&Inside(r,c))&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.03f,out int removed);
            if(removed==0){Object.DestroyImmediate(mesh);continue;}
            string path=Root+"/Meshes/RetainedPublicBatch"+(copies++)+".asset";TownBakeryPlacement.SaveAsset(mesh,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);total+=removed;
        }
        foreach(var node in town.GetComponentsInChildren<Transform>(true).Where(t=>t.childCount==0&&t.GetComponents<Component>().Length==1&&OldProps.Any(r=>Inside(r,t.position))).ToArray())Object.DestroyImmediate(node.gameObject);
        audit.Add("Two replaced decorative footprints only: "+total+" old bed/bicycle triangles removed in "+copies+" private mesh copies; shared original assets, street trees and signal untouched.");
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=Floor,float scale=1)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        item.transform.SetParent(parent,false);item.transform.localPosition=new Vector3(x,y,z);item.transform.localRotation=Quaternion.Euler(0,yaw,0);item.transform.localScale=Vector3.one*scale;return item.transform;
    }
    private static void Combine(Transform site)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>(true);int index=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name="Quarter batch "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/QuarterBatch"+index+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            var node=TownBakeryPlacement.Child(site,"Quarter visual batch "+index++,Vector3.zero);node.gameObject.isStatic=true;node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key.shader.name.Contains("Lettering")||group.Key.HasProperty("_Surface")&&group.Key.GetFloat("_Surface")>.5f)renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
    }
    internal static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin-.0001f&&p.x<=r.xMax+.0001f&&p.z>=r.yMin-.0001f&&p.z<=r.yMax+.0001f;
    internal static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
}
