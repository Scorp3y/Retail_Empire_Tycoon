using System;
using UnityEngine;

namespace MyShopGame.Core
{
    [CreateAssetMenu(menuName = "My Shop Game/Shelf Data", fileName = "ShelfData_")]
    public class ShelfData : ScriptableObject
    {
        [Header("Identity")]
        public string shelfId;
        public ShelfType shelfType = ShelfType.Standard;
        public ProductCategory acceptedCategory = ProductCategory.Any;

        [Header("Capacity")]
        public int baseSlots = 4;
        public int baseMaxStockPerSlot = 10;
        public bool requiresRestock;

        [Header("Tiers")]
        public ShelfTierData[] tiers = Array.Empty<ShelfTierData>();

        [Header("Visual fill")]
        public ShelfFillVisualProfile fillVisualProfile;

        [Header("Bonuses")]
        public float visibilityBonus;
        public float impulseBonus;
        public float baseAttractiveness;

        [Serializable]
        public class ShelfTierData
        {
            public int tierIndex = 1;
            public string tierName = "Tier 1";
            public int slots = 4;
            public int upgradePrice = 100;

            public GameObject tierVisualOverride;
            public Material tierMaterialOverride;

            public float salesMultiplier = 1f;
        }

        public ShelfTierData GetTier(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0)
                return new ShelfTierData { tierIndex = 1, slots = baseSlots, upgradePrice = 0, salesMultiplier = 1f };

            foreach (var t in tiers)
            {
                if (t != null && t.tierIndex == tierIndex)
                    return t;
            }

            return tiers[0];
        }
    }
}
