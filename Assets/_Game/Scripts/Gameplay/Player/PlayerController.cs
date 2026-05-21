using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveStep = 1f;

    public void Move(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector2 delta = direction.normalized * moveStep;
        Vector3 pos = transform.position;
        pos.x += delta.x;
        pos.y += delta.y;
        transform.position = pos;
    }

    public void Rotate(float angleZ)
    {
        transform.Rotate(0f, 0f, angleZ, Space.Self);
    }

    public void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    public void SetRotationZ(float angleZ)
    {
        Vector3 euler = transform.eulerAngles;
        euler.z = angleZ;
        transform.eulerAngles = euler;
    }
}
