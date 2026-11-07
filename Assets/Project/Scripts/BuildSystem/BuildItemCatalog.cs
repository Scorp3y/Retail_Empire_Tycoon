using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.BuildSystem
{
    public sealed class BuildItemCatalog : MonoBehaviour
    {
        private const string WallCollectionPath = "WallCollection";
        [SerializeField] private List<BuildItemData> items = new();
        public IReadOnlyList<BuildItemData> Items => items;
        private void Awake()
        {
            StoreAssortmentPack.Load()?.IncludeFixtures(items);
            foreach(var item in Resources.LoadAll<BuildItemData>("MultiStorey"))
                if(!items.Exists(existing=>existing!=null&&existing.id==item.id))items.Add(item);
            foreach (var item in Resources.LoadAll<BuildItemData>("FrontageDecor"))
                if (!items.Exists(existing => existing != null && existing.id == item.id)) items.Add(item);
            IncludeWallCollection(items);
        }

        public static void IncludeWallCollection(List<BuildItemData> target)
        {
            foreach (var item in LoadWallCollection())
                if (!target.Exists(existing => existing != null && existing.id == item.id)) target.Add(item);
        }

        internal static BuildItemData[] LoadWallCollection() => Resources.LoadAll<BuildItemData>(WallCollectionPath);

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

            var extension=Resources.Load<BuildItemData>("MultiStorey/"+id) ?? Resources.Load<BuildItemData>("FrontageDecor/"+id)
                ?? Resources.Load<BuildItemData>(WallCollectionPath+"/"+id);
            if(extension!=null)return extension;
            Debug.LogWarning("[BuildItemCatalog] Item not found: " + id);
            return null;
        }
    }
}
