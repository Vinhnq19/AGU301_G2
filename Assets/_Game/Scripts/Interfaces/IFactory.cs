using UnityEngine;
public interface IFactory
{
    GameObject Create(string type, Vector3 position);
}
