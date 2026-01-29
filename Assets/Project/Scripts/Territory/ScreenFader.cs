using System.Collections;
using UnityEngine;

public sealed class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private float _fadeTime = 0.25f;

    private Coroutine _r;

    private void Awake()
    {
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);
        if (_cg == null)
            Debug.LogError("[ScreenFader] CanvasGroup не найден. Добавь CanvasGroup на объект затемнения и назначь его в инспекторе.", this);
    }
    public IEnumerator FadeOut()
    {
        yield return FadeTo(1f);
    }
    public IEnumerator FadeIn()
    {
        yield return FadeTo(0f);
        FindObjectOfType<TerritoryPurchaseModeManager>()?.Exit();
    }



    private IEnumerator FadeTo(float target)
    {
        if (_cg == null) yield break;
        if (_r != null) StopCoroutine(_r);
        float start = _cg.alpha;

        float t = 0f;
        while (t < _fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / _fadeTime);
            _cg.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }
        _cg.alpha = target;
    }
}
