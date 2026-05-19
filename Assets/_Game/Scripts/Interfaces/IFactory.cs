using UnityEngine;

/// <summary>
/// Interface định nghĩa hành vi cơ bản của mọi Factory.
/// </summary>
public interface IFactory
{
    /// <summary>
    /// Tạo một sản phẩm mới theo loại và vị trí được chỉ định.
    /// </summary>
    /// <param name="type">Tên/loại sản phẩm cần tạo</param>
    /// <param name="position">Vị trí spawn trong scene</param>
    /// <returns>GameObject sản phẩm vừa tạo, hoặc null nếu loại không hợp lệ</returns>
    GameObject Create(string type, Vector3 position);
}
