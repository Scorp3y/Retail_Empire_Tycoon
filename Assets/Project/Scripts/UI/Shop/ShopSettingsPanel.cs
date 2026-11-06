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
            // Saving and leaving the game live on the parent pause page.
            if (saveButton != null) saveButton.gameObject.SetActive(false);
            if (menuButton != null) menuButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            if (TryGetComponent<ShopPanelFit>(out var fit)) fit.MaximumSize = new Vector2(530, 474);
            ((RectTransform)closeButton.transform).anchoredPosition = new Vector2(24, -414);
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
            hintsButton.onClick.RemoveListener(ToggleHints);
        }
        private void SetMusic(float value) { if (sound != null) sound.SetMusicVolume(value); }
        private void SetEffects(float value) { if (sound != null) sound.SaveSoundVolume(value); }
        private void ChangeLanguage()
        {
            if (language != null) language.SetLanguageByCode("en");
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
            languageButton.GetComponentInChildren<TMPro.TMP_Text>().text = "English · current build";
            languageButton.interactable=false;
        }
    }
}
