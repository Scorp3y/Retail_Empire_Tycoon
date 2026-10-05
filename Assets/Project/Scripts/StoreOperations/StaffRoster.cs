using System;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Owns hiring capacity; a purchased plot adds one store level.</summary>
    public sealed class StaffRoster
    {
        private readonly int[] _counts = new int[4];
        public int Count(StaffRole role) => _counts[(int)role];
        public static int Limit(StaffRole role, int level)
        {
            level = Math.Max(1, Math.Min(6, level));
            return role == StaffRole.Cashier || role == StaffRole.Stocker
                ? 1 + (level - 1) / 2 : 1 + (level - 1) / 3;
        }
        public bool CanHire(StaffRole role, int level) => Count(role) < Limit(role, level);
        public bool TryHire(StaffRole role, int level)
        {
            if (!CanHire(role, level)) return false;
            _counts[(int)role]++;
            return true;
        }
        public bool TryDismiss(StaffRole role)
        {
            if (Count(role) <= 0) return false;
            _counts[(int)role]--;
            return true;
        }
        public int[] Snapshot() => (int[])_counts.Clone();
        public void Restore(int[] counts, int level)
        {
            for (int i = 0; i < 4; i++)
                _counts[i] = counts == null || i >= counts.Length ? 0 : Math.Max(0, Math.Min(counts[i], Limit((StaffRole)i, level)));
        }
    }

    [Serializable]
    public sealed class ShopOperationsSaveData
    {
        public bool isOpen;
        public float reputation = 3f;
        public int[] staff = new int[4];
        public float unpaidWages;
        public int completedSales;
    }
}
