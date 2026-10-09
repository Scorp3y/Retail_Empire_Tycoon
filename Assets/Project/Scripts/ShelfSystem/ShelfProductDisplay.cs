using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class ShelfProductDisplay : MonoBehaviour
    {
        public enum FillMode
        {
            ExactAmount,
            CapacityPercent
        }

        [Header("Refs")]
        [SerializeField] private PlacedShelfStock shelfStock;

        [Header("Slots")]
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private List<Transform> slots = new List<Transform>();

        [Header("Fill Mode")]
        [SerializeField] private FillMode fillMode = FillMode.ExactAmount;

        [Header("Spawn Settings")]
        [SerializeField] private bool clearSlotBeforeSpawn = true;
        [SerializeField] private bool disableSpawnedColliders = true;
        [SerializeField] private bool useSlotRotation = true;
        [SerializeField, Min(0.01f)] private float productScale = 1f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        public float ProductScale => productScale;
        public Bounds WorkAreaBounds
        {
            get
            {
                CollectSlotsIfNeeded();
                var bounds = new Bounds(slots.Count > 0 ? slots[0].position : transform.position, Vector3.zero);
                foreach (var slot in slots) if (slot != null) bounds.Encapsulate(slot.position);
                return bounds;
            }
        }
        public List<Transform> GetWorkSlots(int count)
        {
            CollectSlotsIfNeeded();
            var result = new List<Transform>();
            if (slots.Count == 0) return result;
            int start = shelfStock != null ? CalculateVisibleCount(shelfStock.CurrentAmount, shelfStock.MaxAmount, slots.Count) : 0;
            int remaining = Mathf.Max(1, slots.Count - start);
            for (int i = 0; i < count; i++)
                result.Add(slots[Mathf.Min(slots.Count - 1, start + i % remaining)]);
            return result;
        }

        private void Awake()
        {
            FindMissingRefs();
            CollectSlotsIfNeeded();
        }

        private void OnEnable()
        {
            if (shelfStock != null)
                shelfStock.Changed += HandleShelfChanged;

            Refresh();
        }

        private void OnDisable()
        {
            if (shelfStock != null)
                shelfStock.Changed -= HandleShelfChanged;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (shelfStock == null)
                shelfStock = GetComponent<PlacedShelfStock>();

            productScale = Mathf.Max(0.01f, productScale);
        }
#endif

        public void Refresh()
        {
            ClearSpawnedObjects();

            if (shelfStock == null)
            {
                LogWarning("Shelf Stock is missing.");
                return;
            }

            ProductItemData product = shelfStock.CurrentProduct;

            if (product == null || shelfStock.CurrentAmount <= 0)
                return;

            if (product.ShelfDisplayPrefab == null)
            {
                LogWarning("Product has no Shelf Display Prefab: " + product.DisplayName);
                return;
            }

            CollectSlotsIfNeeded();

            if (slots.Count == 0)
            {
                LogWarning("No slots assigned.");
                return;
            }

            int visibleCount = CalculateVisibleCount(
                shelfStock.CurrentAmount,
                shelfStock.MaxAmount,
                slots.Count
            );

            for (int i = 0; i < visibleCount; i++)
                SpawnProduct(product, slots[i]);
        }

        private int CalculateVisibleCount(int amount, int maxAmount, int slotCount)
        {
            if (amount <= 0 || maxAmount <= 0 || slotCount <= 0)
                return 0;

            if (fillMode == FillMode.ExactAmount)
                return Mathf.Clamp(amount, 0, slotCount);

            float fillPercent = Mathf.Clamp01((float)amount / maxAmount);
            int count = Mathf.CeilToInt(slotCount * fillPercent);

            if (amount > 0)
                count = Mathf.Max(1, count);

            return Mathf.Clamp(count, 0, slotCount);
        }

        private void SpawnProduct(ProductItemData product, Transform slot)
        {
            if (product == null || product.ShelfDisplayPrefab == null || slot == null)
                return;

            if (clearSlotBeforeSpawn)
                ClearChildren(slot);

            GameObject instance = CreateWorkProduct(product, slot);

            _spawnedObjects.Add(instance);
        }

        /// <summary>Creates a preview with the real slot's local pose and inherited scale. The caller owns its lifetime.</summary>
        public GameObject CreateWorkProduct(ProductItemData product, Transform slot)
        {
            if (product == null || product.ShelfDisplayPrefab == null || slot == null) return null;
            GameObject instance = Instantiate(product.ShelfDisplayPrefab, slot);

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one * productScale;

            if (useSlotRotation)
                instance.transform.localRotation = Quaternion.identity;

            slot.GetComponent<ShelfProductSlot>()?.Seat(instance);

            if (disableSpawnedColliders)
                DisableColliders(instance);

            return instance;
        }

        private void CollectSlotsIfNeeded()
        {
            if (slots.Count > 0)
                return;

            if (slotsRoot == null)
                return;

            slots.Clear();

            for (int i = 0; i < slotsRoot.childCount; i++)
            {
                Transform child = slotsRoot.GetChild(i);

                if (child != null)
                    slots.Add(child);
            }
        }

        private void ClearSpawnedObjects()
        {
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (_spawnedObjects[i] != null)
                    Destroy(_spawnedObjects[i]);
            }

            _spawnedObjects.Clear();

            if (!clearSlotBeforeSpawn)
                return;

            foreach (Transform slot in slots)
                ClearChildren(slot);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void DisableColliders(GameObject root)
        {
            if (root == null)
                return;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
                collider.enabled = false;
        }

        private void HandleShelfChanged(PlacedShelfStock changedShelf)
        {
            Refresh();
        }

        private void FindMissingRefs()
        {
            if (shelfStock == null)
                shelfStock = GetComponent<PlacedShelfStock>();
        }

        private void LogWarning(string message)
        {
            if (!debugLogs)
                return;

            Debug.LogWarning("[ShelfProductDisplay] " + message, this);
        }
    }
}
