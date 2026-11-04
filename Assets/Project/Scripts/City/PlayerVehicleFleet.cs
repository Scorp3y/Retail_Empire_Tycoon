using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Logistics;

namespace RetailEmpireTycoon.City
{
    [Serializable]
    public sealed class VehicleFleetSaveData
    {
        public string selectedId = PlayerVehicleFleet.StarterId;
        public List<string> ownedIds = new List<string>();
        public List<VehicleTuningSaveEntry> tuning = new List<VehicleTuningSaveEntry>();
    }

    /// <summary>Owns purchases and selection. Cargo stays in the selected vehicle; exchange requires an empty hold.</summary>
    public sealed class PlayerVehicleFleet
    {
        public const string StarterId = "delivery_pickup";
        private readonly PlayerVehicleDefinition[] catalog;
        private readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, VehicleTuning> tuning = new Dictionary<string, VehicleTuning>(StringComparer.Ordinal);
        private readonly MoneyController money;
        private readonly DeliveryOrders orders;
        private readonly Action persist;
        private bool committing;
        public event Action Changed;
        public event Action TuningChanged;
        public IReadOnlyList<PlayerVehicleDefinition> Catalog => catalog;
        public string SelectedId { get; private set; } = StarterId;
        public PlayerVehicleDefinition Selected => catalog.FirstOrDefault(item => item.id == SelectedId);
        public string SelectedName => Selected != null ? Selected.displayName : "Delivery pickup";
        public string Notice { get; private set; }
        public VehicleTuning SelectedTuning => TuningFor(SelectedId);
        public VehicleTuning TuningFor(string id) => tuning.TryGetValue(id, out var value) ? value : default;
        public int Balance => money.Money;
        public const int PaintPrice = 450;
        public bool DriverHasVehicle {get;private set;}
        public void AssignDriver(bool assigned)=>DriverHasVehicle=assigned;

