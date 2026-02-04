using UnityEngine;

namespace MyShopGame.Core
{
    [CreateAssetMenu(menuName = "My Shop Game/Product", fileName = "Product_")]
    public class ProductData : ScriptableObject
    {
        public string productId;
        public string displayName;
        public Sprite icon;
        public ProductCategory category = ProductCategory.Any;

        [Header("Economy")]
        public int buyCost;
        public int sellPrice;

        [Header("World")]
        public GameObject worldPrefab;
        public ScriptableObject visualToken;
    }
}
