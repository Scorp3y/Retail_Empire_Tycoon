using System;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RetailEmpireTycoon.City
{
    /// <summary>Owns the garage menu and its uncommitted paint preview.</summary>
    internal sealed class WorkshopWindow : IDisposable
    {
        private static readonly Color Ink = new Color32(23, 31, 31, 247);
        private static readonly Color Surface = new Color32(48, 62, 59, 255);
        private static readonly Color Active = new Color32(234, 188, 80, 255);
        private static readonly Color Paper = new Color32(246, 238, 218, 255);
        private static readonly Color Muted = new Color32(187, 201, 190, 255);
        private static readonly string[] Categories = {"Engine", "Handling", "Cargo capacity", "Respray"};
        private readonly ShopUi ui;
        private readonly PlayerVehicleFleet fleet;
        private readonly PickupDrive car;
        private readonly PickupCamera camera;
        private readonly RectTransform root;
        private readonly Button[] tabs = new Button[4];
        private readonly TMP_Text balance, notice;
        private RectTransform details;
        private WorkshopCategory category;
        private Color32 draftPaint;
        private bool paintEdited;

        public WorkshopWindow(ShopUi ui, RectTransform parent, PlayerVehicleFleet fleet, PickupDrive car,
            PickupCamera camera, Action close)
        {
            this.ui = ui;
            this.fleet = fleet;
            this.car = car;
            this.camera = camera;
            var size = new Vector2(434, 735);
            root = ui.Rect("Auto Works tuning", parent, Vector2.up, Vector2.up, new Vector2(30, -28), size);
            root.gameObject.AddComponent<ShopPanelFit>().MaximumSize = size;
            ui.Panel(root, Ink).sprite = null;
            Block(root, "Header stripe", 0, 0, 434, 5, Active);
            Label(root, "AUTO WORKS", 22, -14, 390, 54, 36, Active, true);
            Label(root, fleet.SelectedName.ToUpperInvariant(), 24, -73, 385, 26, 18, Paper, true);
            balance = Label(root, "", 24, -105, 385, 27, 18, Muted);
            for (int i = 0; i < tabs.Length; i++)
            {
                var selected = (WorkshopCategory)i;
                tabs[i] = Button(root, Categories[i], 22, -149 - 43 * i, 390, 38, () => Select(selected));
                tabs[i].GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            }
            Block(root, "Divider", 22, -326, 390, 1, Surface);
            notice = Label(root, "", 23, -606, 388, 53, 17, Muted);
            Button(root, "Change view", 22, -676, 184, 40, camera.FlipWorkshopView);
            Button(root, "Exit  ·  Esc", 228, -676, 184, 40, close);
            Select(WorkshopCategory.Engine);
        }

        private void Select(WorkshopCategory selection)
        {
            car.PreviewPaint(fleet.SelectedTuning.Paint);
            category = selection;
            draftPaint = fleet.SelectedTuning.Paint ?? car.FactoryColor;
            paintEdited = false;
            camera.ShowWorkshopAngle(selection);
            notice.text = selection == WorkshopCategory.Paint ? "Adjust the sliders to preview the paint." : "Choose an upgrade to improve this vehicle.";
            for (int i = 0; i < tabs.Length; i++) Style(tabs[i], i == (int)selection);
            RenderDetails();
        }

        private void RenderDetails()
        {
            balance.text = $"BALANCE   ${fleet.Balance:N0}";
            if (details != null)
            {
                details.gameObject.SetActive(false);
                Object.Destroy(details.gameObject);
            }
            details = ui.Rect("Workshop category details", root, Vector2.up, Vector2.up,
                new Vector2(22, -345), new Vector2(390, 250));
            if (category == WorkshopCategory.Paint) { RenderPaint(); return; }

            var current = fleet.SelectedTuning;
            int level = current.Level(category);
            bool max = level == VehicleTuning.MaxLevel;
            Label(details, $"LEVEL {level} / {VehicleTuning.MaxLevel}", 0, 0, 390, 28, 20, Active, true);
            Block(details, "Stat track", 0, -39, 390, 7, Surface);
            if (level > 0) Block(details, "Installed levels", 0, -39, 390 * level / 3f, 7, Active);
            string title = category switch
            {
                WorkshopCategory.Engine => "TOP SPEED & ACCELERATION",
                WorkshopCategory.Handling => "LOW-SPEED STEERING",
                _ => "PAYLOAD & VOLUME"
            };
            Label(details, title, 0, -63, 390, 29, 17, Muted, true);
            Label(details, Stat(current), 0, -93, 390, 44, 24, Paper, true);
            Label(details, max ? "Maximum upgrade installed" : $"Next   {Stat(current.Upgrade(category))}",
                0, -140, 390, 50, 19, Muted);
            var price = fleet.UpgradePrice(category);
            var buy = Button(details, max ? "Fully upgraded" : $"Buy upgrade  ·  ${price:N0}", 0, -201, 390, 42, () =>
            {
                fleet.BuyUpgrade(category);
                notice.text = fleet.Notice;
                RenderDetails();
            });
            buy.interactable = !max && fleet.Balance >= price;
            Style(buy, true);
        }

        private string Stat(VehicleTuning tuning)
        {
            var model = fleet.Selected;
            return category switch
            {
                WorkshopCategory.Engine => $"{(model != null ? model.topSpeed : 14) * tuning.SpeedMultiplier * 3.6f:0.#} km/h  ·  +{(tuning.AccelerationMultiplier - 1) * 100:0}% accel.",
                WorkshopCategory.Handling => $"{42 * tuning.SteeringMultiplier:0.#}° steering angle",
                _ => $"{(model != null ? model.payloadKg : 350) * tuning.CapacityMultiplier:0.#} kg  ·  {(model != null ? model.cargoM3 : 2) * tuning.CapacityMultiplier:0.##} m³"
            };
        }

        private void RenderPaint()
        {
            var swatch = Block(details, "Paint swatch", 324, 0, 66, 53, draftPaint);
            var hex = Label(details, "", 0, 0, 310, 28, 20, Active, true);
            var apply = Button(details, $"Apply paint  ·  ${PlayerVehicleFleet.PaintPrice:N0}", 0, -202, 248, 42, () =>
            {
                if (fleet.BuyPaint(draftPaint)) paintEdited = false;
                notice.text = fleet.Notice;
                RenderDetails();
            });
            Style(apply, true);
            var factory = Button(details, "Factory paint", 259, -202, 131, 42, () =>
            {
                fleet.BuyPaint(null);
                draftPaint = fleet.SelectedTuning.Paint ?? car.FactoryColor;
                paintEdited = false;
                notice.text = fleet.Notice;
                RenderDetails();
            });
            factory.interactable = fleet.SelectedTuning.painted;

            void ShowColor()
            {
                swatch.color = draftPaint;
                hex.text = $"CUSTOM RGB   #{draftPaint.r:X2}{draftPaint.g:X2}{draftPaint.b:X2}";
                var saved = fleet.SelectedTuning.Paint;
                apply.interactable = paintEdited && fleet.Balance >= PlayerVehicleFleet.PaintPrice &&
                    (!saved.HasValue || !saved.Value.Equals(draftPaint));
            }
            AddChannel("R", 63, draftPaint.r, new Color32(218, 92, 80, 255), v => { draftPaint.r = v; ShowColor(); });
            AddChannel("G", 105, draftPaint.g, new Color32(134, 194, 112, 255), v => { draftPaint.g = v; ShowColor(); });
            AddChannel("B", 147, draftPaint.b, new Color32(102, 158, 228, 255), v => { draftPaint.b = v; ShowColor(); });
            ShowColor();
        }

        private void AddChannel(string channel, float y, byte value, Color tint, Action<byte> changed)
        {
            Label(details, channel, 0, -y, 28, 28, 20, tint, true);
            var number = Label(details, value.ToString(), 348, -y, 42, 28, 17, Paper);
            var rect = ui.Rect("Paint " + channel, details, Vector2.up, Vector2.up, new Vector2(37, -y), new Vector2(299, 30));
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            Block(rect, "Track", 4, -11, 291, 7, Surface);
            var handleArea = ui.Rect("Handle area", rect, Vector2.zero, Vector2.zero, new Vector2(6, 0), new Vector2(-12, 0));
            handleArea.anchorMax = Vector2.one;
            var handle = Block(handleArea, "Handle", 0, 0, 16, 20, tint);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = Vector2.one * .5f;
            handle.rectTransform.pivot = Vector2.one * .5f;
            handle.rectTransform.sizeDelta = new Vector2(16, -8);
            handle.raycastTarget = true;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.maxValue = 255;
            slider.wholeNumbers = true;
            slider.value = value;
            slider.onValueChanged.AddListener(next =>
            {
                var component = (byte)next;
                number.text = component.ToString();
                paintEdited = true;
                changed(component);
                car.PreviewPaint(draftPaint);
                notice.text = "Preview only  ·  Apply paint to keep this color.";
            });
        }

        private Image Block(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = ui.Rect(name, parent, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height));
            var image = ui.Panel(rect, color);
            image.sprite = null;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(Transform parent, string text, float x, float y, float width, float height,
            int size, Color color, bool bold = false)
        {
            var label = ui.Label(parent, text, new Vector2(x, y), new Vector2(width, height), size);
            label.color = color;
            if (bold) label.fontStyle = FontStyles.Bold;
            return label;
        }

        private Button Button(Transform parent, string text, float x, float y, float width, float height, Action action)
        {
            var button = ui.Button(parent, text, new Vector2(x, y), new Vector2(width, height), action);
            Style(button, false);
            return button;
        }

        private static void Style(Button button, bool highlighted)
        {
            button.image.sprite = null;
            button.image.color = highlighted ? Active : Surface;
            button.GetComponentInChildren<TMP_Text>().color = highlighted ? Ink : Paper;
        }

        public void Dispose()
        {
            if (car != null) car.PreviewPaint(fleet.SelectedTuning.Paint);
            if (root != null)
            {
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
        }
    }
}