        public PlayerVehicleFleet(IEnumerable<PlayerVehicleDefinition> catalog, MoneyController money, DeliveryOrders orders, Action persist)
        {
            this.catalog = catalog.OrderBy(item => item.payloadKg).ToArray();
            this.money = money ?? throw new ArgumentNullException(nameof(money));
            this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
            this.persist = persist ?? throw new ArgumentNullException(nameof(persist));
            if (this.catalog.Any(item => string.IsNullOrWhiteSpace(item.id) || item.id == StarterId || item.model == null
                || item.price <= 0 || item.payloadKg <= 0 || item.cargoM3 <= 0) || this.catalog.Select(item => item.id).Distinct().Count() != this.catalog.Length)
                throw new ArgumentException("Invalid player vehicle catalog.");
            ApplyCapacity();
        }
        public bool Owns(string id) => id == StarterId || owned.Contains(id);
        public VehicleFleetSaveData Capture() => new VehicleFleetSaveData
        {
            selectedId = SelectedId,
            ownedIds = owned.OrderBy(id => id).ToList(),
            tuning = tuning.OrderBy(pair => pair.Key)
                .Select(pair => new VehicleTuningSaveEntry {id = pair.Key, tuning = pair.Value}).ToList()
        };
        public void Restore(VehicleFleetSaveData data)
        {
            var restored = data?.ownedIds ?? new List<string>();
            string selection = string.IsNullOrEmpty(data?.selectedId) ? StarterId : data.selectedId;
            if (restored.Any(id => id != StarterId && !catalog.Any(item => item.id == id))
                || selection != StarterId && (!restored.Contains(selection) || !catalog.Any(item => item.id == selection)))
                throw new InvalidOperationException("Save refers to an unavailable player vehicle. Fleet was not changed.");
            if (committing) throw new InvalidOperationException("Cannot restore the fleet during a purchase.");
            RestoreState(data, restored, selection);
            ApplyCapacity(); Changed?.Invoke();
        }
        private void RestoreState(VehicleFleetSaveData data, IEnumerable<string> vehicles, string selection)
        {
            owned.Clear(); foreach (string id in vehicles.Where(id => id != StarterId)) owned.Add(id);
            SelectedId = selection;
            tuning.Clear();
            if (data?.tuning == null) return;
            foreach (var entry in data.tuning)
                if (entry != null && !string.IsNullOrEmpty(entry.id) && Owns(entry.id))
                    tuning[entry.id] = entry.tuning.Normalized();
        }
        public int UpgradePrice(WorkshopCategory category)
        {
            int basePrice = category switch
            {
                WorkshopCategory.Engine => 900,
                WorkshopCategory.Handling => 700,
                WorkshopCategory.Cargo => 1100,
                _ => 0
            };
            return basePrice * (SelectedTuning.Level(category) + 1);
        }
        public bool BuyUpgrade(WorkshopCategory category)
        {
            int price = UpgradePrice(category);
            if (price == 0 || SelectedTuning.Level(category) >= VehicleTuning.MaxLevel)
                return Reject("This upgrade is already at its maximum level.");
            return InstallTuning(SelectedTuning.Upgrade(category), price, "Upgrade installed.");
        }
        public bool BuyPaint(UnityEngine.Color32? color)
        {
            var current = SelectedTuning;
            var next = current.WithPaint(color);
            if (current.painted == next.painted && current.rgb == next.rgb)
                return Reject("This paint is already applied.");
            return InstallTuning(next, color.HasValue ? PaintPrice : 0, "Paint applied.");
        }
        private bool InstallTuning(VehicleTuning next, int price, string success)
        {
            if (DriverHasVehicle) return Reject("Your driver is using this vehicle.");
            if (!money.CanSpend(price)) return Reject("Insufficient funds.");
            return Commit(() =>
            {
                if (!money.TrySpend(price)) throw new InvalidOperationException("Payment changed during purchase.");
                tuning[SelectedId] = next;
            }, success, tuningOnly: true);
        }
        public bool BuyAndSelect(string id)
        {
            var item = catalog.FirstOrDefault(entry => entry.id == id);
            if (item == null) return Reject("This vehicle is unavailable.");
            if (Owns(id)) return Select(id);
            if (!CanExchange()) return false;
            if (!money.CanSpend(item.price)) return Reject("Insufficient funds.");
            return Commit(() => { if (!money.TrySpend(item.price)) throw new InvalidOperationException("Payment changed during purchase."); owned.Add(id); SelectedId = id; }, "Vehicle purchased and selected.");
        }
        public bool Select(string id)
        {
            if (!Owns(id)) return Reject("Buy this vehicle first.");
            if (id == SelectedId) return true;
            if (!CanExchange()) return false;
            return Commit(() => SelectedId = id, "Vehicle selected.");
        }
        private bool CanExchange()
        {
            if(DriverHasVehicle)return Reject("Your driver is using this vehicle. Wait for the delivery to return.");
            if (!orders.Ledger.Entries.Any(entry => entry.location == DeliveryLocation.Pickup)) return true;
            return Reject("Unload your current vehicle before exchanging it. Cargo is preserved.");
        }
        private bool Commit(Action mutation, string success, bool tuningOnly = false)
        {
            if (committing) return false;
            var before = Capture(); int oldMoney = money.Money;
            committing = true;
            try { mutation(); ApplyCapacity(); persist(); }
            catch (Exception error)
            {
                RestoreState(before, before.ownedIds, before.selectedId);
                money.SetMoney(oldMoney); ApplyCapacity();
                UnityEngine.Debug.LogException(error);
                return Reject("Vehicle change could not be saved. Payment refunded.");
            }
            finally { committing = false; }
            Notice = success;
            if (tuningOnly) TuningChanged?.Invoke();
            else Changed?.Invoke();
            return true;
        }
        private void ApplyCapacity()
        {
            orders.pickupCapacityKg = (Selected != null ? Selected.payloadKg : 350) * SelectedTuning.CapacityMultiplier;
            orders.pickupCapacityM3 = (Selected != null ? Selected.cargoM3 : 2) * SelectedTuning.CapacityMultiplier;
        }
        private bool Reject(string message) { Notice = message; return false; }
    }
}
