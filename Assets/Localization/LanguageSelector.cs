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

        SetLanguageByCode("en");
    }

    public void SetLanguageByCode(string code)
    {
        code="en";
        var locale = LocalizationSettings.AvailableLocales.Locales
            .Find(x => x.Identifier.Code == code);

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            ApplyFontSettings(code);
        }
    }

    public void ApplyFontSettings(string code)
    {
        TMP_FontAsset font = englishFont;

        TMP_Text[] allTexts = FindObjectsOfType<TMP_Text>(true);
        foreach (var text in allTexts)
        {
            var control = text.GetComponent<LocalizedFontControl>();
            if (control != null && control.excludeFromFontChange)
                continue;

            if(font!=null)text.font = font;
        }

        var fontControls = FindObjectsOfType<LocalizedFontControl>(true);
        foreach (var control in fontControls)
        {
            control.ApplyFontSize();
        }
    }



    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFontSettings("en");
    }
    private void OnDestroy(){SceneManager.sceneLoaded-=OnSceneLoaded;}
}
