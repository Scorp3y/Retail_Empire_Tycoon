using System;

namespace MyShopGame.Core
{
    public enum BuildCategory
    {
        Shelf,
        Cashier,
        Decoration,
        Utility,
        Other
    }

    public enum BuildSubCategory
    {
        None,
        Food,
        Drinks,
        Promo,
        Storage,
        Decorative
    }

    public enum ProductCategory
    {
        Any,
        Drinks,
        Snacks,
        Food,
        Household,
        Electronics
    }

    public enum ShelfType
    {
        Standard,
        Fridge,
        Promo,
        Premium
    }

    [Flags]
    public enum PlacementRuleFlags
    {
        None = 0,
        InsidePurchasedArea = 1 << 0,
        NoOverlap = 1 << 1,
        RequireAccessibility = 1 << 2,
        RequireFrontClear = 1 << 3,
        RequireWall = 1 << 4,
        RequireIndoor = 1 << 5
    }
}
