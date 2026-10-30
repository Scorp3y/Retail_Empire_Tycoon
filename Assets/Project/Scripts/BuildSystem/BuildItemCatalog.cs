using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class BuildItemCatalog : MonoBehaviour
    {
        [SerializeField] private List<BuildItemData> items = new();
        public IReadOnlyList<BuildItemData> Items => items;
        private void Awake()
        {
            StoreAssortmentPack.Load()?.IncludeFixtures(items);
            foreach(var item in Resources.LoadAll<BuildItemData>("MultiStorey"))
                if(!items.Exists(existing=>existing!=null&&existing.id==item.id))items.Add(item);
        }

        public BuildItemData GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            // Save restoration may ask for an item before this component's Awake.
            StoreAssortmentPack.Load()?.IncludeFixtures(items);

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.id == id)
                    return item;
            }

            var extension=Resources.Load<BuildItemData>("MultiStorey/"+id);
            if(extension!=null)return extension;
            Debug.LogWarning("[BuildItemCatalog] Item not found: " + id);
            return null;
        }
    }
}
