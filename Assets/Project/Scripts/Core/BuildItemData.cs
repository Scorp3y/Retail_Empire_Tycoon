using UnityEngine;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.Core
{
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Build Item", fileName = "BuildItem_")]
    [MovedFrom(false, "MyShopGame.Core", null, "BuildItemData")]
    public class BuildItemData : ScriptableObject
    {
        public const int WallBlockSizeInCells = 1;
        public const int WallThicknessInCells = 2;
        public const int CurrentWallModuleVersion = 3;
        [HideInInspector] public float legacyWallDepth;
        [HideInInspector] public float wallCellSize;
        [HideInInspector] public int wallGeometryVersion;
        [HideInInspector] public GameObject previousWallPrefab;
        [HideInInspector] public Bounds previousWallBounds;
        [HideInInspector] public Vector3 previousWallAlignmentOffset;
        [HideInInspector] public int decorationProportionsVersion;

        public Vector2Int WallFootprint(int version) => version == 0 && id == "wall_01"
            ? new Vector2Int(4, 2) : version < 2 ? new Vector2Int(2, 2) : version == 2 ? Vector2Int.one : footprint;

        public GameObject WallPrefab(int version) => version < 3 && previousWallPrefab != null ? previousWallPrefab : prefab;
        public Bounds WallBounds(int version) => version < 3 && previousWallPrefab != null ? previousWallBounds : placementBounds;

        public Vector2Int WallPivot(int version) => version == 0 && id == "wall_01" ? new Vector2Int(-1, 0) : pivotOffset;

        public Vector3 WallModelScale(int version)
        {
            var savedFootprint = WallFootprint(version);
            var modelBounds = WallBounds(version);
            if(version == 2)return Vector3.one;
            float width = wallCellSize > 0 ? wallCellSize * savedFootprint.x : modelBounds.size.x * savedFootprint.x / footprint.x;
            float depth = id == "wall_01" && legacyWallDepth > 0 ? legacyWallDepth
                : wallCellSize > 0 ? wallCellSize * savedFootprint.y : modelBounds.size.z * savedFootprint.y / footprint.y;
            return new Vector3(width / modelBounds.size.x, 1, depth / modelBounds.size.z);
        }
        [Header("Identity")]
        public string id;
        public string displayName;
        [TextArea(1, 3)]
        public string description;
        public Sprite icon;

        [Header("Shop")]
        public int price = 100;
        [Tooltip("Retired items remain loadable and usable from inventory, but cannot be ordered again.")]
        public bool hiddenFromShop;
        [Min(.01f)] public float weightKg = 25;
        [Min(.001f)] public float cargoVolumeM3 = .25f;
        public bool isAsphalt;
        public bool isParkingSpace;
        [HideInInspector] public int parkingGeometryVersion;
        [HideInInspector] public GameObject previousParkingPrefab;
        [HideInInspector] public Vector2Int previousParkingFootprint;
        [HideInInspector] public Bounds previousParkingBounds;
        public bool isUnloadingGate;
        public bool isWall;
        [Min(0)] public int beautyPoints;
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
        [Tooltip("Offset from bounds centre to the model's connection origin, in prefab space.")]
        public Vector3 placementAlignmentOffset;
        [Range(0, 3)] public int frontFacing;
        public bool twoSidedAccess;
        [Tooltip("Allows shop characters to cross the central opening; placement occupancy remains unchanged.")]
        public bool isDoorway;
        [Tooltip("Customers use the accessible front of this placed object for checkout.")]
        public bool isCheckout;
        public StoreyConnectionKind storeyConnection;
        public StoreyTravelDirection storeyDirection;

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
