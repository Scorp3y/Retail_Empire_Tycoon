using System.Collections.Generic;
using UnityEngine;
using MyShopGame.BuildSystem;

namespace MyShopGame.UI.Windows
{
    public sealed class BuildInventoryWindow : MonoBehaviour
    {
        [Header("Refs")]
        public BuildInventory inventory; 
        public BuildController buildController;

        [Header("UI")]
        public Transform listRoot;
        public BuildInventoryItemRow rowPrefab;

        private readonly List<BuildInventoryItemRow> _rows = new();

        private void OnEnable()
        {
            if (inventory != null)
                inventory.Changed += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.Changed -= Refresh; 
        }

        public void Refresh()
        {
            Clear();
            if (inventory == null) return;
            if (rowPrefab == null) return;
            if (listRoot == null) return;

            foreach (var e in inventory.Entries)
            {
                if (e == null) continue;
                if (e.item == null) continue;
                if (e.count <= 0) continue;

                var row = Instantiate(rowPrefab, listRoot);
                row.Bind(e.item, e.count, buildController);
                _rows.Add(row);
            }
        }

        private void Clear()
        {
            foreach (var r in _rows)
                if (r != null) Destroy(r.gameObject);

            _rows.Clear();
        }
    }
}
