using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.UI.Shop
{
    [MovedFrom(false, "MyShopGame.UI.Shop", null, "ShopItemCard")]
    public sealed class ShopItemCard : MonoBehaviour
    {
        [Header("UI")]
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text priceText;
        public TMP_Text sizeText;
        public Button buyButton;

        [Header("Refs")]
        public MoneyController money;
        public BuildInventory inventory;
        private BuildItemData _item;
        private ShopIconGraphic _parkingIcon;
        private RetailEmpireTycoon.Logistics.DeliveryOrders _orders;

        public void Bind(BuildItemData item, MoneyController moneyController, BuildInventory buildInventory, RetailEmpireTycoon.Logistics.DeliveryOrders orders = null)
        {
            _orders = orders;
            if (money != null) money.Changed -= OnMoneyChanged;
            money = moneyController;
            inventory = buildInventory;
            if (money != null) money.Changed += OnMoneyChanged;
            Bind(item);
        }
        public void Bind(BuildItemData item)
        {
            _item = item;

            ApplyTexts();
            ApplyIcon();
            HookButton();
        }

        private void ApplyTexts()
        {
            if (_item == null) return;

            if (nameText != null)
                nameText.text = ShopText.Item(_item);

            string description = ShopText.Russian && !string.IsNullOrWhiteSpace(_item.russianDescription)
                ? _item.russianDescription : _item.description;
            if (descText != null)
                descText.text = description;

            if (priceText != null)
                priceText.text = MoneyFormat.Compact(_item.price);

            if (sizeText != null)
            {
                var f = _item.footprint;
                sizeText.text = ShopText.Get("Размер (клетки)", "Size (cells)") + $"\n{f.x} × {f.y}";
                if (_orders != null) {sizeText.fontSize=13;sizeText.text += $"\n{_item.weightKg:0.#} kg";}
                if (_item.beautyPoints > 0) sizeText.text += ShopText.Get($"\nКрасота +{_item.beautyPoints}", $"\nBeauty +{_item.beautyPoints}");
                if (_item.decorGroup != DecorGroup.None)
                {
                    int stock=inventory!=null?inventory.GetCount(_item):0;
                    sizeText.text=$"{f.x} × {f.y} · "+ShopText.Get($"Красота +{_item.beautyPoints}",$"Beauty +{_item.beautyPoints}");
                    sizeText.text+="\n"+ShopText.Get($"На складе: {stock}",$"In storage: {stock}");
                    if (_item.decorSeason!=DecorSeason.None) sizeText.text+=" · "+SeasonLabel(_item.decorSeason);
                    if (Locked) sizeText.text+="\n"+ShopText.Get($"Уровень магазина {_item.decorUnlockLevel}",$"Store level {_item.decorUnlockLevel}");
                }
                if (descText == null && _item.isWall && !string.IsNullOrWhiteSpace(_item.russianDescription))
                    sizeText.text = $"{f.x} × {f.y} " + ShopText.Get("кл.", "cells") +
                        $" · {_item.weightKg:0.#} kg\n<size=12>{description}</size>";
            }
            if (buyButton!=null && _item.decorGroup!=DecorGroup.None)
                buyButton.GetComponentInChildren<ShopLocalizedLabel>()?.Set(Locked?"Закрыто":"Купить",Locked?"Locked":"Buy");
            else if (_orders != null && buyButton != null) buyButton.GetComponentInChildren<ShopLocalizedLabel>()?.Set("Заказать","Order");
        }

        private bool Locked => _item != null && _item.decorGroup != DecorGroup.None &&
            (_orders?.DecorationStoreLevel ?? 1) < _item.decorUnlockLevel;

        private static string SeasonLabel(DecorSeason season) => season switch
        {
            DecorSeason.Spring => ShopText.Get("Весна","Spring"),
            DecorSeason.Summer => ShopText.Get("Лето","Summer"),
            DecorSeason.Autumn => ShopText.Get("Осень","Autumn"),
            _ => ShopText.Get("Зима","Winter")
        };

        private void ApplyIcon()
        {
            if (icon == null) return;

            if (_item != null && _item.isParkingSpace)
            {
                if (_parkingIcon == null)
                {
                    var graphic = new GameObject("Parking symbol", typeof(RectTransform));
                    graphic.transform.SetParent(icon.transform, false);
                    var rect = (RectTransform)graphic.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
                    rect.sizeDelta = new Vector2(48,48);
                    _parkingIcon = graphic.AddComponent<ShopIconGraphic>();
                    _parkingIcon.Icon = ShopIcon.Parking;
                    _parkingIcon.raycastTarget = false;
                }
                _parkingIcon.color = _item.isPersonalParking ? new Color32(148,99,15,255) : ShopUiTheme.Ink;
                _parkingIcon.gameObject.SetActive(true);
                icon.enabled = false;
                return;
            }
            if (_parkingIcon != null) _parkingIcon.gameObject.SetActive(false);
            icon.sprite = _item != null ? _item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        private void HookButton()
        {
            if (buyButton == null) return;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);

            RefreshButton();
        }

        private void OnMoneyChanged(int amount) { RefreshButton(); }
        private void RefreshButton()
        {
            if (buyButton != null) buyButton.interactable = _item != null && !_item.hiddenFromShop && !Locked && money != null && inventory != null && money.CanSpend(_item.price);
        }
        private void OnDisable() { if (money != null) money.Changed -= OnMoneyChanged; }
        private void OnEnable() { if (money != null) { money.Changed -= OnMoneyChanged; money.Changed += OnMoneyChanged; } RefreshButton(); }

        private void OnBuyClicked()
        {
            if (_item == null || _item.hiddenFromShop || Locked || money == null || inventory == null) return;
            if (_orders != null) { if(_orders.Order(_item) && _item.decorGroup!=DecorGroup.None) ApplyTexts(); return; }
            if (!money.TrySpend(_item.price)) return;

            inventory.Add(_item, 1);
        }
    }
}
