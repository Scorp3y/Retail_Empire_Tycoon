using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConfirmPurchaseUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private Button _yes;
    [SerializeField] private Button _no;
    [SerializeField] private TMPro.TextMeshProUGUI _title;

    private Action _onYes;
    private Action _onNo;

    private void Awake()
    {
        HideInstant();

        _yes.onClick.AddListener(() => _onYes?.Invoke());
        _no.onClick.AddListener(() => _onNo?.Invoke());
    }

    public void Show(string message, Action onYes, Action onNo)
    {
        _onYes = onYes;
        _onNo = onNo;

        if (_title != null) _title.text = message;

        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        _cg.interactable = true;
    }

    public void HideInstant()
    {
        _cg.alpha = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable = false;

        _onYes = null;
        _onNo = null;
    }
}
