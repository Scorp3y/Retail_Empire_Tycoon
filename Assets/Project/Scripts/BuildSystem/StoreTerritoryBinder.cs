using UnityEngine;
using RetailEmpireTycoon.BuildSystem;

namespace RetailEmpireTycoon.Territory
{
    public sealed class StoreTerritoryBinder : MonoBehaviour
    {
        public TerritoryManager territoryManager;
        public GridSystem grid;
        public StoreBuildArea storeBuildArea;

        private void Awake()
        {
            if (territoryManager == null)
                territoryManager = FindObjectOfType<TerritoryManager>();

            if (grid == null)
                grid = FindObjectOfType<GridSystem>();

            if (storeBuildArea == null)
                storeBuildArea = GetComponentInChildren<StoreBuildArea>(true);

            Apply();
        }

        public void Apply()
        {
            if (territoryManager != null && territoryManager.plotLayout != null)
            {
                territoryManager.plotLayout.Apply();
                return;
            }
            if (territoryManager == null || grid == null || storeBuildArea == null)
                return;

            territoryManager.ClearPurchased();

            if (storeBuildArea.areaRects == null)
                return;

            foreach (var box in storeBuildArea.areaRects)
            {
                if (box == null)
                    continue;

                Bounds b = box.bounds;

                Vector3 minWorld = b.min + new Vector3(0.001f, 0f, 0.001f);
                Vector3 maxWorld = b.max - new Vector3(0.001f, 0f, 0.001f);

                Vector3Int minCell = grid.WorldToCell(minWorld);
                Vector3Int maxCell = grid.WorldToCell(maxWorld);

                if (minCell.x > maxCell.x)
                {
                    int t = minCell.x;
                    minCell.x = maxCell.x;
                    maxCell.x = t;
                }

                if (minCell.z > maxCell.z)
                {
                    int t = minCell.z;
                    minCell.z = maxCell.z;
                    maxCell.z = t;
                }

                minCell.y = 0;
                maxCell.y = 0;

                territoryManager.AddPurchasedRect(minCell, maxCell);
            }
        }
    }
}
