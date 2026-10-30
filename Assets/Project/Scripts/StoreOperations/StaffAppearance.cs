using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Work tools appear during the matching duty; walking uses the common shopper skeleton.</summary>
    public sealed class StaffAppearance:MonoBehaviour
    {
        public StaffRole role;
        public GameObject workTools;
        private void Awake()=>SetWorking(false);
        public void SetWorking(bool working){if(workTools!=null)workTools.SetActive(working);}
    }
}
