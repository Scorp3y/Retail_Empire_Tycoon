using System;
using UnityEngine;

namespace MyShopGame.Economy
{
    [DisallowMultipleComponent]
    public sealed class MoneyController : MonoBehaviour
    {
        [SerializeField]
        private int money = 1000;

        public event Action<int> Changed;

        public int Money => money;

        public bool CanSpend(int amount) => amount >= 0 && money >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
                return false;

            money -= amount;
            Changed?.Invoke(money);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            money += amount;
            Changed?.Invoke(money);
        }
    }
}
