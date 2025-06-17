using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; 

    public int playerMoney = 6000;
    public TextMeshProUGUI moneyText;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        UpdateMoneyUI();
    }

    public bool SpendMoney(int amount)
    {
        if (playerMoney >= amount)
        {
            playerMoney -= amount;
            UpdateMoneyUI();
            return true;
        }
        return false;
    }

    public void AddMoney(int amount)
    {
        playerMoney += amount;
        UpdateMoneyUI();
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = "$" + playerMoney;
    }
}
