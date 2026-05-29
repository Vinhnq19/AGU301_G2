using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    private Rigidbody2D rb;

    private void Start()
    {
        // rb = GetComponent<Rigidbody2D>();
    }
}