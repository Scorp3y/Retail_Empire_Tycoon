using UnityEngine;
using UnityEngine.Localization.Settings;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>English-only release policy. It does not overwrite a player's stored preferences.</summary>
    public static class GameLanguage
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LocalizationSettings.SelectedLocaleChanged-=EnforceEnglish;
            LocalizationSettings.SelectedLocaleChanged+=EnforceEnglish;
            var operation=LocalizationSettings.InitializationOperation;
            if(operation.IsDone)SelectEnglish();
            else operation.Completed+=_=>SelectEnglish();
        }
        private static void EnforceEnglish(UnityEngine.Localization.Locale locale)
        {if(locale!=null && locale.Identifier.Code!="en")SelectEnglish();}
        public static void SelectEnglish()
        {
            var locale=LocalizationSettings.AvailableLocales.Locales.Find(candidate=>candidate.Identifier.Code=="en");
            if(locale==null){Debug.LogError("English locale is missing from the game.");return;}
            LocalizationSettings.SelectedLocale=locale;
        }
    }
}
