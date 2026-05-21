using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private CommandInvoker commandInvoker;

    private void Update()
    {
        if (player == null || commandInvoker == null)
            return;

        if (Input.GetKeyDown(KeyCode.W))
            commandInvoker.ExecuteCommand(new MoveCommand(player, Vector2.up));
        else if (Input.GetKeyDown(KeyCode.S))
            commandInvoker.ExecuteCommand(new MoveCommand(player, Vector2.down));
        else if (Input.GetKeyDown(KeyCode.A))
            commandInvoker.ExecuteCommand(new MoveCommand(player, Vector2.left));
        else if (Input.GetKeyDown(KeyCode.D))
            commandInvoker.ExecuteCommand(new MoveCommand(player, Vector2.right));
        else if (Input.GetKeyDown(KeyCode.Q))
            commandInvoker.ExecuteCommand(new RotateCommand(player, -90f));
        else if (Input.GetKeyDown(KeyCode.E))
            commandInvoker.ExecuteCommand(new RotateCommand(player, 90f));
        else if (Input.GetKeyDown(KeyCode.Z))
            commandInvoker.Undo();
        else if (Input.GetKeyDown(KeyCode.Y))
            commandInvoker.Redo();
    }
}
