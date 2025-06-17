using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public class LoadingManager : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI loadingText, infoText;
    public float fakeFillSpeed = 0.05f;

    private float targetProgress = 0f;
    private List<string> factKeys = new List<string> {
        "fact_1", "fact_2", "fact_3", "fact_4", "fact_5", "fact_6"
    };

    void Start()
    {
        var langCode = PlayerPrefs.GetString("lang", "en");
        var selector = FindObjectOfType<LanguageSelector>();
        if (selector != null)
        {
            selector.ApplyFontSettings(langCode);
        }

        RandomFact();
        StartCoroutine(LoadText());
        StartCoroutine(LoadAsyncScene("Game"));
    }

    IEnumerator LoadAsyncScene(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            targetProgress = Mathf.Clamp01(operation.progress / 0.9f);

            while (loadingBar.value < targetProgress)
            {
                loadingBar.value += Time.deltaTime * fakeFillSpeed;
                yield return null;
            }

            if (operation.progress >= 0.9f && loadingBar.value >= 1f)
            {
                yield return new WaitForSeconds(0.5f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    IEnumerator LoadText()
    {
        int dotCount = 0;
        while (true)
        {
            var localizedString = new LocalizedString("UI_Texts", "loading_base");
            var handle = localizedString.GetLocalizedStringAsync();
            yield return handle;
            loadingText.text = handle.Result + new string('.', dotCount);
            dotCount = (dotCount + 1) % 4;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void RandomFact()
    {
        int index = Random.Range(0, factKeys.Count);
        var factKey = factKeys[index];

        var localizedFact = new LocalizedString("UI_Texts", factKey);
        localizedFact.StringChanged += (value) => infoText.text = value;
        localizedFact.RefreshString();
    }
}
