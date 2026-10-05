using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Compact edge HUD; detailed actions use input-blocking, dismissible modal windows.</summary>
    public sealed class ShopOperationsHud : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Canvas[] legacyCanvases;
        [SerializeField] private StoreOperations shop;
        [SerializeField] private WorkMinigame work;
        [SerializeField] private GameplayControls controls;
        [SerializeField] private MainCamera cameraInput;
        [SerializeField] private ControlPanel settings;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite panelSprite, buttonSprite;
        [SerializeField] private Sprite activeStar, inactiveStar, squareButtonSprite, staffIcon, controlsIcon, windowSprite;
        [SerializeField] private Sprite employeePortrait;
        [SerializeField] private ShopWindow storeWindow;
        private ShopUi _ui;
        private readonly LegacyUiVisibility _legacyUi = new LegacyUiVisibility();
        private RectTransform _status, _actions, _staff, _bindings, _tasks;
        private TMP_Text _staffNotice, _bindingNotice;
        private Button _openButton, _cashierButton, _cleanButton;
        private readonly Dictionary<ShopperVisit, RectTransform> _thiefBars = new Dictionary<ShopperVisit, RectTransform>();
        private readonly List<TMP_Text> _staffLabels = new List<TMP_Text>();
        private readonly List<Button> _hireButtons = new List<Button>();
        private readonly List<Button> _dismissButtons = new List<Button>();
        private readonly List<Button> _bindingButtons = new List<Button>();
        private bool _savedInput;
        private readonly List<Image> _stars = new List<Image>();
        private ShopIconGraphic _lockIcon;
        private const string HintsPreference = "ShopUi.ShowHints";
        private bool _hintsVisible;
        private readonly List<TMP_Text> _shortcutKeys = new List<TMP_Text>();
        private readonly List<TMP_Text> _shortcutNames = new List<TMP_Text>();
        public bool HintsVisible => _hintsVisible;
        private void Awake() { _hintsVisible = PlayerPrefs.GetInt(HintsPreference, 1) != 0; }
        public void SetHintsVisible(bool visible, bool persist = true)
        {
            _hintsVisible = visible;
            if (persist) { PlayerPrefs.SetInt(HintsPreference, visible ? 1 : 0); PlayerPrefs.Save(); }
        }
        public bool ControlsVisible => _bindings != null;
        public bool StaffVisible => _staff != null && storeWindow != null && storeWindow.IsStaffView;
        public bool ModalVisible => ControlsVisible || StaffVisible;

        private void Start()
        {
            _ui = new ShopUi(font, panelSprite, buttonSprite, windowSprite);
            _status = _ui.Rect("Shop rating", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(190, 36));
            for (int i = 0; i < 5; i++)
            {
                var star = _ui.Rect("Rating star " + (i + 1), _status, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 38, 0), new Vector2(34, 34));
                var empty = _ui.Panel(star, Color.white); empty.sprite = inactiveStar; empty.type = Image.Type.Simple; empty.raycastTarget = false;
                var fill = _ui.Rect("Active fill", star, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, star.sizeDelta);
                var image = _ui.Panel(fill, Color.white); image.sprite = activeStar; image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; image.raycastTarget = false; _stars.Add(image);
            }
            _openButton = _ui.IconButton(canvas.transform, ShopIcon.ClosedLock, Vector2.zero, 48, () => shop.SetOpen(!shop.IsOpen));
            var openRect = (RectTransform)_openButton.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1, 1);
            openRect.anchoredPosition = new Vector2(-130, -16);
            _lockIcon = _openButton.GetComponentInChildren<ShopIconGraphic>();
            _openButton.gameObject.AddComponent<ShopLockAnimation>().Initialize(shop, _lockIcon);
            _actions = _ui.Rect("Action shortcuts", canvas.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(192, 238));
            StyleShortcutText(_ui.Heading(_actions, "Управление", "Shortcuts", new Vector2(12, -10), new Vector2(166, 28), 17));
            for (int i = 0; i < 7; i++)
            {
                var key = _ui.Rect("Shortcut key", _actions, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -45 - i * 26), new Vector2(30, 22));
                _ui.Panel(key, ShopUiTheme.Ink).raycastTarget = false;
                var label = _ui.Label(key, "", Vector2.zero, new Vector2(30, 22), 14);
                label.alignment = TextAlignmentOptions.Center; label.color = ShopUiTheme.Paper;
                _shortcutKeys.Add(label);
                var actionName = _ui.Label(_actions, "", new Vector2(52, -45 - i * 26), new Vector2(130, 22), 14);
                StyleShortcutText(actionName);
                _shortcutNames.Add(actionName);
            }
            _tasks = _ui.Rect("Available player jobs", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(440, 36));
            _cashierButton = _ui.Button(_tasks, "", Vector2.zero, new Vector2(214, 36), () => { controls.CloseWindows(); work.BeginCheckout(); });
            _cleanButton = _ui.Button(_tasks, "", new Vector2(226, 0), new Vector2(214, 36), BeginCleaning);
            AddSettingsEntry();
            AddStaffEntries();
            controls.Changed += RefreshBindings;
        }

        private static void StyleShortcutText(TMP_Text text)
        {
            // A light label with a dark outline stays readable against both roads and grass without a panel.
            text.color = ShopUiTheme.Paper;
            text.outlineColor = ShopUiTheme.Ink;
            text.outlineWidth = .18f;
            text.raycastTarget = false;
        }

        private void AddSettingsEntry()
        {
            if (settings == null || settings.settingPanel == null) return;
            if (settings.settingPanel.GetComponent<ShopSettingsPanel>() != null) return;
            // Legacy settings use a 1920x1080 canvas and a 100x100 centered container, not a full-size panel.
            var button = _ui.Button(settings.settingPanel.transform, "", Vector2.zero, new Vector2(92, 92), ToggleControls);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(140, -50);
            button.image.sprite = squareButtonSprite; button.image.color = Color.white; button.image.type = Image.Type.Simple;
            AddIcon(rect, controlsIcon, new Vector2(68, 68));
            var label = _ui.Label(rect, "Управление", new Vector2(100, -20), new Vector2(205, 52), 30);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var background = _ui.Rect("Control label background", rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -20), new Vector2(205, 52));
            var image = _ui.Panel(background, new Color(.2f, .38f, .11f)); image.type = Image.Type.Simple; image.raycastTarget = false; background.SetSiblingIndex(0);
            var muteLabel = settings.settingPanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.text.Trim().Equals("MUTE", StringComparison.OrdinalIgnoreCase));
            var captionImage = muteLabel != null ? muteLabel.GetComponentInParent<Image>() : null;
            if (captionImage != null && captionImage.rectTransform.rect.width < 500 && captionImage.rectTransform.rect.height < 200)
            { image.sprite = captionImage.sprite; image.type = captionImage.type; image.color = captionImage.color; }
        }

        private void AddIcon(RectTransform parent, Sprite sprite, Vector2 size)
        {
            var rect = _ui.Rect("Icon", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = _ui.Panel(rect, Color.white); image.sprite = sprite; image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
        }
        private void AddStaffEntries()
        {
            if (storeWindow == null) return;
            storeWindow.StaffRequested += BuildStaffPage;
            if (storeWindow.GetComponent<ShopWindowNavigation>() != null) return;
            var mainTemplate = storeWindow.MainTabsHost.GetComponentsInChildren<Button>(true).FirstOrDefault();
            var main = _ui.Button(storeWindow.MainTabsHost, "", Vector2.zero, new Vector2(150, 118), storeWindow.OpenStaff);
            MatchCategoryButton(main, mainTemplate);
            var mainRect = (RectTransform)main.transform; mainRect.anchorMin = mainRect.anchorMax = mainRect.pivot = new Vector2(0.5f, 0.5f); mainRect.anchoredPosition = new Vector2(0, -60);
            AddIcon(mainRect, staffIcon, new Vector2(65, 65));
        }
        private static void MatchCategoryButton(Button target, Button template)
        {
            if (template == null || template.image == null) return;
            target.image.sprite = template.image.sprite; target.image.type = template.image.type;
            target.image.pixelsPerUnitMultiplier = template.image.pixelsPerUnitMultiplier;
            target.image.color = template.image.color;
            var rect = (RectTransform)target.transform; var source = (RectTransform)template.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, source.sizeDelta.y * source.localScale.y);
        }

        private void Update()
        {
            if (_status == null) return;
            bool overview = !work.IsActive && !ModalVisible && !controls.LegacyWindowVisible && !controls.IsConstructionActive;
            _status.gameObject.SetActive(overview);
            _actions.gameObject.SetActive(overview && HintsVisible);
            _openButton.gameObject.SetActive(overview);
            for (int i = 0; i < _stars.Count; i++) _stars[i].fillAmount = Mathf.Clamp01(shop.Rating - i);
            RefreshShortcutText();
            int customers = shop.Visits.Count(v => v.Stage == VisitStage.Waiting && !v.Claimed);
            int dirt = shop.Dirt.Count(d => d != null && !d.IsClaimed);
            _tasks.gameObject.SetActive(overview && (customers > 0 || dirt > 0));
            _cashierButton.gameObject.SetActive(customers > 0);
            _cleanButton.gameObject.SetActive(dirt > 0);
            _cashierButton.GetComponentInChildren<TMP_Text>().text = ShopText.Get("Касса: ", "Checkout: ") + customers + "  [" + controls.Key(ShopAction.Checkout) + "]";
            _cleanButton.GetComponentInChildren<TMP_Text>().text = ShopText.Get("Уборка: ", "Cleaning: ") + dirt;
            if (StaffVisible) RefreshStaff();
            RefreshThieves(overview);
            if (overview) HandleThiefClick();
            if (ControlsVisible && Input.GetKeyDown(KeyCode.Escape) && !controls.Capturing.HasValue && !controls.CaptureCancelledThisFrame) CloseControls();
        }

        private void RefreshShortcutText()
        {
            ShopAction[] actions = { ShopAction.Inventory, ShopAction.Warehouse, ShopAction.Catalog, ShopAction.Territory, ShopAction.Staff, ShopAction.OpenStore, ShopAction.Checkout };
            string[] names = { "Инвентарь", "Склад", "Закупки", "Участки", "Персонал", "Магазин", "Касса" };
            string[] english = { "Inventory", "Warehouse", "Orders", "Territories", "Staff", "Store", "Checkout" };
            for (int i = 0; i < actions.Length; i++)
            {
                _shortcutKeys[i].text = controls.Key(actions[i]).ToString();
                _shortcutNames[i].text = ShopText.Get(names[i], english[i]);
            }
        }
        private void BeginCleaning()
        {
            controls.CloseWindows();
            var dirt = shop.Dirt.FirstOrDefault(d => d != null && !d.IsClaimed);
            if (dirt != null) work.BeginCleaning(dirt);
        }
        private void HandleThiefClick()
        {
            if (!Input.GetMouseButtonDown(0) || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, 2000, ~0, QueryTriggerInteraction.Collide)) return;
            var character = hit.collider.GetComponentInParent<ShopCharacter>();
            var visit = shop.Visits.FirstOrDefault(v => v.Character == character && v.Stage == VisitStage.Escaping);
            if (visit != null) shop.ClickThief(visit);
        }
        private void RefreshThieves(bool visible)
        {
            foreach (var pair in _thiefBars.ToArray())
                if (!shop.Visits.Contains(pair.Key) || pair.Key.Stage != VisitStage.Escaping || pair.Key.Character == null)
                { Destroy(pair.Value.gameObject); _thiefBars.Remove(pair.Key); }
            foreach (var visit in shop.Visits.Where(v => v.Stage == VisitStage.Escaping && v.Character != null))
            {
                if (!_thiefBars.TryGetValue(visit, out var rect))
                {
                    rect = _ui.Rect("Thief HP", canvas.transform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(105, 30));
                    var captured = visit;
                    var button = _ui.Button(rect, "", Vector2.zero, rect.sizeDelta, () => shop.ClickThief(captured));
                    button.image.color = new Color(0.75f, 0.15f, 0.08f);
                    _thiefBars.Add(visit, rect);
                }
                Vector3 screen = Camera.main.WorldToScreenPoint(visit.Character.transform.position + Vector3.up * 0.6f);
                rect.gameObject.SetActive(visible && screen.z > 0);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var point);
                rect.localPosition = point;
                rect.GetComponentInChildren<TMP_Text>().text = "ВОР  " + visit.RemainingCatchClicks + " / " + shop.Balance.thiefClicks;
            }
        }

        private void LockModalInput()
        {
            _savedInput = cameraInput.enabled;
            cameraInput.enabled = false;
            _legacyUi.Hide(legacyCanvases, canvas);
        }
        private void RestoreModalInput()
        {
            if (cameraInput != null) cameraInput.enabled = _savedInput;
            _legacyUi.Restore();
        }
        public void ToggleStaff()
        {
            if (StaffVisible) { CloseStaff(); return; }
            if (work.IsActive || ControlsVisible || controls.IsConstructionActive) return;
            controls.CloseWindows();
            if (storeWindow == null) return;
            storeWindow.gameObject.SetActive(true);
            storeWindow.OpenStaff();
        }
        private void BuildStaffPage()
        {
            if (_staff != null) { _staff.gameObject.SetActive(true); RefreshStaff(); return; }
            _staff = _ui.Rect("Staff recruitment", storeWindow.StaffHost, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(560, 440));
            storeWindow.AttachStaffPage(_staff.gameObject);
            _ui.Heading(_staff, "Команда магазина", "Your team", new Vector2(4, -10), new Vector2(530, 30), 23);
            _ui.Copy(_staff, "Зарплата начисляется, пока магазин открыт.", "Wages are paid while the store is open.", new Vector2(4, -44), new Vector2(540, 30), 15).color = ShopUiTheme.Muted;
            _staffLabels.Clear(); _hireButtons.Clear(); _dismissButtons.Clear();
            for (int i = 0; i < 4; i++)
            {
                var role = (StaffRole)i;
                float y = -80 - i * 80;
                var row = _ui.Rect("Recruit " + role, _staff, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(560, 72));
                _ui.Panel(row, new Color32(235, 235, 217, 255));
                var theme = Resources.Load<ShopUiTheme>("ShopUi/Theme");
                Sprite rolePortrait = theme != null && theme.staffPortraits != null && i < theme.staffPortraits.Length
                    ? theme.staffPortraits[i] : employeePortrait;
                if (rolePortrait != null)
                {
                    var portrait = _ui.Rect("Employee portrait", row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -4), new Vector2(60, 64));
                    var image = _ui.Panel(portrait, Color.white); image.sprite = rolePortrait; image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
                }
                var badge = _ui.Rect("Profession badge", row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(49, -46), new Vector2(24, 24));
                _ui.Panel(badge, ShopUiTheme.Paper).raycastTarget = false;
                _ui.Icon(badge, (ShopIcon)((int)ShopIcon.Cashier + i), Vector2.zero, new Vector2(19, 19));
                _staffLabels.Add(_ui.Label(row, "", new Vector2(82, -7), new Vector2(254, 58), 15));
                _hireButtons.Add(_ui.Button(row, "Нанять", "Hire", new Vector2(350, -8), new Vector2(98, 28), () => shop.TryHire(role)));
                var dismiss = _ui.Button(row, "Уволить", "Dismiss", new Vector2(350, -39), new Vector2(98, 26), () => shop.TryDismiss(role));
                dismiss.image.color = ShopUiTheme.Line; _dismissButtons.Add(dismiss);
                var count = _ui.Label(row, "", new Vector2(464, -18), new Vector2(80, 34), 18);
                count.gameObject.name = "Employee count";
            }
            _staffNotice = _ui.Label(_staff, "", new Vector2(4, -405), new Vector2(550, 28), 16);
            RefreshStaff();
        }
        private void RefreshStaff()
        {
            string[] names = { "Кассир", "Охранник", "Складовщик", "Уборщик" };
            string[] englishNames = { "Cashier", "Guard", "Stocker", "Cleaner" };
            for (int i = 0; i < 4; i++)
            {
                var role = (StaffRole)i;
                _staffLabels[i].text = "<b>" + ShopText.Get(names[i], englishNames[i]) + "</b>\n" + ShopText.Get("Найм ", "Hire ") + MoneyFormat.Compact(shop.Balance.HireCost(role))
                    + "  ·  " + MoneyFormat.Compact(shop.Balance.Wage(role)) + ShopText.Get("/мин", "/min");
                var count = _staffLabels[i].transform.parent.Find("Employee count").GetComponent<TMP_Text>();
                count.text = shop.Staff.Count(role) + " / " + StaffRoster.Limit(role, shop.Level);
                _hireButtons[i].interactable = shop.Staff.CanHire(role, shop.Level) && shop.CanAffordHire(role);
                _dismissButtons[i].interactable = shop.Staff.Count(role) > 0;
            }
            _staffNotice.text = ShopText.Get("Баланс: ", "Balance: ") + MoneyFormat.Compact(shop.Money);
        }
        public void CloseStaff()
        {
            if (storeWindow != null && storeWindow.IsStaffView) storeWindow.ShowMainTabs();
        }
        public void ToggleControls()
        {
            if (ControlsVisible) { CloseControls(); return; }
            if (work.IsActive || controls.IsConstructionActive) return;
            CloseStaff(); LockModalInput();
            _bindings = _ui.Modal("Key binding settings", canvas.transform, new Vector2(560, 616));
            _ui.Heading(_bindings, "Управление", "Controls", new Vector2(24, -18), new Vector2(455, 34), 26);
            _ui.IconButton(_bindings, ShopIcon.Close, new Vector2(504, -14), 36, CloseControls);
            _bindingButtons.Clear();
            foreach (ShopAction action in Enum.GetValues(typeof(ShopAction)))
            {
                float y = -60 - (int)action * 34;
                _ui.Label(_bindings, GameplayControls.Name(action), new Vector2(24, y), new Vector2(315, 30), 18);
                var key = _ui.Button(_bindings, "", new Vector2(405, y), new Vector2(131, 30), () => controls.Capture(action));
                key.image.color = ShopUiTheme.Line; _bindingButtons.Add(key);
            }
            _bindingNotice = _ui.Label(_bindings, "", new Vector2(24, -481), new Vector2(512, 55), 16);
            _bindingNotice.color = ShopUiTheme.Muted;
            _ui.Button(_bindings, "По умолчанию", "Reset defaults", new Vector2(24, -562), new Vector2(240, 34), controls.ResetBindings).image.color = ShopUiTheme.Line;
            _ui.Button(_bindings, "Готово", "Done", new Vector2(296, -562), new Vector2(240, 34), CloseControls);
            RefreshBindings();
        }
        private void RefreshBindings()
        {
            if (!ControlsVisible) return;
            for (int i = 0; i < _bindingButtons.Count; i++)
                _bindingButtons[i].GetComponentInChildren<TMP_Text>().text = controls.Capturing == (ShopAction)i ? ShopText.Get("Нажми…", "Press…") : controls.Key((ShopAction)i).ToString();
            _bindingNotice.text = controls.BindingNotice ?? ShopText.Get("Нажми на клавишу справа, чтобы изменить её. WASD, стрелки и Esc зарезервированы.", "Select a key on the right to change it. WASD, arrow keys and Esc are reserved.");
        }
        public void CloseControls()
        {
            if (!ControlsVisible) return;
            if (controls != null) controls.CancelCapture();
            _ui.CloseModal(_bindings); _bindings = null;
            RestoreModalInput();
        }
        private void OnDisable() { CloseStaff(); CloseControls(); }
        private void OnDestroy() { if (controls != null) controls.Changed -= RefreshBindings; if (storeWindow != null) storeWindow.StaffRequested -= BuildStaffPage; }
    }
}
