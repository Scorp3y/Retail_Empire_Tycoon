using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.Territory;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class StoreUpgradeValidation
{
    private const string Folder="Library/StoreUpgradeQA";
    private static int checks;
    private static readonly StringBuilder report=new StringBuilder();
    private static void Require(bool condition,string message)
    {
        checks++;
        if(condition)return;
        File.WriteAllText(Folder+"/integration.txt",report+"\nFAILED: "+message);
        throw new InvalidOperationException(message);
    }
    public static void ValidateAssets()
    {
        Directory.CreateDirectory(Folder);
        var layout=Object.FindObjectOfType<TerritoryPlotLayout>(true);
        Require(layout!=null&&layout.plots.Count==9,"Nine purchasable zones are required.");
        Require(layout.initialBounds.width>=14.9f&&layout.initialBounds.height>=11.9f,"The starting land was not expanded 3x.");
        foreach(var plot in layout.plots)
        {
            Require(plot.bounds.width>=14&&plot.bounds.height>=11,"An expanded zone is too small.");
            foreach(var other in layout.plots.Where(p=>p.id!=plot.id))
                Require(Mathf.Min(plot.bounds.xMax,other.bounds.xMax)-Mathf.Max(plot.bounds.xMin,other.bounds.xMin)<.001f || Mathf.Min(plot.bounds.yMax,other.bounds.yMax)-Mathf.Max(plot.bounds.yMin,other.bounds.yMin)<.001f,"Land rectangles overlap: "+plot.id+" / "+other.id);
        }
        File.WriteAllText(Folder+"/routes.txt",string.Join("\n",Object.FindObjectsOfType<CarSpawner>(true).Select(s=>s.name+": "+string.Join(" -> ",s.waypoints.Where(t=>t!=null).Select(t=>t.position.ToString())))));
        foreach(string id in new[]{"wall_01","wallcorner_01"})
        {
            var item=Object.FindObjectOfType<BuildItemCatalog>(true).GetById(id);
            Require(item.isWall,"Wall item marker is missing.");
            float blockSize=layout.grid.cellSize*BuildItemData.WallBlockSizeInCells;
            Require(item.footprint==Vector2Int.one*BuildItemData.WallBlockSizeInCells&&item.pivotOffset==Vector2Int.zero,"A wall module must occupy exactly one construction block.");
            Require(item.placementBounds.size.x<=blockSize+.0001f&&item.placementBounds.size.z<=blockSize+.0001f,"Wall mesh does not fit a single construction block.");
            Require(item.prefab.GetComponent<StoreWallOccluder>()!=null,"Built walls cannot participate in visibility or intersection checks.");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(id=="wall_01"?"Assets/Prefabs/Structures/Wall.prefab":"Assets/Prefabs/Structures/Wall_Corner.prefab");
            Require(item.prefab.GetComponentInChildren<MeshFilter>().sharedMesh==original.GetComponentInChildren<MeshFilter>().sharedMesh,"Original wall mesh was replaced.");
            Require(item.prefab.GetComponentInChildren<Renderer>().sharedMaterial==original.GetComponentInChildren<Renderer>().sharedMaterial,"Original wall material was replaced.");
        }
        ValidateCornerConnections(layout.grid, Object.FindObjectOfType<BuildItemCatalog>(true));
        var bread=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shelf/Fresh Market Shelf.prefab");
        Require(bread.GetComponentsInChildren<ShelfProductSlot>().Length==54&&bread.GetComponent<PlacedShelfStock>().MaxAmount==54,"Bakery rack capacity/layout is not 54.");
        var catalog=Object.FindObjectOfType<BuildItemCatalog>(true);
        var styles=AssetDatabase.FindAssets("t:BuildItemData",new[]{StoreUpgradeSetup.Folder}).Select(g=>AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Require(styles.Count(i=>i.placementKind==PlacementKind.Floor)==6,"Six extra floor styles are required.");
        Require(styles.Count(i=>i.category==BuildCategory.Decoration&&i.beautyPoints>0)==6,"Six functional decorations are required.");
        foreach(var item in styles)
        {
            Require(catalog.GetById(item.id)==item&&item.prefab!=null&&item.icon!=null,"Generated item is not registered or its icon is missing: "+item.id);
            if(item.placementKind==PlacementKind.Floor)Require(item.floorMaterial!=null&&item.floorMaterial.GetTexture("_BaseMap")!=null,"Floor pattern material is missing: "+item.id);
            if(item.category==BuildCategory.Decoration)Require(item.placementBounds.min.y>=.095f,"Decoration sinks into the imported shop floor: "+item.id);
        }
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Market"}))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var nature=prefab.transform.Find("Trees - Unpurchased territory");
            if(nature==null)continue;
            var instance=Object.Instantiate(prefab);
            try
            {
                foreach(Transform prop in instance.transform.Find("Trees - Unpurchased territory"))
                {
                    if(prop.GetComponentInChildren<Renderer>(true)==null)continue;
                    var box=CityUpdateSetup.Measure(prop.gameObject);
                    var initial=layout.initialBounds;
                    Require(box.max.x<=initial.xMin||box.min.x>=initial.xMax-.02f||box.max.z<=initial.yMin||box.min.z>=initial.yMax-.02f,"A stage prop blocks expanded starting land: "+prop.name);
                }
            }
            finally {Object.DestroyImmediate(instance);}
        }
        var pickup=GameObject.Find("Player pickup");
        var bounds=CityUpdateSetup.Measure(pickup);
        Require(Mathf.Max(bounds.size.x,bounds.size.z)>.95f,"Parked pickup is still too small.");
        var world=GameObject.Find("World").transform;
        File.WriteAllText(Folder+"/world.txt",string.Join("\n",world.Cast<Transform>().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true).Select(r=>$"{root.name}/{r.name} center={r.bounds.center} size={r.bounds.size}"))));
        File.WriteAllText(Folder+"/assets.txt",$"PASS {checks} asset assertions. Pickup bounds {bounds.size}.\n");
    }
    private static void ValidateCornerConnections(GridSystem grid, BuildItemCatalog catalog)
    {
        var corner=catalog.GetById("wallcorner_01");
        var wall=catalog.GetById("wall_01");
        var cornerModel=Object.Instantiate(corner.prefab);
        var wallModel=Object.Instantiate(wall.prefab);
        try
        {
            var anchor=new Vector3Int(12,0,12);
            var junction=grid.CellToWorld(anchor);
            for(int facing=0;facing<4;facing++)
            foreach(var localDirection in new[]{Vector3.right,Vector3.forward})
            {
                var rotation=Quaternion.Euler(0,facing*90,0);
                var direction=rotation*localDirection;
                cornerModel.transform.SetPositionAndRotation(BuildPlacementPose.Position(grid,corner,anchor,facing%2!=0,facing),rotation);
                var neighbour=anchor+new Vector3Int(Mathf.RoundToInt(direction.x),0,Mathf.RoundToInt(direction.z));
                int wallFacing=(facing+(localDirection==Vector3.forward?1:0))%4;
                wallModel.transform.SetPositionAndRotation(BuildPlacementPose.Position(grid,wall,neighbour,wallFacing%2!=0,wallFacing),Quaternion.Euler(0,wallFacing*90,0));
                var cornerFace=ConnectionVertices(cornerModel,junction,direction,grid.cellSize*.5f);
                var wallFace=ConnectionVertices(wallModel,junction,direction,grid.cellSize*.5f);
                Require(cornerFace.Length>0&&wallFace.Length>0,"Corner has no connection face at the neighbouring cell boundary.");
                Require(cornerFace.All(v=>wallFace.Any(w=>(v-w).sqrMagnitude<.00000001f))&&wallFace.All(v=>cornerFace.Any(w=>(v-w).sqrMagnitude<.00000001f)),"Corner and straight wall profiles do not meet in orientation "+facing+" direction "+localDirection);
            }
        }
        finally {Object.DestroyImmediate(cornerModel);Object.DestroyImmediate(wallModel);}
    }
    private static Vector3[] ConnectionVertices(GameObject model,Vector3 origin,Vector3 direction,float distance)
    {
        return model.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)))
            .Where(v=>Mathf.Abs(Vector3.Dot(v-origin,direction)-distance)<.00001f).ToArray();
    }
    public static void Run()
    {
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"QA must never access the player save.");
        Object.FindObjectOfType<CityTrip>().StartCoroutine(Check());
    }
    private static IEnumerator Check()
    {
        report.Clear();
        var build=Object.FindObjectOfType<BuildController>();
        var layout=Object.FindObjectOfType<TerritoryPlotLayout>();
        var progression=layout.progression;
        var saved=progression.BuildSaveData();
        var catalog=Object.FindObjectOfType<BuildItemCatalog>();
        var shop=Object.FindObjectOfType<ShopWindow>(true);
        var operations=Object.FindObjectOfType<StoreOperations>();
        var orders=Object.FindObjectOfType<DeliveryOrders>();
        var version=Object.FindObjectOfType<GameVersionLabel>();
        Require(version!=null,"Game version HUD is missing.");
        var versionLabel=version.uiRoot.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.text==GameVersionLabel.CurrentVersion);
        var versionRect=versionLabel.transform.parent.GetComponent<RectTransform>();
        Require(versionRect.anchorMin==Vector2.zero&&versionRect.pivot==Vector2.zero&&!versionLabel.raycastTarget,"Version is not in the bottom-left corner or blocks clicks.");
        // All objects and inventories below belong only to the no-save sandbox.
        foreach(var id in new[]{TerritoryId.Purple,TerritoryId.Red,TerritoryId.Green,TerritoryId.Yellow,TerritoryId.Pink,TerritoryId.EastSouth,TerritoryId.EastNorth,TerritoryId.NorthWest,TerritoryId.NorthEast})
        {
            Require(progression.IsTerritoryAvailable(id),"Purchase progression does not unlock "+id);
            progression.MarkPurchased(id);
            var plot=layout.plots.Single(p=>p.id==id);
            Require(build.territory.IsCellPurchased(build.grid.WorldToCell(new Vector3(plot.bounds.center.x,0,plot.bounds.center.y))),"New zone does not grant buildable cells.");
        }
        var snapshot=JsonUtility.FromJson<TerritorySaveData>(JsonUtility.ToJson(progression.BuildSaveData()));
        progression.ApplySaveData(snapshot);
        Require(progression.Purchased.Count==9&&progression.State.CurrentLevel==StoreLevelId.Lvl6,"Expanded land save roundtrip changed the starter-building stage.");
        progression.ApplySaveData(saved);
        Require(!progression.IsTerritoryAvailable(TerritoryId.EastSouth),"Extra plots unlocked before the original progression was complete.");
        report.AppendLine("PASS purchased land, adjacent expansion unlocks, old progression and save roundtrip");

        shop.gameObject.SetActive(true);shop.OpenVehicles();yield return null;
        var car=shop.GetComponentInChildren<VehicleShopCard>();
        Require(car!=null&&car.icon.sprite!=null&&!car.stateButton.interactable&&car.title.text.Length>0,"Default pickup card is not owned/selected.");
        yield return new WaitForEndOfFrame();
        Capture("Vehicles");
        shop.OpenCategory_Decoration();yield return null;
        Require(shop.GetComponentsInChildren<ShopItemCard>().Length==6,"Decoration section does not show six actual items.");
        yield return new WaitForEndOfFrame();
        Capture("Decorations");
        shop.OpenCategory_Structures();yield return null;
        Require(shop.GetComponentsInChildren<ShopItemCard>().Any(c=>c.nameText.text==ShopText.Item(catalog.GetById("floor_wood"))),"Floor styles are missing from construction.");
        var retiredFloor=catalog.GetById("floor_01");
        Require(retiredFloor!=null&&retiredFloor.hiddenFromShop,"Legacy floor must remain resolvable for saves but retired from sale.");
        Require(!shop.GetComponentsInChildren<ShopItemCard>().Any(c=>c.nameText.text==ShopText.Item(retiredFloor)),"Retired floor is still in the shop list.");
        int oldBalance=orders.money.Money;
        Require(!orders.Order(retiredFloor)&&orders.money.Money==oldBalance,"Retired floor can still be ordered or consumes money.");
        build.inventory.Add(retiredFloor,1);
        Require(build.inventory.GetCount(retiredFloor)>0,"Retired floor can no longer be used from existing inventory.");
        yield return new WaitForEndOfFrame();Capture("Structures");
        shop.Close();
        report.AppendLine("PASS vehicle ownership card, decoration department and floor catalogue");

        var bread=catalog.GetById("shelf_fresh_01");
        var shelfAnchor=build.grid.WorldToCell(new Vector3(8,0,5));
        build.inventory.Add(bread,2);build.EnterBuildMode(bread);
        Require(build.TryPlaceAt(shelfAnchor),"Cannot place bakery rack on expanded starting land.");
        var rack=Object.FindObjectsOfType<PlacedShelfStock>().Single(s=>s.GetComponent<PlacedObject>()?.anchorCell==shelfAnchor);
        var product=orders.productCatalog.Products.First(p=>p.Id=="product_bread");
        rack.SetStockFromSave(product,54);rack.GetComponent<ShelfProductDisplay>().Refresh();
        Require(rack.CurrentAmount==54,"Expanded bakery rack cannot hold 54 loaves.");
        var request=new PlacementRequest(bread,shelfAnchor,false,0);
        var access=ShelfAccessArea.Cells(build.grid,request).ToArray();
        var decor=catalog.GetById("decor_plant");build.inventory.Add(decor,2);build.EnterBuildMode(decor);
        Require(!build.TryPlaceAt(access[0]),"A decoration blocked the customer side of the shelf.");
        Require(build.inventory.GetCount(decor)==2,"Rejected placement consumed inventory.");
        // The same clearance is checked in every rotation and in both placement orders.
        var clearance=new Rule_ShelfApproach(build.grid,build.territory);
        for(int facing=0;facing<4;facing++)
        {
            var candidate=new PlacementRequest(bread,shelfAnchor+new Vector3Int(16,0,10),facing%2!=0,facing);
            var aisle=ShelfAccessArea.Cells(build.grid,candidate).ToArray();
            Require(aisle.Length>0&&clearance.Evaluate(candidate).ok,"A valid shelf approach was rejected.");
            build.grid.Occupy(aisle.Take(1));
            Require(!clearance.Evaluate(candidate).ok,"A shelf was placed facing an existing obstacle.");
            build.grid.Release(aisle.Take(1));
        }
        report.AppendLine("PASS 54 bread slots, buyer-side reservation, four orientations and no inventory loss on rejection");

        var wall=catalog.GetById("wall_01");build.inventory.Add(wall,3);build.EnterBuildMode(wall);
        var wallAnchor=build.grid.WorldToCell(new Vector3(11,0,7));
        Require(build.TryPlaceAt(wallAnchor),"Cannot place a wall on expanded land.");
        var secondAnchor=wallAnchor+new Vector3Int(wall.footprint.x,0,0);
        Require(build.TryPlaceAt(secondAnchor),"Adjacent wall cannot be built without overlap.");
        var walls=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item==wall).OrderBy(p=>p.anchorCell.x).ToArray();
        var first=walls.Single(p=>p.anchorCell==wallAnchor).GetComponent<StoreWallOccluder>().WorldBounds;
        var second=walls.Single(p=>p.anchorCell==secondAnchor).GetComponent<StoreWallOccluder>().WorldBounds;
        Require(Mathf.Abs(first.max.x-second.min.x)<.0001f,"Adjacent walls leave a visible gap.");
        Require(!build.TryPlaceAt(wallAnchor),"Overlapping walls were accepted.");
        Require(walls.Single(p=>p.anchorCell==wallAnchor).occupiedCells.Count==1,"A new wall must occupy one fine grid cell.");
        var corner=catalog.GetById("wallcorner_01");build.inventory.Add(corner,2);build.EnterBuildMode(corner);
        Require(!build.TryPlaceAt(secondAnchor+Vector3Int.right),"An incorrectly turned corner can leave a broken wall joint.");
        build.RotateSelected(-1);
        Require(build.TryPlaceAt(secondAnchor+Vector3Int.right*BuildItemData.WallBlockSizeInCells),"Corner wall does not join a straight one-block section.");
        Require(!build.TryPlaceAt(secondAnchor),"Corner wall can overlap an existing straight wall.");
        build.EnterBuildMode(wall);
        for(int facing=0;facing<4;facing++)
        {
            var candidate=new PlacementRequest(wall,wallAnchor+new Vector3Int(10,0,10),facing%2!=0,facing);
            Require(build.grid.GetFootprintCells(candidate.anchorCell,wall.footprint,candidate.rotated,wall.pivotOffset).Count()==1,"Wall footprint is not one fine grid cell in every orientation.");
            Require(new Rule_VisualOverlap(build.grid).Evaluate(candidate).ok,"Clear wall position rejected in a rotated orientation.");
        }
        yield return null;yield return null;
        Require(Object.FindObjectsOfType<StoreWallOccluder>().All(w=>w.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff)),"Walls disappear while building walls.");
        build.ExitBuildMode();
        report.AppendLine("PASS touching wall seams, overlap protection and no wall cutaway during wall construction");

        var decorAnchor=build.grid.WorldToCell(new Vector3(4,0,7));
        operations.Advance(3);float originalRating=operations.Rating;
        build.EnterBuildMode(decor);Require(build.TryPlaceAt(decorAnchor),"A decoration cannot be placed.");build.ExitBuildMode();
        operations.Advance(3);
        Require(operations.BeautyPoints==decor.beautyPoints&&operations.Rating>originalRating,"Decoration does not improve store beauty/rating.");
        var accessPoint=build.grid.CellToWorld(access.Last());
        Require(operations.Navigation.IsWalkable(accessPoint),"The protected shelf access aisle still contains a physical obstacle.");
        var floor=Object.FindObjectOfType<FloorPainter>();
        floor.PaintCells(access.Take(1).ToList(),catalog.GetById("floor_wood"));
        Require(floor.BuildSaveData().Any(t=>t.itemId=="floor_wood"),"Floor style is not retained in save data.");
        Require(floor.AreCellsValid(access.Take(1).ToList(),catalog.GetById("floor_wood")),"Floor cannot be painted in the shelf access aisle.");
        var largePreview=floor.GetRectCells(shelfAnchor,shelfAnchor+new Vector3Int(70,0,60));
        floor.ShowPreview(largePreview,true);floor.ShowPreview(largePreview,false);
        var brush=floor.previewRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Floor brush preview");
        Require(brush.sharedMesh.vertexCount==largePreview.Count*4&&brush.GetComponent<Collider>()==null,"Large floor preview creates individual tiles or changes physics.");
        floor.ClearPreview();Require(!brush.gameObject.activeSelf,"Floor brush preview did not clear.");
        report.AppendLine("PASS decorative beauty bonus, floor-style save identity and floor painting in customer aisles");

        orders.money.SetMoney(50000);Require(orders.Order(product),"QA order failed.");yield return null;
        var notice=orders.GetComponent<DeliveryNotice>();
        var noticeRect=notice.uiRoot.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="Delivery notice");
        Require(!noticeRect.gameObject.activeSelf,"Routine paid-order toast is still visible.");
        orders.Ledger.Restore(new DeliverySaveData());
        orders.money.SetMoney(0);Require(!orders.Order(product),"An unaffordable order was accepted.");yield return null;
        Require(noticeRect.gameObject.activeSelf&&orders.NoticeIsError,"Delivery failure feedback was suppressed.");
        orders.money.SetMoney(50000);Require(orders.Order(product),"Could not clear the error with a successful order.");orders.Ledger.Restore(new DeliverySaveData());
        report.AppendLine("PASS paid-order message removed without suppressing delivery errors");

        var cameraRoot=new GameObject("QA orbit camera");var target=new GameObject("QA vehicle orientation");
        target.transform.rotation=Quaternion.Euler(0,30,0);
        var orbit=cameraRoot.AddComponent<PickupCamera>();orbit.target=target.transform;orbit.ReadMouse=false;
        orbit.Orbit(90,100);Require(Mathf.Abs(orbit.Yaw-120)<.001f&&orbit.Pitch==65,"Mouse orbit does not respect vehicle orientation/pitch limit.");
        orbit.Orbit(-90,-100);Require(Mathf.Abs(orbit.Yaw-30)<.001f&&orbit.Pitch==12,"Lower camera pitch limit failed.");
        orbit.ResetBehindVehicle();Require(Mathf.Abs(Mathf.DeltaAngle(orbit.Yaw,30))<.001f&&orbit.Pitch==20,"Reset behind vehicle failed.");
        orbit.SetInteractionBlocked(true);orbit.SetInteractionBlocked(false);
        Object.Destroy(cameraRoot);Object.Destroy(target);
        report.AppendLine("PASS independent vehicle camera orbit, pitch limits, reset and interaction mode transitions");
        var cameraInput=Object.FindObjectOfType<MainCamera>();
        cameraInput.enabled=false;
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var rackPosition=rack.transform.position;
        camera.transform.position=rackPosition+new Vector3(1.1f,1.6f,1.8f);camera.transform.LookAt(rackPosition+Vector3.up*.3f+Vector3.right*.4f);
        var sourceMaterials=bread.prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        var originalColors=sourceMaterials.Select(m=>m.color).ToArray();
        build.EnterBuildMode(bread);
        build.enabled=false;
        var ghostRequest=new PlacementRequest(bread,shelfAnchor+new Vector3Int(6,0,0),false,0);
        build.preview.SetPose(BuildPlacementPose.Position(build.grid,bread,ghostRequest.anchorCell,false,0),Quaternion.identity);
        build.preview.ShowPlacement(build.grid,ghostRequest,PlacementResult.Success());
        var ghostRenderers=build.preview.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name!="Placement footprint").ToArray();
        var ghostMaterials=ghostRenderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        Require(ghostMaterials.Length>0&&ghostMaterials.All(m=>m.color.a>=.3f&&m.color.a<1),"Preview is too transparent to recognise the object.");
        Require(ghostMaterials.All(m=>!sourceMaterials.Contains(m)&&sourceMaterials.Any(s=>s.mainTexture==m.mainTexture)),"Preview replaces model textures or mutates original shared materials.");
        var validColors=ghostMaterials.Select(m=>m.color).ToArray();
        build.preview.SetValid(false);
        Require(ghostMaterials.Where((m,i)=>m.color!=validColors[i]).Any(),"Invalid placement has no visual indication.");
        Require(sourceMaterials.Select((m,i)=>m.color==originalColors[i]).All(equal=>equal),"Ghost transparency leaked into the real model.");
        build.preview.SetValid(true);
        yield return new WaitForEndOfFrame();Capture("Construction");
        build.ExitBuildMode();build.enabled=true;camera.transform.SetPositionAndRotation(position,rotation);cameraInput.enabled=true;
        var mode=Object.FindObjectOfType<TerritoryPurchaseModeManager>(true);
        mode.Enter();yield return new WaitForSecondsRealtime(.8f);
        Require(Camera.main.transform.position.y<45,"Initial land purchase view is too far away to click the available plot.");
        yield return new WaitForEndOfFrame();Capture("ExpandedLand");mode.Exit();yield return new WaitForSecondsRealtime(.8f);
        // Exercise the actual loader, without reading or writing the player's save.
        var placementSnapshot=build.BuildPlacedSaveData();
        var legacyAnchor=wallAnchor+new Vector3Int(18,0,0);
        placementSnapshot.Add(new PlacedBuildSaveData{itemId=wall.id,x=legacyAnchor.x,z=legacyAnchor.z,facing=0});
        var previousAnchor=legacyAnchor+new Vector3Int(0,0,8);
        placementSnapshot.Add(new PlacedBuildSaveData{itemId=wall.id,x=previousAnchor.x,z=previousAnchor.z,facing=0,wallModuleVersion=1});
        build.ApplyPlacedSaveData(placementSnapshot,catalog);yield return null;yield return null;
        var restored=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item==wall).ToArray();
        var legacy=restored.Single(p=>p.anchorCell==legacyAnchor);
        var modern=restored.Single(p=>p.anchorCell==wallAnchor);
        Require(legacy.occupiedCells.Count==8&&legacy.wallModuleVersion==0,"Old saved wall lost its multi-cell dimensions.");
        Require(Mathf.Abs(legacy.GetComponent<StoreWallOccluder>().WorldBounds.size.x-4*build.grid.cellSize)<.0001f,"Old wall shrank on save load.");
        Require(modern.occupiedCells.Count==1&&modern.wallModuleVersion==2,"New single-cell wall lost its module version on load.");
        var previous=restored.Single(p=>p.anchorCell==previousAnchor);
        Require(previous.occupiedCells.Count==4&&Mathf.Abs(previous.GetComponent<StoreWallOccluder>().WorldBounds.size.x-2*build.grid.cellSize)<.0001f,"Version-one wall shrank on save load.");
        var editing=build.GetComponent<BuildEditingController>();
        editing.Begin();Require(editing.Select(previous)&&editing.PreviewAt(previousAnchor).ok,"A historical wall cannot be edited at its original size: "+editing.CandidateResult.reason);editing.Finish();
        var savedWalls=build.BuildPlacedSaveData();
        Require(savedWalls.Single(p=>p.x==legacyAnchor.x&&p.z==legacyAnchor.z).wallModuleVersion==0&&savedWalls.Single(p=>p.x==wallAnchor.x&&p.z==wallAnchor.z).wallModuleVersion==2,"Saving mixes legacy and one-block wall formats.");
        build.ApplyPlacedSaveData(savedWalls,catalog);yield return null;yield return null;
        Require(Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==wall&&p.anchorCell==legacyAnchor).occupiedCells.Count==8,"Legacy wall geometry changes on the second load.");
        Require(Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==wall&&p.anchorCell==previousAnchor).occupiedCells.Count==4,"Version-one wall changes on the second load.");
        report.AppendLine("PASS one-block walls and corners, original meshes/materials, retired floor and old/new wall save compatibility");
        File.WriteAllText(Folder+"/integration.txt",report+$"\nSTORE UPGRADE PASSED: {checks} assertions. Player save components absent.\n");
    }
    private static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
}
