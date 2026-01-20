using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StorePrefabSpawner : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public StoreLevelId level;
        public GameObject prefab;
    }

    [SerializeField] private Transform _spawnParent;
    [SerializeField] private List<Entry> _prefabs = new();

    private readonly Dictionary<StoreLevelId, GameObject> _map = new();
    private GameObject _currentInstance;

    private void Awake()
    {
        if (_spawnParent == null) _spawnParent = transform;

        _map.Clear();
        foreach (var e in _prefabs)
        {
            if (e.prefab != null)
                _map[e.level] = e.prefab;
        }
    }

    public void Spawn(StoreLevelId level)
    {
        if (_currentInstance != null)
            Destroy(_currentInstance);

        if (!_map.TryGetValue(level, out var prefab) || prefab == null)
        {
            Debug.LogError($"No prefab assigned for level {level}", this);
            return;
        }

        _currentInstance = Instantiate(prefab, _spawnParent);
        _currentInstance.transform.localPosition = Vector3.zero;
        _currentInstance.transform.localRotation = Quaternion.identity;
        _currentInstance.transform.localScale = Vector3.one;
    }
}
