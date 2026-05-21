using UnityEngine;

public class RotateCommand : Command
{
    private readonly PlayerController _player;
    private readonly float _angleZ;
    private float _previousRotationZ;

    public RotateCommand(PlayerController player, float angleZ)
    {
        _player = player;
        _angleZ = angleZ;
    }

    public override void Execute()
    {
        _previousRotationZ = _player.transform.eulerAngles.z;
        _player.Rotate(_angleZ);
        Debug.Log($"[RotateCommand] Execute — angle: {_angleZ}°, was: {_previousRotationZ}°");
    }

    public override void Undo()
    {
        _player.SetRotationZ(_previousRotationZ);
        Debug.Log($"[RotateCommand] Undo — restored Z: {_previousRotationZ}°");
    }
}
