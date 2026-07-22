using UnityEngine;

[ExecuteAlways]
public class ShadowSprite : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer targetSprite;
    [SerializeField] private SpriteRenderer shadowSprite;

    [Header("Light")]
    [Range(0, 360)]
    public float lightAngle = 45f;

    public float shadowDistance = 0.5f;
    public float shadowScaleX = 1.2f;
    public float shadowScaleY = 0.6f;

    [Range(0, 1)]
    public float shadowAlpha = 0.4f;

    private void LateUpdate()
    {
        if (targetSprite == null || shadowSprite == null)
            return;

        // Copy sprite
        shadowSprite.sprite = targetSprite.sprite;
        shadowSprite.flipX = targetSprite.flipX;
        shadowSprite.flipY = targetSprite.flipY;

        // Hướng bóng (ngược chiều ánh sáng)
        Vector2 dir = Quaternion.Euler(0, 0, lightAngle) * Vector2.right;

        shadowSprite.transform.position =
            transform.position - (Vector3)dir * shadowDistance;

        shadowSprite.transform.rotation = Quaternion.identity;

        shadowSprite.transform.localScale = new Vector3(
            transform.localScale.x * shadowScaleX,
            transform.localScale.y * shadowScaleY,
            1f
        );

        Color c = Color.black;
        c.a = shadowAlpha;
        shadowSprite.color = c;
    }
}