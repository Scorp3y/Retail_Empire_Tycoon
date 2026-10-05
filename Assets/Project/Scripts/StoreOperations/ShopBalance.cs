using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    public enum StaffRole { Cashier, Guard, Stocker, Cleaner }

    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Shop balance")]
    public sealed class ShopBalance : ScriptableObject
    {
        [Min(3)] public float baseArrivalSeconds = 16;
        [Min(10)] public float customerPatienceSeconds = 75;
        [Range(0, 0.3f)] public float thiefChance = 0.08f;
        [Range(0, 1)] public float litterChance = 0.25f;
        [Min(1)] public int basketSize = 3;
        [Min(3)] public int thiefClicks = 8;
        [Min(1)] public float staffTaskSeconds = 6;
        public Vector4 hiringCosts = new Vector4(150, 180, 160, 100);
        public Vector4 wagesPerMinute = new Vector4(12, 10, 12, 8);

        public int HireCost(StaffRole role) => Mathf.Max(0, Mathf.RoundToInt(hiringCosts[(int)role]));
        public int Wage(StaffRole role) => Mathf.Max(0, Mathf.RoundToInt(wagesPerMinute[(int)role]));
        public float ArrivalSeconds(int level, float rating)
            => Mathf.Max(4f, baseArrivalSeconds / ((1f + 0.15f * (level - 1)) * Mathf.Lerp(0.55f, 1.35f, Mathf.InverseLerp(1, 5, rating))));
    }
}
