using System.Collections.Generic;
using UnityEngine;

public sealed class StorePrefabSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public StoreLevelId level;
        public GameObject prefab;
    }

    [SerializeField] private Transform _root;
    [SerializeField] private List<Entry> _prefabs;

    private GameObject _current;

    public void Spawn(StoreLevelId level)
    {
        if (_current != null)
            Destroy(_current);

        var entry = _prefabs.Find(e => e.level == level);
        if (entry.prefab == null)
        {
            Debug.LogError($"No prefab for level {level}");
            return;
        }

        _current = Instantiate(entry.prefab, _root);
    }
}
