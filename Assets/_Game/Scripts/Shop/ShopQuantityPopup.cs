using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Popup overlay nhập số lượng khi Buy/Sell trong shop.
///
/// Hành vi:
/// - <b>Confirm</b>: clamp số lượng về [1, max] rồi invoke onConfirm.
///   Popup <b>GIỮ NGUYÊN mở</b> (không ẩn) để người chơi có thể giao dịch tiếp.
/// - <b>Cancel</b>: ẩn popup.
/// - Nút xác nhận hiện dynamic "<c>Buy - &lt;tổng&gt;$</c>" / "<c>Sell - &lt;tổng&gt;$</c>"
///   với tổng = số lượng (đã clamp) × đơn giá, cập nhật theo ô nhập.
///
/// Popup là một view "ngu" — không tự biết giới hạn (max) hay đơn giá;
/// Presenter tính max (stock cho Buy, resource đang có cho Sell) và đơn giá
/// (Price cho Buy, Sell cho Sell) rồi truyền vào.
/// </summary>
public class ShopQuantityPopup : MonoBehaviour
{
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Tooltip("Text trên nút xác nhận — hiện dynamic 'Buy - <tổng>$' / 'Sell - <tổng>$'.")]
    [SerializeField] private TextMeshProUGUI confirmLabel;

    [Tooltip("Tiêu đề popup, ví dụ 'Buy Wood'. Tùy chọn (bỏ trống nếu không dùng).")]
    [SerializeField] private TextMeshProUGUI titleLabel;

    [SerializeField] private Image icon;

    [SerializeField] private float maxSize = 170;

    private Action<int> _onConfirm;
    private int _maxQty = 1;
    private ShopAction _action;
    private int _unitPrice;

    /// <summary>Wire các nút + ô nhập. Gọi 1 lần khi khởi tạo.</summary>
    public void Initialize()
    {
        if (quantityInput != null)
        {
            quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Cập nhật label dynamic mỗi khi ô nhập đổi
            quantityInput.onValueChanged.RemoveAllListeners();
            quantityInput.onValueChanged.AddListener(OnQuantityChanged);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    /// <summary>
    /// Hiện popup cho 1 item + thao tác. Reset ô nhập về "1".
    /// </summary>
    /// <param name="itemName">Tên item (cho tiêu đề).</param>
    /// <param name="action">Buy hoặc Sell.</param>
    /// <param name="maxQty">Số lượng tối đa khả thi (stock cho Buy, resource đang có cho Sell).</param>
    /// <param name="unitPrice">Đơn giá để tính tổng trên nút (Price cho Buy, Sell cho Sell).</param>
    /// <param name="onConfirm">Callback nhận số lượng đã clamp khi ấn xác nhận.</param>
    public void Show(string itemName, Sprite itemIcon, ShopAction action, int maxQty, int unitPrice, Action<int> onConfirm)
    {
        Debug.Log($"[ShopQuantityPopup] Show called: item={itemName}, action={action}, maxQty={maxQty}, unitPrice={unitPrice}");
        _onConfirm = onConfirm;
        _maxQty = maxQty < 1 ? 1 : maxQty;
        _action = action;
        _unitPrice = unitPrice;

        SetIcon(itemIcon);

        if (titleLabel != null)
        {
            titleLabel.text = $"{(action == ShopAction.Buy ? "Buy" : "Sell")} {itemName}";
        }

        if (quantityInput != null)
        {
            // Đặt "1" — cũng fire onValueChanged → cập nhật confirmLabel.
            // (Nếu text đã là "1" từ lần trước, onValueChanged không fire → gọi UpdateConfirmLabel() bên dưới.)
            quantityInput.text = "1";
        }

        // FIX: Popup nằm dưới ShopPanel (inactive mặc định). Đảm bảo parent chain
        // đều active + đẩy popup lên sibling cuối (render trên cùng canvas).
        ActivateParentChain();
        transform.SetAsLastSibling();

        gameObject.SetActive(true);

        Debug.Log($"[ShopQuantityPopup] Activated: activeInHierarchy={gameObject.activeInHierarchy}");

        // Đảm bảo label đúng cho thao tác/đơn giá mới dù onValueChanged không fire.
        UpdateConfirmLabel();
    }

    /// <summary>Set icon cua item cho popup (null -> an icon).</summary>
    private void SetIcon(Sprite itemIcon)
    {
        if (icon == null)
        {
            return;
        }

        if (itemIcon == null)
        {
            icon.enabled = false;
            return;
        }

        icon.sprite = itemIcon;
        icon.enabled = true;
        RectTransform rt = icon.rectTransform;
        float width = itemIcon.rect.width;
        float height = itemIcon.rect.height;
        if (width > height)
        {
            rt.sizeDelta = new Vector2(maxSize, maxSize * height / width);
        }
        else
        {
            rt.sizeDelta = new Vector2(maxSize * width / height, maxSize);
        }
    }

    /// <summary>Bật mọi parent inactive lên active — fix lỗi popup invisible khi parent bị disable.</summary>
    private void ActivateParentChain()
    {
        var parent = transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
            }
            parent = parent.parent;
        }
    }

    /// <summary>Ẩn popup.</summary>
    public void Hide() => gameObject.SetActive(false);

    private void OnConfirmClicked()
    {
        int qty = ClampQuantity(ParseQuantity(), _maxQty);

        // Phản ánh số đã clamp lên UI (cũng cập nhật lại confirmLabel); popup GIỮ NGUYÊN mở.
        if (quantityInput != null)
        {
            quantityInput.text = qty.ToString();
        }

        _onConfirm?.Invoke(qty);
    }

    private void OnCancelClicked() => Hide();

    /// <summary>Callback khi ô nhập đổi → cập nhật dynamic label.</summary>
    private void OnQuantityChanged(string _)
    {
        UpdateConfirmLabel();
    }

    /// <summary>Cập nhật nút xác nhận theo số lượng hiện tại (đã clamp) × đơn giá.</summary>
    private void UpdateConfirmLabel()
    {
        if (confirmLabel == null)
            return;

        int qty = ClampQuantity(ParseQuantity(), _maxQty);
        confirmLabel.text = FormatConfirmLabel(_action, qty, _unitPrice);
    }

    private int ParseQuantity()
    {
        if (quantityInput != null && int.TryParse(quantityInput.text, out int value))
        {
            return value;
        }
        return 1;
    }

    /// <summary>Clamp số lượng nhập về <c>[1, max]</c>. Max được floor ở 1. Pure — unit-test được.</summary>
    public static int ClampQuantity(int raw, int max)
    {
        int ceiling = max < 1 ? 1 : max;
        if (raw < 1) return 1;
        if (raw > ceiling) return ceiling;
        return raw;
    }

    /// <summary>
    /// Tạo text cho nút xác nhận: "<c>&lt;Buy/Sell&gt; - &lt;tổng&gt;$</c>" với
    /// tổng = <paramref name="quantity"/> × <paramref name="unitPrice"/>. Pure — unit-test được.
    /// </summary>
    public static string FormatConfirmLabel(ShopAction action, int quantity, int unitPrice)
    {
        string label = action == ShopAction.Buy ? "Buy" : "Sell";
        int total = quantity * unitPrice;
        return $"{label} - {total}$";
    }
}
