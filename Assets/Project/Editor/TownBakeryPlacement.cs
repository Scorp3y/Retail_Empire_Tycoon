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

/// <summary>Local, reversible bakery replacement. Never rebuilds the accepted town or its other suppliers.</summary>
internal sealed class TownBakeryPlacement
{
    public const string Root="Assets/Art/TownBakery";
    public const string SiteName="Urban bakery A";
    public static readonly Rect Site=new(-84,-187,23,35);
    public static readonly Rect OldRow=new(-101.7f,-172.1f,37.4f,12.7f);
    public static readonly Rect OldDepot=new(-281,-1,58,62);
    public static readonly Rect[] Permissions={new(-102,-188,42,37),OldDepot,new(-68.3f,-192.1f,7,6.2f)};
    public static readonly Vector3 HousePosition=new(-76,.07f,-179);
    public static readonly Vector3 Bay=new(-76,.075f,-169.6f);
    // The complete house/court turns around its parcel centre, not around world origin.
    public static readonly Vector3 RevisionPivot=new(-76,0,-170);
    public static readonly Rect[] RevisionPermissions={new(-109,-193,71,47)};
    public static readonly Rect[] DetailPermissions={new(-78.5f,-169,5,1),new(-83.5f,-153.6f,7,4.2f)};
    public static Vector3 RevisedPoint(Vector3 p)=>new(2*RevisionPivot.x-p.x,p.y,2*RevisionPivot.z-p.z);
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> prefabs=new();
    private readonly List<string> audit=new();
    private Material material;
    public TownBakeryPlacement(GameObject town){this.town=town;}

