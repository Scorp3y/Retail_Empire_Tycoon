using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Core
{
    /// <summary>
    /// Additional saleable goods and fixtures shared by shopping, storage and save lookup.
    /// Existing scene catalogues remain authoritative for their IDs. Loading a pack never grants stock.
    /// </summary>
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Store Assortment Pack")]
    public sealed class StoreAssortmentPack : ScriptableObject
    {
        public const string ResourcePath = "StoreAssortment/Expansion";
        [SerializeField] private ProductItemData[] products = System.Array.Empty<ProductItemData>();
        [SerializeField] private BuildItemData[] fixtures = System.Array.Empty<BuildItemData>();

        public IReadOnlyList<ProductItemData> Products => products;
        public IReadOnlyList<BuildItemData> Fixtures => fixtures;
        public static StoreAssortmentPack Load() => Resources.Load<StoreAssortmentPack>(ResourcePath);

        public void IncludeProducts(List<ProductItemData> catalogue)
        {
            foreach (var product in products)
                if (product != null && !catalogue.Exists(existing => existing != null && existing.Id == product.Id))
                    catalogue.Add(product);
        }

        public void IncludeFixtures(List<BuildItemData> catalogue)
        {
            foreach (var fixture in fixtures)
                if (fixture != null && !catalogue.Exists(existing => existing != null && existing.id == fixture.id))
                    catalogue.Add(fixture);
        }
    }
}
