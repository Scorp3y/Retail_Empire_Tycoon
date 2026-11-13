using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

internal static class DecorCollectionPlay
{
    internal static IEnumerable<object> Verify(StarterStoreInitialization starter)
    {
        var catalog=starter.catalog;
        var build=starter.building;
        var floors=starter.floors;
        var inventory=build.inventory;
        var orders=starter.cityTrip.orders;
        BuildItemData Item(string id)=>catalog.GetById(id);
        var lockedPlot = starter.land.plots.First(plot=>!starter.land.progression.State.IsPurchased(plot.id) &&
            plot.bounds.Overlaps(starter.land.initialBounds));
        var lockedOverlap = lockedPlot.bounds;
        var initial = starter.land.initialBounds;
        var overlapPoint = new Vector3((Mathf.Max(initial.xMin,lockedOverlap.xMin)+Mathf.Min(initial.xMax,lockedOverlap.xMax))*.5f,
            0,(Mathf.Max(initial.yMin,lockedOverlap.yMin)+Mathf.Min(initial.yMax,lockedOverlap.yMax))*.5f);
        var locked = build.grid.WorldToCell(overlapPoint);
        Require(!build.territory.IsCellPurchased(locked) && !build.territory.IsParkingCellAllowed(locked),
            "A locked parcel overlaps starter construction rights.");
        build.EnterBuildMode(Item("wall_finish_01"));
        Require(!build.TryPlaceAt(locked),"Construction bypassed locked parcel ownership.");
        build.ExitBuildMode();
        build.EnterBuildMode(Item("Floor_26_BrickPavers"));
        Require(!build.TryPaintCells(new List<Vector3Int>{locked}),"Floor painting bypassed locked parcel ownership.");
        build.ExitBuildMode();
        var landBefore = starter.land.progression.BuildSaveData();
        starter.land.progression.MarkPurchased(lockedPlot.id);
        Require(build.territory.IsCellPurchased(locked),"Purchased parcel did not unlock construction.");
        starter.land.progression.ApplySaveData(landBefore);
        var entries=catalog.Items.Where(i=>i!=null && i.decorGroup!=DecorGroup.None).ToArray();
        var second=Resources.Load<DecorCollectionManifest>("DecorCollectionII/Manifest").items;
        Require(second.Length==50 && second.All(i=>i==Item(i.id)) && second.GroupBy(i=>i.decorGroup).All(group=>group.Count()==10),
            "The second collection must resolve 50 entries, ten in each group.");
        Require(second.Where(item=>item.decorGroup==DecorGroup.Lighting).All(item=>
            item.prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(renderer=>renderer.sharedMaterials)
                .Any(material=>material.IsKeywordEnabled("_EMISSION")) &&
            item.prefab.GetComponentInChildren<Light>()==null), "Lighting decor must use emissive materials.");
        Require(entries.Length==98 && entries.Select(i=>i.id).Distinct().Count()==98 && entries.All(i=>i.prefab!=null && i.icon!=null),
            "The catalog must resolve 98 unique decor models and icons.");
        Require(entries.Count(i=>i.decorSeason!=DecorSeason.None)==9, "Missing seasonal labels.");
        var legacyIds=new[]{"decor_plant","decor_planter","decor_lamp","decor_bench","decor_bin",
            "frontage_bench","frontage_street_lamp","frontage_bin","frontage_planter"};
        Require(legacyIds.All(id=>Item(id).prefab.transform.Find("Collection model")!=null),"A rework lost its saved catalog ID.");
        var existing=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null && p.item.id.StartsWith("frontage_"))
            .Select(p=>(p.item.id,p.anchorCell,p.transform.position)).ToArray();
        Require(existing.Length>0,"Missing starter frontage props.");

