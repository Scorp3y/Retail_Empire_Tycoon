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

    private GameObject currentInstance;

    public void Spawn(StoreLevelId level)
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }

        var entry = prefabs.Find(p => p.level == level);
        if (entry == null || entry.prefab == null)
            return;

        currentInstance = Instantiate(entry.prefab, root);
    }
}
