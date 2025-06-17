using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;

public class SaveManager : MonoBehaviour
{
    private string saveFilePath;
    private GameData gameData;
    public static SaveManager Instance;
    public Button saveButton;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        gameData = new GameData();
        saveFilePath = Application.persistentDataPath + "/gameData.json";

        
    }

    void Start()
    {
        LoadGame();
        InvokeRepeating("AutoSave", 60f, 60f);

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveButtonClicked);
        }
    }

    public void SaveGame()
    {
        
        gameData.playerMoney = GameManager.Instance.playerMoney;

        
        gameData.products.Clear();
        foreach (var product in WarehouseManager.Instance.products)
        {
            gameData.products.Add(new ProductData { productName = product.productName, quantity = product.quantity });
        }



        gameData.shopItems.Clear();
        foreach (var shopItem in ShopManager.Instance.shopItems)
        {
            gameData.shopItems.Add(new ShopItemData
            {
                itemName = shopItem.itemName,
                isActive = shopItem.isActive 
            });
        }




        string json = JsonUtility.ToJson(gameData, true);
        try
        {
            File.WriteAllText(saveFilePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error saving game: " + e.Message);
        }
    }

    public void LoadGame()
    {

        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            gameData = JsonUtility.FromJson<GameData>(json);

            if (GameManager.Instance != null)
                GameManager.Instance.playerMoney = gameData.playerMoney;

            if (WarehouseManager.Instance != null)
            {
                foreach (var productData in gameData.products)
                {
                    var product = WarehouseManager.Instance.products.Find(p => p.productName == productData.productName);
                    if (product != null)
                    {
                        product.quantity = productData.quantity;
                        WarehouseManager.Instance.UpdateWarehouseUI(product);
                        WarehouseManager.Instance.UpdateProductState(product);
                    }
                }
            }

            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.LoadProducts(gameData.products);
            }

            if (ShopManager.Instance != null)
            {
                foreach (var shopItemData in gameData.shopItems)
                {
                    var shopItem = ShopManager.Instance.shopItems.Find(s => s.itemName == shopItemData.itemName);
                    if (shopItem != null)
                    {
                        shopItem.isActive = shopItemData.isActive;

                        if (shopItem.objectToActivate != null)
                            shopItem.objectToActivate.SetActive(shopItem.isActive);

                        if (shopItem.buyButton != null)
                        {
                            if (!shopItem.isActive)
                            {
                                shopItem.buyButton.gameObject.SetActive(true);
                                shopItem.buyButton.onClick.RemoveAllListeners();
                                shopItem.buyButton.onClick.AddListener(() => ShopManager.Instance.BuyItem(shopItem));
                            }
                            else
                            {
                                Destroy(shopItem.buyButton.gameObject);
                            }
                        }
                    }
                }

                WarehouseManager.Instance.EnableProductButtons();
            }


        }
    }

    public void AutoSave()
    {
        SaveGame();
    }

    private void OnSaveButtonClicked()
    {
        SaveGame();
    }
}

