using Unity.Netcode;
using UnityEngine;


public class Shop : NetworkBehaviour
{
    [SerializeField] private ShopPresenter presenter;
    [SerializeField] private ShopView view;
    [SerializeField] private ShopModel model;

    private void Awake()
    {
        presenter = new ShopPresenter(view, model);
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            view.OpenShop();
        }
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            view.CloseShop();
        }
    }
}