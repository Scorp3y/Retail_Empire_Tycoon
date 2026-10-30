using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.Products
{
    [DisallowMultipleComponent]
    public sealed class ProductCatalog : MonoBehaviour
    {
        [SerializeField] private List<ProductItemData> products = new List<ProductItemData>();

        private readonly Dictionary<string, ProductItemData> _byId = new Dictionary<string, ProductItemData>();

        public IReadOnlyList<ProductItemData> Products => products;

        private void Awake()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Важно: НЕ удаляем null элементы.
            // Иначе Unity не даёт увеличить Size списка через Inspector.
        }
#endif

        public ProductItemData GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (_byId.Count == 0)
                RebuildCache();

            return _byId.TryGetValue(id, out ProductItemData product) ? product : null;
        }

        public void RebuildCache()
        {
            StoreAssortmentPack.Load()?.IncludeProducts(products);
            _byId.Clear();

            foreach (ProductItemData product in products)
            {
                if (product == null)
                    continue;

                if (string.IsNullOrWhiteSpace(product.Id))
                {
                    Debug.LogWarning("[ProductCatalog] Product has empty Id: " + product.name, this);
                    continue;
                }

                if (_byId.ContainsKey(product.Id))
                {
                    Debug.LogWarning("[ProductCatalog] Duplicate product Id: " + product.Id, this);
                    continue;
                }

                _byId.Add(product.Id, product);
            }
        }
    }
}
