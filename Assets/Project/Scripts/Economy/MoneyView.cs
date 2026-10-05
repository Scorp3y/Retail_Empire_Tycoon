using UnityEngine;
using TMPro;
using RetailEmpireTycoon.Economy;

namespace RetailEmpireTycoon.UI
{
    [DisallowMultipleComponent]
    public sealed class MoneyView : MonoBehaviour
    {
        [SerializeField] private MoneyController moneyController;
        [SerializeField] private TMP_Text moneyText;

        private void OnEnable()
        {
            if (moneyController == null)
                return;

            moneyController.Changed += OnMoneyChanged;
            OnMoneyChanged(moneyController.Money);
        }

        private void OnDisable()
        {
            if (moneyController == null)
                return;

            moneyController.Changed -= OnMoneyChanged;
        }

        private void OnMoneyChanged(int amount)
        {
            if (moneyText != null)
                moneyText.text = MoneyFormat.Compact(amount);
        }
    }
}
