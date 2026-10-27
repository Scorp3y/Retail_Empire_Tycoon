using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>The approved visual customer variants. Trading and staffing do not depend on this asset.</summary>
    [CreateAssetMenu(menuName="Retail Empire Tycoon/Shoppers/Roster")]
    public sealed class ShopperRoster : ScriptableObject
    {
        [SerializeField] private GameObject[] variants = Array.Empty<GameObject>();
        public IReadOnlyList<GameObject> Variants => Array.AsReadOnly(variants);
        public ShopperSelection CreateSelection(int seed) => new ShopperSelection(variants, seed);
    }

    /// <summary>Uses every appearance once before reshuffling, without consuming gameplay's random stream.</summary>
    public sealed class ShopperSelection
    {
        private readonly GameObject[] variants;
        private readonly int[] order;
        private readonly System.Random random;
        private int cursor;
        private int previous = -1;
        public int Count => variants.Length;

        public ShopperSelection(IReadOnlyList<GameObject> source, int seed)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("At least one shopper appearance is required.", nameof(source));

            variants = new GameObject[source.Count];
            order = new int[source.Count];
            random = new System.Random(seed);
            var distinct = new HashSet<GameObject>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null || !distinct.Add(source[i]))
                    throw new ArgumentException("Shopper appearances must be non-null and distinct.", nameof(source));
                variants[i] = source[i];
                order[i] = i;
            }
            cursor = order.Length;
        }

        public GameObject Next()
        {
            if (cursor == order.Length)
            {
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int index = random.Next(i + 1);
                    int value = order[i];
                    order[i] = order[index];
                    order[index] = value;
                }
                if (order.Length > 1 && order[0] == previous)
                {
                    int value = order[0];
                    order[0] = order[1];
                    order[1] = value;
                }
                cursor = 0;
            }
            previous = order[cursor++];
            return variants[previous];
        }
    }
}
