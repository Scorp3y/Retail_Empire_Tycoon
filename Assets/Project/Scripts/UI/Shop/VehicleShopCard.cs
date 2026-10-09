using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>The starting pickup is granted to every player, not delivered as inventory.</summary>
    public sealed class VehicleShopCard : MonoBehaviour
    {
        public Image icon;
        public TMP_Text title;
        public TMP_Text details;
        public Button stateButton;
        public void Bind(Sprite image, RetailEmpireTycoon.Logistics.DeliveryOrders orders)
        {
            icon.sprite = image;
            icon.enabled = image != null;
            title.text = ShopText.Get("Рабочий пикап", "Delivery pickup");
            float weight = orders != null ? orders.pickupCapacityKg : 350;
            float volume = orders != null ? orders.pickupCapacityM3 : 2;
            details.text = ShopText.Get($"Стартовый автомобиль\nГруз: {weight:0} кг\nКузов: {volume:0.#} м³", $"Starting vehicle\nPayload: {weight:0} kg\nCargo bed: {volume:0.#} m³");
            stateButton.onClick.RemoveAllListeners();
            stateButton.interactable = false;
            stateButton.GetComponentInChildren<ShopLocalizedLabel>()?.Set("Куплена · Выбрана", "Owned · Selected");
        }
    }
}
