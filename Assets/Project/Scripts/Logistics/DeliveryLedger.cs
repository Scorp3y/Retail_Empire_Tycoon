using System;
using System.Collections.Generic;
using System.Linq;

namespace RetailEmpireTycoon.Logistics
{
    public enum DeliveryItemKind { Product, Building }
    public enum DeliveryLocation { Depot, Pickup }

    [Serializable]
    public sealed class DeliveryEntry
    {
        public string itemId;
        public DeliveryItemKind kind;
        public DeliveryLocation location;
        public int quantity;
        public DeliveryEntry Copy() => (DeliveryEntry)MemberwiseClone();
    }

    [Serializable]
    public sealed class DeliverySaveData
    {
        public int version = 1;
        public List<DeliveryEntry> entries = new List<DeliveryEntry>();
    }

    /// <summary>Owns quantities awaiting pickup and in transit. It never contains delivered shop inventory.</summary>
    public sealed class DeliveryLedger
    {
        private readonly List<DeliveryEntry> entries = new List<DeliveryEntry>();
        public IReadOnlyList<DeliveryEntry> Entries => entries.Select(e => e.Copy()).ToArray();
        public event Action Changed;
        public int Count(string id, DeliveryItemKind kind, DeliveryLocation location) =>
            entries.Where(e => e.itemId == id && e.kind == kind && e.location == location).Sum(e => e.quantity);

        public void Order(string id, DeliveryItemKind kind, int quantity)
        {
            Validate(id, quantity);
            Add(id, kind, DeliveryLocation.Depot, quantity); Changed?.Invoke();
        }

        public bool Transfer(string id, DeliveryItemKind kind, DeliveryLocation from, DeliveryLocation to, int quantity)
        {
            Validate(id, quantity);
            if (from == to || Count(id, kind, from) < quantity) return false;
            checked { int destination = Count(id, kind, to) + quantity; }
            Remove(id, kind, from, quantity); Add(id, kind, to, quantity); Changed?.Invoke(); return true;
        }

        public bool Deliver(string id, DeliveryItemKind kind, int quantity)
        {
            Validate(id, quantity);
            if (Count(id, kind, DeliveryLocation.Pickup) < quantity) return false;
            Remove(id, kind, DeliveryLocation.Pickup, quantity); Changed?.Invoke(); return true;
        }

        public DeliverySaveData Capture() => new DeliverySaveData { entries = entries.Select(e => e.Copy()).ToList() };
        public void Restore(DeliverySaveData data)
        {
            // Validate before touching live state; malformed save data must not silently delete paid orders.
            if (data != null && data.version != 1) throw new InvalidOperationException("Unsupported delivery save version.");
            var restored = data?.entries ?? new List<DeliveryEntry>();
            foreach (var entry in restored)
            {
                if (entry == null) throw new InvalidOperationException("Null delivery entry.");
                Validate(entry.itemId, entry.quantity);
                if (!Enum.IsDefined(typeof(DeliveryLocation), entry.location) || !Enum.IsDefined(typeof(DeliveryItemKind), entry.kind))
                    throw new InvalidOperationException("Unknown delivery location or item kind.");
            }
            var merged = restored.GroupBy(e => new { e.itemId, e.kind, e.location }).Select(group => new DeliveryEntry
            { itemId = group.Key.itemId, kind = group.Key.kind, location = group.Key.location, quantity = group.Sum(e => e.quantity) }).ToArray();
            entries.Clear(); entries.AddRange(merged);
            Changed?.Invoke();
        }

        private void Add(string id, DeliveryItemKind kind, DeliveryLocation location, int quantity)
        {
            var entry = entries.FirstOrDefault(e => e.itemId == id && e.kind == kind && e.location == location);
            if (entry == null) entries.Add(new DeliveryEntry { itemId = id, kind = kind, location = location, quantity = quantity });
            else entry.quantity = checked(entry.quantity + quantity);
        }
        private void Remove(string id, DeliveryItemKind kind, DeliveryLocation location, int quantity)
        {
            var entry = entries.First(e => e.itemId == id && e.kind == kind && e.location == location);
            entry.quantity -= quantity; if (entry.quantity == 0) entries.Remove(entry);
        }
        private static void Validate(string id, int quantity)
        {
            if (string.IsNullOrWhiteSpace(id) || quantity <= 0) throw new ArgumentException("Delivery requires an item ID and positive quantity.");
        }
    }
}
