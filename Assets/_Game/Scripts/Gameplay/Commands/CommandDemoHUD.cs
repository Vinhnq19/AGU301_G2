using UnityEngine;

public class CommandDemoHUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private CommandInvoker commandInvoker;

    private void OnGUI()
    {
        if (player == null || commandInvoker == null)
            return;

        Vector3 p = player.transform.position;
        GUI.Label(new Rect(10, 10, 400, 22), $"Position: ({p.x:F1}, {p.y:F1})");
        GUI.Label(new Rect(10, 32, 400, 22), $"Rotation Z: {player.transform.eulerAngles.z:F0}°");
        GUI.Label(new Rect(10, 54, 400, 22), $"Undo stack: {commandInvoker.UndoCount} | Redo stack: {commandInvoker.RedoCount}");
        GUI.Label(new Rect(10, 76, 500, 22), "WASD: Move | Q/E: Rotate | Z: Undo | Y: Redo");
    }
}
