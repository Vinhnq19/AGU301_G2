using UnityEngine;
public abstract class Factory : MonoBehaviour
{
    public abstract GameObject Create(string type, Vector3 position);
}
