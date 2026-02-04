using System.Collections.Generic;
using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.UI.Shop
{
    public sealed class ShopTab_Build : MonoBehaviour
    {
        public BuildCategory categoryFilter = BuildCategory.Shelf;

        public Transform listRoot;
        public ShopItemCard cardPrefab;

        private readonly List<ShopItemCard> _cards = new List<ShopItemCard>();

        public void Bind(List<BuildItemData> items)
        {
            Clear();

            if (items == null)
                return;

            foreach (var it in items)
            {
                if (it == null)
                    continue;

                if (it.category != categoryFilter)
                    continue;

                var card = Instantiate(cardPrefab, listRoot);
                card.Bind(it);
                _cards.Add(card);
            }
        }

        private void Clear()
        {
            foreach (var c in _cards)
            {
                if (c != null)
                    Destroy(c.gameObject);
            }
            _cards.Clear();
        }
    }
}
