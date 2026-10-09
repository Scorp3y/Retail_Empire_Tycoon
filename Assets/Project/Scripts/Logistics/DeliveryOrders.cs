using System;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Products;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>Purchases reserve paid stock at individual suppliers. Only unloading grants stock to the shop.</summary>
    public sealed class DeliveryOrders : MonoBehaviour
    {
        public MoneyController money;
        public BuildInventory buildings;
        public ProductInventory products;
        public BuildItemCatalog buildCatalog;
        public ProductCatalog productCatalog;
        public SaveManager save;
        [Min(1)] public float pickupCapacityKg = 350;
        [Min(.1f)] public float pickupCapacityM3 = 2;
        [Min(1)] public float shopCapacityKg = 2000;
        [Min(1)] public float depotCapacityKg = 10000;
        public DeliveryLedger Ledger { get; } = new DeliveryLedger();
        public event Action Changed;
        public string Notice { get; private set; }
        public bool NoticeIsError {get;private set;}

        private void Awake() { Ledger.Changed += Notify; }
        private void OnDestroy() { Ledger.Changed -= Notify; }
        private void Notify() { Changed?.Invoke(); }
        public float DepotWeightKg => Weight(DeliveryLocation.Depot);
        public float WaitingWeightAt(SupplierKind supplier) => Ledger.Entries
            .Where(e=>e.location==DeliveryLocation.Depot&&SupplierFor(e)==supplier).Sum(e=>UnitWeight(e)*e.quantity);
        public float PickupWeightKg => Weight(DeliveryLocation.Pickup);
        public float PickupVolumeM3 => Ledger.Entries.Where(e => e.location == DeliveryLocation.Pickup).Sum(e => UnitVolume(e) * e.quantity);
        public float ShopWeightKg => products.BuildSaveData().Sum(e => (productCatalog.GetById(e.productId)?.UnitWeightKg ?? 0) * e.count)
            + buildings.BuildSaveData().Sum(e => (buildCatalog.GetById(e.itemId)?.weightKg ?? 0) * e.count);
        public string ItemName(DeliveryEntry entry) => entry.kind == DeliveryItemKind.Product
            ? UI.Shop.ShopText.Item(productCatalog.GetById(entry.itemId)) : UI.Shop.ShopText.Item(buildCatalog.GetById(entry.itemId));
        public int PackageQuantity(DeliveryEntry entry) => entry.kind == DeliveryItemKind.Product
            ? productCatalog.GetById(entry.itemId).BoxAmount : 1;

        public SupplierKind SupplierFor(DeliveryEntry entry)
        {
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            if(entry.kind==DeliveryItemKind.Product)
            {
                var item=productCatalog.GetById(entry.itemId);
                if(item==null)throw new InvalidOperationException("Unknown paid product: "+entry.itemId);
                return SupplierRouting.ForProduct(item.StorageType);
            }
            var building=buildCatalog.GetById(entry.itemId);
            if(building==null)throw new InvalidOperationException("Unknown paid building: "+entry.itemId);
            return SupplierRouting.ForBuilding(building);
        }

        public bool LoadFrom(DeliveryEntry entry,SupplierKind supplier)
        {
            if(entry==null)return false;
            if(SupplierFor(entry)!=supplier)return Reject("Этот заказ находится у поставщика «"+SupplierRouting.Name(SupplierFor(entry))+"».");
            return Load(entry);
        }

        public bool Order(ProductItemData item) => item != null && Purchase(item.Id, DeliveryItemKind.Product, item.BoxAmount, item.BuyPrice, item.UnitWeightKg);
        public bool Order(BuildItemData item)
        {
            if (item == null) return false;
            if (item.hiddenFromShop) return Reject("Этот предмет больше не продаётся.");
            return Purchase(item.id, DeliveryItemKind.Building, 1, item.price, item.weightKg);
        }
        private bool Purchase(string id, DeliveryItemKind kind, int quantity, int price, float unitWeight)
        {
            var supplier=SupplierFor(new DeliveryEntry{itemId=id,kind=kind});
            if (WaitingWeightAt(supplier) + unitWeight * quantity > depotCapacityKg) return Reject("Склад этого поставщика заполнен — заберите предыдущие заказы.");
            if (!money.CanSpend(price)) return Reject("Недостаточно денег.");
            var before = Ledger.Capture(); int oldMoney = money.Money;
            try
            {
                if (!money.TrySpend(price)) return false;
                Ledger.Order(id, kind, quantity); Persist();
                var ordered=new DeliveryEntry {itemId=id,kind=kind};
                Notice = "Заказ оплачен. Выдача: «"+SupplierRouting.Name(SupplierFor(ordered))+"»."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error)
            {
                Ledger.Restore(before); money.SetMoney(oldMoney);
                Debug.LogException(error, this); return Reject("Не удалось сохранить заказ. Деньги возвращены.");
            }
        }

        public bool Load(DeliveryEntry entry)
        {
            if (entry == null || entry.location != DeliveryLocation.Depot) return false;
            int quantity = Mathf.Min(PackageQuantity(entry), Ledger.Count(entry.itemId, entry.kind, DeliveryLocation.Depot));
            if (quantity <= 0) return false;
            if (!new CargoCapacity(pickupCapacityKg,pickupCapacityM3).Fits(PickupWeightKg,PickupVolumeM3,UnitWeight(entry)*quantity,UnitVolume(entry)*quantity))
                return Reject("В пикапе недостаточно места или превышен допустимый вес.");
            var before = Ledger.Capture();
            try
            {
                if (!Ledger.Transfer(entry.itemId, entry.kind, DeliveryLocation.Depot, DeliveryLocation.Pickup, quantity)) return false;
                Persist(); Notice = "Груз загружен в пикап."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error) { Ledger.Restore(before); Debug.LogException(error, this); return Reject("Загрузка отменена: не удалось сохранить груз."); }
        }

        public bool Unload()
        {
            var cargo = Ledger.Entries.Where(e => e.location == DeliveryLocation.Pickup).ToArray();
            if (cargo.Length == 0) return Reject("В пикапе нет груза.");
            if (ShopWeightKg + PickupWeightKg > shopCapacityKg + .001f) return Reject("Склад магазина заполнен. Освободите место перед разгрузкой.");
            var before = Ledger.Capture(); var productStock = products.BuildSaveData(); var buildStock = buildings.BuildSaveData();
            try
            {
                // Resolve every ID before changing quantities; missing imported assets must not consume a paid shipment.
                foreach (var entry in cargo) { UnitWeight(entry); UnitVolume(entry); }
                foreach (var entry in cargo)
                {
                    if (entry.kind == DeliveryItemKind.Product) products.Add(productCatalog.GetById(entry.itemId), entry.quantity);
                    else buildings.Add(buildCatalog.GetById(entry.itemId), entry.quantity);
                    if (!Ledger.Deliver(entry.itemId, entry.kind, entry.quantity)) throw new InvalidOperationException("Cargo changed during unload.");
                }
                Persist(); Notice = "Груз доставлен на склад магазина."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error)
            {
                products.ApplySaveData(productStock, productCatalog); buildings.ApplySaveData(buildStock, buildCatalog);
                Ledger.Restore(before); Debug.LogException(error, this); return Reject("Разгрузка отменена: не удалось сохранить доставку.");
            }
        }
        private float Weight(DeliveryLocation location) => Ledger.Entries.Where(e => e.location == location).Sum(e => UnitWeight(e) * e.quantity);
        public float UnitWeight(DeliveryEntry entry)
        {
            if (entry.kind == DeliveryItemKind.Product)
            {
                var item = productCatalog.GetById(entry.itemId);
                if (item == null) throw new InvalidOperationException("Unknown paid product: " + entry.itemId);
                return item.UnitWeightKg;
            }
            var building = buildCatalog.GetById(entry.itemId);
            if (building == null) throw new InvalidOperationException("Unknown paid building: " + entry.itemId);
            return building.weightKg;
        }
        private float UnitVolume(DeliveryEntry entry)
        {
            if (entry.kind == DeliveryItemKind.Product)
            {
                var item = productCatalog.GetById(entry.itemId);
                if (item == null) throw new InvalidOperationException("Unknown paid product: " + entry.itemId);
                return item.UnitVolumeM3;
            }
            var building = buildCatalog.GetById(entry.itemId);
            if (building == null) throw new InvalidOperationException("Unknown paid building: " + entry.itemId);
            return building.cargoVolumeM3;
        }
        private void Persist() { if (save != null) save.SaveGame(); }
        private bool Reject(string message) { Notice = message; NoticeIsError=true; Notify(); return false; }
    }
}
