using System;
using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class PlacedShelfStock : MonoBehaviour
    {
        [Header("Shelf Type")]
        [SerializeField] private ShelfStorageType shelfType = ShelfStorageType.Fresh;

        [Header("Accepted Product Types")]
        [SerializeField] private List<ProductStorageType> acceptedProductTypes = new List<ProductStorageType>();

        [Header("Extra Product Rules")]
        [SerializeField] private List<ProductItemData> extraAllowedProducts = new List<ProductItemData>();
        [SerializeField] private List<ProductItemData> blockedProducts = new List<ProductItemData>();

        [Header("Stock")]
        [SerializeField] private ProductItemData currentProduct;
        [SerializeField] private ProductItemData assignedProduct;
        [SerializeField, Min(0)] private int currentAmount;
        [SerializeField, Min(1)] private int maxAmount = 24;

        [Header("Empty Visual")]
        [SerializeField] private GameObject emptyMarker;

        public event Action<PlacedShelfStock> Changed;

        public ShelfStorageType ShelfType => shelfType;
        public ProductItemData CurrentProduct => currentProduct;
        public ProductItemData AssignedProduct => currentProduct != null ? currentProduct : assignedProduct;
        public int CurrentAmount => currentAmount;
        public int CapacityFor(ProductItemData product)
        {
            var display = GetComponent<ShelfProductDisplay>();
            return display != null && display.UsesSlotLimits ? Mathf.Min(maxAmount, display.CompatibleCapacity(product)) : maxAmount;
        }
        public int MaxAmount => AssignedProduct != null ? CapacityFor(AssignedProduct) : maxAmount;
        public int FreeSpace => Mathf.Max(0, MaxAmount - currentAmount);
        public bool IsEmpty => currentAmount <= 0;
        public bool IsFull => currentAmount >= MaxAmount;

        private void OnEnable()
        {
            RefreshVisual();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxAmount = Mathf.Max(1, maxAmount);
            currentAmount = Mathf.Clamp(currentAmount, 0, maxAmount);

            if (currentAmount <= 0)
                currentProduct = null;
        }
#endif

        public bool CanAccept(ProductItemData product)
        {
            if (product == null)
                return false;

            if (IsFull)
                return false;

            if (!IsProductAllowed(product))
                return false;

            return IsEmpty || currentProduct == product;
        }

        public bool RefillFromInventory(ProductInventory inventory, ProductItemData product)
        {
            if (inventory == null || !CanAccept(product))
                return false;

            int available = inventory.GetCount(product);
            int amountToMove = Mathf.Min(available, Mathf.Max(0, CapacityFor(product) - currentAmount));

            if (amountToMove <= 0)
                return false;

            if (!inventory.TryConsume(product, amountToMove))
                return false;

            currentProduct = product;
            assignedProduct = product;
            currentAmount += amountToMove;

            NotifyChanged();
            return true;
        }

        public bool TryTakeOne(out ProductItemData product)
        {
            product = null;

            if (currentProduct == null || currentAmount <= 0)
                return false;

            product = currentProduct;
            currentAmount--;

            if (currentAmount <= 0)
                currentProduct = null;

            NotifyChanged();
            return true;
        }

        public void SetStockFromSave(ProductItemData product, int amount)
        {
            assignedProduct = product;
            if (product == null || amount <= 0)
            {
                currentProduct = null;
                currentAmount = 0;
                NotifyChanged();
                return;
            }

            currentProduct = product;
            currentAmount = Mathf.Clamp(amount, 0, CapacityFor(product));

            if (currentAmount <= 0)
                currentProduct = null;

            NotifyChanged();
        }

        public void ClearStock()
        {
            assignedProduct = null;
            currentProduct = null;
            currentAmount = 0;
            NotifyChanged();
        }

        public string GetProductName()
        {
            return currentProduct != null ? currentProduct.DisplayName : "None";
        }

        public bool IsProductAllowed(ProductItemData product)
        {
            if (product == null)
                return false;

            var display = GetComponent<ShelfProductDisplay>();
            if (display != null && display.CompatibleCapacity(product) == 0)
                return false;

            if (blockedProducts.Contains(product))
                return false;

            if (extraAllowedProducts.Contains(product))
                return true;

            var type = product.StorageType;
            // New ranges reuse the physical equipment they require, while keeping
            // distinct catalogue groups and stable pre-existing save enum values.
            var equipmentType = type == ProductStorageType.Preserves ? ProductStorageType.Groceries
                : type == ProductStorageType.ReadyMeals ? ProductStorageType.Dairy
                : type >= ProductStorageType.PetSupplies && type <= ProductStorageType.Kitchenware
                    ? ProductStorageType.Household : type;
            return acceptedProductTypes.Contains(ProductStorageType.Any)
                || acceptedProductTypes.Contains(type) || acceptedProductTypes.Contains(equipmentType);
        }

        private void NotifyChanged()
        {
            RefreshVisual();
            Changed?.Invoke(this);
        }

        private void RefreshVisual()
        {
            if (emptyMarker != null)
                emptyMarker.SetActive(IsEmpty);
        }
    }
}
