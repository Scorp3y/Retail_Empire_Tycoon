using UnityEngine;
using TMPro;

public class LocalizedFontControl : MonoBehaviour
{
    public float sizeEnglish = 60f;
    public float sizeRussian = 40f;

    public bool excludeFromFontChange = false;

    private TMP_Text text;

    void Start()
    {
        text = GetComponent<TMP_Text>();
        ApplyFontSize();
    }

    public void ApplyFontSize()
    {
        if (excludeFromFontChange || text == null) return;

        string lang = PlayerPrefs.GetString("lang", "en");
        float size = lang == "ru" ? sizeRussian : sizeEnglish;

        text.fontSize = size;
    }
}
