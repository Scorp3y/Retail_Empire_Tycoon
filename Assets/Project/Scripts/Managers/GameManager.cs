using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
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

        UpdateMoneyUI();
    }

    public bool SpendMoney(int amount)
    {
        if (playerMoney < amount) return false;

        playerMoney -= amount;
        UpdateMoneyUI();
        return true;
    }

    public void AddMoney(int amount)
    {
        playerMoney += amount;
        UpdateMoneyUI();
    }

    public void ForceRefreshUI() => UpdateMoneyUI();

    private void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = "$" + playerMoney;
    }
}
