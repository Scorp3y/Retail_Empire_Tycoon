using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class TownParkingAndRailUpdate
{
    private const string MeshFolder = "Assets/Prefabs/TownWorld/AccessUpdateMeshes";
    private static readonly Vector3 FloristShift = new(65, .24f, 450);
    private static readonly Rect FloristPlot = new(143, 468, 36, 24);

    public static void TidyGardenCourt()
    {
        GameObject town = null;
        int exit = 0;
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            RebuildGardenCourt(town);
            PrefabUtility.SaveAsPrefabAsset(town, TownWorldAssembler.PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save Garden Court.");
            AssetDatabase.SaveAssets();
        }
        catch (Exception error) { Debug.LogException(error); exit = 1; }
        finally { if (town != null) PrefabUtility.UnloadPrefabContents(town); }
        EditorApplication.Exit(exit);
    }

    private static void RebuildGardenCourt(GameObject town)
    {
        if (town.transform.Find("Garden Court lawn and restored sidewalk") != null) return;
        var plot = TownFloristPlacement.Site;
        var house = town.transform.Find("Garden Court apartments");
        var oldWalk = town.transform.Find("Garden Court entrance walk");
        if (house == null || oldWalk == null) throw new InvalidOperationException("The former florist parcel is missing.");
        var filters = town.GetComponentsInChildren<MeshFilter>().Where(f =>
        {
            var renderer = f.GetComponent<MeshRenderer>();
            if (f.transform.IsChildOf(house) || f.transform == oldWalk || renderer == null) return false;
            var b = renderer.bounds;
            return b.min.y <= .22f && plot.Overlaps(new Rect(b.min.x,b.min.z,b.size.x,b.size.z));
        });
        var surfaces = new TownCityCorrectionsGeometry(filters);
        surfaces.ClipGround(plot,.22f,minimum:.006f);
        Directory.CreateDirectory(MeshFolder+"/Meshes");
        AssetDatabase.Refresh();
        int count = 0;
        foreach (var view in surfaces.Views.Where(v => v.Removed.Count > 0))
        {
            view.Save("GardenCourtGround_"+count++,MeshFolder);
            foreach (var collider in view.Filter.GetComponents<MeshCollider>())
                if (collider.sharedMesh == view.Source) collider.sharedMesh = view.Filter.sharedMesh;
        }
        if (count == 0) throw new InvalidOperationException("No obsolete pavement was found on the parcel.");
        var yard = new TownMeshBuilder();
        yard.Ground(plot.center.x,plot.center.y,plot.width,plot.height,.043f,TownColor.Grass);
        // Patch the old loading entrance at sidewalk height, outside the road and crossings.
        yard.Ground(77,37.7f,2,6,.041f,TownColor.Pavement);
        AddMesh(town.transform,"Garden Court lawn and restored sidewalk",yard,true);
    }

    public static void Review()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            const string output = "Docs/TownAccessUpdate";
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var town = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
            var site = town.transform.Find("Pinebrook/" + TownFloristPlacement.SiteName);
            var court = site.GetComponent<SupplierLoadingCourt>();
            using (var map = new TownMapData(town))
            {
                var navigation = new TownNavigation(map);
                navigation.Tick(new Vector3(-24,0,310),Vector3.forward,0,0);
                var point = map.Points.Single(p => p.Checkpoint == court.checkpoint);
                navigation.Select(point);
                if (navigation.State != TownNavigationState.Following ||
                    Vector3.Distance(navigation.PlannedRoute.Last(),map.ToNative(court.checkpoint.transform.position)) > .1f)
                    throw new InvalidOperationException("No connected GPS route to the relocated florist.");
            }
            Physics.SyncTransforms();
            var route = new[]{court.streetAccess,court.entrance,court.staging,court.checkpoint.transform.position};
            for (int i=1;i<route.Length;i++)
            for (int sample=0;sample<=20;sample++)
            {
                var p = Vector3.Lerp(route[i-1],route[i],sample/20f);
                var obstacles = Physics.OverlapBox(p+Vector3.up*1.1f,new Vector3(2.5f,.65f,1.35f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                if (obstacles.Any(c => c.transform.IsChildOf(town.transform)))
                    throw new InvalidOperationException($"Loading lane blocked at {p}: {string.Join(", ",obstacles.Select(c=>c.name))}.");
            }
            TownFloristBatch.Render(scene,output+"/PinebrookFlorist.png",new Vector3(116,22,449),new Vector3(157,1,480),0,1200,750);
            TownFloristBatch.Render(scene,output+"/GardenCourt.png",new Vector3(68,18,11),new Vector3(95,3,30),0,1200,750);
            var train = town.GetComponentInChildren<TownRailTraffic>();
            var points = town.layout.railway.points;
            float bridge = TownRailTraffic.Length(points.TakeWhile(p => p.x < 345).ToArray())+42;
            train.Advance((bridge-train.TravelDistance)/train.metresPerSecond);
            TownFloristBatch.Render(scene,output+"/RailBridge.png",new Vector3(293,23,203),new Vector3(326,3,229),0,1200,750);
            float length = TownRailTraffic.Length(points);
            train.Advance((length-70-train.TravelDistance)/train.metresPerSecond);
            if (!train.IsInService || train.cars.Any(car => car.localPosition.x < 854))
                throw new InvalidOperationException("The last car is not covered inside the tunnel.");
            TownFloristBatch.Render(scene,output+"/RailTunnel.png",new Vector3(808,18,472),new Vector3(865,4,488),0,1200,750);
            train.Advance(15);
            if (train.IsInService) throw new InvalidOperationException("The train did not end its trip inside the tunnel.");
            Debug.Log("PASS: florist GPS and 63 clearance samples; train passes bridge and disappears inside the tunnel.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void Run()
    {
        GameObject town = null;
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            if (town.transform.Find("Railway approach update") != null)
                throw new InvalidOperationException("The town update is already installed.");
            Directory.CreateDirectory(MeshFolder+"/Meshes");
            AssetDatabase.Refresh();
            RelocateFlorist(town);
            RepairRailway(town);
            PrefabUtility.SaveAsPrefabAsset(town, TownWorldAssembler.PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Failed to save the updated town.");
            AssetDatabase.SaveAssets();
            Debug.Log("Town update saved: florist, bridge, track and woodland portal.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally { if (town != null) PrefabUtility.UnloadPrefabContents(town); }
        EditorApplication.Exit(0);
    }

    private static void RelocateFlorist(GameObject town)
    {
        var florist = town.transform.Find(TownFloristPlacement.SiteName);
        var district = town.transform.Find("Pinebrook");
        var checkpoint = town.GetComponent<TownWorldPlaces>().suppliers.Single(p => p.supplier == SupplierKind.Decoration);
        var court = florist.GetComponent<SupplierLoadingCourt>();
        if (court.checkpoint != checkpoint || !checkpoint.transform.IsChildOf(florist))
            throw new InvalidOperationException("The florist and its original loading checkpoint are not linked.");

        var planting = district.GetComponentsInChildren<Transform>().Where(t =>
            (t.name.StartsWith("nature_", StringComparison.Ordinal) || t.name.StartsWith("native_MixedPlanting_", StringComparison.Ordinal))
            && FloristPlot.Contains(new Vector2(t.position.x, t.position.z))).ToArray();
        foreach (var plant in planting) Object.DestroyImmediate(plant.gameObject);
        ClearNativeTrees(town);

        florist.SetParent(district, true);
        florist.position += FloristShift;
        court.bounds = new Rect(court.bounds.position + new Vector2(FloristShift.x, FloristShift.z), court.bounds.size);
        court.streetAccess = new Vector3(136, 0, court.streetAccess.z + FloristShift.z);
        court.entrance += FloristShift;
        court.staging += FloristShift;
        court.approachWaypoints = court.approachWaypoints.Select(p => p + FloristShift).ToArray();

        var apron = new TownMeshBuilder();
        float lane = court.streetAccess.z;
        var walks = new TownCityCorrectionsGeometry(district.Find("Paths"));
        walks.ClipGround(new Rect(139.85f,lane-3,3.15f,6), .25f,
            (view, _) => view.Filter.name == "WalksAndSquaresUnion");
        foreach (var view in walks.Views.Where(v => v.Removed.Count > 0))
        {
            view.Save("FloristSidewalkCut", MeshFolder);
            foreach (var collider in view.Filter.GetComponents<MeshCollider>())
                if (collider.sharedMesh == view.Source) collider.sharedMesh = view.Filter.sharedMesh;
        }
        apron.Quad(new Vector3(139.85f,.02f,lane+3),new Vector3(143,.325f,lane+3),
            new Vector3(143,.325f,lane-3),new Vector3(139.85f,.02f,lane-3),TownColor.Asphalt);
        AddMesh(district, "Florist driveway connection", apron, true);
        // The old parcel's paving is baked into the street meshes and cannot move with the shop.
        var paving = new TownMeshBuilder();
        paving.Ground(161,480,36,24,.313f,TownColor.Pavement);
        paving.Ground(160,487.7f,34,6,.326f,TownColor.Asphalt);
        paving.Ground(145.7f,476.2f,5.4f,13,.326f,TownColor.Asphalt);
        paving.Ground(166.4f,485.5f,10.7f,11.8f,.326f,TownColor.Asphalt);
        foreach(float z in new[]{470.2f,475.6f,481.2f})
            paving.Ground(145.7f,z,5.1f,.055f,.334f,TownColor.White);
        var bay = checkpoint.transform.position;
        foreach(float z in new[]{485.55f,489.85f})
            paving.Ground(bay.x,z,6.8f,.07f,.335f,TownColor.Yellow);
        foreach(float x in new[]{164.9f,171.7f})
            paving.Ground(x,bay.z,.07f,4.37f,.335f,TownColor.Yellow);
        AddMesh(district,"Florist forecourt paving",paving);

        var housing = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Buildings/ApartmentSmall.prefab");
        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(housing, town.transform);
        replacement.name = "Garden Court apartments";
        replacement.transform.SetPositionAndRotation(new Vector3(95,.04f,30), Quaternion.Euler(0,270,0));
        foreach (var renderer in replacement.GetComponentsInChildren<MeshRenderer>())
            renderer.sharedMaterial = Palette();
        var bounds = replacement.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
        if (!TownFloristPlacement.Site.Contains(new Vector2(bounds.min.x,bounds.min.z)) ||
            !TownFloristPlacement.Site.Contains(new Vector2(bounds.max.x,bounds.max.z)))
            throw new InvalidOperationException("Replacement building exceeds the former florist parcel.");
        var sidewalk = new TownMeshBuilder();
        sidewalk.Ground((78+bounds.min.x)*.5f,30,bounds.min.x-78,3,.055f,TownColor.Pavement);
        foreach(float z in new[]{23f,37f})
        {
            sidewalk.Box(new Vector3(86,.07f,z),new Vector3(9,.1f,3),TownColor.Stone);
            sidewalk.Box(new Vector3(86,.125f,z),new Vector3(8.7f,.05f,2.7f),TownColor.Grass);
            PlacePine(town.transform, "Garden Court tree",new Vector3(86,.15f,z),.65f);
        }
        AddMesh(town.transform, "Garden Court entrance walk", sidewalk);
        if (!replacement.GetComponentsInChildren<Collider>().Any())
        {
            var box = replacement.AddComponent<BoxCollider>();
            box.center = replacement.transform.InverseTransformPoint(bounds.center);
            box.size = replacement.transform.InverseTransformDirection(bounds.size);
            box.size = new Vector3(Mathf.Abs(box.size.x),Mathf.Abs(box.size.y),Mathf.Abs(box.size.z));
        }
        RebuildGardenCourt(town);
    }

    private static void ClearNativeTrees(GameObject town)
    {
        var trees = town.GetComponentsInChildren<Transform>().Where(t =>
            (t.name == "Pine" || t.name == "TreeRound" || t.name == "TreeWide") &&
            new Rect(141,465,41,30).Contains(new Vector2(t.position.x,t.position.z))).ToArray();
        if (trees.Length == 0) return;
        var triangles = new HashSet<(Vector3Int,Vector3Int,Vector3Int)>();
        Mesh NativeMesh(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Art/TownKit/Prefabs/Nature/{name}.prefab").GetComponent<MeshFilter>().sharedMesh;
        var bush = NativeMesh("BushRound");
        foreach (var tree in trees)
        {
            IndexTriangles(triangles,NativeMesh(tree.name),tree.localToWorldMatrix);
            IndexTriangles(triangles,bush,Matrix4x4.TRS(tree.position+new Vector3(2,0,-2),Quaternion.identity,Vector3.one*1.2f));
            Object.DestroyImmediate(tree.gameObject);
        }
        int count = TrimGeometry(town,triangles,new Rect(130,455,64,53),"FloristTreeClearance");
        if (count == 0) throw new InvalidOperationException("Florist woodland clearance did not find the batched trees.");
    }

    private static void RepairRailway(GameObject town)
    {
        var layout = town.GetComponent<TownWorldPlaces>().layout;
        var original = layout.railway.points;
        int bank = Array.FindIndex(original,p => p.x == 345 && p.z == 228);
        if (bank < 0 || original.Length < bank+11) throw new InvalidOperationException("Unexpected original railway alignment.");

        var old = TownWorldAssembler.RailGeometry(original.Skip(bank).ToArray(), false);
        var removedGeometry = new HashSet<(Vector3Int,Vector3Int,Vector3Int)>();
        Index(old.rails); Index(old.ballast); Index(TownWorldAssembler.LegacyRailBridge());
        void Index(TownMeshBuilder draft)
        {
            var mesh = draft.Build("Legacy alignment");
            IndexTriangles(removedGeometry,mesh,Matrix4x4.identity);
            Object.DestroyImmediate(mesh);
        }

        int removed = TrimGeometry(town,removedGeometry,new Rect(298,224,443,274),"RailClearance");
        if (removed < removedGeometry.Count * .9f)
            throw new InvalidOperationException($"The authored rail did not match the saved mesh: {removed}/{removedGeometry.Count}.");

        var route = original.Take(bank+1).ToList();
        int rejoin = bank+10;
        var a = route.Last();
        var d = original[rejoin];
        var b = a + Vector3.right*22;
        var c = d - (original[rejoin+1]-original[rejoin-1]).normalized*24;
        for (int i=1;i<=30;i++)
        {
            float t=i/30f,u=1-t;
            route.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);
        }
        route.AddRange(original.Skip(rejoin+1));
        route.Add(new Vector3(825,2.2f,488));
        route.Add(new Vector3(915,2.2f,488));
        route.Add(new Vector3(970,2.2f,488));
        layout.railway.points = route.ToArray();
        EditorUtility.SetDirty(layout);

        var group = TownBakeryPlacement.Child(town.transform,"Railway approach update",Vector3.zero);
        var (rails, ballast) = TownWorldAssembler.RailGeometry(route.Skip(bank).ToArray(),false);
        AddMesh(group,"Eased railway approach and extension",rails);
        AddMesh(group,"Continuous ballast and embankment",ballast);
        var bridge = new TownMeshBuilder();
        bridge.Box(new Vector3(322,3.91f,228),new Vector3(46,.61f,5.3f),TownColor.Stone);
        foreach (float x in new[]{301f,343f})
            bridge.Box(new Vector3(x,-.1f,228),new Vector3(4,7.55f,6.2f),TownColor.Stone);
        for (int i=0;i<10;i++)
            bridge.Box(new Vector3(301.3f+i*4.6f,3.46f,228),new Vector3(.27f,.35f,5.5f),TownColor.Frame);
        foreach (float z in new[]{225.48f,230.52f})
        {
            bridge.Box(new Vector3(322,4.35f,z),new Vector3(46,.22f,.22f),TownColor.Frame);
            bridge.Box(new Vector3(322,6.15f,z),new Vector3(46,.15f,.16f),TownColor.Frame);
            for (int i=0;i<=10;i++)
                bridge.Box(new Vector3(299+i*4.6f,5.25f,z),new Vector3(.15f,1.86f,.15f),TownColor.Frame);
            for (int i=0;i<10;i++)
            {
                var start = new Vector3(299+i*4.6f,i%2==0?4.35f:6.15f,z);
                var end = new Vector3(start.x+4.6f,i%2==0?6.15f:4.35f,z);
                bridge.Box((start+end)*.5f,new Vector3(.13f,.14f,Vector3.Distance(start,end)),TownColor.Frame,Quaternion.LookRotation(end-start));
            }
        }
        AddMesh(group,"Railway river bridge and abutments",bridge);

        var portal = new TownMeshBuilder();
        portal.Ground(886,488,308,82,.012f,TownColor.Grass);
        // A covered corridor hides every carriage before the service is recycled.
        portal.Box(new Vector3(950,3.4f,484.7f),new Vector3(191,6.8f,1.4f),TownColor.Stone);
        portal.Box(new Vector3(950,3.4f,491.3f),new Vector3(191,6.8f,1.4f),TownColor.Stone);
        portal.Box(new Vector3(950,7.3f,488),new Vector3(191,1.4f,8),TownColor.Stone);
        portal.Box(new Vector3(1045,4,488),new Vector3(1,8,8),TownColor.Frame);
        foreach (float side in new[]{-1f,1f})
        {
            float inner = 488+side*4, outer = 488+side*24;
            var nearTop = new Vector3(854,8,inner);
            var farTop = new Vector3(1045,8,inner);
            var farFoot = new Vector3(1045,0,outer);
            var nearFoot = new Vector3(839,0,outer);
            if (side > 0) portal.Quad(nearTop,nearFoot,farFoot,farTop,TownColor.LeafLight);
            else portal.Quad(nearTop,farTop,farFoot,nearFoot,TownColor.LeafLight);
        }
        portal.Ground(950,488,191,8,8.005f,TownColor.Leaf);
        AddMesh(group,"Woodland railway tunnel",portal);
        for (int i=0;i<13;i++)
        {
            float x = 766+i*21;
            float edge = 18+(i*13)%18;
            PlacePine(group,"Railway woodland",new Vector3(x,.02f,488+edge),1.6f+(i%3)*.3f);
            PlacePine(group,"Railway woodland",new Vector3(x+7,.02f,488-edge),1.5f+(i%4)*.25f);
        }
        for (int i=0;i<8;i++)
            PlacePine(group,"Tunnel ridge woodland",new Vector3(865+i*23,8.01f,488+(i%3-1)*2),1.35f+(i%3)*.2f);
        if (TownRailTraffic.Length(layout.railway.points) <= TownRailTraffic.Length(original)+220)
            throw new InvalidOperationException("The new route must extend more than 220 metres.");
        Debug.Log($"Replaced {removed} old rail and bridge triangles without removing nearby scenery.");
    }

    private static void IndexTriangles(HashSet<(Vector3Int,Vector3Int,Vector3Int)> target,Mesh mesh,Matrix4x4 pose)
    {
        var vertices = mesh.vertices.Select(pose.MultiplyPoint3x4).ToArray();
        var indices = mesh.triangles;
        for (int i=0;i<indices.Length;i+=3)
            target.Add(Key(vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]));
    }

    private static int TrimGeometry(GameObject town,HashSet<(Vector3Int,Vector3Int,Vector3Int)> triangles,Rect area,string name)
    {
        int removed = 0, copies = 0;
        var palette = Palette();
        foreach (var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null || renderer.sharedMaterial != palette) continue;
            var bounds = renderer.bounds;
            if (!area.Overlaps(new Rect(bounds.min.x,bounds.min.z,bounds.size.x,bounds.size.z))) continue;
            var original = filter.sharedMesh;
            var revised = TownBakeryPlacement.CopyWithoutTriangles(original, filter.transform,
                (a,b,c) => triangles.Contains(Key(a,b,c)),out int count);
            if (revised == null) continue;
            removed += count;
            string path = $"{MeshFolder}/{name}_{copies++}.asset";
            TownBakeryPlacement.SaveAsset(revised,path);
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            foreach (var collider in filter.GetComponents<MeshCollider>())
                if (collider.sharedMesh == original) collider.sharedMesh = filter.sharedMesh;
        }
        return removed;
    }

    private static (Vector3Int,Vector3Int,Vector3Int) Key(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3Int Quantize(Vector3 p) => Vector3Int.RoundToInt(p*1000);
        int Compare(Vector3Int p,Vector3Int q) => (p.x,p.y,p.z).CompareTo((q.x,q.y,q.z));
        var x=Quantize(a); var y=Quantize(b); var z=Quantize(c);
        if (Compare(x,y)>0) (x,y)=(y,x);
        if (Compare(y,z)>0) (y,z)=(z,y);
        if (Compare(x,y)>0) (x,y)=(y,x);
        return (x,y,z);
    }

    private static Material Palette() =>
        AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");

    private static void PlacePine(Transform parent,string name,Vector3 position,float scale)
    {
        var pine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/Pine.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(pine,parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0,position.x*17,0);
        instance.transform.localScale = Vector3.one*scale;
        instance.GetComponent<MeshRenderer>().sharedMaterial = Palette();
    }

    private static void AddMesh(Transform parent,string name,TownMeshBuilder draft,bool collision=false)
    {
        var mesh = draft.Build(name);
        string path = $"{MeshFolder}/{name.Replace(' ','_')}.asset";
        TownBakeryPlacement.SaveAsset(mesh,path);
        var obj = TownBakeryPlacement.Child(parent,name,Vector3.zero).gameObject;
        obj.isStatic = true;
        obj.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        obj.AddComponent<MeshRenderer>().sharedMaterial = Palette();
        if (collision) obj.AddComponent<MeshCollider>().sharedMesh = obj.GetComponent<MeshFilter>().sharedMesh;
    }
}
