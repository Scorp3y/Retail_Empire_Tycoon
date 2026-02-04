using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.Core
{
    [CreateAssetMenu(menuName = "My Shop Game/Build Item", fileName = "BuildItem_")]
    public class BuildItemData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        [TextArea(1, 3)]
        public string description;
        public Sprite icon;

        [Header("Shop")]
        public int price = 100;
        public bool unlockedByDefault = true;
        public BuildUnlockCondition unlockCondition;

        [Header("Category")]
        public BuildCategory category = BuildCategory.Shelf;
        public BuildSubCategory subCategory = BuildSubCategory.None;

        [Header("Placement")]
        public Vector2Int footprint = new Vector2Int(2, 1);
        public bool allowRotation = true;
        public PlacementRuleFlags ruleFlags =
            PlacementRuleFlags.InsidePurchasedArea |
            PlacementRuleFlags.NoOverlap |
            PlacementRuleFlags.RequireAccessibility;

        [Min(1)]
        public int accessibilitySides = 1;

        public Vector2Int pivotOffset;

        [Header("Prefab")]
        public GameObject prefab;
        public Material previewValidMaterial;
        public Material previewInvalidMaterial;

        [Header("Extra data")]
        public ShelfData shelfData;
        public ScriptableObject decorationData;
        public ScriptableObject cashierData;
    }
}
