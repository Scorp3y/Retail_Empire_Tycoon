using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace RetailEmpireTycoon.Economy
{
    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.Economy", null, "MoneyController")]
    public sealed class MoneyController : MonoBehaviour
    {
        [SerializeField] private int money = 6000;

        public event Action<int> Changed;

        public int Money => money;

        private void Start()
        {
            Changed?.Invoke(money);
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && money >= amount;
        }

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

            money = (int)Math.Min(int.MaxValue, (long)money + amount);
            Changed?.Invoke(money);
        }

        public void SetMoney(int value)
        {
            money = Mathf.Max(0, value);
            Changed?.Invoke(money);
        }
    }
}
