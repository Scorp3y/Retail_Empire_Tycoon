using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>Displays delivered storage separately from paid goods awaiting collection.</summary>
    public sealed class ShopStorageStatus : MonoBehaviour
    {
        public DeliveryOrders orders;
        public TMP_Text label;
        private void OnEnable()
        {
            if(orders==null) return;
            orders.Changed+=Refresh;orders.products.Changed+=Refresh;orders.buildings.Changed+=Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if(orders==null) return;
            orders.Changed-=Refresh;orders.products.Changed-=Refresh;orders.buildings.Changed-=Refresh;
        }
        private void Refresh()
        {
            if(label==null||orders==null) return;
            label.text=ShopText.Get("Склад: ","Storage: ")+$"{orders.ShopWeightKg:0.#} / {orders.shopCapacityKg:0} kg"
                +" • "+ShopText.Get("Ожидает: ","Awaiting: ")+$"{orders.DepotWeightKg:0.#} kg";
        }
    }
}
