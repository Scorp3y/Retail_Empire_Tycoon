using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StoreLevelManager : MonoBehaviour
{
    [Serializable]
    public struct LevelEntry
    {
        public StoreLevelId LevelId;
        public GameObject Root;
    }

    [SerializeField] private List<LevelEntry> _levels = new();
    private readonly Dictionary<StoreLevelId, GameObject> _map = new();

    private void Awake()
    {
        _map.Clear();
        foreach (var e in _levels)
        {
            if (e.Root == null) continue;
            _map[e.LevelId] = e.Root;
        }
    }

    public void SwitchTo(StoreLevelId id)
    {
        foreach (var kv in _map)
        {
            if (kv.Value != null)
                kv.Value.SetActive(kv.Key == id);
        }
    }
}
