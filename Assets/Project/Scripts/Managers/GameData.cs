using System;
using System.Collections.Generic;
using RetailEmpireTycoon.SaveSystem;

[Serializable]
public class GameData
{
    public bool usesModularStore;
    public int playerMoney = 6000;
    public RetailEmpireTycoon.Logistics.DeliverySaveData deliveries = new RetailEmpireTycoon.Logistics.DeliverySaveData();
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
    public int z;
    public bool rotated;
    public int facing;
    // Missing in old saves: their wall sections retain the previous multi-cell dimensions.
    public int wallModuleVersion;
    public bool playerParking;
}

[Serializable]
public class FloorTileSaveData
{
    public string itemId;
    public int x;
    public int z;
}
