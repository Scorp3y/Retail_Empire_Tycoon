using TMPro;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine;

public class LanguageSelector : MonoBehaviour
{
    [Header("Font Settings")]
    public TMP_FontAsset englishFont;
    public TMP_FontAsset russianFont;

    [Header("Font Sizes")]
    public float englishFontSize = 80f;
    public float russianFontSize = 80f;
    private static LanguageSelector instance;


    void Awake()
    {
           SceneManager.sceneLoaded += OnSceneLoaded;     

    }

    void Start()
    {

        string savedLang = PlayerPrefs.GetString("lang", "");
        if (string.IsNullOrEmpty(savedLang))
        {
            string systemLang = Application.systemLanguage.ToString().ToLower();
            savedLang = systemLang.StartsWith("ru") ? "ru" : "en";
            PlayerPrefs.SetString("lang", savedLang);
        }

        SetLanguageByCode(savedLang);
    }

    public void SetLanguageByCode(string code)
    {
        var locale = LocalizationSettings.AvailableLocales.Locales
            .Find(x => x.Identifier.Code == code);

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString("lang", code);
            ApplyFontSettings(code);
        }
    }

    public void ApplyFontSettings(string code)
    {
        TMP_FontAsset font = code == "ru" ? russianFont : englishFont;

        TMP_Text[] allTexts = FindObjectsOfType<TMP_Text>(true);
        foreach (var text in allTexts)
        {
            var control = text.GetComponent<LocalizedFontControl>();
            if (control != null && control.excludeFromFontChange)
                continue;

            text.font = font;
        }

        var fontControls = FindObjectsOfType<LocalizedFontControl>(true);
        foreach (var control in fontControls)
        {
            control.ApplyFontSize();
        }
    }



    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string currentLang = PlayerPrefs.GetString("lang", "en");
        ApplyFontSettings(currentLang);
    }
}
