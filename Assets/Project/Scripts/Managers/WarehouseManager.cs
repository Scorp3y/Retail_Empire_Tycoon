using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class WarehouseManager : MonoBehaviour
{
    int playerMoney;
    bool shelfBought = false;

    public List<Product> products = new List<Product>();

    void Start()
    {
        foreach (var product in products)
        {
            if (!dataLoaded) product.quantity = 0;

            UpdateWarehouseUI(product);
            UpdateProductState(product);

            Product current = product;

            if (!shelfBought)
            {
                if (product.buy1Button != null) product.buy1Button.interactable = false;
                if (product.buy5Button != null) product.buy5Button.interactable = false;
                if (product.buy10Button != null) product.buy10Button.interactable = false;
            }
        }


    }

    void BuyProduct(Product product, int amount)
    {
        if (GameManager.Instance == null || product == null)
        {
            Debug.LogError("GameManager or product is null");
            return;
        }

        int totalCost = product.purchasingPrice * amount;

        if (GameManager.Instance.SpendMoney(totalCost))
        {
            product.quantity += amount;
            UpdateWarehouseUI(product);
            UpdateProductState(product);
        }
    }


    public void UpdateWarehouseUI(Product product)
    {
        product.warehouseText.text = $"{product.quantity} pc";
    }

    public void UpdateProductState(Product product)
    {
        if (product.itemObject != null)
        {
            product.itemObject[0].SetActive(product.quantity > 0);
            product.itemObject[1].SetActive(product.quantity > 0);
            product.itemObject[2].SetActive(product.quantity > 0);
        }
    }
    public void RefreshProductUI(Product product)
    {
        UpdateWarehouseUI(product);
        UpdateProductState(product);
    }

    public static WarehouseManager Instance;

    void Awake()
    {
        Instance = this;
    }

    public bool TryTakeProduct(string productName, int quantity)
    {
        var product = products.Find(p => p.productName == productName);

        if (product != null && product.quantity >= quantity)
        {
            product.quantity -= quantity;
            UpdateWarehouseUI(product);
            UpdateProductState(product);
            return true;
        }

        return false;
    }

    private bool dataLoaded = false;

    public void LoadProducts(List<ProductData> savedProducts)
    {
        foreach (var productData in savedProducts)
        {
            var product = products.Find(p => p.productName == productData.productName);
            if (product != null)
            {
                product.quantity = productData.quantity;
                RefreshProductUI(product);
            }
        }

        if (ShopManager.Instance.shopItems.Exists(i => i.isActive && i.itemName.StartsWith("Shelf")))
        {
            EnableProductButtons();
        }


        dataLoaded = true;
    }

    void OnEnable()
    {
        ShopManager.OnShelfPurchased += EnableProductButtons;
    }

    void OnDisable()
    {
        ShopManager.OnShelfPurchased -= EnableProductButtons;
    }

    public void EnableProductButtons()
    {
        foreach (var product in products)
        {
            ShopItem shelf = ShopManager.Instance.shopItems.Find(
                s => s.itemName == product.requiredShelfName
            );

            if (shelf != null && shelf.isActive)
            {
                EnableProductButtonsForProduct(product);
            }
            else
            {
                DisableProductButtonsForProduct(product);
            }

            RefreshProductUI(product);
        }
    }


    public void EnableProductButtonsForProduct(Product product)
    {
        if (product.buy1Button != null)
        {
            product.buy1Button.interactable = true;
            product.buy1Button.onClick.AddListener(() => BuyProduct(product, 1));
        }

        if (product.buy5Button != null)
        {
            product.buy5Button.interactable = true;
            product.buy5Button.onClick.AddListener(() => BuyProduct(product, 5));
        }

        if (product.buy10Button != null)
        {
            product.buy10Button.interactable = true;
            product.buy10Button.onClick.AddListener(() => BuyProduct(product, 10));
        }
    }

    void DisableProductButtonsForProduct(Product product)
    {
        if (product.buy1Button != null)
        {
            product.buy1Button.interactable = false;
        }

        if (product.buy5Button != null)
        {
            product.buy5Button.interactable = false;
        }

        if (product.buy10Button != null)
        {
            product.buy10Button.interactable = false;
        }
    }


}
