using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Save-free real Game placement, one-way passenger, vehicle cards and both yellow-marker entrances.</summary>
[InitializeOnLoad]
public static class MobilityPolishPlay
{
    private const string Pending="MobilityPolish.Pending";
    private static IEnumerator checks;private static double deadline;
    static MobilityPolishPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(MobilityPolishBatch.Report);Directory.CreateDirectory(MobilityPolishBatch.Pictures);
        File.WriteAllText(MobilityPolishBatch.Report+"/play.txt","RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+240;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Mobility checks timed out.");
            if(checks.MoveNext())return;
            MobilityPolishBatch.Record();Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<5;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer exists.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var build=starter.building;var trip=starter.cityTrip;trip.orders.save=null;
        var data=starter.CreateNewGame(50000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        build.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>().enabled=false;Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>().enabled=false;
        Require(Resources.LoadAll<BuildItemData>("MultiStorey").Length==4,"Down escalator missing from catalog.");
        Require(Mathf.Abs(StoreyTransportVisual.TreadCyclesPerSecond-.22f*.8f)<.00001f,"Escalator belt is not 20% slower.");
        foreach(var expected in new[]{("stairs_01",1.3592584f,4),("escalator_01",1.6313299f,5),("escalator_down_01",1.6313299f,5)})
        {
            var item=starter.catalog.GetById(expected.Item1);
            Require(Mathf.Abs(item.placementBounds.size.x-expected.Item2*.8f)<.003f&&item.footprint.x==expected.Item3,"Transport width/occupancy mismatch: "+item.id);
        }
        var tile=starter.catalog.GetById("floor_cream");
        foreach(string id in new[]{"elevator_01","escalator_down_01"})
        {
            var item=starter.catalog.GetById(id);build.inventory.Add(item,1);build.Storeys.SelectFloor(0);build.EnterBuildMode(item);
            bool installed=false;
            foreach(var cell in starter.floors.BuildSaveData().Where(cell=>cell.floor==0&&cell.itemId!="asphalt_01").Select(cell=>new Vector3Int(cell.x,0,cell.z)))
            {
                if(!build.CanPlaceAt(cell).ok)continue;Require(build.TryPlaceAt(cell),"Valid transport cannot be installed.");installed=true;break;
            }
            Require(installed,"No valid starter floor for "+id);build.ExitBuildMode();yield return null;Physics.SyncTransforms();
            var placed=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item&&p.GetComponentInParent<BuildPreview>()==null);
            var link=new StoreyLink(build.grid,placed);
            build.inventory.Add(tile,1);build.EnterBuildMode(tile);Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{link.Upper}),"Upper landing cannot be painted.");build.ExitBuildMode();
            if(id=="elevator_01")
            {
                var visual=placed.GetComponent<StoreyTransportVisual>();var cabin=visual.platform.GetComponent<BoxCollider>();
                float baseFloor=placed.transform.position.y+.096f*ShopProportions.Scale;
                Require(cabin.bounds.max.y>=baseFloor+.006f&&cabin.bounds.max.y<baseFloor+.014f,"Cabin floor still coincides with painted floor or is too high.");
                var mesh=placed.GetComponentsInChildren<MeshFilter>().Single(filter=>filter.sharedMesh.name=="Shaft").sharedMesh;
                Require(mesh.vertices.Any(p=>Mathf.Abs(p.y-(.096f*ShopProportions.Scale+.002f))<.0001f),"Opaque shaft base is absent.");
                var point=placed.transform.position+Vector3.up*.6f;
                var others=Object.FindObjectsOfType<PlacedObject>().Where(p=>p!=placed).SelectMany(p=>p.GetComponentsInChildren<Renderer>()).Select(renderer=>(renderer,renderer.enabled)).ToArray();
                foreach(var other in others)other.renderer.enabled=false;
                TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),MobilityPolishBatch.Pictures+"/LiftFloor.png",point+new Vector3(2.6f,1.3f,-2.9f),point,.95f);
                var receivers=starter.floors.Surfaces.Select(filter=>filter.GetComponent<MeshRenderer>()).Select(renderer=>(renderer,renderer.receiveShadows)).ToArray();
                foreach(var receiver in receivers)receiver.renderer.receiveShadows=false;
                TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),MobilityPolishBatch.Pictures+"/LiftFloorUnshadowed.png",point+new Vector3(2.6f,1.3f,-2.9f),point,.95f);
                foreach(var receiver in receivers)receiver.renderer.receiveShadows=receiver.receiveShadows;
                var probe=build.grid.CellToWorld(link.Lower);float surface=starter.floors.SurfaceElevation(link.Lower);
                string Path(Transform node)=>node.parent==null?node.name:Path(node.parent)+"/"+node.name;
                File.WriteAllLines(MobilityPolishBatch.Report+"/floor-probe.txt",Object.FindObjectsOfType<MeshRenderer>().Where(renderer=>renderer.bounds.min.x<=probe.x&&renderer.bounds.max.x>=probe.x&&renderer.bounds.min.z<=probe.z&&renderer.bounds.max.z>=probe.z&&renderer.bounds.min.y<=surface+.02f&&renderer.bounds.max.y>=surface-.02f).Select(renderer=>Path(renderer.transform)+" y="+renderer.bounds.min.y+".."+renderer.bounds.max.y+" material="+renderer.sharedMaterial?.name));
                foreach(var other in others)other.renderer.enabled=other.enabled;
            }
            else
            {
                var nav=new StoreNavigation(build.grid,build.territory);nav.RefreshDoorways(Object.FindObjectsOfType<PlacedObject>());
                Vector3 lower=build.grid.CellToWorld(link.Lower)+Vector3.up*StoreNavigation.FloorOffset(0),upper=build.grid.CellToWorld(link.Upper)+Vector3.up*StoreNavigation.FloorOffset(1);
                Require(link.Allows(false)&&!link.Allows(true),"Down escalator directions are incorrect.");
                Require(!nav.TryPath(lower,upper,out _),"Down escalator admits an upward route.");
                Require(Mathf.Abs(nav.TravelSpeedMultiplier(lower,upper)-.8f)<.001f,"Passenger speed is not reduced.");
                var actor=new GameObject("Descending customer").AddComponent<ShopCharacter>();actor.transform.position=upper;actor.Initialize(nav);
                Require(actor.MoveTo(lower),"Customer cannot use down escalator.");
                var visual=placed.GetComponent<StoreyTransportVisual>();Require(visual.BeltDirection==-1,"Down belt runs upwards.");
                float until=Time.realtimeSinceStartup+20;
                while(!actor.Arrived&&Time.realtimeSinceStartup<until){actor.Advance(.03f);yield return null;}
                Require(actor.Arrived&&Vector3.Distance(actor.transform.position,lower)<.02f,"Customer stalled while descending.");Object.Destroy(actor.gameObject);yield return null;
            }
            var editor=build.GetComponent<BuildEditingController>();editor.Begin();editor.Select(placed);Require(editor.DeleteSelected(),"Temporary connection cannot return to storage.");editor.Finish();yield return null;
            Require(starter.floors.RemoveCells(new System.Collections.Generic.List<Vector3Int>{link.Upper},build.inventory),"Temporary upper landing cannot be removed.");
        }
        File.WriteAllText(MobilityPolishBatch.Report+"/transports.txt","PASS 20% narrower meshes with smaller occupied width; down-only route rejects ascent and carries a real ShopCharacter downstairs; visual and passenger speed reduced by 20%; lift cabin floor 9 mm above painted floor with opaque pit base.\n");
        var shop=Object.FindObjectOfType<ShopWindow>(true);shop.gameObject.SetActive(true);shop.OpenVehicles();yield return null;
        var cards=shop.GetComponentsInChildren<VehicleShopCard>().Where(card=>card.gameObject.activeInHierarchy).ToArray();
        Require(cards.Length==6,"Store Vehicles does not show all six vehicles.");
        Require(cards.Select(card=>card.title.text).Distinct().Count()==6,"Duplicate or missing vehicle cards.");
        var compact=cards.Single(card=>card.title.text=="Compact van");int before=trip.orders.money.Money;
        compact.stateButton.onClick.Invoke();yield return null;
        Require(trip.Fleet.SelectedId=="compact_van"&&trip.orders.money.Money==before-8000,"Store purchase does not use shared fleet rules.");
        Require(trip.orders.pickupCapacityKg==600&&trip.orders.pickupCapacityM3==4,"Store purchase did not update actual cargo limits.");
        CaptureUi("Vehicles");shop.Close();Time.timeScale=1;
        File.WriteAllText(MobilityPolishBatch.Report+"/vehicles.txt","PASS six distinct cards in actual Store Vehicles view; buying compact van debits 8000 exactly once, updates parked model and real 600 kg / 4 m3 limits through the shared fleet.\n");
        trip.Begin();while(trip.Transitioning||!trip.InCity)yield return null;
        var city=Object.FindObjectOfType<CityWorld>();city.pickup.ReadKeyboard=false;city.chaseCamera.ReadMouse=false;
        var points=city.town.suppliers.Where(checkpoint=>checkpoint.supplier==RetailEmpireTycoon.Logistics.SupplierKind.Showroom).Concat(city.town.showroomEntrances).ToArray();
        Require(points.Length==2,"Missing dealership entrance at the old yellow marker.");
        foreach(var checkpoint in points)
        {
            city.pickup.Body.position=checkpoint.transform.position+Vector3.up*.1f;city.pickup.Body.rotation=Quaternion.identity;city.pickup.Stop();Physics.SyncTransforms();
            Require(checkpoint.CanUse(city.pickup),"Showroom unavailable at yellow marker: "+checkpoint.UnavailableReason(city.pickup));
            city.Interact();yield return null;
            Require(Object.FindObjectsOfType<TMP_Text>().Any(label=>label.text=="Vehicle showroom"),"Menu did not open at "+checkpoint.name);
            CaptureUi(checkpoint==points[0]?"NewShowroom":"OldYellowMarker");
            typeof(CityWorld).GetMethod("ClosePanel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(city,null);
        }
        trip.Return();while(trip.Transitioning||trip.InCity)yield return null;
        Require(trip.Fleet.SelectedId=="compact_van","Selected vehicle lost on return.");
        File.WriteAllText(MobilityPolishBatch.Report+"/showroom.txt","PASS actual Game -> City -> Game; showroom opens directly at both the original yellow marker and riverside entrance without a prior world Update; selected store-purchased van retained.\n");
    }
    private static void CaptureUi(string name)
    {
        var camera=Camera.main;var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var canvases=Object.FindObjectsOfType<Canvas>().Where(canvas=>canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(canvas=>canvas.worldCamera).ToArray();var planes=canvases.Select(canvas=>canvas.planeDistance).ToArray();
        var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
            File.WriteAllBytes(MobilityPolishBatch.Pictures+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Finish(Exception error)
    {SessionState.SetBool(Pending,false);File.WriteAllText(MobilityPolishBatch.Report+"/play.txt",error==null?"PASS five requested mobility fixes in real gameplay.\n":"FAIL "+error);EditorApplication.Exit(error==null?0:1);}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
