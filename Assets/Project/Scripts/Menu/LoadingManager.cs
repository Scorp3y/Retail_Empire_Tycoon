using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.UI.Shop;

public class LoadingManager : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI loadingText, infoText;
    public float fakeFillSpeed = 0.05f;

    private float targetProgress = 0f;
    private LocalizedString fact;
    private List<string> factKeys = new List<string> {
        "fact_1", "fact_2", "fact_3", "fact_4", "fact_5", "fact_6"
    };

    void Start()
    {
        // City/shop keep their camera listener; the additive loading scene must not create a second one.
        var journey = FindObjectOfType<RetailEmpireTycoon.City.CityTrip>(true);
        if(journey!=null && journey.Transitioning)
        {
            foreach(var listener in gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AudioListener>(true))) listener.enabled=false;
            foreach(var events in gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))) events.enabled=false;
        }
        foreach(var canvas in gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)))
        { canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=1000; }
        var theme=Resources.Load<ShopUiTheme>("ShopUi/Theme");
        foreach(var text in new[]{loadingText,infoText}) if(text!=null && theme!=null)
        {
            text.font=theme.regularFont;
            var control=text.GetComponent<LocalizedFontControl>()??text.gameObject.AddComponent<LocalizedFontControl>();control.excludeFromFontChange=true;
        }
        if(loadingBar!=null)
        {
            loadingBar.interactable=false;
            if(loadingBar.fillRect!=null) {var image=loadingBar.fillRect.GetComponent<Image>();if(image!=null) {image.sprite=null;image.color=ShopUiTheme.Green;}}
        }
        var langCode = PlayerPrefs.GetString("lang", "en");
        var selector = FindObjectOfType<LanguageSelector>();
        if (selector != null)
        {
            selector.ApplyFontSettings(langCode);
        }

        RandomFact();
        StartCoroutine(LoadText());
        // A city trip owns additive loading; boot/menu loading still opens Game normally.
        if (journey == null || !journey.Transitioning) StartCoroutine(LoadAsyncScene("Game"));
    }

    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
        if (loadingBar != null) loadingBar.value = targetProgress;
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
                loadingBar.value = Mathf.MoveTowards(loadingBar.value, targetProgress, Time.unscaledDeltaTime * 1.5f);
                yield return null;
            }

            if (operation.progress >= 0.9f && loadingBar.value >= 1f)
            {
                yield return new WaitForSecondsRealtime(0.3f);
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
            loadingText.text = handle.Result + new string('.', dotCount) + $" {Mathf.RoundToInt(targetProgress*100)}%";
            dotCount = (dotCount + 1) % 4;
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    void RandomFact()
    {
        int index = Random.Range(0, factKeys.Count);
        var factKey = factKeys[index];

        fact = new LocalizedString("UI_Texts", factKey);
        fact.StringChanged += UpdateFact;
        fact.RefreshString();
    }
    private void UpdateFact(string value) {if(infoText!=null) infoText.text=value;}
    private void OnDestroy() {if(fact!=null) fact.StringChanged-=UpdateFact;}
}
