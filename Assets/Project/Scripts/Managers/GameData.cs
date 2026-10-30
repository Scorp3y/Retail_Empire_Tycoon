using System;
using System.Collections.Generic;
using RetailEmpireTycoon.SaveSystem;

[Serializable]
public class GameData
{
    public const int StartingCapital = 300000;
    public bool usesModularStore;
    public int shopProportionsVersion;
    public int starterSiteVersion;
    public int playerMoney = StartingCapital;
    public RetailEmpireTycoon.City.VehicleFleetSaveData vehicles = new RetailEmpireTycoon.City.VehicleFleetSaveData();
    public RetailEmpireTycoon.Logistics.DeliverySaveData deliveries = new RetailEmpireTycoon.Logistics.DeliverySaveData();
    public RetailEmpireTycoon.Logistics.DriverJourneySaveData driverJourney = new RetailEmpireTycoon.Logistics.DriverJourneySaveData();
    public RetailEmpireTycoon.StoreOperations.ShopOperationsSaveData shopOperations = new RetailEmpireTycoon.StoreOperations.ShopOperationsSaveData();

    public TerritorySaveData territory = new TerritorySaveData();

    public List<BuildInventorySaveEntry> buildInventory = new List<BuildInventorySaveEntry>();
    public List<PlacedBuildSaveData> placedObjects = new List<PlacedBuildSaveData>();
    public List<FloorTileSaveData> floorTiles = new List<FloorTileSaveData>();
    public List<ProductInventorySaveEntry> productInventory = new List<ProductInventorySaveEntry>();
    public List<ShelfStockSaveEntry> shelfStocks = new List<ShelfStockSaveEntry>();
}

[Serializable]
public class TerritorySaveData
{
    // Missing in older saves: legacy region ownership must be expanded without taking land away.
    public int layoutVersion;
    public List<string> purchased = new List<string>();
    public string storeLevel = "Lvl1";
}

[Serializable]
public class BuildInventorySaveEntry
{
    public string itemId;
    public int count;
}

[Serializable]
public class PlacedBuildSaveData
{
    public string itemId;
    public int x;
    public int floor;
    public int z;
    public bool rotated;
    public int facing;
    // Missing in old saves: their wall sections retain the previous multi-cell dimensions.
    public int wallModuleVersion;
    public bool playerParking;
    public int parkingModuleVersion;
}

[Serializable]
public class FloorTileSaveData
{
    public string itemId;
    public int x;
    // Zero when absent in legacy saves. Layer 1 is both a ceiling and the second-storey floor.
    public int floor;
    public int z;
}
