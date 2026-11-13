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
        public bool primaryPlayerParking;
        public int parkingModuleVersion;
        public DecorMount decorMount;
        public Vector3Int? decorEnd;
        public Vector3 decorScale = Vector3.one;
        public string SignText { get; private set; }
        private TMPro.TextMeshPro signLabel;

        internal void SetSignText(string text)
        {
            if (DecorSign.CharacterLimit(item) == 0) return;
            SignText = text == null ? null : DecorSign.LimitText(item, text);
            if (signLabel == null) signLabel = DecorSign.Create(transform, item, SignText);
            else signLabel.text = DecorSign.Caption(item, SignText);
        }

        public List<Vector3Int> occupiedCells = new List<Vector3Int>();

        internal bool IsPendingRemoval { get; private set; }

        internal void RemoveFromScene()
        {
            // Unity destroys objects at the end of the frame. The save may be collected earlier.
            IsPendingRemoval = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
