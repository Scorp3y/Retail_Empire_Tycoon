using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour
{
    public string itemName = "Shelf_1";
    public int baseCost;
    public bool isActive = false; 
    public GameObject objectToActivate;
    public Button buyButton;

    public int GetCurrentCost()
    {
        return baseCost; 
    }


}




