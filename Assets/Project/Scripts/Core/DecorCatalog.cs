using System;

namespace RetailEmpireTycoon.Core
{
    public enum DecorGroup { None, Plants, Lighting, Seating, Walls, Storefront }
    public enum DecorSeason { None, Spring, Summer, Autumn, Winter }

    [Flags]
    public enum DecorMount
    {
        None = 0, Floor = 1, Counter = 2, Wall = 4, Ceiling = 8, Frontage = 16,
        Street = 32, EntryMat = 64, Awning = 128, Garland = 256
    }

    public static class DecorCatalog
    {
        public const int BeautyCopies = 3;
        public static bool IsMounted(DecorMount mount) =>
            (mount & (DecorMount.Counter | DecorMount.Wall | DecorMount.Ceiling | DecorMount.Awning | DecorMount.Garland)) != 0;
        public static bool UsesFloorSpace(DecorMount mount) => !IsMounted(mount) && mount != DecorMount.EntryMat;

        public static string Section(DecorGroup group) => group switch
        {
            DecorGroup.Plants => "Plants & Flowers",
            DecorGroup.Lighting => "Lighting",
            DecorGroup.Seating => "Seating & Amenities",
            DecorGroup.Walls => "Walls & Details",
            _ => "Storefront & Seasonal"
        };
    }
}

