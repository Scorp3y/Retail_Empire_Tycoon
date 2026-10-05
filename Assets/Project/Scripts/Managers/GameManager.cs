using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private RetailEmpireTycoon.Economy.MoneyController wallet;
    public static GameManager Instance;

    public int playerMoney = 6000;
    public TextMeshProUGUI moneyText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (moneyText == null)
            moneyText = GameObject.Find("MoneyText")?.GetComponent<TextMeshProUGUI>();

        RefreshWallet();
    }

    private void OnEnable()
    {
        if (wallet != null) wallet.Changed += OnWalletChanged;
        RefreshWallet();
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= OnWalletChanged;
    }

    private void RefreshWallet()
    {
        if (wallet != null) playerMoney = wallet.Money;
        UpdateMoneyUI();
    }

    private void OnWalletChanged(int amount)
    {
        playerMoney = amount;
        UpdateMoneyUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool SpendMoney(int amount)
    {
        if (wallet != null) return wallet.TrySpend(amount);
        if (amount < 0) return false;
        if (playerMoney < amount) return false;

        playerMoney -= amount;
        UpdateMoneyUI();
        return true;
    }

    public void AddMoney(int amount)
    {
        if (wallet != null) { wallet.Add(amount); return; }
        if (amount <= 0) return;
        playerMoney += amount;
        UpdateMoneyUI();
    }

    public void ForceRefreshUI() => RefreshWallet();

    private void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = RetailEmpireTycoon.Economy.MoneyFormat.Compact(wallet != null ? wallet.Money : playerMoney);
    }
}
