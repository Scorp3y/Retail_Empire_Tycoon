using System.Collections;
using UnityEngine;

public sealed class TerritoryPurchaseController : MonoBehaviour
{
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private StorePrefabSpawner _prefabSpawner;
    [SerializeField] private ScreenFader _fader;
    [SerializeField] private ConfirmPurchaseUI _confirm;
    [SerializeField] private InputBlocker _inputBlocker;
    [SerializeField] private TerritoryRaycaster _raycaster;
    [SerializeField] private CameraModeController _cameraMode;
    private bool _busy;


    private void Awake()
    {
        if (_progression == null) _progression = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        if (_prefabSpawner == null) _prefabSpawner = FindObjectOfType<StorePrefabSpawner>(true);

        if (_fader == null) _fader = FindObjectOfType<ScreenFader>(true);
        if (_confirm == null) _confirm = FindObjectOfType<ConfirmPurchaseUI>(true);
    }

    public void RequestPurchase(TerritoryId id, int price)
    {

        _confirm.Show(
            $"Are you sure you will buy this territory? {price}?",
            onYes: () => StartCoroutine(PurchaseRoutine(id, price)),
            onNo: () => _confirm.HideInstant()
        );

    }

    private IEnumerator PurchaseRoutine(TerritoryId id, int price)
    {
        if (_busy)
            yield break;

        _busy = true;

        _confirm.HideInstant();

        if (_inputBlocker != null)
            _inputBlocker.SetBlocked(true);

        if (_raycaster != null)
            _raycaster.enabled = false;

        if (_cameraMode != null)
            _cameraMode.enabled = false;

        if (_fader != null)
        {
            _fader.SetInstant(1f);
            yield return null;
        }

        if (GameManager.Instance == null || !GameManager.Instance.SpendMoney(price))
        {
            if (_fader != null)
                yield return _fader.FadeIn();

            if (_cameraMode != null)
                _cameraMode.enabled = true;

            if (_raycaster != null)
                _raycaster.enabled = true;

            if (_inputBlocker != null)
                _inputBlocker.SetBlocked(false);

            _busy = false;
            yield break;
        }

        _progression.MarkPurchased(id);

        SaveManager.Instance?.SaveGame();

        if (_prefabSpawner != null)
            _prefabSpawner.Spawn(_progression.State.CurrentLevel);

        FindObjectOfType<TerritoryPurchaseModeManager>(true)?.Exit();

        if (_fader != null)
            yield return _fader.FadeIn();

        if (_cameraMode != null)
            _cameraMode.enabled = true;

        if (_raycaster != null)
            _raycaster.enabled = true;

        if (_inputBlocker != null)
            _inputBlocker.SetBlocked(false);

        _busy = false;
    }


}
