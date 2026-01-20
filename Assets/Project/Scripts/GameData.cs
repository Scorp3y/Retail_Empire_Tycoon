using System;
using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    public int playerMoney;
    public List<ProductData> products = new List<ProductData>();
    public List<ShopItemData> shopItems = new List<ShopItemData>();
    public TerritorySaveData territory = new TerritorySaveData();
}

[System.Serializable]
public class TerritorySaveData
{
    public List<string> purchased = new List<string>();
    public string storeLevel = "Lvl1";                  
}

[System.Serializable]
public class ProductData
{
    public string productName;
    public int price;
    public int quantity;
}

[System.Serializable]
public class ShopItemData
{
    public string itemName;
    public bool isActive;
}
