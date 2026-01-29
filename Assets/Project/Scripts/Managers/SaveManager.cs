using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SaveManager : MonoBehaviour
{
    private string saveFilePath;
    private GameData gameData;

    public static SaveManager Instance;
    public Button saveButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        gameData = new GameData();
        saveFilePath = Path.Combine(Application.persistentDataPath, "gameData.json");
    }

    private void Start()
    {
        StartCoroutine(LoadAfterOneFrame());
        InvokeRepeating(nameof(AutoSave), 60f, 60f);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonClicked);
    }

    private IEnumerator LoadAfterOneFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        LoadGame();

        if (GameManager.Instance != null)
            GameManager.Instance.ForceRefreshUI();

        if (WarehouseManager.Instance != null)
            WarehouseManager.Instance.EnableProductButtons();
    }

    public void SaveGame()
    {
        gameData ??= new GameData();

        // money
        if (GameManager.Instance != null)
            gameData.playerMoney = GameManager.Instance.playerMoney;

        // products
        gameData.products ??= new List<ProductData>();
        gameData.products.Clear();

        if (WarehouseManager.Instance != null && WarehouseManager.Instance.products != null)
        {
            foreach (var product in WarehouseManager.Instance.products)
            {
                if (product == null) continue;

                gameData.products.Add(new ProductData
                {
                    productName = product.productName,
                    quantity = product.quantity
                });
            }
        }

        // shop items
        gameData.shopItems ??= new List<ShopItemData>();
        gameData.shopItems.Clear();

        if (ShopManager.Instance != null && ShopManager.Instance.shopItems != null)
        {
            foreach (var shopItem in ShopManager.Instance.shopItems)
            {
                if (shopItem == null) continue;

                gameData.shopItems.Add(new ShopItemData
                {
                    itemName = shopItem.itemName,
                    isActive = shopItem.isActive
                });
            }
        }

        // territory
        var prog = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        if (prog != null)
            gameData.territory = prog.BuildSaveData();

        string json = JsonUtility.ToJson(gameData, true);
        File.WriteAllText(saveFilePath, json);
    }

    public void LoadGame()
    {
        if (!File.Exists(saveFilePath))
        {
            SpawnStoreFromProgressOrDefault();
            return;
        }

        string json = File.ReadAllText(saveFilePath);
        gameData = JsonUtility.FromJson<GameData>(json);

        // money
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerMoney = gameData.playerMoney;
            GameManager.Instance.ForceRefreshUI();
        }

        // products
        if (WarehouseManager.Instance != null && gameData.products != null)
            WarehouseManager.Instance.LoadProducts(gameData.products);

        // shop items
        if (ShopManager.Instance != null && gameData.shopItems != null)
        {
            foreach (var d in gameData.shopItems)
            {
                var shopItem = ShopManager.Instance.shopItems.Find(x => x.itemName == d.itemName);
                if (shopItem == null) continue;

                shopItem.isActive = d.isActive;

                if (shopItem.objectToActivate != null)
                    shopItem.objectToActivate.SetActive(shopItem.isActive);

                if (shopItem.buyButton != null)
                {
                    if (!shopItem.isActive)
                    {
                        shopItem.buyButton.gameObject.SetActive(true);
                        shopItem.buyButton.onClick.RemoveAllListeners();

                        ShopItem captured = shopItem;
                        shopItem.buyButton.onClick.AddListener(
                            () => ShopManager.Instance.BuyItem(captured)
                        );
                    }
                    else
                    {
                        Destroy(shopItem.buyButton.gameObject);
                    }
                }
            }
        }

        SpawnStoreFromProgressOrDefault();
    }

    private void SpawnStoreFromProgressOrDefault()
    {
        var prog = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        var spawner = FindObjectOfType<StorePrefabSpawner>();

        if (prog != null && gameData != null && gameData.territory != null)
            prog.ApplySaveData(gameData.territory);

        if (spawner != null)
        {
            var level = (prog != null)
                ? prog.State.CurrentLevel
                : StoreLevelId.Lvl1;

            spawner.Spawn(level);
        }
    }

    public void AutoSave() => SaveGame();
    private void OnSaveButtonClicked() => SaveGame();
}
