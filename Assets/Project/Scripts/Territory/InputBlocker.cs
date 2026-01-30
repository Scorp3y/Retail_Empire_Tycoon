using UnityEngine;

public sealed class InputBlocker : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cg;

    private void Awake()
    {
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
        SetBlocked(false);
    }

    public void SetBlocked(bool blocked)
    {
        if (_cg == null) return;

        gameObject.SetActive(blocked);
        _cg.alpha = 0f;
        _cg.blocksRaycasts = blocked;
        _cg.interactable = blocked;
    }
}
