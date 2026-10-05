using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System.Collections;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.SaveSystem;

public class SaveManager : MonoBehaviour
{
    private string saveFilePath;
    private GameData gameData;

    public static SaveManager Instance;


    [Header("UI")]
    public Button saveButton;

    [Header("Money")]
    [SerializeField] private MoneyController _money;

    [Header("Build Save")]
    [SerializeField] private BuildInventory _buildInventory;
    [SerializeField] private BuildController _buildController;
    [SerializeField] private FloorPainter _floorPainter;
    [SerializeField] private BuildItemCatalog _buildCatalog;
    [SerializeField] private ProductSaveService productSaveService;
    [SerializeField] private RetailEmpireTycoon.StoreOperations.StoreOperations shopOperations;

    [Header("Territory/Store")]
    [SerializeField] private StorePrefabSpawner _storeSpawner;
    [SerializeField] private StoreProgression _progression;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (productSaveService == null)
            productSaveService = FindObjectOfType<ProductSaveService>(true);

        saveFilePath = Path.Combine(Application.persistentDataPath, "gameData.json");
        gameData = new GameData();

        FindRefs();
    }

    private void Start()
    {
        StartCoroutine(LoadAfterOneFrame());
        InvokeRepeating(nameof(AutoSave), 60f, 60f);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonClicked);
    }

    private void FindRefs()
    {
        if (shopOperations == null) shopOperations = FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>(true);
        if (_money == null)
            _money = FindObjectOfType<MoneyController>(true);

        if (_buildInventory == null)
            _buildInventory = FindObjectOfType<BuildInventory>(true);

        if (_buildController == null)
            _buildController = FindObjectOfType<BuildController>(true);

        if (_floorPainter == null)
            _floorPainter = FindObjectOfType<FloorPainter>(true);

        if (_buildCatalog == null)
            _buildCatalog = FindObjectOfType<BuildItemCatalog>(true);

        if (_progression == null)
            _progression = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);

        if (_storeSpawner == null)
            _storeSpawner = FindObjectOfType<StorePrefabSpawner>(true);

        if (productSaveService == null)
            productSaveService = FindObjectOfType<ProductSaveService>(true);
    }



    private IEnumerator LoadAfterOneFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        LoadGame();
    }


    public void SaveGame()
    {
        FindRefs();

        gameData ??= new GameData();
        if (shopOperations != null) gameData.shopOperations = shopOperations.BuildSaveData();

        if (_money != null)
            gameData.playerMoney = _money.Money;

        if (_progression != null)
            gameData.territory = _progression.BuildSaveData();

        if (_buildInventory != null)
            gameData.buildInventory = _buildInventory.BuildSaveData();

        if (_buildController != null)
            gameData.placedObjects = _buildController.BuildPlacedSaveData();

        if (_floorPainter != null)
            gameData.floorTiles = _floorPainter.BuildSaveData();

        if (productSaveService != null)
        {
            gameData.productInventory = productSaveService.BuildWarehouseSaveData();
            gameData.shelfStocks = productSaveService.BuildShelfSaveData();
        }

        string json = JsonUtility.ToJson(gameData, true);
        File.WriteAllText(saveFilePath, json);

        Debug.Log("[SaveManager] Saved: " + saveFilePath);
    }

    public void LoadGame()
    {
        FindRefs();

        if (!File.Exists(saveFilePath))
        {
            SpawnStoreFromProgressOrDefault();
            StartCoroutine(LoadProductsAfterWorldLoaded(gameData));
            return;
        }

        string json = File.ReadAllText(saveFilePath);
        gameData = JsonUtility.FromJson<GameData>(json);

        SpawnStoreFromProgressOrDefault();

        FindRefs();

        if (_money != null)
            _money.SetMoney(gameData.playerMoney);

        if (_buildInventory != null && _buildCatalog != null)
            _buildInventory.ApplySaveData(gameData.buildInventory, _buildCatalog);

        if (_buildController != null && _buildCatalog != null)
            _buildController.ApplyPlacedSaveData(gameData.placedObjects, _buildCatalog);

        if (_floorPainter != null && _buildCatalog != null)
            _floorPainter.ApplySaveData(gameData.floorTiles, _buildCatalog);

        StartCoroutine(LoadProductsAfterWorldLoaded(gameData));

        Debug.Log("[SaveManager] Loaded: " + saveFilePath);
    }


    private void SpawnStoreFromProgressOrDefault()
    {
        if (_storeSpawner == null)
            return;

        StoreLevelId desiredLevel = StoreLevelId.Lvl1;

        if (_progression != null && gameData?.territory != null)
        {
            _progression.ApplySaveData(gameData.territory);
            desiredLevel = _progression.State.CurrentLevel;
        }

        _storeSpawner.Spawn(desiredLevel);
    }

    private IEnumerator LoadProductsAfterWorldLoaded(GameData loadedData)
    {
        yield return null;
        yield return null;

        if (loadedData == null)
            yield break;

        if (productSaveService == null)
            productSaveService = FindObjectOfType<ProductSaveService>(true);

        if (productSaveService == null)
        {
            Debug.LogWarning("[SaveManager] ProductSaveService not found. Products were not loaded.");
            yield break;
        }

        productSaveService.ApplyWarehouseSaveData(loadedData.productInventory);
        productSaveService.ApplyShelfSaveData(loadedData.shelfStocks);
        if (shopOperations != null) shopOperations.ApplySaveData(loadedData.shopOperations);

        Debug.Log("[SaveManager] Products loaded.");
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
