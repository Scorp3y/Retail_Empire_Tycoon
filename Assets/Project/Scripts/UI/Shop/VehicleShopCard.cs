using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using RetailEmpireTycoon.City;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>One owned or purchasable vehicle, using the same fleet rules as the city showroom.</summary>
    public sealed class VehicleShopCard : MonoBehaviour
    {
        public Image icon;
        public TMP_Text title;
        public TMP_Text details;
        public Button stateButton;
        public void Bind(Sprite image, RetailEmpireTycoon.Logistics.DeliveryOrders orders,PlayerVehicleDefinition vehicle=null,Action refresh=null)
        {
            icon.sprite = image;
            icon.enabled = image != null;
            var trip=orders!=null?orders.GetComponent<RetailEmpireTycoon.City.CityTrip>():null;
            if(vehicle!=null&&vehicle.icon!=null){icon.sprite=vehicle.icon;icon.enabled=true;}
            var fleet=trip!=null?trip.Fleet:null;string id=vehicle!=null?vehicle.id:PlayerVehicleFleet.StarterId;
            title.text = vehicle!=null?vehicle.displayName:"Delivery pickup";
            float weight = vehicle!=null?vehicle.payloadKg:350, volume=vehicle!=null?vehicle.cargoM3:2;
            bool selected=fleet==null?vehicle==null:fleet.SelectedId==id,owned=fleet==null?vehicle==null:fleet.Owns(id);
            details.text = $"{weight:0} kg\n{volume:0.#} m³\n"+(owned?"Owned":$"${vehicle.price:N0}");
            stateButton.onClick.RemoveAllListeners();
            stateButton.interactable = fleet!=null&&!selected&&(owned||orders.money.CanSpend(vehicle.price));
            string label=selected?"Owned · Selected":owned?"Select":"Buy vehicle";
            var localized=stateButton.GetComponentInChildren<ShopLocalizedLabel>();
            if(localized!=null)localized.Set(label,label);else stateButton.GetComponentInChildren<TMP_Text>().text=label;
            stateButton.onClick.AddListener(()=>
            {
                bool changed=owned?fleet.Select(id):fleet.BuyAndSelect(id);
                if(changed)refresh?.Invoke();else details.text=fleet.Notice;
            });
        }
    }
}
