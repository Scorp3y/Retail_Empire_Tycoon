using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    public sealed class ShopDirtSpot : MonoBehaviour
    {
        public bool IsClaimed { get; private set; }
        public bool TryClaim() { if (IsClaimed) return false; IsClaimed = true; return true; }
        public void Release() { IsClaimed = false; }
    }
}
