using System;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ShopView
{
    [SerializeField] private GameObject shopPanel;

    [SerializeField] private Button coinTabButton;
    [SerializeField] private Button tokenTabButton;

    [SerializeField] private Image coinTabImage;
    [SerializeField] private Image tokenTabImage;

    public event Action<CurrencyType> OnTabChanged;

    public ShopView()
    {
        coinTabButton.onClick.AddListener(() =>
            OnTabChanged?.Invoke(CurrencyType.Coin));

        tokenTabButton.onClick.AddListener(() =>
            OnTabChanged?.Invoke(CurrencyType.Token));
    }

    public void SetTab(CurrencyType active)
    {
        coinTabImage.color =
            active == CurrencyType.Coin
            ? Color.white
            : new Color(0f, 1f, 0f);

        tokenTabImage.color =
            active == CurrencyType.Token
            ? Color.white
            : new Color(0f, 1f, 0f);
    }

    public void OpenShop() => shopPanel.SetActive(true);
    public void CloseShop() => shopPanel.SetActive(false);
}