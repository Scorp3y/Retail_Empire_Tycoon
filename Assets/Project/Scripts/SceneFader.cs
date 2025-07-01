using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 2f;

    void Awake()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color color = fadeImage.color;
            fadeImage.color = new Color(color.r, color.g, color.b, 1f); 
        }
    }

    void Start()
    {
        if (fadeImage != null)
            StartCoroutine(FadeFromBlack());
    }

    IEnumerator FadeFromBlack()
    {
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeImage.color = new Color(color.r, color.g, color.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }

        fadeImage.color = new Color(color.r, color.g, color.b, 0f);
        fadeImage.gameObject.SetActive(false); 
    }
}
