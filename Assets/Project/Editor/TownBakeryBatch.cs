using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Build, saved-asset audit and genuine Unity camera evidence, only in the disposable clone.</summary>
public static class TownBakeryBatch
{
    private const string Report="Library/TownBakeryQA";
    private const string Pictures="Docs/TownBakeryReview";
    public static void Generate()
    {Generate(false);}
    public static void Revise()
    {Generate(true);}
    public static void RemoveDetails()
    {Generate(true,true);}
    /// <summary>Fresh-process cameras ensure evidence is not based on cached pre-edit render data.</summary>
    public static void Review()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();revision=true;Capture("After");Validate();
            File.WriteAllText(Report+"/review.txt","PASS: revised saved town reopened and captured in a fresh native Unity process.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error)
        {File.WriteAllText(Report+"/review.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static bool revision;
    private static Vector3 House=>revision?TownBakeryPlacement.RevisedPoint(TownBakeryPlacement.HousePosition):TownBakeryPlacement.HousePosition;
    private static Vector3 Bay=>revision?TownBakeryPlacement.RevisedPoint(TownBakeryPlacement.Bay):TownBakeryPlacement.Bay;
    private static Vector3 Heading=>revision?Vector3.back:Vector3.forward;
    private static void Generate(bool revise,bool removeDetails=false)
    {
        try
        {
            revision=revise;
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
            File.WriteAllText(Report+"/result.txt","RUNNING\n");
            Capture("Before");
            var permissions=removeDetails?TownBakeryPlacement.DetailPermissions:revision?TownBakeryPlacement.RevisionPermissions:TownBakeryPlacement.Permissions;
            var outside=TownPilotScopePreservation.Capture(permissions);
            string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            // The legacy helper's 16 m frontage margin reaches into the previous bread yard.
            // Protect Decoration from its actual southern frontage; the authorised former bread
            // garage extends to z=58.25 and must not be mistaken for Decoration's geometry.
            var decorationArea=new Rect(-282,59.8f,70,62.2f);
            suppliers[SupplierKind.Decoration]=TownFarmPreservation.Capture(decorationArea,SupplierKind.Decoration);
            TownCourtyardValidation.ResidentialSnapshot homes;
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                if(removeDetails)new TownBakeryPlacement(root).RemoveDetails();
                else if(revision)new TownBakeryPlacement(root).Revise();
                else new TownBakeryPlacement(root).Build();
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Capture("After");
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(permissions));
            Require(TownFarmPreservation.Capture()==farm,"Farm changed.");
            var current=TownSupplierPreservation.Capture();
            current[SupplierKind.Decoration]=TownFarmPreservation.Capture(decorationArea,SupplierKind.Decoration);
            foreach(var supplier in suppliers.Where(p=>p.Key!=SupplierKind.Bakery))
                Require(current.TryGetValue(supplier.Key,out var hash)&&hash==supplier.Value,"Protected supplier changed: "+supplier.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: saved geometry, materials and colliders outside the authorised parcels unchanged.\n"+
                "Farm and all other suppliers unchanged. Original 62 residential buildings and 299 door routes unchanged.\n"+outside+"\n");
            Validate();
            File.WriteAllText(Report+"/result.txt",removeDetails?"BAKERY PASSED: blank rear panel and local bus shelter removed; canopy, shutter, public pavement and functional pickup preserved; exact outside-scope protection passed.\n":"BAKERY PASSED: 10 native assets, urban storefront and rear pickup collection, obsolete depot removed, scoped protection and saved route checks.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error)
        {Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>(true)).Single();
        var checkpoint=town.suppliers.Single(c=>c.supplier==SupplierKind.Bakery);
        Require(Vector3.Distance(checkpoint.transform.position,Bay)<.001f,"Bread checkpoint did not move to the new yard.");
        Require(checkpoint.requiresRearDocking&&Vector3.Dot(checkpoint.transform.forward,Heading)>.999f,"Bay heading must point away from rear goods door.");
        Require(town.GetComponentsInChildren<DeliveryCheckpoint>(true).Count(c=>c.supplier==SupplierKind.Bakery)==1,"Duplicate bread supplier.");
        var court=town.GetComponentsInChildren<SupplierLoadingCourt>().Single(c=>c.checkpoint==checkpoint);
        Require(court.designVehicleLength==6.8f&&court.approachWaypoints.Length==1,"Compact bakery approach contract missing.");
        Physics.SyncTransforms();
        var route=new[]{court.streetAccess,court.entrance}.Concat(court.approachWaypoints).Concat(new[]{court.staging,checkpoint.transform.position}).ToArray();
        int probes=0;
        for(int leg=1;leg<route.Length;leg++)
        {
            var a=route[leg-1];var b=route[leg];var direction=Vector3.ProjectOnPlane(b-a,Vector3.up);
            Require(direction.sqrMagnitude>.1f,"Empty access leg.");
            int steps=Mathf.CeilToInt(direction.magnitude*2);
            for(int sample=0;sample<=steps;sample++)
            {
                var p=Vector3.Lerp(a,b,(float)sample/steps);probes++;
                var hits=Physics.OverlapBox(new Vector3(p.x,1.1f,p.z),new Vector3(1.35f,.65f,2.5f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                Require(hits.Length==0,"Bakery pickup route blocked at "+p+": "+string.Join(", ",hits.Select(c=>c.name)));
                var floor=Physics.RaycastAll(new Vector3(p.x,4,p.z),Vector3.down,5,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y<.25f).ToArray();
                Require(floor.Length>0,"Route has no existing physical floor at "+p);
            }
        }
        if(revision)
        {
            var paving=AssetDatabase.LoadAssetAtPath<Mesh>(TownBakeryPlacement.Root+"/Meshes/SitePaving.asset");
            var white=RetailEmpireTycoon.Editor.TownKit.TownBakeryModels.UV(RetailEmpireTycoon.Editor.TownKit.TownBakeryModels.Color(RetailEmpireTycoon.Editor.TownKit.TownBakeryModels.Paint.White));
            Require(paving.uv.All(uv=>Vector2.Distance(uv,white)>.001f),"White driveway arrows remain in the saved paving mesh.");
            var prototype=AssetDatabase.LoadAssetAtPath<GameObject>(TownBakeryPlacement.Root+"/Prefabs/BakeryHouse.prefab");
            Require(prototype.transform.Find("BakeryName")!=null&&prototype.transform.Find("RearCollection")==null,"Requested building signage changes missing.");
            Require(town.transform.Find("Mixed low-rise streets/NeighbourhoodShops")==null,"Marked tea/book shops still exist.");
            Require(town.transform.Find("Mixed low-rise streets/GaragePair")==null,"Marked garages still exist.");
            var house=town.transform.Find(TownBakeryPlacement.SiteName+"/BakeryHouse");
            Require(Vector3.Distance(house.position,House)<.001f&&Vector3.Dot(house.forward,Vector3.forward)>.999f,"Bakery did not turn 180 degrees.");
            var shelter=town.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="BusShelter"&&Mathf.Abs(t.position.x+80)<.1f&&Mathf.Abs(t.position.z+151.5f)<.1f);
            if(shelter!=null)Require(Vector3.Dot(shelter.forward,Vector3.forward)>.999f,"Local shelter did not turn 180 degrees.");
            else TownBakeryPlacement.RequireDetailsRemoved(town.gameObject);
            // Audit retained ground separately from shadowed camera pixels: a tall building's
            // shadow is not an obsolete garage apron. New pavement must have replaced its mesh.
            var apron=new Rect(-56,-186,16,25.5f);int asphaltTriangles=0;
            var asphalt=RetailEmpireTycoon.Editor.TownKit.TownPalette.UV(RetailEmpireTycoon.Editor.TownKit.TownColor.Asphalt);
            foreach(var filter in town.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.transform.IsChildOf(house.parent))continue;
                var mesh=filter.sharedMesh;var p=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();var uv=mesh.uv;
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    if(p[a].y<.25f&&p[b].y<.25f&&p[c].y<.25f&&apron.Contains(new Vector2(p[a].x,p[a].z))&&apron.Contains(new Vector2(p[b].x,p[b].z))&&apron.Contains(new Vector2(p[c].x,p[c].z))&&Vector2.Distance(uv[a],asphalt)<.001f)asphaltTriangles++;
                }
            }
            Require(asphaltTriangles==0,"Saved obsolete garage asphalt remains: "+asphaltTriangles+" triangles.");
            // The former shop wall may no longer substitute for the fence.
            for(float z=-185;z<-166;z+=.4f)
                Require(Physics.OverlapBox(new Vector3(-69.1f,1,z),new Vector3(.15f,.3f,.22f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c.transform.IsChildOf(house.parent)),"Continuous fence gap at "+z);
        }
        else
        {
            var box=town.transform.Find("Mixed low-rise streets/NeighbourhoodShops").GetComponent<BoxCollider>();
            Require(Mathf.Abs(box.bounds.max.x+83)<.01f&&Mathf.Abs(box.bounds.min.x+101)<.01f,"Tea/book shop collision did not match retained half.");
            Require(town.transform.Find("Mixed low-rise streets/GaragePair")!=null,"Neighbouring garages were removed.");
        }
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" saved-scene pickup corridor and physical floor samples; one functional bakery checkpoint; "+(revision?"marked legacy shops/garage removed, local shelter turned or removed, fence closed":"retained tea/book shops and garage")+".\n"+
            "Design vehicle: pickup / short van, 6.8 m; no claim of articulated truck support. Live driving is tested separately.\n");
    }
    private static void Capture(string phase)
    {
        string directory=Pictures+"/"+phase;Directory.CreateDirectory(directory);
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath,OpenSceneMode.Single);
        if(phase=="After")
        {
            // Pose the REAL player pickup for visual scale only; no scene or spawn point is saved.
            var city=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CityWorld>(true)).Single();
            var car=city.pickup.transform;car.SetPositionAndRotation(Bay,Quaternion.LookRotation(Heading));
            var bodies=car.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();
            if(bodies.Length>0)car.position+=Vector3.up*(.087f-bodies.Min(b=>b.min.y));
        }
        var focus=House;
        Render(scene,directory+"/Facade.png",focus+new Vector3(19,13,revision?25:-25),focus+Vector3.up*3.9f,0);
        Render(scene,directory+"/RearYard.png",focus+new Vector3(20,15,revision?-28:28),new Vector3(-75,3,-170),0);
        Render(scene,directory+"/Top.png",new Vector3(-74,85,-169),new Vector3(-74,0,-169),26);
        if(phase=="After"&&revision)
        {
            // A separate diagnostic view distinguishes pavement geometry from long
            // shadows cast by surrounding blocks. Never change saved scene lighting.
            var lights=Object.FindObjectsOfType<Light>().Where(l=>l.type==LightType.Directional).ToArray();
            var shadows=lights.Select(l=>l.shadows).ToArray();
            try
            {
                foreach(var light in lights)light.shadows=LightShadows.None;
                Render(scene,directory+"/TopGeometry.png",new Vector3(-74,85,-169),new Vector3(-74,0,-169),26);
            }
            finally{for(int i=0;i<lights.Length;i++)lights[i].shadows=shadows[i];}
        }
        Render(scene,directory+"/CityContext.png",new Vector3(-35,55,-225),new Vector3(-76,1,-169),0);
        Render(scene,directory+"/FormerDepot.png",new Vector3(-210,37,-10),new Vector3(-252,0,32),0);
        File.WriteAllText(directory+"/complete.txt","PASS: five native Unity 2022.3 cameras, saved City scene. These are actual model renders, not AI concepts.\n");
    }
    private static void Render(Scene scene,string path,Vector3 position,Vector3 target,float ortho)
    {
        var go=new GameObject("Bakery review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.GetComponent<Camera>();camera.enabled=false;camera.transform.position=position;camera.transform.LookAt(target);
        camera.fieldOfView=50;camera.nearClipPlane=.1f;camera.farClipPlane=1500;camera.orthographic=ortho>0;camera.orthographicSize=ortho;
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var texture=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;Texture2D png=null;
        try
        {
            Require(texture.Create(),"Native review render target unavailable.");camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
            png=new Texture2D(1600,1000,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();
            File.WriteAllBytes(path,png.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;texture.Release();Object.DestroyImmediate(texture);if(png!=null)Object.DestroyImmediate(png);Object.DestroyImmediate(go);}
    }
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
}
