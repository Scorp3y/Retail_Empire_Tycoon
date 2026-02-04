using System.Collections.Generic;
using UnityEngine;

namespace MyShopGame.Territory
{
    [DisallowMultipleComponent]
    public sealed class TerritoryManager : MonoBehaviour, ITerritoryQuery
    {
        [System.Serializable]
        public class PurchasedRect
        {
            public Vector3Int min;
            public Vector3Int max;
        }

        [Header("Debug purchased areas")]
        [SerializeField]
        private List<PurchasedRect> purchased = new List<PurchasedRect>();

        public bool IsCellPurchased(Vector3Int cell)
        {
            if (purchased == null || purchased.Count == 0)
                return true;

            foreach (var r in purchased)
            {
                if (r == null)
                    continue;

                var inside =
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
