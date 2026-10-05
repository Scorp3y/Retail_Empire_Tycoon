using System.Collections;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Settings UI delegates to the project's sound, locale and camera controllers.</summary>
    public sealed class ShopSettingsPanel : MonoBehaviour
    {
        public ControlPanel controlPanel;
        public ShopOperationsHud operationsHud;
        public LanguageSelector language;
        public PlaySound sound;
        public Slider musicVolume;
        public Slider effectsVolume;
        public Button controlsButton;
        public Button languageButton;
        public Button closeButton;
        public Button menuButton;
        public Button exitButton;
        public Button saveButton;
        public Button hintsButton;
        private void OnEnable()
        {
            musicVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", .2f));
            effectsVolume.SetValueWithoutNotify(PlayerPrefs.GetFloat("SoundVolume", .2f));
            musicVolume.onValueChanged.AddListener(SetMusic);
            effectsVolume.onValueChanged.AddListener(SetEffects);
            controlsButton.onClick.AddListener(operationsHud.ToggleControls);
            languageButton.onClick.AddListener(ChangeLanguage);
            closeButton.onClick.AddListener(controlPanel.ExitSetting);
            menuButton.onClick.AddListener(AskMainMenu);
            exitButton.onClick.AddListener(AskExit);
            hintsButton.onClick.AddListener(ToggleHints);
            RefreshLanguage();
            RefreshHints();
        }
        private void OnDisable()
        {
            musicVolume.onValueChanged.RemoveListener(SetMusic);
            effectsVolume.onValueChanged.RemoveListener(SetEffects);
            controlsButton.onClick.RemoveListener(operationsHud.ToggleControls);
            languageButton.onClick.RemoveListener(ChangeLanguage);
            closeButton.onClick.RemoveListener(controlPanel.ExitSetting);
            menuButton.onClick.RemoveListener(AskMainMenu);
            exitButton.onClick.RemoveListener(AskExit);
            hintsButton.onClick.RemoveListener(ToggleHints);
        }
        private void SetMusic(float value) { if (sound != null) sound.SetMusicVolume(value); }
        private void SetEffects(float value) { if (sound != null) sound.SaveSoundVolume(value); }
        private void ChangeLanguage()
        {
            if (language != null) language.SetLanguageByCode(ShopText.Russian ? "en" : "ru");
            RefreshLanguage();
            RefreshHints();
        }
        private void ToggleHints() { operationsHud.SetHintsVisible(!operationsHud.HintsVisible); RefreshHints(); }
        private void RefreshHints()
        {
            hintsButton.GetComponentInChildren<TMPro.TMP_Text>().text = operationsHud.HintsVisible
                ? ShopText.Get("Подсказки: вкл.", "Hints: on") : ShopText.Get("Подсказки: выкл.", "Hints: off");
            hintsButton.image.color = operationsHud.HintsVisible ? ShopUiTheme.Green : ShopUiTheme.Line;
        }
        private void RefreshLanguage()
        {
            languageButton.GetComponentInChildren<TMPro.TMP_Text>().text = ShopText.Russian ? "Русский   ›" : "English   ›";
        }
        private void AskMainMenu() { Confirm(ShopText.Get("Вернуться в главное меню?", "Return to main menu?"), controlPanel.BackMenu); }
        private void AskExit() { Confirm(ShopText.Get("Выйти из игры?", "Exit the game?"), controlPanel.ExitGame); }
        private void Confirm(string question, System.Action action)
        {
            var ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var panel = ui.Modal("Leave confirmation", transform.parent, new Vector2(420, 190));
            ui.Label(panel, question, new Vector2(24, -22), new Vector2(370, 40), 23);
            ui.Copy(panel, "Несохранённые изменения будут потеряны.", "Unsaved changes will be lost.", new Vector2(24, -70), new Vector2(370, 36), 16).color = ShopUiTheme.Muted;
            ui.Button(panel, "Отмена", "Cancel", new Vector2(24, -130), new Vector2(172, 36), () => ui.CloseModal(panel)).image.color = ShopUiTheme.Line;
            ui.Button(panel, "Продолжить", "Continue", new Vector2(224, -130), new Vector2(172, 36), () => { ui.CloseModal(panel); action(); });
        }
    }
}
