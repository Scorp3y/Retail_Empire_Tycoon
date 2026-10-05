using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Products;
using RetailEmpireTycoon.UI.Shop;
using RetailEmpireTycoon.UI.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>UI integration tests run only in the disposable scene, with no SaveManager.</summary>
public static class ShopUiValidation
{
    private const string Folder = "Library/ShopUiQA";
    [MenuItem("Retail Empire/UI/Open isolated UI preview")]
    public static void OpenPreview() { ShopGameplaySandbox.Open(); }

    [MenuItem("Retail Empire/UI/Validate redesigned UI in sandbox %#F2")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires isolated sandbox Play Mode.");
        if (Object.FindObjectOfType<SaveManager>(true) != null) throw new InvalidOperationException("A UI test must not write the player save.");
        Object.FindObjectOfType<StoreOperations>().StartCoroutine(Check());
    }
    private static IEnumerator Check()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/validation.txt", "UI validation is running.\n");
        var log = new StringBuilder();
        bool completed = false;
        var controls = Object.FindObjectOfType<GameplayControls>();
        var shop = Object.FindObjectOfType<ShopWindow>(true);
        var inventory = Object.FindObjectOfType<BuildInventoryWindow>(true);
        var wallet = Object.FindObjectOfType<MoneyController>();
        var buildInventory = Object.FindObjectOfType<BuildInventory>();
        var products = Object.FindObjectOfType<ProductInventory>();
        var hud = Object.FindObjectOfType<ShopOperationsHud>();
        var settings = Object.FindObjectOfType<ControlPanel>();
        int initialMoney = wallet.Money;
        float initialTimeScale = Time.timeScale;
        int beforeCount = 0;
        bool previousOpen = Object.FindObjectOfType<StoreOperations>().IsOpen;
        Object.FindObjectOfType<StoreOperations>().SetOpen(false);
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/CashRegister.asset");
        var product = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/MilkBottle_Product.asset");
        var oldLanguage = PlayerPrefs.GetString("lang", "ru");
        bool initialHints = hud.HintsVisible;
        bool hadHintsPreference = PlayerPrefs.HasKey("ShopUi.ShowHints");
        int initialHintsPreference = PlayerPrefs.GetInt("ShopUi.ShowHints", 1);
        var language = Object.FindObjectOfType<LanguageSelector>(true);
        var territory = Object.FindObjectOfType<TerritoryPurchaseModeManager>();
        try
        {
            // UI animations use unscaled time; wages and in-flight sales must not alter purchase assertions.
            Time.timeScale = 0;
            language.SetLanguageByCode("ru"); wallet.SetMoney(50000); controls.CloseWindows();
            yield return Capture("hud");
            Require(Object.FindObjectOfType<ShopHudPresenter>() != null, "New edge HUD is absent.");
            var shortcutRoot = Object.FindObjectsOfType<RectTransform>(true).First(t => t.name == "Action shortcuts");
            Require(shortcutRoot.GetComponent<Graphic>() == null, "Shortcut panel still has a background.");
            Require(Object.FindObjectsOfType<TMP_Text>().Any(t => t.text == "$50000"), "Wallet did not refresh.");
            wallet.SetMoney(1000000);
            Require(Object.FindObjectsOfType<TMP_Text>().Any(t => t.text == "$1M"), "Million suffix did not refresh.");
            wallet.SetMoney(50000);
            log.AppendLine("PASS edge HUD and wallet event update");
            var store = Object.FindObjectOfType<StoreOperations>();
            var lockAnimation = Object.FindObjectOfType<ShopLockAnimation>();
            Require(lockAnimation != null, "Store lock animation missing.");
            store.SetOpen(true); yield return null;
            Require(lockAnimation.Progress > 0 && lockAnimation.Progress < 1, "Lock jumps instead of animating open.");
            float animationDeadline = Time.realtimeSinceStartup + 10;
            while (lockAnimation.Progress < 1 && Time.realtimeSinceStartup < animationDeadline) yield return null;
            Require(lockAnimation.Progress == 1, "Lock did not finish opening.");
            store.SetOpen(false); yield return null;
            Require(lockAnimation.Progress > 0 && lockAnimation.Progress < 1, "Lock jumps instead of animating closed.");
            animationDeadline = Time.realtimeSinceStartup + 10;
            while (lockAnimation.Progress > 0 && Time.realtimeSinceStartup < animationDeadline) yield return null;
            Require(lockAnimation.Progress == 0, "Lock did not finish closing.");
            var cameraMode = Object.FindObjectOfType<CameraModeController>();
            var cameraPosition = Camera.main.transform.position;
            var cameraRotation = Camera.main.transform.rotation;
            territory.Enter(); yield return Capture("territories");
            var territoryBack = GameObject.Find("Return from territories").GetComponent<Button>();
            RequireButtonReceivesPointer(territoryBack); territoryBack.onClick.Invoke(); yield return null;
            Require(!territory.IsActive && !territoryBack.gameObject.activeSelf, "Territory back button did not leave purchase mode.");
            float cameraDeadline = Time.realtimeSinceStartup + 10;
            while (cameraMode.IsLocked && Time.realtimeSinceStartup < cameraDeadline) yield return null;
            Require(!cameraMode.IsLocked && Vector3.Distance(Camera.main.transform.position, cameraPosition) < .01f
                && Quaternion.Angle(Camera.main.transform.rotation, cameraRotation) < .1f, "Territory return did not restore the camera.");
            log.AppendLine("PASS opening/closing lock animation and actionable territory return button");
            shop.gameObject.SetActive(true); shop.ShowMainTabs(); yield return Capture("departments");
            RequireCentered((RectTransform)shop.transform);
            shop.gameObject.SetActive(true); shop.OpenCategory_Structures();
            yield return Capture("structures");
            File.WriteAllLines(Folder + "/graphics.txt", shop.GetComponentsInChildren<Graphic>().Select(g => $"{g.name}: {g.GetType().Name}, enabled={g.enabled}, layer={g.gameObject.layer}, color={g.color}, material={g.materialForRendering.name}, shader={g.materialForRendering.shader.name}, rect={g.rectTransform.rect}"));
            Require(Object.FindObjectsOfType<ShopIconGraphic>().All(g => g.GetComponent<CanvasRenderer>() != null), "Navigation icon has no CanvasRenderer.");
            var firstBuy = shop.GetComponentsInChildren<ShopItemCard>().First().buyButton;
            RequireButtonReceivesPointer(firstBuy);
            var card = shop.GetComponentsInChildren<ShopItemCard>().First(c => c.nameText.text == "Касса");
            beforeCount = buildInventory.GetCount(item);
            card.buyButton.onClick.Invoke();
            Require(wallet.Money == 50000 - item.price && buildInventory.GetCount(item) == beforeCount + 1, "Equipment purchase failed.");
            wallet.SetMoney(0); Require(!card.buyButton.interactable, "Equipment purchase remains enabled without money.");
            wallet.SetMoney(50000); Require(card.buyButton.interactable, "Equipment purchase failed to re-enable.");
            log.AppendLine("PASS real equipment purchase, wallet and disabled button state");
            shop.OpenCategory_Shelves(); yield return Capture("shelves");
            Require(shop.GetComponentsInChildren<ShopItemCard>().Length >= 5, "Shelf catalog is incomplete.");
            var shelfCards = shop.GetComponentsInChildren<ShopItemCard>();
            Require(shelfCards[0].nameText.text == "Овощи и фрукты" && shelfCards[1].nameText.text == "Хлебный стеллаж",
                "Basic produce and bakery racks are not the first catalog row.");
            Require(shop.GetComponent<ShopWindowNavigation>().categories.Length == 2, "Personnel duplicated in equipment categories.");
            var scroll = shop.GetComponentInChildren<ScrollRect>();
            Require(scroll.vertical && !scroll.horizontal, "Catalog still scrolls horizontally.");
            Require(scroll.content.rect.height > scroll.viewport.rect.height, "Catalog does not expose the last row by scrolling.");
            scroll.verticalNormalizedPosition = 0; yield return Capture("shelves_bottom");
            log.AppendLine("PASS category navigation, complete vertical catalog and bottom row");
            shop.OpenProducts(); yield return Capture("products");
            Require(scroll.content.anchoredPosition.y < 1, "Changing category leaves the first products offscreen.");
            int beforeProduct = products.GetCount(product);
            var productCard = shop.GetComponentsInChildren<ProductShopItemCard>().First(c => c.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Молоко"));
            productCard.GetComponentInChildren<Button>().onClick.Invoke();
            Require(products.GetCount(product) == beforeProduct + product.BoxAmount, "Product purchase did not add a pack.");
            log.AppendLine("PASS product purchase and real warehouse pack count");
            hud.ToggleStaff(); yield return Capture("staff");
            Require(hud.StaffVisible, "Staff is not inside the store window.");
            var portraits = shop.GetComponentsInChildren<Image>().Where(i => i.name == "Employee portrait").Select(i => i.sprite).ToArray();
            Require(portraits.Length == 4 && portraits.All(p => p != null) && portraits.Distinct().Count() == 4, "Professions do not have four distinct avatars.");
            Require(shop.GetComponentsInChildren<Button>().Count(b => b.GetComponentInChildren<TMP_Text>()?.text == "Нанять") == 4, "Four professions are not available.");
            var hire = shop.GetComponentsInChildren<Button>().First(b => b.interactable && b.GetComponentInChildren<TMP_Text>()?.text == "Нанять");
            hire.onClick.Invoke(); yield return null;
            Require(Object.FindObjectOfType<StoreOperations>().Staff.Count(StaffRole.Cashier) > 0, "Staff hire action failed.");
            log.AppendLine("PASS personnel route and existing hiring rules");
            controls.CloseWindows(); inventory.gameObject.SetActive(true); inventory.ShowFurniture(); yield return Capture("inventory");
            RequireCentered((RectTransform)inventory.transform);
            Require(inventory.GetComponentsInChildren<BuildInventoryItemRow>().Any(r => r.nameText.text == "Касса"), "Bought checkout missing from inventory.");
            inventory.ShowProducts(); yield return Capture("warehouse");
            var stockRow = inventory.GetComponentsInChildren<ProductInventoryItemRow>().First(r => r.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Молоко"));
            Require(stockRow.GetComponentInChildren<Button>().interactable, "Stocked product cannot be selected.");
            log.AppendLine("PASS inventory and warehouse navigation, available restocking action");
            controls.CloseWindows(); settings.OpenSetting(); yield return Capture("settings");
            var panel = settings.settingPanel.GetComponent<ShopSettingsPanel>();
            Require(panel != null && panel.musicVolume != null && panel.effectsVolume != null, "Sound controls missing.");
            Require(panel.musicVolume.handleRect.GetComponent<ShopCapsuleGraphic>() != null && panel.effectsVolume.handleRect.GetComponent<ShopCapsuleGraphic>() != null, "Sliders still use stretched bitmap handles.");
            Require(Mathf.Abs(panel.musicVolume.handleRect.rect.height - 26) < .1f
                && Mathf.Abs(panel.effectsVolume.handleRect.rect.height - 26) < .1f, "Slider handles stretch beyond their intended height.");
            foreach (var slider in new[] { panel.musicVolume, panel.effectsVolume })
            {
                var track = (RectTransform)slider.transform.Find("Track");
                Vector3 trackCenter = slider.transform.InverseTransformPoint(track.TransformPoint(track.rect.center));
                Vector3 handleCenter = slider.transform.InverseTransformPoint(slider.handleRect.TransformPoint(slider.handleRect.rect.center));
                Require(Mathf.Abs(trackCenter.y - handleCenter.y) < .1f, "Slider handle is displaced above the rail.");
            }
            bool beforeHints = hud.HintsVisible;
            panel.hintsButton.onClick.Invoke(); Require(hud.HintsVisible != beforeHints, "Hints preference does not toggle.");
            Require(PlayerPrefs.GetInt("ShopUi.ShowHints", -1) == (hud.HintsVisible ? 1 : 0), "Hints preference is not persisted.");
            settings.ExitSetting(); yield return null;
            var hints = Object.FindObjectsOfType<RectTransform>(true).First(t => t.name == "Action shortcuts");
            Require(hints.gameObject.activeSelf == hud.HintsVisible, "Hints visibility ignores settings.");
            settings.OpenSetting(); yield return null; panel.hintsButton.onClick.Invoke();
            Require(hud.HintsVisible == beforeHints, "Hints could not be re-enabled.");
            panel.languageButton.onClick.Invoke(); Require(!ShopText.Russian, "Language selector failed.");
            var cornerWall = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Wall_Corner.asset");
            if (cornerWall != null) Require(!ShopText.Item(cornerWall).Contains("_"), "Wall name retains underscore.");
            yield return Capture("settings_english");
            panel.languageButton.onClick.Invoke(); Require(ShopText.Russian, "Russian selector failed.");
            Require(panel.GetComponentsInChildren<TMP_Text>().All(t => t.GetComponent<LocalizedFontControl>()?.excludeFromFontChange == true), "Locale selector can overwrite theme fonts.");
            log.AppendLine("PASS settings sound controls and bilingual theme typography");
            log.AppendLine("PASS centered windows, distinct staff avatars, vector sliders, persistent hide/show hints");
            panel.controlsButton.onClick.Invoke(); yield return Capture("controls");
            Require(hud.ControlsVisible && !settings.GetComponentInParent<Canvas>().enabled, "Binding modal does not isolate underlying UI.");
            hud.CloseControls(); Require(settings.GetComponentInParent<Canvas>().enabled, "Settings canvas did not restore.");
            panel.menuButton.onClick.Invoke(); yield return Capture("leave_confirmation");
            var leave = GameObject.Find("Leave confirmation");
            Require(leave != null, "Menu transition has no confirmation.");
            leave.GetComponentsInChildren<Button>().First().onClick.Invoke();
            settings.ExitSetting();
            var confirm = Object.FindObjectOfType<ConfirmPurchaseUI>(true);
            bool accepted = false;
            confirm.Show(ShopText.Get("Расширить магазин за $2000?", "Expand your store for $2000?"), () => accepted=true, () => { });
            yield return Capture("territory_confirmation");
            Require(!accepted, "Displaying confirmation bought territory."); confirm.HideInstant();
            var theme=Resources.Load<ShopUiTheme>("ShopUi/Theme");Require(theme.regularFont.HasCharacter('Я') && theme.regularFont.HasCharacter('ё'), "Missing Cyrillic glyphs.");
            log.AppendLine("PASS keyboard settings modal input and Cyrillic font");
            var shelf = Object.FindObjectOfType<PlacedShelfStock>();
            var shelfInfo = Object.FindObjectOfType<ShelfInfoWindow>(true);
            if (shelf != null && shelfInfo != null)
            {
                foreach (var point in new[] { Vector2.zero, new Vector2(Screen.width, 0),
                    new Vector2(0, Screen.height), new Vector2(Screen.width, Screen.height) })
                {
                    shelfInfo.Show(shelf, point);
                    yield return null;
                    RequireWithinCanvas((RectTransform)shelfInfo.transform);
                }
                yield return Capture("shelf_information");
                shelfInfo.Hide();
                log.AppendLine("PASS shelf information stays within all four screen edges");
            }
            File.WriteAllText(Folder + "/validation.txt", log + "UI VALIDATION PASSED\n"); Debug.Log("Shop UI validation PASSED. Screenshots: " + Folder);
            completed = true;
        }
        finally
        {
            if (territory.IsActive) territory.Exit();
            controls.CloseWindows(); hud.CloseControls();
            wallet.SetMoney(initialMoney); language.SetLanguageByCode(oldLanguage);
            hud.SetHintsVisible(initialHints, false);
            if (hadHintsPreference) PlayerPrefs.SetInt("ShopUi.ShowHints", initialHintsPreference); else PlayerPrefs.DeleteKey("ShopUi.ShowHints");
            PlayerPrefs.Save();
            Object.FindObjectOfType<StoreOperations>().SetOpen(previousOpen);
            Time.timeScale = initialTimeScale;
            if (!completed) File.WriteAllText(Folder + "/validation.txt",log+"UI VALIDATION DID NOT COMPLETE; inspect the Unity error.\n");
        }
    }
    private static IEnumerator Capture(string name)
    {
        yield return null; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        foreach (var fit in Object.FindObjectsOfType<ShopPanelFit>())
            RequireWithinCanvas((RectTransform)fit.transform);
        var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
    private static void RequireWithinCanvas(RectTransform panel)
    {
        var canvas = (RectTransform)panel.GetComponentInParent<Canvas>().transform;
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector3 point = canvas.InverseTransformPoint(corner);
            Require(point.x >= canvas.rect.xMin - 1 && point.x <= canvas.rect.xMax + 1
                && point.y >= canvas.rect.yMin - 1 && point.y <= canvas.rect.yMax + 1,
                "A UI window leaves the canvas: " + panel.name);
        }
    }
    private static void RequireCentered(RectTransform panel)
    {
        var canvas = (RectTransform)panel.GetComponentInParent<Canvas>().transform;
        Vector3 point = canvas.InverseTransformPoint(panel.TransformPoint(panel.rect.center));
        Require(Vector2.Distance(point, canvas.rect.center) < 1, "Window is not centered: " + panel.name);
    }
    private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static void RequireButtonReceivesPointer(Button button)
    {
        var system = EventSystem.current;
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var results = new System.Collections.Generic.List<RaycastResult>();
        system.RaycastAll(new PointerEventData(system) { position = point }, results);
        Require(results.Count > 0 && results[0].gameObject.GetComponentInParent<Button>() == button,
            "An overlay blocks the catalog purchase button.");
    }
}
