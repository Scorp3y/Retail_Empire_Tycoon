using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.BuildSystem
{
    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "PlacedObject")]
    public sealed class PlacedObject : MonoBehaviour
    {
        public BuildItemData item;

        [Header("Runtime")]
        public Vector3Int anchorCell;
        public bool rotated;
        public int facing;
        public int wallModuleVersion;
        public bool playerParking;

        public List<Vector3Int> occupiedCells = new List<Vector3Int>();
    }
}
