using System;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>Capacity is limited by both mass and volume; equality at the limit is allowed.</summary>
    public sealed class CargoCapacity
    {
        public float MaximumKg { get; }
        public float MaximumM3 { get; }
        public CargoCapacity(float maximumKg,float maximumM3)
        {
            if(float.IsNaN(maximumKg)||float.IsInfinity(maximumKg)||maximumKg<=0
                ||float.IsNaN(maximumM3)||float.IsInfinity(maximumM3)||maximumM3<=0) throw new ArgumentOutOfRangeException();
            MaximumKg=maximumKg;MaximumM3=maximumM3;
        }
        public bool Fits(float currentKg,float currentM3,float addedKg,float addedM3)
        {
            if(!Valid(currentKg)||!Valid(currentM3)||!Valid(addedKg)||!Valid(addedM3)) return false;
            return currentKg+addedKg<=MaximumKg+.001f && currentM3+addedM3<=MaximumM3+.0001f;
        }
        private static bool Valid(float value) => !float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0;
    }
}
