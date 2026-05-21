using UnityEngine;

public class MoveCommand : Command
{
    private readonly PlayerController _player;
    private readonly Vector2 _direction;
    private Vector2 _previousPosition;

    public MoveCommand(PlayerController player, Vector2 direction)
    {
        _player = player;
        _direction = direction;
    }

    public override void Execute()
    {
        _previousPosition = (Vector2)_player.transform.position;
        _player.Move(_direction);
        Debug.Log($"[MoveCommand] Execute — direction: {_direction}, from: {_previousPosition}");
    }

    public override void Undo()
    {
        _player.SetPosition(_previousPosition);
        Debug.Log($"[MoveCommand] Undo — restored position: {_previousPosition}");
    }
}
