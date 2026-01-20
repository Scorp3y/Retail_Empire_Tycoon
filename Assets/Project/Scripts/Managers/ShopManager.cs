using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    public List<ShopItem> shopItems = new List<ShopItem>();
    public delegate void ShelfPurchasedAction();
    public static event ShelfPurchasedAction OnShelfPurchased;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    void Start()
    {
        LoadShopItems();
    }

    void LoadShopItems()
    {
        foreach (ShopItem item in shopItems)
        {
            item.objectToActivate.SetActive(item.isActive);
            item.buyButton.gameObject.SetActive(!item.isActive);

            if (item.isActive)
            {
                if (item.itemName == "Shelf")
                {
                    WarehouseManager.Instance.EnableProductButtons();
                }
            }
            else
            {
                item.buyButton.onClick.RemoveAllListeners();
                item.buyButton.onClick.AddListener(() => BuyItem(item));

            }
            if (item.objectToActivate == null)
            {
                Debug.LogError($"ShopItem '{item.itemName}' has no objectToActivate assigned.", item);
                continue;
            }

            if (item.buyButton == null)
            {
                Debug.LogError($"ShopItem '{item.itemName}' has no buyButton assigned.", item);
                continue;
            }

        }

    }


    public void BuyItem(ShopItem item)
    {
        int cost = item.GetCurrentCost();

        if (GameManager.Instance.SpendMoney(cost))
        {
            item.isActive = true;
            item.objectToActivate.SetActive(true);
            Destroy(item.buyButton.gameObject);

            if (item.itemName == "Shelf_1")
            {
                OnShelfPurchased?.Invoke();
            }
            WarehouseManager.Instance.EnableProductButtons();
            SaveManager.Instance.SaveGame();
        }
    }

}
