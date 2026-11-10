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
        var entries=catalog.Items.Where(i=>i!=null && i.decorGroup!=DecorGroup.None).ToArray();
        Require(entries.Length==48 && entries.Select(i=>i.id).Distinct().Count()==48 && entries.All(i=>i.prefab!=null && i.icon!=null),
            "The catalog must resolve 48 unique decor models and icons.");
        Require(entries.Count(i=>i.decorSeason!=DecorSeason.None)==4, "Missing seasonal labels.");
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
        shop.GetComponentsInChildren<Button>().Single(button=>button.name==DecorCatalog.Section(DecorGroup.Storefront)).onClick.Invoke();
        yield return null;
        var autumn=Item("decor_autumn_crate");
        var autumnCard=shop.GetComponentsInChildren<ShopItemCard>().Single(card=>card.icon.sprite==autumn.icon);
        Require(autumnCard.sizeText.text.Contains("Autumn") && autumnCard.buyButton.interactable,"Seasonal catalog rule missing.");
        int budget=orders.money.Money;
        int deliveryEntries=orders.Ledger.Entries.Count();
        autumnCard.buyButton.onClick.Invoke();
        Require(orders.money.Money==budget-autumn.price && inventory.GetCount(autumn)==1 &&
            orders.Ledger.Entries.Count()==deliveryEntries,"Decor purchase did not go directly to storage.");
        var fountain=Item("decor_water_feature");
        budget=orders.money.Money;
        Require(!orders.Order(fountain) && orders.money.Money==budget && inventory.GetCount(fountain)==0,"Locked item charged the player.");
        controls.HandleEscape();
        yield return null;

        var origin=new Vector3Int(200,0,190);
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
            return Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.anchorCell==at);
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

        var counter=Place("shelf_produce_01",origin+new Vector3Int(15,0,13));
        Refuses("decor_counter_succulent",origin+new Vector3Int(19,0,18));
        var succulentCell=counter.occupiedCells.OrderBy(c=>(build.grid.CellToWorld(c)-counter.transform.position).sqrMagnitude).First();
        var succulent=Place("decor_counter_succulent",succulentCell);
        Require(succulent.decorMount==DecorMount.Counter && succulent.transform.position.y>counter.transform.position.y+.2f,
            "Counter decor did not snap above the fixture.");
        var plant=Place("decor_snake_plant",origin+new Vector3Int(5,0,16),1);
        Refuses("decor_snake_plant",origin+new Vector3Int(29,0,15));
        var outdoor=Place("decor_water_feature",origin+new Vector3Int(27,0,8));
        Require(outdoor.decorMount==DecorMount.Street && outdoor.occupiedCells.Count==36,"Outdoor footprint is incorrect.");
        var window=Place("wall_finish_25",origin+new Vector3Int(22,0,23));
        var canopy=Place("decor_shop_awning",window.anchorCell);
        Require(canopy.decorMount==DecorMount.Awning,"Awning missed the display window.");
        Refuses("decor_shop_awning",window.anchorCell);
        Refuses("decor_entry_mat",origin+new Vector3Int(19,0,18));
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
        Debug.Log("Decor Collection QA passed: 48 items, five tabs, season and lock states, purchases, mounts, movement and legacy load.");
    }

    private static void Require(bool valid,string message)
    {
        if(!valid)throw new InvalidOperationException(message);
    }
}

