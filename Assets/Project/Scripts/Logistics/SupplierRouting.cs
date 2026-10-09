using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.Logistics
{
    public enum SupplierKind { Wholesale, Farmers, Bakery, Equipment, Construction, Decoration, Showroom }

    /// <summary>Stable category routing: existing paid orders keep their IDs and save format.</summary>
    public static class SupplierRouting
    {
        public static SupplierKind ForProduct(ProductStorageType type)
        {
            if(type==ProductStorageType.Bakery)return SupplierKind.Bakery;
            if(type==ProductStorageType.Produce)return SupplierKind.Farmers;
            return SupplierKind.Wholesale;
        }
        public static SupplierKind ForBuilding(BuildItemData item)
        {
            if(item==null)throw new System.ArgumentNullException(nameof(item));
            // The legacy cash register is listed under Structures, but its actual purpose is trading equipment.
            if(item.isCheckout)return SupplierKind.Equipment;
            var category=item.category;
            if(category==BuildCategory.Decoration)return SupplierKind.Decoration;
            if(category==BuildCategory.Structures)return SupplierKind.Construction;
            return SupplierKind.Equipment;
        }
        public static string Name(SupplierKind supplier)
        {
            switch(supplier)
            {
                case SupplierKind.Wholesale:return "Продуктовая база";
                case SupplierKind.Farmers:return "Фермерский рынок";
                case SupplierKind.Bakery:return "Пекарня";
                case SupplierKind.Equipment:return "Торговое оборудование";
                case SupplierKind.Construction:return "Строймаркет";
                case SupplierKind.Decoration:return "Цветы и декор";
                case SupplierKind.Showroom:return "Автосалон";
                default:return "Поставщик";
            }
        }
    }
}