        var controls=Object.FindObjectOfType<GameplayControls>();
        var shop=Object.FindObjectOfType<ShopWindow>(true);
        controls.RequestShop();
        shop.OpenCategory(BuildCategory.Decoration);
        yield return null;
        foreach (var group in entries.Select(i=>i.decorGroup).Distinct())
        {
            string label=DecorCatalog.Section(group);
            shop.GetComponentsInChildren<Button>().Single(button=>button.name==label).onClick.Invoke();
            yield return null;
            var cards=shop.GetComponentsInChildren<ShopItemCard>();
            Require(entries.Where(i=>i.decorGroup==group).All(item=>cards.Any(card=>card.icon.sprite==item.icon)),
                "Category missing cards: "+label);
        }
        var progression=Object.FindObjectOfType<StoreProgression>();
        var previousTerritory=progression.BuildSaveData();
        var regions=progression.State.Purchased;
        regions.UnionWith(new[]{TerritoryId.Purple,TerritoryId.Red,TerritoryId.Green});
        shop.Refresh();
        yield return null;
        shop.transform.Find("Build sections").GetComponentsInChildren<Button>().Single(button=>button.name=="All").onClick.Invoke();
        yield return null;
        var available=shop.GetComponentsInChildren<ShopItemCard>();
        Require(entries.Append(Item("decor_sign")).All(item=>available.Any(card=>card.icon.sprite==item.icon && card.buyButton.interactable)),
            "All decorations must be purchasable after reaching the required store level.");
        progression.ApplySaveData(previousTerritory);
        shop.Refresh();
        yield return null;
        shop.GetComponentsInChildren<Button>().Single(button=>button.name==DecorCatalog.Section(DecorGroup.Storefront)).onClick.Invoke();
        yield return null;
        var autumn=Item("decor_autumn_crate");
        var autumnCard=shop.GetComponentsInChildren<ShopItemCard>().Single(card=>card.icon.sprite==autumn.icon);
        Require(autumnCard.sizeText.text.Contains("Autumn") && autumnCard.buyButton.interactable,"Seasonal catalog rule missing.");
        int budget=orders.money.Money;
        int deliveryEntries=orders.Ledger.Entries.Count();
        int autumnStock=inventory.GetCount(autumn);
        autumnCard.buyButton.onClick.Invoke();
        Require(autumnStock>=3 && orders.money.Money==budget-autumn.price && inventory.GetCount(autumn)==autumnStock+1 &&
            orders.Ledger.Entries.Count()==deliveryEntries,"Decor purchase did not go directly to storage.");
        var fountain=Item("decor_water_feature");
        budget=orders.money.Money;
        int fountainStock=inventory.GetCount(fountain);
        Require(fountainStock>=3 && !orders.Order(fountain) && orders.money.Money==budget && inventory.GetCount(fountain)==fountainStock,
            "Locked item charged the player or changed the starter stock.");
        var snowflake=Item("decor_snowflake_panel");
        var snowflakeCard=shop.GetComponentsInChildren<ShopItemCard>().Single(card=>card.icon.sprite==snowflake.icon);
        Require(snowflakeCard.sizeText.text.Contains("Winter") && snowflakeCard.buyButton.interactable,
            "The winter decoration must be available with its season label.");
        int snowflakeStock=inventory.GetCount(snowflake);
        snowflakeCard.buyButton.onClick.Invoke();
        Require(snowflakeStock>=3 && orders.money.Money==budget-snowflake.price && inventory.GetCount(snowflake)==snowflakeStock+1 &&
            orders.Ledger.Entries.Count()==deliveryEntries,"The new collection purchase did not go directly to storage.");
        var pergola=Item("decor_wooden_pergola");
        budget=orders.money.Money;
        int pergolaStock=inventory.GetCount(pergola);
        Require(pergolaStock>=3 && !orders.Order(pergola) && orders.money.Money==budget && inventory.GetCount(pergola)==pergolaStock,
            "A locked second collection item charged the player.");
        controls.HandleEscape();
        yield return null;

