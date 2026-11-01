using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Shopping taxonomy only. Refrigeration and supplier routing remain storage rules.</summary>
    public sealed class ProductDepartments
    {
        [Serializable] public sealed class Entry { public string id, department, section; }
        [Serializable] private sealed class Document { public Entry[] products; }
        private readonly Dictionary<string, Entry> entries;
        public ProductDepartments()
        {
            var source = Resources.Load<TextAsset>("ShopUi/ProductDepartments");
            if (source == null) throw new InvalidOperationException("Product department definitions are missing.");
            entries = JsonUtility.FromJson<Document>(source.text).products.ToDictionary(e => e.id);
        }
        public Entry For(ProductItemData product)
        {
            if (entries.TryGetValue(product.Id, out var entry)) return entry;
            return new Entry { id=product.Id, department=product.StorageType.ToString(), section="Other" };
        }
    }
}
