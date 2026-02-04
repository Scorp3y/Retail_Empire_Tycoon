using UnityEngine;

namespace MyShopGame.Territory
{
    public interface ITerritoryQuery
    {
        bool IsCellPurchased(Vector3Int cell);
    }
}
