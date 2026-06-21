using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Transient toast for shop transaction feedback (success/failure). Shows, waits,
/// then hides. A "dumb" view: ShopPresenter formats the message; this only displays it.
/// Optional CanvasGroup drives a simple alpha fade; if unassigned it just show/hides.
/// </summary>
public class TransactionToast : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float duration = 2f;
    [SerializeField] private Color successColor = Color.white;
    [SerializeField] private Color failureColor = new Color(1f, 0.35f, 0.35f);

    private Coroutine _routine;

    private void Awake()
    {
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    public void Show(string message, bool success)
    {
        if (label != null)
        {
            label.text = message;
            label.color = success ? successColor : failureColor;
        }

        gameObject.SetActive(true);
        SetAlpha(1f);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(duration);
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    private void SetAlpha(float a)
    {
        if (canvasGroup != null) canvasGroup.alpha = a;
    }
}
