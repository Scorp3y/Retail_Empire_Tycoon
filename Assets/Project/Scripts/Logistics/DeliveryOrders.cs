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
        [Min(1)] public float shopCapacityKg = 20000;
        [Min(1)] public float depotCapacityKg = 10000;
        public DeliveryLedger Ledger { get; } = new DeliveryLedger();
        public event Action Changed;
        public string Notice { get; private set; }
        public bool NoticeIsError {get;private set;}
        public bool NoticeIsInline {get;private set;}

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
            if(SupplierFor(entry)!=supplier)return Reject("Collect this order from "+SupplierRouting.Name(SupplierFor(entry))+".");
            return Load(entry);
        }

        public bool Order(ProductItemData item) => item != null && Purchase(item.Id, DeliveryItemKind.Product, item.BoxAmount, item.BuyPrice, item.UnitWeightKg);
        /// <summary>Pay and persist an entire product basket once, or restore both money and orders.</summary>
        public bool OrderProducts(System.Collections.Generic.IReadOnlyDictionary<ProductItemData,int> basket)
        {
            NoticeIsInline=true;
            if (basket == null || basket.Count == 0) return Reject("Your basket is empty.");
            long total = 0;
            var weights = new System.Collections.Generic.Dictionary<SupplierKind,float>();
            foreach (var line in basket)
            {
                if (line.Key == null || line.Value < 1 || line.Value > 999 || productCatalog.GetById(line.Key.Id) != line.Key)
                    return Reject("Invalid basket item.");
                total += (long)line.Key.BuyPrice * line.Value;
                var supplier = SupplierRouting.ForProduct(line.Key.StorageType);
                weights.TryGetValue(supplier, out float weight);
                weights[supplier] = weight + line.Key.UnitWeightKg * line.Key.BoxAmount * line.Value;
            }
            if (total > int.MaxValue || !money.CanSpend((int)total)) return Reject("Insufficient funds.");
            foreach (var entry in weights)
                if (WaitingWeightAt(entry.Key) + entry.Value > depotCapacityKg) return Reject(SupplierRouting.Name(entry.Key) + " depot is full.");
            var before = Ledger.Capture(); int oldMoney = money.Money;
            try
            {
                if (!money.TrySpend((int)total)) return false;
                foreach (var line in basket) Ledger.Order(line.Key.Id, DeliveryItemKind.Product, checked(line.Key.BoxAmount * line.Value));
                Persist(); Notice="Order paid. Collect goods from the listed suppliers."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error)
            {
                Ledger.Restore(before); money.SetMoney(oldMoney); Debug.LogException(error,this);
                return Reject("Order cancelled: payment refunded.");
            }
        }
        public bool Order(BuildItemData item)
        {
            if (item == null) return false;
            if (item.hiddenFromShop) return Reject("This item is no longer for sale.");
            if (item.decorGroup != DecorGroup.None) return BuyDecoration(item);
            return Purchase(item.id, DeliveryItemKind.Building, 1, item.price, item.weightKg);
        }

        public int DecorationStoreLevel => FindObjectOfType<StoreOperations.StoreOperations>()?.Level ?? 1;

        private bool BuyDecoration(BuildItemData item)
        {
            NoticeIsInline=false;
            if (DecorationStoreLevel < item.decorUnlockLevel) return Reject("Expand your store to unlock this decoration.");
            if (ShopWeightKg + item.weightKg > shopCapacityKg) return Reject("The store warehouse is full.");
            int balance=money.Money;
            if (!money.TrySpend(item.price)) return Reject("Insufficient funds.");
            try
            {
                buildings.Add(item,1);
                Persist();
            }
            catch(Exception error)
            {
                buildings.TryConsume(item,1);
                money.SetMoney(balance);
                Debug.LogException(error,this);
                return Reject("Purchase cancelled: payment refunded.");
            }
            Notice="Decoration purchased and added to storage.";
            NoticeIsError=false;
            Notify();
            return true;
        }
        private bool Purchase(string id, DeliveryItemKind kind, int quantity, int price, float unitWeight)
        {
            NoticeIsInline=false;
            var supplier=SupplierFor(new DeliveryEntry{itemId=id,kind=kind});
            if (WaitingWeightAt(supplier) + unitWeight * quantity > depotCapacityKg) return Reject("This supplier depot is full. Collect your previous orders.");
            if (!money.CanSpend(price)) return Reject("Insufficient funds.");
            var before = Ledger.Capture(); int oldMoney = money.Money;
            try
            {
                if (!money.TrySpend(price)) return false;
                Ledger.Order(id, kind, quantity); Persist();
                var ordered=new DeliveryEntry {itemId=id,kind=kind};
                Notice = "Order paid. Pickup at: "+SupplierRouting.Name(SupplierFor(ordered))+"."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error)
            {
                Ledger.Restore(before); money.SetMoney(oldMoney);
                Debug.LogException(error, this); return Reject("The order could not be saved. Payment refunded.");
            }
        }

        public bool Load(DeliveryEntry entry)
        {
            NoticeIsInline=false;
            if (entry == null || entry.location != DeliveryLocation.Depot) return false;
            int quantity = Mathf.Min(PackageQuantity(entry), Ledger.Count(entry.itemId, entry.kind, DeliveryLocation.Depot));
            if (quantity <= 0) return false;
            if (!new CargoCapacity(pickupCapacityKg,pickupCapacityM3).Fits(PickupWeightKg,PickupVolumeM3,UnitWeight(entry)*quantity,UnitVolume(entry)*quantity))
                return Reject("The pickup has insufficient space or its weight limit would be exceeded.");
            var before = Ledger.Capture();
            try
            {
                if (!Ledger.Transfer(entry.itemId, entry.kind, DeliveryLocation.Depot, DeliveryLocation.Pickup, quantity)) return false;
                Persist(); Notice = "Cargo loaded into the pickup."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error) { Ledger.Restore(before); Debug.LogException(error, this); return Reject("Loading cancelled: cargo could not be saved."); }
        }

        public bool Unload()
        {
            NoticeIsInline=false;
            var cargo = Ledger.Entries.Where(e => e.location == DeliveryLocation.Pickup).ToArray();
            if (cargo.Length == 0) return Reject("The pickup is empty.");
            var before = Ledger.Capture(); var productStock = products.BuildSaveData(); var buildStock = buildings.BuildSaveData();
            try
            {
                // Resolve every ID before changing quantities; missing imported assets must not consume a paid shipment.
                foreach (var entry in cargo) { UnitWeight(entry); UnitVolume(entry); }
                float freeKg = Mathf.Max(0, shopCapacityKg - ShopWeightKg); int delivered = 0;
                foreach (var entry in cargo)
                {
                    float weight = UnitWeight(entry);
                    int quantity = weight <= 0 ? entry.quantity : Mathf.Min(entry.quantity, Mathf.FloorToInt((freeKg + .001f) / weight));
                    if (quantity <= 0) continue;
                    if (entry.kind == DeliveryItemKind.Product) products.Add(productCatalog.GetById(entry.itemId), quantity);
                    else buildings.Add(buildCatalog.GetById(entry.itemId), quantity);
                    if (!Ledger.Deliver(entry.itemId, entry.kind, quantity)) throw new InvalidOperationException("Cargo changed during unload.");
                    freeKg = Mathf.Max(0, freeKg - weight * quantity); delivered += quantity;
                }
                if (delivered == 0) return Reject("The store warehouse is full. Free some space before unloading.");
                Persist(); Notice = Ledger.Entries.Any(entry => entry.location == DeliveryLocation.Pickup)
                    ? "Warehouse filled. Remaining cargo stays in your vehicle; free storage space to unload it."
                    : "Cargo delivered to the store warehouse."; NoticeIsError=false; Notify(); return true;
            }
            catch (Exception error)
            {
                products.ApplySaveData(productStock, productCatalog); buildings.ApplySaveData(buildStock, buildCatalog);
                Ledger.Restore(before); Debug.LogException(error, this); return Reject("Unloading cancelled: the delivery could not be saved.");
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
        public float UnitVolume(DeliveryEntry entry)
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
