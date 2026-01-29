using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Product : MonoBehaviour
{
    public string productName;
    public int purchasingPrice;
    public int sellPrice;
    public int quantity;
    public Transform shelfPoint;

    public TextMeshProUGUI warehouseText;

    public Button buy1Button, buy5Button, buy10Button;
    public string requiredShelfName = "Shelf_1";
    public GameObject[] itemObject;
}
