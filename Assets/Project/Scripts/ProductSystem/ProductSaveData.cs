using System;
using UnityEngine;

namespace RetailEmpireTycoon.SaveSystem
{
    [Serializable]
    public sealed class ProductInventorySaveEntry
    {
        public string productId;
        public int count;
    }

    [Serializable]
    public sealed class ShelfStockSaveEntry
    {
        public string sceneShelfId;
        public string buildItemId;

        public int anchorX;
        public int anchorY;
        public int anchorZ;

        public bool rotated;
        public int facing;

        public string productId;
        public int amount;

        public Vector3Int AnchorCell => new Vector3Int(anchorX, anchorY, anchorZ);
    }
}