    /// <summary>Removes the rear blank sign and the local shelter, preserving the public pavement.</summary>
    public void RemoveDetails()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var house=town.transform.Find(SiteName+"/BakeryHouse");
        if(house==null||Vector3.Dot(house.forward,Vector3.forward)<.999f)throw new InvalidOperationException("Expected the installed north-facing bakery.");
        int panels=0,shelterFaces=0,copies=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var source=filter.sharedMesh;var world=source.vertices.Select(filter.transform.TransformPoint).ToArray();
            var kept=new List<int>[source.subMeshCount];var clipped=new List<ClipTriangle>[source.subMeshCount];int edits=0;
            var uv=source.uv;var frame=TownPalette.UV(TownColor.Frame);var wood=TownPalette.UV(TownColor.Wood);
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                kept[sub]=new List<int>();clipped[sub]=new List<ClipTriangle>();var indices=source.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    bool panel=IsRearPanel(house,world[a])&&IsRearPanel(house,world[b])&&IsRearPanel(house,world[c]);
                    bool shelter=AllInside(DetailPermissions[1],world[a],world[b],world[c])
                        &&(Mathf.Max(world[a].y,Mathf.Max(world[b].y,world[c].y))>.15f||Vector2.Distance(uv[a],frame)<.001f||Vector2.Distance(uv[a],wood)<.001f);
                    if(panel||shelter){edits++;if(panel)panels++;else shelterFaces++;continue;}
                    kept[sub].Add(a);kept[sub].Add(b);kept[sub].Add(c);
                }
            }
            if(edits==0)continue;
            if(kept.All(k=>k.Count==0))
            {Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            string path=Root+"/Meshes/DetailTrim"+(copies++)+".asset";
            SaveAsset(BuildTrimmed(source,kept,clipped,ClipVertex.Read(source,world),filter.transform),path);
            filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        if(panels!=12||shelterFaces<150)throw new InvalidOperationException("Detail recognition failed: rear panel="+panels+" triangles; shelter="+shelterFaces+".");
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {
            var bounds=TownResidentialPlacement.ColliderBounds(collider);
            if(bounds.max.y>.25f&&AllInside(DetailPermissions[1],bounds.min,bounds.max))Object.DestroyImmediate(collider);
        }
        var shelterNode=town.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BusShelter"&&Mathf.Abs(t.position.x+80)<.1f&&Mathf.Abs(t.position.z+151.5f)<.1f);
        Object.DestroyImmediate(shelterNode.gameObject);
        var recipe=TownBakeryModels.Create().Single(a=>a.Id=="BakeryHouse");
        var mesh=recipe.Draft.Mesh.Build("BakeryHouse");mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        SaveAsset(mesh,Root+"/Meshes/BakeryHouse.asset");AssetDatabase.SaveAssets();
        audit.Add("Removed rear blank sign: "+panels+" triangles. Removed local bus shelter: "+shelterFaces+" triangles and its elevated colliders. Public pavement, canopy, shutter and pickup contract retained.");
        File.WriteAllLines("Library/TownBakeryQA/placement.txt",audit);
    }

    private static bool IsRearPanel(Transform house,Vector3 world)
    {
        var p=house.InverseTransformPoint(world);
        return Mathf.Abs(p.x)<=2.401f&&p.y>=3.349f&&p.y<=3.851f&&p.z>=-7.856f&&p.z<=-7.684f;
    }

    internal static void RequireDetailsRemoved(GameObject root)
    {
        var house=root.transform.Find(SiteName+"/BakeryHouse");
        if(house==null)throw new InvalidOperationException("Installed bakery missing.");
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;var world=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=world[indices[i]];var b=world[indices[i+1]];var c=world[indices[i+2]];
                if(IsRearPanel(house,a)&&IsRearPanel(house,b)&&IsRearPanel(house,c))throw new InvalidOperationException("Blank rear panel still present in saved geometry.");
                if(AllInside(DetailPermissions[1],a,b,c)&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.25f)throw new InvalidOperationException("Local shelter geometry remains.");
            }
        }
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))
        {
            var bounds=TownResidentialPlacement.ColliderBounds(collider);
            if(bounds.max.y>.25f&&AllInside(DetailPermissions[1],bounds.min,bounds.max))throw new InvalidOperationException("Invisible shelter collision remains.");
        }
    }

    public void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(SiteName)!=null)throw new InvalidOperationException("Bakery exists; regenerate from the recorded original town, not incrementally.");
        Catalogue();
        ClearLegacy();
        var site=Child(town.transform,SiteName,Vector3.zero);
        Place(site,"BakeryHouse",HousePosition,180);
        Decorate(site);
        Layout(site);
        Combine(site,"Bakery complex", "BakeryComplex");
        var places=town.GetComponent<TownWorldPlaces>();
        var station=places.suppliers.Single(c=>c.supplier==SupplierKind.Bakery);
        var oldCourt=town.GetComponentsInChildren<SupplierLoadingCourt>(true).Single(c=>c.checkpoint==station);
        Object.DestroyImmediate(oldCourt);
        station.transform.SetParent(site,false);station.transform.localPosition=Bay;station.transform.localRotation=Quaternion.identity;
        station.name="Пекарня — задняя выдача";station.displayName="Пекарня";station.kind=CheckpointKind.Depot;
        station.requiresRearDocking=true;station.radius=2.2f;
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=station;
        court.bounds=new Rect(-83,-173.4f,22,21.4f);court.streetAccess=new Vector3(-65,0,-192);
        court.entrance=new Vector3(-65,0,-186);court.staging=new Vector3(-76,0,-158);
        court.approachWaypoints=new[]{new Vector3(-65,0,-158)};court.designVehicleLength=6.8f;
        ReuseFormerDepot();
        File.WriteAllLines("Library/TownBakeryQA/placement.txt",audit);
    }

    public void Revise()
    {
        TownWorldSafety.RequireIsolatedEditor();
        var previous=town.transform.Find(SiteName);
        if(previous==null)throw new InvalidOperationException("Install the original bakery before applying its local revision.");
        if(Vector3.Dot(previous.forward,Vector3.forward)<.999f)throw new InvalidOperationException("This one-time bakery correction is already installed. Do not turn the shelter twice.");
        var station=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Bakery);
        station.transform.SetParent(town.transform,true);
        Object.DestroyImmediate(previous.gameObject);
        var gable=town.transform.Find("Retained tea and book shop end wall");
        if(gable!=null)Object.DestroyImmediate(gable.gameObject);
        Catalogue();
        ClearRevisionLegacy();
        var site=Child(town.transform,SiteName,Vector3.zero);
        Place(site,"BakeryHouse",HousePosition,180);
        Decorate(site,true);
        Layout(site,false);
        Combine(site,"Bakery complex","BakeryComplex");
        // All physical parts and baked visual groups rotate together; the gameplay contract
        // below uses the resulting world coordinates rather than stale pre-turn positions.
        site.SetPositionAndRotation(RevisionPivot-Quaternion.Euler(0,180,0)*RevisionPivot,Quaternion.Euler(0,180,0));
        station.transform.SetParent(site,true);
        station.transform.SetPositionAndRotation(RevisedPoint(Bay),Quaternion.Euler(0,180,0));
        station.name="Bakery — rear collection";station.displayName="Bakery";
        station.requiresRearDocking=true;station.radius=2.2f;
        var court=site.gameObject.AddComponent<SupplierLoadingCourt>();court.checkpoint=station;
        court.bounds=Rect.MinMaxRect(-91,-188,-69,-166.6f);
        court.streetAccess=RevisedPoint(new Vector3(-65,0,-192));
        court.entrance=RevisedPoint(new Vector3(-65,0,-186));
        court.staging=RevisedPoint(new Vector3(-76,0,-158));
        court.approachWaypoints=new[]{RevisedPoint(new Vector3(-65,0,-158))};court.designVehicleLength=6.8f;
        File.WriteAllLines("Library/TownBakeryQA/placement.txt",audit);
    }

    private void ClearRevisionLegacy()
    {
        var stopArea=new Rect(-83.5f,-153.6f,7,4.2f);
        var shopsArea=new Rect(-103,-175.5f,40,16.2f);
        var garageArea=new Rect(-56,-193,16,32.5f);
        var terraceArea=new Rect(-61,-180.5f,9,9);
        var revisedSite=new Rect(-91,-188,23,35);
        var stopPivot=new Vector3(-80,0,-151.5f);
        int copies=0,removed=0,turned=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var source=filter.sharedMesh;var world=source.vertices.Select(filter.transform.TransformPoint).ToArray();
            var attributes=ClipVertex.Read(source,world);
            var kept=new List<int>[source.subMeshCount];var moved=new List<ClipTriangle>[source.subMeshCount];int edits=0;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                kept[sub]=new List<int>();moved[sub]=new List<ClipTriangle>();var indices=source.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    float top=Mathf.Max(world[a].y,Mathf.Max(world[b].y,world[c].y));
                    // Rotate only the shelter above its existing repaired floor. The baked
                    // city contains many objects in shared meshes, so moving the group is unsafe.
                    if(top>.1f&&AllInside(stopArea,world[a],world[b],world[c]))
                    {
                        ClipVertex Turn(int index)
                        {
                            var v=attributes[index];v.P=stopPivot+Quaternion.Euler(0,180,0)*(v.P-stopPivot);
                            v.N=filter.transform.InverseTransformDirection(Quaternion.Euler(0,180,0)*filter.transform.TransformDirection(v.N));
                            var tangent=filter.transform.InverseTransformDirection(Quaternion.Euler(0,180,0)*filter.transform.TransformDirection(new Vector3(v.Tangent.x,v.Tangent.y,v.Tangent.z)));
                            v.Tangent=new Vector4(tangent.x,tangent.y,tangent.z,v.Tangent.w);return v;
                        }
                        moved[sub].Add(new ClipTriangle(Turn(a),Turn(b),Turn(c)));edits++;turned++;continue;
                    }
                    bool shops=top>.1f&&AllInside(shopsArea,world[a],world[b],world[c]);
                    bool garage=AllInside(garageArea,world[a],world[b],world[c]);
                    bool terrace=AllInside(terraceArea,world[a],world[b],world[c]);
                    var footprint=Rect.MinMaxRect(Mathf.Min(world[a].x,Mathf.Min(world[b].x,world[c].x)),Mathf.Min(world[a].z,Mathf.Min(world[b].z,world[c].z)),
                        Mathf.Max(world[a].x,Mathf.Max(world[b].x,world[c].x)),Mathf.Max(world[a].z,Mathf.Max(world[b].z,world[c].z)));
                    // The former public promenade is one long raised triangle spanning
                    // the parcel, not several little slabs wholly inside it. Remove its
                    // complete local strip so it cannot poke through the new driveway.
                    bool oldWalks=top>.1f&&top<.25f&&AllInside(RevisionPermissions[0],world[a],world[b],world[c])
                        &&(footprint.Overlaps(Site)||footprint.Overlaps(revisedSite));
                    if(shops||garage||terrace||oldWalks){edits++;removed++;continue;}
                    kept[sub].Add(a);kept[sub].Add(b);kept[sub].Add(c);
                }
            }
            if(edits==0)continue;
            if(kept.All(k=>k.Count==0)&&moved.All(k=>k.Count==0))
            {Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            string path=Root+"/Meshes/RevisionTrim"+(copies++)+".asset";
            SaveAsset(BuildTrimmed(source,kept,moved,attributes,filter.transform),path);
            filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {
            var bounds=TownResidentialPlacement.ColliderBounds(collider);
            if(bounds.max.y>.25f&&(AllInside(OldRow,bounds.min,bounds.max)||AllInside(garageArea,bounds.min,bounds.max)||AllInside(terraceArea,bounds.min,bounds.max)))Object.DestroyImmediate(collider);
        }
        var shelter=town.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BusShelter"&&Vector3.Distance(t.position,new Vector3(-80,.04f,-151.5f))<.1f);
        shelter.rotation=Quaternion.Euler(0,180,0)*shelter.rotation;
        foreach(var name in new[]{"NeighbourhoodShops","GaragePair","CafeTerrace"})
        {
            foreach(var item in town.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name&&t.position.x>-103&&t.position.x<-38&&t.position.z>-193&&t.position.z<-146).ToArray())Object.DestroyImmediate(item.gameObject);
        }
        var paving=new TownMeshBuilder();
        // Replace the removed service aprons with one flush public pavement, not an
        // asphalt colour overlay with obsolete raised doorway paths sticking through it.
        paving.Ground(-48,-173,16,26,.16f,TownColor.Pavement);
        paving.Ground(-56.5f,-176,9,9,.16f,TownColor.Pavement);
        // The demolished promenade previously covered this public sidewalk band.
        // Stay below the courtyard asphalt: restoring pavement must not recreate a
        // raised stripe across the vehicle surface we have just repaired.
        paving.Ground(-76,-185.25f,64,2.5f,.06f,TownColor.Pavement);
        var flat=Child(town.transform,"Former local garages — continuous pavement",Vector3.zero);
        var pavement=paving.Build("Continuous public pavement");pavement.uv2=pavement.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        string floorPath=Root+"/Meshes/FormerGaragesPaving.asset";SaveAsset(pavement,floorPath);
        flat.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(floorPath);
        flat.gameObject.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");flat.gameObject.isStatic=true;
        audit.Add("Revision: removed "+removed+" marked legacy triangles, turned "+turned+" shelter triangles in "+copies+" local mesh copies. Shared source meshes untouched.");
    }

    private void Catalogue()
    {
        foreach(string directory in new[]{"Materials","Meshes","Prefabs"})Directory.CreateDirectory(Root+"/"+directory);
        AssetDatabase.Refresh();
        var pixels=TownBakeryModels.Atlas();
        var texture=new Texture2D(32,2,TextureFormat.RGBA32,false,false){name="BakeryPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply();SaveAsset(texture,Root+"/Materials/BakeryPalette.asset");
        var original=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(original==null)throw new InvalidOperationException("The existing town material is required.");
        material=new Material(original){name="BakerySurface"};material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/BakeryPalette.asset"));
        material.SetFloat("_PaletteIndex",-1);material.enableInstancing=true;SaveAsset(material,Root+"/Materials/BakerySurface.mat");
        material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/BakerySurface.mat");
        var typography=new TownBakeryTypography();
        foreach(var asset in TownBakeryModels.Create())
        {
            var mesh=asset.Draft.Mesh.Build(asset.Id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            if(mesh.vertexCount==0||mesh.bounds.min.y<-.001f||mesh.vertices.Any(p=>!Finite(p)))throw new InvalidOperationException("Invalid bakery asset "+asset.Id);
            int triangleCount=mesh.triangles.Length/3;
            SaveAsset(mesh,Root+"/Meshes/"+asset.Id+".asset");
            var model=new GameObject(asset.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                model.isStatic=true;model.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+asset.Id+".asset");
                model.GetComponent<MeshRenderer>().sharedMaterial=material;
                typography.Attach(model);
                if(asset.Id=="BakeryHouse")AttachGlazing(model.transform);
                foreach(var solid in asset.Draft.Solids)
                {
                    var piece=Child(model.transform,"Physical "+solid.Id,solid.Center);piece.localRotation=solid.Rotation;
                    piece.gameObject.AddComponent<BoxCollider>().size=solid.Size;piece.gameObject.isStatic=true;
                }
                prefabs.Add(asset.Id,PrefabUtility.SaveAsPrefabAsset(model,Root+"/Prefabs/"+asset.Id+".prefab"));
                audit.Add(asset.Id+": "+triangleCount+" triangles; "+asset.Draft.Solids.Count+" physical parts.");
            }
            finally{Object.DestroyImmediate(model);}
        }
        AssetDatabase.SaveAssets();
    }
    private static void AttachGlazing(Transform house)
    {
        // Separate translucent panes keep the baked bread visible; an opaque blue palette
        // rectangle would hide it and make the storefront look like a warehouse again.
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP glass shader is missing.");
        var glass=new Material(shader){name="BakeryWindowGlass",renderQueue=(int)RenderQueue.Transparent,enableInstancing=true};
        glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_Smoothness",.7f);glass.SetColor("_BaseColor",new Color(.62f,.76f,.8f,.10f));
        glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetShaderPassEnabled("ShadowCaster",false);
        string materialPath=Root+"/Materials/BakeryWindowGlass.mat";SaveAsset(glass,materialPath);
        var mesh=new TownMeshBuilder(TownBakeryModels.UV);
        void Pane(Vector3 p,float width,float yaw)
        {
            var part=new TownMeshBuilder(TownBakeryModels.UV);float x=width/2,h=1.16f;
            part.Quad(new Vector3(-x,-h,0),new Vector3(x,-h,0),new Vector3(x,h,0),new Vector3(-x,h,0),TownBakeryModels.Color(TownBakeryModels.Paint.White));
            var draft=part.Build("Glass pane");
            try{mesh.Append(draft,Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),Vector3.one));}finally{Object.DestroyImmediate(draft);}
        }
        foreach(float x in new[]{-4.15f,4.15f})Pane(new Vector3(x,1.53f,6.44f),3.34f,0);
        foreach(float z in new[]{-2.75f,1.2f})Pane(new Vector3(-7.44f,1.53f,z),3.04f,270);
        string meshPath=Root+"/Meshes/BakeryWindowGlass.asset";SaveAsset(mesh.Build("Bakery window glazing"),meshPath);
        var panes=Child(house,"Transparent display glazing",Vector3.zero);panes.gameObject.isStatic=true;
        panes.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var renderer=panes.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    private void Decorate(Transform site,bool closeBoundary=false)
    {
        foreach(float x in new[]{-80.15f,-71.85f})Place(site,"WindowPlanter",new Vector3(x,.09f,-186),180);
        foreach(float x in new[]{-82.1f,-77.55f,-74.45f,-69.8f})Place(site,"TopiaryPlanter",new Vector3(x,.09f,-185.7f),180);
        Place(site,"MenuBoard",new Vector3(-72.95f,.09f,-186.65f),180);
        foreach(float z in new[]{-181.9f,-176.8f})Place(site,"WindowPlanter",new Vector3(-68.45f,.09f,z),90);
        foreach(float z in new[]{-183.7f,-178.9f,-174.2f})Place(site,"TopiaryPlanter",new Vector3(-68.6f,.09f,z));
        Place(site,"BreadTrolley",new Vector3(-79.35f,.09f,-172.4f));
        Place(site,"CratePallet",new Vector3(-71.25f,.09f,-172.1f));
        Place(site,"CratePallet",new Vector3(-71.25f,.09f,-170.85f));
        foreach(float z in new[]{-170.8f,-166.9f,-163,-154.2f})Place(site,"TopiaryPlanter",new Vector3(-82.5f,.09f,z));
        foreach(float z in new[]{-173.4f,-168.4f,-164.8f,-154.2f})Place(site,"TopiaryPlanter",new Vector3(-61.9f,.09f,z));
        foreach(float x in new[]{-81f,-71f})Place(site,"GardenBed",new Vector3(x,.09f,-153.65f));
        Place(site,"StreetLantern",new Vector3(-83.3f,.07f,-186.6f));
        // The retained book shop itself closes the middle of this boundary. A continuous
        // fence here would pass through its wall, rather than preserving the neighbour.
        if(closeBoundary)FenceLine(site,new Vector3(-82.9f,.07f,-185.6f),new Vector3(-82.9f,.07f,-152.4f));
        else
        {
            FenceLine(site,new Vector3(-82.9f,.07f,-185.6f),new Vector3(-82.9f,.07f,-173.9f));
            FenceLine(site,new Vector3(-82.9f,.07f,-159.8f),new Vector3(-82.9f,.07f,-152.4f));
        }
        FenceLine(site,new Vector3(-61,.07f,-185.6f),new Vector3(-61,.07f,-152.4f));
        FenceLine(site,new Vector3(-82.9f,.07f,-152.4f),new Vector3(-61,.07f,-152.4f));
        // No gate across the vehicle entrance or planter in its swept path.
        foreach(float x in new[]{-68.1f,-61})Place(site,"BrickPier",new Vector3(x,.07f,-185.6f));
    }
    private void FenceLine(Transform site,Vector3 a,Vector3 b)
    {
        float length=Vector3.Distance(a,b);int pieces=Mathf.CeilToInt(length/3);float step=length/pieces;
        float yaw=Mathf.Abs(a.x-b.x)<.001f?90:0;
        for(int i=0;i<pieces;i++)
        {
            var fence=Place(site,"IronFence",Vector3.Lerp(a,b,(i+.5f)/pieces),yaw);
            fence.localScale=new Vector3(step/3,1,1);
        }
        for(int i=0;i<=pieces;i+=3)Place(site,"BrickPier",Vector3.Lerp(a,b,(float)i/pieces));
    }
    private void Layout(Transform site,bool arrows=true)
    {
        var d=new BakeryDraft();
        d.Mesh.Ground(-72.5f,-162.65f,21,21.4f,.085f,TownBakeryModels.Color(TownBakeryModels.Paint.Asphalt));
        d.Mesh.Ground(-65,-171.9f,6.2f,28.2f,.085f,TownBakeryModels.Color(TownBakeryModels.Paint.Asphalt));
        d.Mesh.Ground(-76,-186,15.5f,2,.088f,TownBakeryModels.Color(TownBakeryModels.Paint.Pavement));
        // Driveway joins the carriageway without a vertical lip; slope is well below 5 percent.
        d.Quad(new Vector3(-68.1f,.04f,-192),new Vector3(-61.9f,.04f,-192),new Vector3(-61.9f,.085f,-186),new Vector3(-68.1f,.085f,-186),TownBakeryModels.Paint.Asphalt);
        foreach(float x in new[]{-78.15f,-73.85f})d.Mesh.Ground(x,Bay.z,.08f,6.8f,.097f,TownBakeryModels.Color(TownBakeryModels.Paint.Yellow));
        foreach(float z in new[]{Bay.z-3.4f,Bay.z+3.4f})d.Mesh.Ground(-76,z,4.38f,.08f,.097f,TownBakeryModels.Color(TownBakeryModels.Paint.Yellow));
        if(arrows)
        {
            Arrow(d,new Vector3(-65,.098f,-183));Arrow(d,new Vector3(-65,.098f,-174));
            Arrow(d,new Vector3(-76,.098f,Bay.z+1));
        }
        for(float x=-82.5f;x<=-69.5f;x+=1.5f)d.Mesh.Ground(x,-186,.018f,2,.095f,TownBakeryModels.Color(TownBakeryModels.Paint.Stone));
        var geometry=Child(site,"Paving and loading markings",Vector3.zero);
        var mesh=d.Mesh.Build("Bakery site paving");mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();SaveAsset(mesh,Root+"/Meshes/SitePaving.asset");
        geometry.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SitePaving.asset");
        geometry.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;geometry.gameObject.isStatic=true;
        PhysicalFloor(site,"Court floor",new Rect(-83,-173.35f,21,21.4f),.085f,.085f);
        PhysicalFloor(site,"Driveway floor",new Rect(-68.1f,-186,6.2f,28.2f),.085f,.085f);
        PhysicalFloor(site,"Street transition",new Rect(-68.1f,-192,6.2f,6),.04f,.085f);
    }
    private static void PhysicalFloor(Transform parent,string name,Rect bounds,float south,float north)
    {
        var mesh=new TownMeshBuilder();mesh.Quad(new Vector3(bounds.xMin,south,bounds.yMin),new Vector3(bounds.xMin,north,bounds.yMax),
            new Vector3(bounds.xMax,north,bounds.yMax),new Vector3(bounds.xMax,south,bounds.yMin),TownColor.Asphalt);
        string path=Root+"/Meshes/"+name.Replace(" ","")+".asset";SaveAsset(mesh.Build(name),path);
        Child(parent,name,Vector3.zero).gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
    private static void Arrow(BakeryDraft d,Vector3 p)
    {
        d.Mesh.Ground(p.x,p.z-.15f,.19f,1.2f,p.y,TownBakeryModels.Color(TownBakeryModels.Paint.White));
        d.Mesh.Triangle(p+new Vector3(-.58f,0,.1f),p+new Vector3(0,0,.93f),p+new Vector3(.58f,0,.1f),TownBakeryModels.Color(TownBakeryModels.Paint.White));
    }

    private void ClearLegacy()
    {
        int removed=0,changed=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var source=filter.sharedMesh;if(source==null)throw new InvalidOperationException("Missing source mesh.");
            var vertices=source.vertices;var world=vertices.Select(filter.transform.TransformPoint).ToArray();
            ClipVertex[] attributeCache=null;
            var kept=new List<int>[source.subMeshCount];var clipped=new List<ClipTriangle>[source.subMeshCount];int edits=0;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                kept[sub]=new List<int>();clipped[sub]=new List<ClipTriangle>();var indices=source.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    bool raised=Mathf.Max(world[a].y,Mathf.Max(world[b].y,world[c].y))>.24f;
                    bool row=raised&&AllInside(OldRow,world[a],world[b],world[c]);
                    if(row&&Mathf.Max(world[a].x,Mathf.Max(world[b].x,world[c].x))>-83)
                    {
                        attributeCache??=ClipVertex.Read(source,world);
                        var polygon=new List<ClipVertex>{attributeCache[a],attributeCache[b],attributeCache[c]};
                        polygon=ClipLeft(polygon,-83);
                        for(int t=1;t+1<polygon.Count;t++)clipped[sub].Add(new ClipTriangle(polygon[0],polygon[t],polygon[t+1]));
                        edits++;continue;
                    }
                    // Reuse this whole interior, including obsolete flat arrows/bay outlines.
                    // Large shared ground triangles and the northern shared boundary remain untouched.
                    bool former=AllInside(OldDepot,world[a],world[b],world[c])&&Mathf.Min(world[a].z,Mathf.Min(world[b].z,world[c].z))>.02f&&Mathf.Max(world[a].z,Mathf.Max(world[b].z,world[c].z))<59.8f;
                    bool local=raised&&AllInside(Site,world[a],world[b],world[c])&&!row;
                    bool entrance=raised&&AllInside(Permissions[2],world[a],world[b],world[c]);
                    if(former||local||entrance){edits++;continue;}
                    kept[sub].Add(a);kept[sub].Add(b);kept[sub].Add(c);
                }
            }
            if(edits==0)continue;removed+=edits;
            if(kept.All(k=>k.Count==0)&&clipped.All(k=>k.Count==0))
            {Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            attributeCache??=ClipVertex.Read(source,world);
            var mesh=BuildTrimmed(source,kept,clipped,attributeCache,filter.transform);
            string path=Root+"/Meshes/LegacyTrim"+changed+".asset";changed++;
            SaveAsset(mesh,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {
            var bounds=TownResidentialPlacement.ColliderBounds(collider);
            if(bounds.max.y<.25f)continue;
            if(AllInside(OldRow,bounds.min,bounds.max))
            {
                var box=collider as BoxCollider;
                if(box!=null&&bounds.size.x>30)
                {
                    Vector3 center=bounds.center;center.x=(-101-83)/2f;
                    box.center=box.transform.InverseTransformPoint(center);box.size=new Vector3(18,box.size.y,box.size.z);continue;
                }
                if(bounds.max.x<=-83)continue;
                if(bounds.min.x<-83)throw new InvalidOperationException("Unexpected collider straddles retained shops: "+collider.name);
                Object.DestroyImmediate(collider);continue;
            }
            if(AllInside(OldDepot,bounds.min,bounds.max)&&bounds.min.z>.02f&&bounds.max.z<59.8f||AllInside(Site,bounds.min,bounds.max)||AllInside(Permissions[2],bounds.min,bounds.max))Object.DestroyImmediate(collider);
        }
        // Close the preserved row's freshly exposed east gable; original tea/book shop facades stay intact.
        var cap=new TownMeshBuilder();cap.Box(new Vector3(-83.025f,2.1f,-166),new Vector3(.05f,4.2f,10),TownColor.Cream);
        var capObject=Child(town.transform,"Retained tea and book shop end wall",Vector3.zero);
        SaveAsset(cap.Build("Retained shop gable"),Root+"/Meshes/RetainedShopsGable.asset");
        capObject.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/RetainedShopsGable.asset");
        capObject.gameObject.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        capObject.gameObject.isStatic=true;
        audit.Add("Scoped removal: "+removed+" triangles edited in "+changed+" copied meshes. Original mesh assets never overwritten.");
    }
    private void ReuseFormerDepot()
    {
        // The obsolete bread base becomes a public pocket garden, not a second working bread supplier.
        var park=Child(town.transform,"Former bakery — pocket garden",Vector3.zero);
        var d=new BakeryDraft();d.Mesh.Ground(-252,33,50,42,.081f,TownBakeryModels.Color(TownBakeryModels.Paint.Leaf));
        d.Mesh.Ground(-252,33,3,42,.084f,TownBakeryModels.Color(TownBakeryModels.Paint.Pavement));
        d.Mesh.Ground(-252,36,50,3,.086f,TownBakeryModels.Color(TownBakeryModels.Paint.Pavement));
        var geometry=Child(park,"Garden surfaces",Vector3.zero);SaveAsset(d.Mesh.Build("Former depot garden"),Root+"/Meshes/FormerDepotGarden.asset");
        geometry.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/FormerDepotGarden.asset");
        geometry.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;geometry.gameObject.isStatic=true;
        foreach(float x in new[]{-268f,-259f,-245f,-236f})
            foreach(float z in new[]{23f,45f}){Place(park,"GardenBed",new Vector3(x,.09f,z));Place(park,"TopiaryPlanter",new Vector3(x,.09f,z+3));}
        foreach(float z in new[]{18f,50f})Place(park,"StreetLantern",new Vector3(-254,.08f,z));
        var cafe=ExistingScenery(park,"Assets/Art/TownDetails/Prefabs/Buildings/CornerCafe.prefab",new Vector3(-252,.07f,10.5f),180,1);
        cafe.gameObject.AddComponent<BoxCollider>().center=Vector3.up*3.1f;cafe.GetComponent<BoxCollider>().size=new Vector3(24,6.2f,14);
        foreach(float x in new[]{-270f,-261f,-243f,-234f})
            foreach(float z in new[]{26f,47f})
            {
                var tree=ExistingScenery(park,"Assets/Art/TownKit/Prefabs/Nature/TreeRound.prefab",new Vector3(x,.09f,z),0,1.3f);
                var trunk=tree.gameObject.AddComponent<CapsuleCollider>();trunk.center=Vector3.up*.9f;trunk.height=1.8f;trunk.radius=.2f;
            }
        foreach(float x in new[]{-256f,-248f})foreach(float z in new[]{27f,39f,49f})
            ExistingScenery(park,"Assets/Art/TownKit/Prefabs/Street/Bench.prefab",new Vector3(x,.09f,z),x<-252?90:270,.6f);
        Combine(park,"Garden details","FormerDepotDetails");
        audit.Add("Obsolete bread base removed; its western parcel reused as a pocket garden. The north shared boundary remains unchanged.");
    }
    private static Transform ExistingScenery(Transform parent,string path,Vector3 position,float yaw,float scale)
    {
        var prototype=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prototype==null)throw new InvalidOperationException("Existing scenery model missing: "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prototype);PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent,false);instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);instance.transform.localScale=Vector3.one*scale;
        var surface=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>()){renderer.sharedMaterial=surface;renderer.gameObject.isStatic=true;}
        return instance.transform;
    }

    private void Combine(Transform root,string name,string assetId)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>(true);
        int groupIndex=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            if(group.Key==null)throw new InvalidOperationException("Bakery material is missing.");
            var parts=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            string path=Root+"/Meshes/"+assetId+groupIndex+".asset";SaveAsset(mesh,path);
            var geometry=Child(root,name+" "+groupIndex,Vector3.zero);geometry.gameObject.isStatic=true;
            geometry.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer=geometry.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
            if(group.Key.shader.name=="Universal Render Pipeline/Bakery Lettering"||group.Key.HasProperty("_Surface")&&group.Key.GetFloat("_Surface")>.5f)renderer.shadowCastingMode=ShadowCastingMode.Off;
            groupIndex++;
        }
        foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
    }

    private Transform Place(Transform parent,string id,Vector3 p,float yaw=0)
    {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);
        PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent,false);instance.transform.localPosition=p;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);return instance.transform;
    }
    internal static Transform Child(Transform parent,string name,Vector3 p)
    {var result=new GameObject(name).transform;result.SetParent(parent,false);result.localPosition=p;return result;}
    private static bool AllInside(Rect r,params Vector3[] points)=>points.All(p=>p.x>=r.xMin-.0001f&&p.x<=r.xMax+.0001f&&p.z>=r.yMin-.0001f&&p.z<=r.yMax+.0001f);
    private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    internal static void SaveAsset(Object asset,string path)
    {
        var existing=AssetDatabase.LoadMainAssetAtPath(path);
        if(existing==null)AssetDatabase.CreateAsset(asset,path);
        else
        {
            // CopySerialized alone does not invalidate Unity's native mesh/render buffers.
            // During an incremental edit that leaves old arrows visible and may combine
            // stale indices into a new complex, despite apparently updated asset YAML.
            if(asset is Mesh source&&existing is Mesh target)
            {
                target.Clear(false);target.indexFormat=source.indexFormat;target.name=source.name;
                target.vertices=source.vertices;target.normals=source.normals;
                target.uv=source.uv;target.uv2=source.uv2;
                if(source.colors.Length>0)target.colors=source.colors;
                if(source.tangents.Length>0)target.tangents=source.tangents;
                target.subMeshCount=source.subMeshCount;
                for(int sub=0;sub<source.subMeshCount;sub++)target.SetIndices(source.GetIndices(sub),source.GetTopology(sub),sub,false);
                target.bounds=source.bounds;target.UploadMeshData(false);
            }
            else if(asset is Texture2D texture&&existing is Texture2D saved)
            {
                saved.Reinitialize(texture.width,texture.height,texture.format,false);
                saved.SetPixels32(texture.GetPixels32());saved.Apply(false,false);
                saved.name=texture.name;saved.filterMode=texture.filterMode;saved.wrapMode=texture.wrapMode;
            }
            else EditorUtility.CopySerialized(asset,existing);
            EditorUtility.SetDirty(existing);Object.DestroyImmediate(asset);
        }
    }

    /// <summary>Copies a shared native mesh while removing selected complete triangles; all kept attributes remain exact.</summary>
    internal static Mesh CopyWithoutTriangles(Mesh source,Transform pose,Func<Vector3,Vector3,Vector3,bool> remove,out int removed)
    {
        var world=source.vertices.Select(pose.TransformPoint).ToArray();var kept=new List<int>[source.subMeshCount];var clipped=new List<ClipTriangle>[source.subMeshCount];removed=0;
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            kept[sub]=new List<int>();clipped[sub]=new List<ClipTriangle>();var indices=source.GetTriangles(sub);
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                if(remove(world[a],world[b],world[c])){removed++;continue;}
                kept[sub].Add(a);kept[sub].Add(b);kept[sub].Add(c);
            }
        }
        return removed==0?null:BuildTrimmed(source,kept,clipped,ClipVertex.Read(source,world),pose);
    }

    private struct ClipVertex
    {
        public Vector3 P,N;public Vector2 UV,UV2;public Color Color;public Vector4 Tangent;
        public static ClipVertex[] Read(Mesh mesh,Vector3[] world)
        {
            var n=mesh.normals;var uv=mesh.uv;var uv2=mesh.uv2;var colors=mesh.colors;var tangents=mesh.tangents;
            var result=new ClipVertex[world.Length];
            for(int i=0;i<world.Length;i++)result[i]=new ClipVertex{P=world[i],N=n[i],UV=uv[i],UV2=uv2.Length==world.Length?uv2[i]:Vector2.zero,
                Color=colors.Length==world.Length?colors[i]:UnityEngine.Color.white,Tangent=tangents.Length==world.Length?tangents[i]:Vector4.zero};
            return result;
        }
        public static ClipVertex Lerp(ClipVertex a,ClipVertex b,float t)=>new(){P=Vector3.Lerp(a.P,b.P,t),N=Vector3.Lerp(a.N,b.N,t).normalized,UV=Vector2.Lerp(a.UV,b.UV,t),UV2=Vector2.Lerp(a.UV2,b.UV2,t),Color=UnityEngine.Color.Lerp(a.Color,b.Color,t),Tangent=Vector4.Lerp(a.Tangent,b.Tangent,t)};
    }
    private readonly struct ClipTriangle
    {public readonly ClipVertex A,B,C;public ClipTriangle(ClipVertex a,ClipVertex b,ClipVertex c){A=a;B=b;C=c;}}
    private static List<ClipVertex> ClipLeft(List<ClipVertex> input,float cut)
    {
        var result=new List<ClipVertex>();
        for(int i=0;i<input.Count;i++)
        {
            var a=input[i];var b=input[(i+1)%input.Count];bool keepA=a.P.x<=cut,keepB=b.P.x<=cut;
            if(keepA)result.Add(a);
            if(keepA!=keepB)result.Add(ClipVertex.Lerp(a,b,(cut-a.P.x)/(b.P.x-a.P.x)));
        }
        return result;
    }
    private static Mesh BuildTrimmed(Mesh source,List<int>[] kept,List<ClipTriangle>[] clipped,ClipVertex[] attributes,Transform pose)
    {
        var points=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var colors=new List<Color>();var tangents=new List<Vector4>();
        var submeshes=new List<int>[kept.Length];var inverse=pose.worldToLocalMatrix;var originalPoints=source.vertices;
        // Unchanged vertices must keep their original local floats. A world/local
        // round trip on the rotated bakery introduces small coordinate changes to
        // neighbouring details, even when their geometry is not being edited.
        int Add(ClipVertex v,Vector3? original=null){int i=points.Count;points.Add(original??inverse.MultiplyPoint3x4(v.P));normals.Add(v.N);uv.Add(v.UV);uv2.Add(v.UV2);colors.Add(v.Color);tangents.Add(v.Tangent);return i;}
        for(int sub=0;sub<kept.Length;sub++)
        {
            submeshes[sub]=new List<int>();
            foreach(int index in kept[sub])submeshes[sub].Add(Add(attributes[index],originalPoints[index]));
            foreach(var tri in clipped[sub]){submeshes[sub].Add(Add(tri.A));submeshes[sub].Add(Add(tri.B));submeshes[sub].Add(Add(tri.C));}
        }
        var result=new Mesh{name="Bakery scoped copy of "+source.name,indexFormat=IndexFormat.UInt32};result.SetVertices(points);result.SetNormals(normals);result.SetUVs(0,uv);result.SetUVs(1,uv2);
        if(source.colors.Length==source.vertexCount)result.SetColors(colors);if(source.tangents.Length==source.vertexCount)result.SetTangents(tangents);
        result.subMeshCount=kept.Length;for(int sub=0;sub<kept.Length;sub++)result.SetTriangles(submeshes[sub],sub);
        result.RecalculateBounds();return result;
    }
}