        var origin=new Vector3Int(1000,0,1000);
        build.territory.AddPurchasedRect(origin,origin+new Vector3Int(40,0,30));
        List<Vector3Int> Rectangle(int x,int z,int width,int depth,int layer=0) =>
            Enumerable.Range(x,width).SelectMany(a=>Enumerable.Range(z,depth).Select(b=>origin+new Vector3Int(a,layer,b))).ToList();
        floors.PaintCells(Rectangle(0,0,25,24),Item("floor_cream"));
        floors.PaintCells(Rectangle(25,0,12,24),Item("asphalt_01"));
        var upper=Rectangle(8,5,8,8,1);

        PlacedObject Place(string id,Vector3Int at,int facing=0,Vector3Int? end=null)
        {
            var item=Item(id);
            inventory.Add(item);
            build.EnterBuildMode(item);
            build.RotateSelected(facing);
            var check=build.CanPlaceAt(at,end);
            Require(check.ok && build.TryPlaceAt(at,end),id+": "+check.message);
            build.ExitBuildMode();
            return Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.anchorCell==at && p.facing==facing);
        }
        void Refuses(string id,Vector3Int at)
        {
            var item=Item(id);
            inventory.Add(item);
            build.EnterBuildMode(item);
            Require(!build.TryPlaceAt(at) && inventory.GetCount(item)>0,"Invalid placement accepted: "+id);
            build.ExitBuildMode();
        }

        var wall=Place("wall_finish_01",origin+new Vector3Int(4,0,4));
        var clock=Place("decor_wall_clock",wall.anchorCell);
        Require(clock.decorMount==DecorMount.Wall && clock.occupiedCells.Count==0,"Wall decoration occupies the walking grid.");
        Refuses("decor_wall_clock",origin+new Vector3Int(18,0,6));
        var editor=build.GetComponent<BuildEditingController>();
        editor.Begin();
        Require(editor.Select(wall) && !editor.DeleteSelected(),"A wall holding a decoration could be removed.");
        editor.Finish();

        var ceilingAt=origin+new Vector3Int(10,0,7);
        Refuses("decor_hanging_ivy",ceilingAt);
        floors.PaintCells(upper,Item("floor_cream"));
        var pendant=Place("decor_hanging_ivy",ceilingAt);
        Require(pendant.decorMount==DecorMount.Ceiling && !floors.RemoveCells(upper,inventory),
            "Ceiling support was lost or could be deleted.");
        var cordWall=Place("wall_finish_01",origin+new Vector3Int(4,0,11));
        var cordEnd=origin+new Vector3Int(10,0,11);
        var garland=Place("decor_light_garland",cordWall.anchorCell,0,cordEnd);
        Require(garland.decorEnd==cordEnd && garland.decorScale.x>1.1f,"Garland did not stretch between wall and ceiling.");
        var singleWall=Place("wall_finish_08",origin+new Vector3Int(20,0,4));
        var secondWall=Place("wall_finish_08",origin+new Vector3Int(22,0,7),1);
        Physics.SyncTransforms();
        var pickSurface=typeof(BuildController).Assembly.GetType("RetailEmpireTycoon.BuildSystem.DecorPlacement")
            .GetMethod("PickSurface",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach (var wallFace in new[] { singleWall, secondWall })
        {
            var faceBounds=wallFace.GetComponentInChildren<StoreWallOccluder>().WorldBounds;
            for (int side=-1;side<=1;side+=2)
            {
                var normal=wallFace.transform.forward*side;
                var ray=new Ray(faceBounds.center+normal*.45f,-normal);
                var parameters=new object[] {ray,Item("decor_autumn_leaf_garland"),0,build.grid,null,null};
                int facing=(wallFace.facing+(side<0?2:0))%4;
                Require((bool)pickSurface.Invoke(null,parameters) && (int)parameters[5]==facing &&
                    wallFace.occupiedCells.Contains((Vector3Int)parameters[4]),
                    "Pointer did not identify the correct side of the wall.");
            }
        }
        Refuses("decor_autumn_leaf_garland",origin+new Vector3Int(19,0,4));
        var autumnGarland=Place("decor_autumn_leaf_garland",singleWall.occupiedCells.Last());
        var wallBounds=singleWall.GetComponentInChildren<StoreWallOccluder>().WorldBounds;
        var garlandBounds=autumnGarland.GetComponentInChildren<MeshRenderer>().bounds;
        Require(autumnGarland.decorMount==DecorMount.Wall && !autumnGarland.decorEnd.HasValue &&
            autumnGarland.occupiedCells.Count==0 && Mathf.Abs(garlandBounds.center.x-wallBounds.center.x)<.002f &&
            garlandBounds.size.x<wallBounds.size.x && garlandBounds.min.z>wallBounds.max.z &&
            garlandBounds.min.y>wallBounds.center.y, "Autumn garland must hang centered and flush in front of a single wall.");
        editor.Begin();
        Require(editor.Select(autumnGarland),"Could not select the wall garland.");
        editor.Rotate(1);
        Require(editor.PreviewAt(secondWall.anchorCell).ok && editor.Confirm() &&
            autumnGarland.anchorCell==secondWall.anchorCell && autumnGarland.facing==1,
            "Could not move the wall garland onto a rotated wall.");
        editor.Finish();
        var legacyWall=Place("wall_finish_01",origin+new Vector3Int(11,0,21));
        var legacyEnd=Place("wall_finish_01",origin+new Vector3Int(17,0,21)).anchorCell;
        var legacyAutumn=Place("decor_autumn_leaf_garland",legacyWall.anchorCell,0,legacyEnd);
        Require(legacyAutumn.decorMount==DecorMount.Garland && legacyAutumn.decorEnd==legacyEnd,
            "Two-point autumn garlands from existing saves must keep their original mount.");

        var counter=Place("shelf_produce_01",origin+new Vector3Int(15,0,13));
        var floorSucculent=Place("decor_counter_succulent",origin+new Vector3Int(19,0,18));
        Require(floorSucculent.decorMount==DecorMount.Floor && floorSucculent.occupiedCells.Count==1,
            "Small decor must also reserve a cell when placed directly on the floor.");
        var succulentCell=counter.occupiedCells.OrderBy(c=>(build.grid.CellToWorld(c)-counter.transform.position).sqrMagnitude).First();
        var succulent=Place("decor_counter_succulent",succulentCell);
        Require(succulent.decorMount==DecorMount.Counter && succulent.transform.position.y>counter.transform.position.y+.2f,
            "Counter decor did not snap above the fixture.");
        var shelfLight=Place("decor_shelf_light_bar",succulentCell);
        Require(shelfLight.decorMount==DecorMount.ShelfUnderside && shelfLight.occupiedCells.Count==0 &&
            shelfLight.transform.position.y<succulent.transform.position.y,"The shelf light must attach below the product surface.");
        editor.Begin();
        Require(editor.Select(counter) && !editor.DeleteSelected(),"A shelf with attached decor could be removed.");
        editor.Finish();
        Refuses("decor_shelf_light_bar",origin+new Vector3Int(19,0,18));
        var plant=Place("decor_snake_plant",origin+new Vector3Int(5,0,16),1);
        Refuses("decor_snake_plant",origin+new Vector3Int(29,0,15));
        var outdoor=Place("decor_water_feature",origin+new Vector3Int(27,0,8));
        Require(outdoor.decorMount==DecorMount.Street && outdoor.occupiedCells.Count==36,"Outdoor footprint is incorrect.");
        var window=Place("wall_finish_25",origin+new Vector3Int(22,0,23));
        var canopy=Place("decor_shop_awning",window.anchorCell);
        Require(canopy.decorMount==DecorMount.Awning,"Awning missed the display window.");
        Refuses("decor_shop_awning",window.anchorCell);
        var outerWall=Place("wall_finish_01",origin+new Vector3Int(18,0,24));
        var innerLamp=Place("decor_roof_lantern",outerWall.anchorCell,2);
        var roofLamp=Place("decor_roof_lantern",outerWall.anchorCell);
        Require(roofLamp.decorMount==DecorMount.ExteriorWall && roofLamp.occupiedCells.Count==0 &&
            roofLamp.transform.position.z>innerLamp.transform.position.z,
            "The outdoor lantern must attach to the exterior wall.");
        var summerFlags=Place("decor_summer_stripe_flags",origin+new Vector3Int(27,0,16));
        Require(summerFlags.decorMount==DecorMount.Street && summerFlags.occupiedCells.Count==14,
            "The summer flags must reserve their outdoor footprint.");
        var paperLantern=Place("decor_paper_lantern",origin+new Vector3Int(12,0,10));
        Require(paperLantern.decorMount==DecorMount.Ceiling,"The paper lantern must hang from the ceiling.");
        var uplight=Item("decor_aisle_uplight");
        var baseAnchor=origin+new Vector3Int(3,0,17);
        var grid=build.grid;
        var expected=BuildPlacementPose.Position(grid,uplight,baseAnchor,false,0);
        expected.y=floors.SurfaceElevation(baseAnchor);
        var direction=new Vector3(.74f,-.37f,.55f).normalized;
        var pointer=new Ray(expected-direction*8,direction);
        Require(BuildPlacementPose.TryPointerAnchor(grid,floors,uplight,pointer,0,false,true,false,out var lampAnchor) &&
            lampAnchor==baseAnchor,"A floor lamp must snap with the center of its four cells under the cursor.");

        var noCeiling=grid.CellToWorld(origin+new Vector3Int(22,0,3));
        noCeiling.y=floors.SurfaceElevation(grid.WorldToCell(noCeiling));
        var ceilingPointer=new Ray(noCeiling-direction*8,direction);
        var pendantItem=paperLantern.item;
        Require(BuildPlacementPose.TryPointerAnchor(grid,floors,pendantItem,ceilingPointer,0,true,true,false,out var invalidAnchor) &&
            Vector3.Distance(BuildPlacementPose.Position(grid,pendantItem,invalidAnchor,false,0),new Vector3(noCeiling.x,grid.origin.y,noCeiling.z)) <= grid.cellSize,
            "A missing ceiling must not displace the red preview away from the cursor.");

        build.EnterBuildMode(uplight);
        var request=new PlacementRequest(uplight,lampAnchor,false,0);
        build.preview.SetPose(expected,Quaternion.identity);
        build.preview.ShowPlacement(grid,request,build.CanPlaceAt(lampAnchor),floors);
        var paint=build.preview.GetComponentsInChildren<MeshFilter>().Single(mesh=>mesh.name=="Placement footprint");
        var marks=paint.sharedMesh.vertices.Select(paint.transform.TransformPoint).ToArray();
        Require(marks.Length==16 && marks.All(vertex=>
        {
            var cell=grid.WorldToCell(vertex);
            cell.y=lampAnchor.y;
            return Mathf.Abs(vertex.y-floors.SurfaceElevation(cell)-.003f)<.001f;
        }),
            "The four placement markers must be drawn directly above the painted floor.");
        build.ExitBuildMode();
        Place("decor_entry_mat",origin+new Vector3Int(19,0,21));
        var entry=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item.id=="door_01" && p.anchorCell.y==0)
            .OrderBy(p=>p.anchorCell.z).First();
        var mat=Item("decor_entry_mat");
        inventory.Add(mat);
        build.EnterBuildMode(mat);
        var matCells=Enumerable.Range(-5,11).SelectMany(x=>Enumerable.Range(-5,11)
            .Select(z=>entry.anchorCell+new Vector3Int(x,0,z)));
        Require(matCells.Any(cell=>build.TryPlaceAt(cell)),"Entry mat cannot be installed at the starter entrance.");
        build.ExitBuildMode();

        editor.Begin();
        var movedCell=origin+new Vector3Int(6,0,17);
        Require(editor.Select(plant) && editor.PreviewAt(movedCell).ok && editor.Confirm(),"Could not move floor decor.");
        Require(plant.anchorCell==movedCell && plant.facing==1,"Move lost the chosen position or rotation.");
        int oldStock=inventory.GetCount(plant.item);
        Require(editor.Select(plant) && editor.DeleteSelected() && inventory.GetCount(plant.item)==oldStock+1,
            "Returning decor did not credit warehouse stock.");
        editor.Finish();

        var smallSign=Place("decor_aisle_numbers",origin+new Vector3Int(10,0,6));
        var bigSign=Place("decor_department_sign",origin+new Vector3Int(12,0,12));
        editor.Begin();
        Require(editor.Select(smallSign),"Could not select small sign.");
        editor.SetSignText("OPEN 123456789");
        Require(editor.Select(bigSign),"Could not select large sign.");
        editor.SetSignText("Fruit & veg");
        Require(smallSign.SignText=="OPEN 123" && bigSign.SignText=="Fruit & veg" &&
            bigSign.GetComponentInChildren<TMPro.TextMeshPro>().text==bigSign.SignText,"Sign text did not update with its size limit.");
        foreach (var sign in new[] { smallSign, bigSign })
        {
            var label=sign.GetComponentInChildren<TMPro.TextMeshPro>();
            label.ForceMeshUpdate();
            Require(label.textInfo.characterCount==sign.SignText.Length &&
                label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(character=>character.isVisible)==
                sign.SignText.Count(character=>!char.IsWhiteSpace(character)) && label.textBounds.size.y>.02f,
                "Sign lettering was clipped or too small to read.");
        }
        editor.Finish();

        var before=build.BuildPlacedSaveData();
        var roundTrip=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(new GameData{placedObjects=before}));
        build.ApplyPlacedSaveData(roundTrip.placedObjects,catalog);
        yield return null;
        var after=build.BuildPlacedSaveData();
        Require(before.Where(p=>p.decorMount!=DecorMount.None).All(p=>after.Any(a=>a.itemId==p.itemId &&
            a.x==p.x && a.z==p.z && a.floor==p.floor && a.facing==p.facing && a.decorMount==p.decorMount &&
            a.hasDecorEnd==p.hasDecorEnd && a.decorEnd==p.decorEnd)),"A mounted item failed its save/load round trip.");
        Require(existing.All(old=>Object.FindObjectsOfType<PlacedObject>().Any(p=>p.item.id==old.id &&
            p.anchorCell==old.anchorCell && p.transform.position==old.position)),"A legacy frontage prop moved during save/load.");
        Require(after.Any(p=>p.itemId=="decor_aisle_numbers" && p.hasSignText && p.signText=="OPEN 123") &&
            after.Any(p=>p.itemId=="decor_department_sign" && p.hasSignText && p.signText=="Fruit & veg"),
            "Edited sign text was lost when loading.");

        var auditArea = new Vector3Int(1060,0,1060);
        build.territory.AddPurchasedRect(auditArea,auditArea+new Vector3Int(30,0,30));
        var floorAnchor = auditArea + new Vector3Int(2,0,2);
        var wallAnchor = auditArea + new Vector3Int(14,0,4);
        var windowAnchor = auditArea + new Vector3Int(14,0,9);
        var counterAnchor = auditArea + new Vector3Int(14,0,15);
        var streetAnchor = auditArea + new Vector3Int(2,0,15);
        var auditFloor = Enumerable.Range(0,10).SelectMany(x=>Enumerable.Range(0,10)
            .Select(z=>floorAnchor+new Vector3Int(x,0,z))).ToList();
        floors.PaintCells(auditFloor,Item("Floor_26_BrickPavers"));
        floors.PaintCells(auditFloor.Select(cell=>cell+Vector3Int.up).ToList(),Item("floor_cream"));
        floors.PaintCells(auditFloor.Select(cell=>cell+streetAnchor-floorAnchor).ToList(),Item("asphalt_01"));
        Place("wall_finish_01",wallAnchor);
        Place("wall_finish_25",windowAnchor);
        var auditCounter=Place("shelf_produce_01",counterAnchor);
        var rows = new List<string>{"Item,Mount,Result"};
        foreach(var item in entries.Append(Item("decor_sign")))
        {
            var mount=item.decorMount;
            var anchors=new List<(Vector3Int Cell,int Facing,Vector3Int? End)>();
            if((mount & DecorMount.Floor)!=0 || mount==DecorMount.None) anchors.Add((floorAnchor,0,null));
            if((mount & DecorMount.Ceiling)!=0) anchors.Add((floorAnchor,0,null));
            if((mount & (DecorMount.Wall|DecorMount.ExteriorWall))!=0)
            {
                anchors.Add((wallAnchor,0,null));
                anchors.Add((wallAnchor,2,null));
            }
            if((mount & DecorMount.Awning)!=0) anchors.Add((windowAnchor,0,null));
            if((mount & DecorMount.Street)!=0) anchors.Add((streetAnchor,0,null));
            if(mount==DecorMount.EntryMat) anchors.Add((floorAnchor,0,null));
            if(mount==DecorMount.Frontage)
            {
                var doorCell=entry.anchorCell;
                anchors.AddRange(Enumerable.Range(-7,15).SelectMany(x=>Enumerable.Range(-6,13)
                    .Select(z=>(doorCell+new Vector3Int(x,0,z),0,(Vector3Int?)null))));
            }
            if((mount & (DecorMount.Counter|DecorMount.ShelfUnderside))!=0)
                anchors.AddRange(auditCounter.occupiedCells.Select(cell=>(cell,0,(Vector3Int?)null)));
            if(mount==DecorMount.Garland) anchors.Add((wallAnchor,0,wallAnchor+new Vector3Int(-6,0,1)));
            inventory.Add(item);
            build.EnterBuildMode(item);
            string result="No compatible spot";
            bool placedAny=false;
            foreach(var (cell,facing,end) in anchors)
            {
                build.RotateSelected(facing);
                var check=build.CanPlaceAt(cell,end);
                if (!check.ok) result=check.message;
                if (check.ok && build.TryPlaceAt(cell,end))
                {
                    var placed=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.anchorCell==cell && p.facing==facing);
                    placedAny=true;
                    result=placed.decorMount.ToString();
                    if (placed.decorMount is DecorMount.Wall or DecorMount.ExteriorWall)
                    {
                        build.RotateSelected(2);
                        Require(build.TryPlaceAt(cell),item.id+" cannot mount on the opposite face of the same wall.");
                        var opposite=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.anchorCell==cell && p.facing==2);
                        opposite.gameObject.SetActive(false);
                        Object.Destroy(opposite.gameObject);
                        result+=" both faces";
                    }
                    build.grid.Release(placed.occupiedCells);
                    placed.gameObject.SetActive(false);
                    Object.Destroy(placed.gameObject);
                    build.ExitBuildMode();
                    break;
                }
                build.RotateSelected(-facing);
            }
            build.ExitBuildMode();
            rows.Add(item.id+",\""+mount+"\","+ (placedAny ? "PASS " : "FAIL ")+result);
        }
        System.IO.File.WriteAllLines("Library/DecorCollectionQA/catalog-placement.csv",rows);
        Require(rows.Count==100 && rows.Skip(1).All(row=>row.Contains(",PASS ")),
            "Full catalog placement failed: "+string.Join(" | ",rows.Where(row=>row.Contains(",FAIL "))));
        Debug.Log("Decor Collection QA passed: 98 items, five tabs, season and lock states, purchases, mounts, movement and legacy load.");
    }

    private static void Require(bool valid,string message)
    {
        if(!valid)throw new InvalidOperationException(message);
    }
}

