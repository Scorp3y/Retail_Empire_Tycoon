using System;
using System.Collections.Generic;
using UnityEngine;

public class StorePrefabSpawner : MonoBehaviour
{
    [Serializable]
    public class StorePrefabEntry
    {
        public StoreLevelId level;
        public GameObject prefab;
    }

    [SerializeField] private Transform root;
    [SerializeField] private List<StorePrefabEntry> prefabs = new();
    [SerializeField] private List<StorePrefabEntry> modularPrefabs = new();
    public bool UsesModularStore { get; set; }

    private GameObject currentInstance;

    public void Spawn(StoreLevelId level)
    {
        if (currentInstance != null)
        {
            currentInstance.SetActive(false);
            Destroy(currentInstance);
            currentInstance = null;
        }

        var entries = UsesModularStore ? modularPrefabs : prefabs;
        var entry = entries.Find(p => p.level == level);
        if (entry == null || entry.prefab == null)
            throw new InvalidOperationException("Missing store landscape prefab for " + level);

        currentInstance = Instantiate(entry.prefab, root);
    }
}
