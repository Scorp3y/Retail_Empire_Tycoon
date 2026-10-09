using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Translated display names never change the stable IDs used by saved games.</summary>
    public static class ShopText
    {
        public static bool Russian => PlayerPrefs.GetString("lang", "ru") == "ru";
        public static string Get(string ru, string en) => Russian ? ru : en;
        public static string Item(BuildItemData item)
        {
            if (item == null) return Get("Предмет", "Item");
            switch(item.id)
            {
                case "floor_cream": return Get("Светлая плитка", "Cream tile");
                case "floor_graphite": return Get("Графитовая плитка", "Graphite tile");
                case "floor_checker": return Get("Шахматный пол", "Checkerboard");
                case "floor_wood": return Get("Деревянный пол", "Wooden floor");
                case "floor_concrete": return Get("Бетон", "Concrete");
                case "floor_terrazzo": return Get("Терраццо", "Terrazzo");
                case "decor_plant": return Get("Растение в горшке", "Potted plant");
                case "decor_bench": return Get("Деревянная скамья", "Wooden bench");
                case "decor_bin": return Get("Урна", "Waste bin");
                case "decor_lamp": return Get("Напольный светильник", "Floor lamp");
                case "decor_planter": return Get("Цветочная кадка", "Flower planter");
                case "decor_sign": return Get("Вывеска магазина", "Market sign");
            }
            if (item.id == "wallcorner_01") return Get("Угловая стена", "Corner wall");
            if (item.id == "shelf_fresh_01") return Get("Хлебный стеллаж", "Bakery rack");
            if (item.id == "shelf_produce_01") return Get("Овощи и фрукты", "Produce rack");
            if (item.id == "asphalt_01") return Get("Асфальт", "Asphalt");
            if (item.id == "parking_01") return Get("Парковка", "Parking bay");
            if (item.id == "unloading_gate_01") return Get("Ворота разгрузки", "Unloading gate");
            if (item.id == "storage_rack_01") return Get("Складской стеллаж", "Storage rack");
            if (!Russian) return CleanName(item.displayName);
            switch (item.id)
            {
                case "shelf_fresh_01": return "Хлебный стеллаж";
                case "shelf_wallgoods_01": return "Пристенный стеллаж";
                case "shelf_doublesided_01": return "Двойной стеллаж";
                case "shelf_refrigerated_01": return "Холодильная витрина";
                case "shelf_cold_01": return "Морозильная витрина";
                case "cash_register_01": return "Касса";
                case "door_01": return "Входная дверь";
                case "floor_01": return "Пол";
                case "wall_01": return "Стена";
                case "wallcorner_01": return "Угловая стена";
                default: return CleanName(item.displayName);
            }
        }
        public static string Item(ProductItemData item)
        {
            if (item == null) return Get("Товар", "Product");
            if (!Russian) return item.DisplayName;
            switch (item.Id)
            {
                case "product_milk": return "Молоко";
                case "product_bread": return "Батон";
                case "product_baguette": return "Багет";
                case "product_brown_bread": return "Тёмный хлеб";
                case "product_white_bread": return "Белый хлеб";
                case "product_bun": return "Булочка";
                case "product_sesame_bun": return "Булочка с кунжутом";
                case "product_oil": return "Подсолнечное масло";
                case "product_sodacola": return "Кола";
                case "product_steak": return "Стейк";
                case "product_apple": return "Яблоки";
                case "product_banana": return "Бананы";
                case "product_orange": return "Апельсины";
                case "product_pear": return "Груши";
                case "product_lemon": return "Лимоны";
                case "product_grape": return "Виноград";
                case "product_carrot": return "Морковь";
                case "product_cucumber": return "Огурцы";
                case "product_tomato": return "Помидоры";
                case "product_broccoli": return "Брокколи";
                case "product_potato": return "Картофель";
                case "product_pepper": return "Сладкий перец";
                case "product_cheese": return "Сыр";
                case "product_eggs": return "Яйца";
                case "product_sausage": return "Колбаса";
                case "product_chicken": return "Куриное филе";
                case "product_ketchup": return "Кетчуп";
                case "product_mustard": return "Горчица";
                case "product_juice": return "Сок";
                case "product_icecream": return "Мороженое";
                default: return item.DisplayName;
            }
        }
        public static string ProductType(ProductItemData item)
        {
            if (item == null) return "";
            switch (item.StorageType)
            {
                case ProductStorageType.Dairy: return Get("Молочные продукты", "Dairy");
                case ProductStorageType.Bakery: return Get("Выпечка", "Bakery");
                case ProductStorageType.Oil:
                case ProductStorageType.Groceries: return Get("Бакалея", "Groceries");
                case ProductStorageType.Drinks: return Get("Напитки", "Drinks");
                case ProductStorageType.Meat: return Get("Мясо", "Meat");
                case ProductStorageType.Produce: return Get("Овощи и фрукты", "Produce");
                case ProductStorageType.Frozen: return Get("Заморозка", "Frozen");
                default: return Get("Товары", "Products");
            }
        }
        private static string CleanName(string name) => (name ?? "").Replace('_', ' ');
        public static string ShelfHint(ProductItemData item)
        {
            if (item == null) return "";
            switch (item.StorageType)
            {
                case ProductStorageType.Bakery: return Get("Хлебный стеллаж", "Bakery rack");
                case ProductStorageType.Produce: return Get("Овощной стеллаж", "Produce rack");
                case ProductStorageType.Dairy: return Get("Холодильник", "Refrigerated display");
                case ProductStorageType.Meat:
                case ProductStorageType.Frozen: return Get("Морозильная витрина", "Cold pantry");
                case ProductStorageType.Oil:
                case ProductStorageType.Groceries:
                case ProductStorageType.Drinks: return Get("Пристенный / двойной", "Wall / double-sided shelf");
                default: return ProductType(item);
            }
        }
    }
}
