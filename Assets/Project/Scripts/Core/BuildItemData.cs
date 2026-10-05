using UnityEngine;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.Core
{
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Build Item", fileName = "BuildItem_")]
    [MovedFrom(false, "MyShopGame.Core", null, "BuildItemData")]
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

        public PlacementKind placementKind = PlacementKind.Object;
        [Header("Floor Paint")]
        public Material floorMaterial;



        [Min(1)]
        public int accessibilitySides = 1;

        public Vector2Int pivotOffset;

        [Header("Model alignment (measured in scaled prefab space)")]
        public bool alignModelToFootprint;
        public Bounds placementBounds;
        [Range(0, 3)] public int frontFacing;
        public bool twoSidedAccess;
        [Tooltip("Allows shop characters to cross the central opening; placement occupancy remains unchanged.")]
        public bool isDoorway;
        [Tooltip("Customers use the accessible front of this placed object for checkout.")]
        public bool isCheckout;

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
