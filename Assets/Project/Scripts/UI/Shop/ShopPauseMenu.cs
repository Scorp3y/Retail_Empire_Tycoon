using System;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Owns the pause overlay, its subpages and the previous simulation/input state.</summary>
    internal sealed class ShopPauseMenu
    {
        private readonly ShopUi ui;
        private readonly Transform canvas;
        private readonly ControlPanel settings;
        private readonly MainCamera cameraInput;
        private RectTransform menu, confirmation;
        private Transform settingsParent;
        private int settingsSibling;
        private TMP_Text notice, question;
        private Action leave;
        private float previousTimeScale;
        private bool previousCamera, showingSettings, showingBindings;
        internal bool IsOpen => menu != null;

        internal ShopPauseMenu(ShopUi ui, Transform canvas, ControlPanel settings, MainCamera cameraInput)
        {
            this.ui = ui; this.canvas = canvas; this.settings = settings; this.cameraInput = cameraInput;
        }

        internal void Open()
        {
            if (IsOpen) return;
            previousTimeScale = Time.timeScale;
            previousCamera = cameraInput != null && cameraInput.enabled;
            Time.timeScale = 0;
            LockCamera();
            menu = ui.Modal("Pause menu", canvas, new Vector2(406, 442));
            var shade = menu.parent.GetComponent<Image>();
            shade.color = new Color(.025f, .055f, .046f, .49f);

            var header = ui.Rect("Pause header", menu, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(12, -12), new Vector2(382, 74));
            ui.Panel(header, ShopUiTheme.Ink);
            ui.Icon(header, ShopIcon.Pause, new Vector2(-154, 0), new Vector2(28, 28), ShopUiTheme.Paper);
            ui.Heading(header, "Пауза", "Game paused", new Vector2(66, -19), new Vector2(287, 39), 27).color = ShopUiTheme.Paper;
            ActionRow("Продолжить", "Resume", ShopIcon.Play, 104, Close, true);
            var settingsButton = ActionRow("Настройки", "Settings", ShopIcon.Settings, 157, OpenSettings);
            settingsButton.interactable = settings != null && settings.settingPanel != null;
            var saveButton = ActionRow("Сохранить игру", "Save game", ShopIcon.Save, 210, Save);
            saveButton.interactable = SaveManager.Instance != null;
            ActionRow("Главное меню", "Main menu", ShopIcon.Back, 274,
                () => Confirm("Return to the main menu?", () => settings.BackMenu())).interactable = settings != null;
            ActionRow("Выйти из игры", "Exit game", ShopIcon.Exit, 327, () => Confirm("Exit the game?", Application.Quit));
            notice = ui.Label(menu, "Esc  ·  Resume", new Vector2(28, -391), new Vector2(350, 30), 15);
            notice.color = ShopUiTheme.Muted;
        }

        private Button ActionRow(string ru, string en, ShopIcon icon, int y, Action action, bool primary = false)
        {
            var button = ui.Button(menu, ru, en, new Vector2(28, -y), new Vector2(350, 43), action);
            if (!primary) button.image.color = ShopUiTheme.Line;
            ui.Icon(button.transform, icon, new Vector2(-146, 0), new Vector2(21, 21));
            return button;
        }

        private void OpenSettings()
        {
            var panel = settings.settingPanel.transform;
            settingsParent = panel.parent;
            settingsSibling = panel.GetSiblingIndex();
            panel.SetParent(menu.parent, false);
            showingSettings = true;
            menu.gameObject.SetActive(false);
            settings.OpenSetting();
        }

        internal bool SuspendSettingsForBindings()
        {
            if (!showingSettings || showingBindings) return false;
            showingBindings = true;
            settings.settingPanel.SetActive(false);
            menu.parent.gameObject.SetActive(false);
            return true;
        }

        internal void ResumeSettingsFromBindings()
        {
            if (!showingBindings) return;
            showingBindings = false;
            menu.parent.gameObject.SetActive(true);
            settings.OpenSetting();
        }

        internal void Tick()
        {
            if (showingSettings && !showingBindings && !settings.settingPanel.activeSelf)
                ReturnFromSettings();
        }

        internal void Escape()
        {
            if (showingSettings) ReturnFromSettings();
            else if (confirmation != null && confirmation.gameObject.activeSelf) BackFromConfirmation();
            else Close();
        }

        private void ReturnFromSettings()
        {
            RestoreSettingsParent();
            LockCamera();
            menu.gameObject.SetActive(true);
        }

        private void RestoreSettingsParent()
        {
            if (!showingSettings) return;
            showingSettings = showingBindings = false;
            var panel = settings.settingPanel.transform;
            panel.gameObject.SetActive(false);
            panel.SetParent(settingsParent, false);
            panel.SetSiblingIndex(settingsSibling);
        }

        private void Save()
        {
            try
            {
                notice.text = SaveManager.Instance != null && SaveManager.Instance.TrySaveGame()
                    ? "Game saved." : "Save is unavailable right now.";
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                notice.text = "Save failed. Try again before leaving.";
            }
        }

        private void Confirm(string title, Action action)
        {
            leave = action;
            menu.gameObject.SetActive(false);
            if (confirmation == null)
            {
                confirmation = ui.Rect("Leave paused game", menu.parent, Vector2.one * .5f, Vector2.one * .5f,
                    Vector2.zero, new Vector2(424, 194));
                ui.Panel(confirmation, ShopUiTheme.Paper);
                ui.Border(confirmation);
                confirmation.gameObject.AddComponent<ShopPanelFit>().MaximumSize = confirmation.sizeDelta;
                question = ui.Label(confirmation, "", new Vector2(24, -25), new Vector2(375, 38), 23);
                ui.Label(confirmation, "Unsaved progress will be lost.", new Vector2(24, -82), new Vector2(375, 32), 16).color = ShopUiTheme.Muted;
                ui.Button(confirmation, "Back", new Vector2(24, -132), new Vector2(172, 38), BackFromConfirmation);
                ui.Button(confirmation, "Leave", new Vector2(228, -132), new Vector2(172, 38), () =>
                {
                    var next = leave;
                    Close();
                    next?.Invoke();
                }).image.color = ShopUiTheme.Line;
            }
            question.text = title;
            confirmation.gameObject.SetActive(true);
        }

        private void BackFromConfirmation()
        {
            confirmation.gameObject.SetActive(false);
            leave = null;
            menu.gameObject.SetActive(true);
        }

        private void LockCamera() { if (cameraInput != null) cameraInput.enabled = false; }

        internal void Close()
        {
            if (!IsOpen) return;
            RestoreSettingsParent();
            ui.CloseModal(menu);
            menu = confirmation = null;
            leave = null;
            Time.timeScale = previousTimeScale;
            if (cameraInput != null) cameraInput.enabled = previousCamera;
        }
    }
}
