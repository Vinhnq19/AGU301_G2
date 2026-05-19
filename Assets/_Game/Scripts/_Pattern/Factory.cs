using UnityEngine;

/// <summary>
/// Abstract base class cho Factory Pattern.
/// Mỗi Concrete Factory kế thừa lớp này và triển khai logic tạo sản phẩm cụ thể.
/// </summary>
public abstract class Factory : MonoBehaviour
{
    /// <summary>
    /// Phương thức trừu tượng tạo sản phẩm dựa trên loại được chỉ định.
    /// Phải được override bởi Concrete Factory.
    /// </summary>
    /// <param name="type">Tên/loại sản phẩm cần tạo</param>
    /// <param name="position">Vị trí spawn trong scene</param>
    /// <returns>GameObject sản phẩm vừa được tạo, hoặc null nếu loại không hợp lệ</returns>
    public abstract GameObject Create(string type, Vector3 position);
}
