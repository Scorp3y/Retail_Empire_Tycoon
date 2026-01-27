using System.Collections;
using UnityEngine;

public sealed class TerritoryPurchaseController : MonoBehaviour
{
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private StorePrefabSpawner _prefabSpawner;
    [SerializeField] private ScreenFader _fader;
    [SerializeField] private ConfirmPurchaseUI _confirm; 

    private void Awake()
    {
        if (_progression == null) _progression = FindObjectOfType<StoreProgression>();
        if (_prefabSpawner == null) _prefabSpawner = FindObjectOfType<StorePrefabSpawner>();
    }

    public void RequestPurchase(TerritoryId id, int price)
    {
        Debug.Log($"[PurchaseController] RequestPurchase {id} {price}");

        if (_confirm == null)
        {
            Debug.LogError("[PurchaseController] _confirm is NULL (не назначен ConfirmPurchaseUI в инспекторе!)");
            return;
        }

        _confirm.Show(
            $"Уверены ли вы купить эту территорию за {price}?",
            onYes: () => StartCoroutine(PurchaseRoutine(id, price)),
            onNo: () => _confirm.HideInstant()
        );


    }



    private IEnumerator PurchaseRoutine(TerritoryId id, int price)
    {
        _confirm.HideInstant();

        if (GameManager.Instance == null || !GameManager.Instance.SpendMoney(price))
            yield break;

        if (_fader != null)
            yield return _fader.FadeOut();

        _progression.MarkPurchased(id);

        if (_prefabSpawner != null)
            _prefabSpawner.Spawn(_progression.State.CurrentLevel);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();

        if (_fader != null)
            yield return _fader.FadeIn();

        FindObjectOfType<TerritoryPurchaseModeManager>(true)?.Exit();

    }

}
