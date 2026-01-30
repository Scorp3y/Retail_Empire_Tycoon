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
    }
    
    public void SetInstant(float alpha, bool blockRaycasts = true)
    {
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);
        if (_cg == null) return;

        if (!_cg.gameObject.activeInHierarchy)
            _cg.gameObject.SetActive(true);

        _cg.alpha = Mathf.Clamp01(alpha);
        _cg.blocksRaycasts = blockRaycasts && _cg.alpha > 0.001f;
        _cg.interactable = false;
    }


    public IEnumerator FadeOut()
    {
        yield return FadeTo(1f);
    }
    public IEnumerator FadeIn()
    {
        yield return FadeTo(0f);
    }

    private IEnumerator FadeTo(float target)
    {
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);
        if (_cg == null) yield break;

        if (!_cg.gameObject.activeInHierarchy)
            _cg.gameObject.SetActive(true);

        _cg.blocksRaycasts = true;
        _cg.interactable = false;
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

        if (Mathf.Approximately(target, 0f))
            _cg.blocksRaycasts = false;
    }
}
