using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class BuildItemCatalog : MonoBehaviour
    {
        [SerializeField] private List<BuildItemData> items = new();
        public IReadOnlyList<BuildItemData> Items => items;

        public BuildItemData GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.id == id)
                    return item;
            }

            Debug.LogWarning("[BuildItemCatalog] Item not found: " + id);
            return null;
        }
    }
}
