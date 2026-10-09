using UnityEngine;

namespace RetailEmpireTycoon.Core
{
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Product Item", fileName = "Product_")]
    public sealed class ProductItemData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        [Header("Economy")]
        [SerializeField, Min(0)] private int buyPrice = 10;
        [SerializeField, Min(0)] private int sellPrice = 20;

        [Header("Box")]
        [SerializeField, Min(1)] private int boxAmount = 10;
        [SerializeField, Min(.001f)] private float unitWeightKg = .5f;
        [SerializeField, Min(.0001f)] private float unitVolumeM3 = .01f;

        [Header("Product Type")]
        [SerializeField] private ProductStorageType storageType = ProductStorageType.Any;

        [Header("Shelf Visual")]
        [SerializeField] private GameObject shelfDisplayPrefab;

        [Header("Legacy Category")]
        [SerializeField] private ProductCategory category = ProductCategory.Any;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int BuyPrice => buyPrice;
        public int SellPrice => sellPrice;
        public int BoxAmount => boxAmount;
        public float UnitWeightKg => unitWeightKg;
        public float UnitVolumeM3 => unitVolumeM3;
        public ProductStorageType StorageType => storageType;
        public GameObject ShelfDisplayPrefab => shelfDisplayPrefab;
        public ProductCategory Category => category;

#if UNITY_EDITOR
        private void OnValidate()
        {
            buyPrice = Mathf.Max(0, buyPrice);
            sellPrice = Mathf.Max(0, sellPrice);
            boxAmount = Mathf.Max(1, boxAmount);
        }
#endif
    }
}
