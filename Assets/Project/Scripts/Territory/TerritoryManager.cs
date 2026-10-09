using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.Territory
{
    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.Territory", null, "TerritoryManager")]
    public sealed class TerritoryManager : MonoBehaviour, ITerritoryQuery
    {
        [System.Serializable]
        [MovedFrom(false, "MyShopGame.Territory", null, "PurchasedRect")]
        public class PurchasedRect
        {
            public Vector3Int min;
            public Vector3Int max;
        }

        [Header("Debug purchased areas")]
        [SerializeField]
        private List<PurchasedRect> purchased = new List<PurchasedRect>();

        public IReadOnlyList<PurchasedRect> PurchasedRects => purchased;
        public TerritoryPlotLayout plotLayout;

        public void ClearPurchased()
        {
            purchased.Clear();
        }

        public bool IsCellPurchased(Vector3Int cell)
        {
            if (purchased == null || purchased.Count == 0)
                return false;

            foreach (var r in purchased)
            {
                if (r == null)
                    continue;

                bool inside =
                    cell.x >= r.min.x && cell.x <= r.max.x &&
                    cell.z >= r.min.z && cell.z <= r.max.z;

                if (inside)
                    return true;
            }

            return false;
        }

        public void AddPurchasedRect(Vector3Int min, Vector3Int max)
        {
            purchased ??= new List<PurchasedRect>();
            purchased.Add(new PurchasedRect { min = min, max = max });
        }
    }
}
