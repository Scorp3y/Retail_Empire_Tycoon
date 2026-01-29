using UnityEngine;

public sealed class StoreBootstrapper : MonoBehaviour
{
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private StorePrefabSpawner _spawner;

    private void Awake()
    {
        if (_progression == null) _progression = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        if (_spawner == null) _spawner = FindObjectOfType<StorePrefabSpawner>(true);
    }

    private void Start()
    {
        if (_progression == null || _spawner == null) return;
        _spawner.Spawn(_progression.State.CurrentLevel);
    }
}
