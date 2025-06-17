using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public int playerMoney;
    public List<ProductData> products = new List<ProductData>();
    public List<ShopItemData> shopItems = new List<ShopItemData>();
}

[System.Serializable]
public class ProductData
{
    public string productName;
    public int price;
    public Transform shelfPosition;
    public int quantity;
}

[System.Serializable]
public class ShopItemData
{
    public string itemName;
    public bool isActive; 
}

