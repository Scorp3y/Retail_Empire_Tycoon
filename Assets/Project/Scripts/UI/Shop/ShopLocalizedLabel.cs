using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Fixed copy follows the existing language selector, while keeping theme typography.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ShopLocalizedLabel : MonoBehaviour
    {
        [SerializeField] private string russian;
        [SerializeField] private string english;
        private TMP_Text _label;
        public void Set(string ru, string en) { russian = ru; english = en; Refresh(); }
        private void OnEnable() { UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += OnLocale; Refresh(); }
        private void OnDisable() { LocalizationSettings.SelectedLocaleChanged -= OnLocale; }
        private void OnLocale(UnityEngine.Localization.Locale locale) { Refresh(); }
        private void Refresh()
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            _label.text = ShopText.Get(russian, english);
        }
    }
}
