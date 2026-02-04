using System.Collections.Generic;
using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
    [DisallowMultipleComponent]
    public sealed class PlacedObject : MonoBehaviour
    {
        public BuildItemData item;

        [Header("Runtime")]
        public Vector3Int anchorCell;
        public bool rotated;
        public int facing;

        public List<Vector3Int> occupiedCells = new List<Vector3Int>();
    }
}
