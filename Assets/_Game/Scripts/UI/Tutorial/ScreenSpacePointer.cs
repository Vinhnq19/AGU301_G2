using UnityEngine;
using UnityEngine.UI;

namespace Assets._Game.Scripts.UI.Tutorial
{
    public class ScreenSpacePointer : MonoBehaviour
    {
        [SerializeField] private RectTransform _pointerRect;
        [SerializeField] private Image _pointerImage;
        [SerializeField] private float _screenPadding = 50f;
        [SerializeField] private float _onScreenOffset = 100f; // float above target on screen

        [Header("Icons")]
        [SerializeField] private Sprite _arrowSprite;
        [SerializeField] private Sprite _leftClickSprite;
        [SerializeField] private float _distanceThreshold = 1.5f;

        private Vector3 _targetPosition;
        private bool _hasTarget;
        private bool _useClickIconOnApproach;
        private float _currentOffsetOverride = -1f;
        private float _currentDistanceThresholdOverride = -1f;
        private Camera _mainCamera;
        private DungeonBuilder.Player.PlayerController _localPlayer;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_pointerRect == null)
            {
                _pointerRect = GetComponent<RectTransform>();
            }

            // Start hidden by default so it doesn't float at the player spawn point
            gameObject.SetActive(false);
        }

        private void Start()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            // Increase visual size slightly as requested
            if (_pointerRect != null)
            {
                _pointerRect.sizeDelta = new Vector2(65f, 65f);
            }
            
            // Disable raycast target so it doesn't block player clicks
            if (_pointerImage != null)
            {
                _pointerImage.raycastTarget = false;
            }
        }

        public void SetTarget(Vector3 targetPos, bool useClickIconOnApproach = false, float offsetOverride = -1f, float distanceThresholdOverride = -1f)
        {
            _targetPosition = targetPos;
            _hasTarget = true;
            _useClickIconOnApproach = useClickIconOnApproach;
            _currentOffsetOverride = offsetOverride;
            _currentDistanceThresholdOverride = distanceThresholdOverride;
            gameObject.SetActive(true);
        }

        public void SetTarget(Transform target, bool useClickIconOnApproach = false, float offsetOverride = -1f, float distanceThresholdOverride = -1f)
        {
            if (target != null)
            {
                SetTarget(target.position, useClickIconOnApproach, offsetOverride, distanceThresholdOverride);
            }
            else
            {
                ClearTarget();
            }
        }

        public void ClearTarget()
        {
            _hasTarget = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_hasTarget)
            {
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Find local player if not cached
            if (_localPlayer == null)
            {
                var players = FindObjectsByType<DungeonBuilder.Player.PlayerController>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.IsOwner)
                    {
                        _localPlayer = p;
                        break;
                    }
                }
            }

            bool showClickIcon = false;
            if (_useClickIconOnApproach && _localPlayer != null)
            {
                Vector2 playerPos2D = new Vector2(_localPlayer.transform.position.x, _localPlayer.transform.position.y);
                Vector2 targetPos2D = new Vector2(_targetPosition.x, _targetPosition.y);
                float distance = Vector2.Distance(playerPos2D, targetPos2D);
                float actualThreshold = _currentDistanceThresholdOverride >= 0f ? _currentDistanceThresholdOverride : _distanceThreshold;
                if (distance <= actualThreshold)
                {
                    showClickIcon = true;
                }
            }

            if (showClickIcon)
            {
                if (_pointerImage != null && _leftClickSprite != null)
                {
                    _pointerImage.sprite = _leftClickSprite;
                    _pointerImage.color = Color.white;
                }
            }
            else
            {
                if (_pointerImage != null && _arrowSprite != null)
                {
                    _pointerImage.sprite = _arrowSprite;
                    _pointerImage.color = Color.yellow;
                }
            }

            Vector3 screenPosition = _mainCamera.WorldToScreenPoint(_targetPosition);

            // Check if target is behind camera or off-screen
            bool isOffScreen = screenPosition.x < _screenPadding ||
                               screenPosition.x > Screen.width - _screenPadding ||
                               screenPosition.y < _screenPadding ||
                               screenPosition.y > Screen.height - _screenPadding ||
                               screenPosition.z < 0;

            if (isOffScreen)
            {
                // Target is behind camera, flip it
                if (screenPosition.z < 0)
                {
                    screenPosition *= -1f;
                }

                // Center of the screen is the origin
                Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
                Vector3 dir = (screenPosition - screenCenter).normalized;

                // Calculate slope and clamp to edge
                float xOffset = Screen.width / 2f - _screenPadding;
                float yOffset = Screen.height / 2f - _screenPadding;

                // Find clamp position on the bounding box of the screen
                float tX = dir.x != 0 ? xOffset / Mathf.Abs(dir.x) : float.MaxValue;
                float tY = dir.y != 0 ? yOffset / Mathf.Abs(dir.y) : float.MaxValue;
                float t = Mathf.Min(tX, tY);

                Vector3 clampPosition = screenCenter + dir * t;

                _pointerRect.position = clampPosition;

                if (showClickIcon)
                {
                    _pointerRect.rotation = Quaternion.Euler(0, 0, 0f);
                }
                else
                {
                    // Rotate to point towards the target (flipped by 180 degrees as requested)
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _pointerRect.rotation = Quaternion.Euler(0, 0, angle + 90f);
                }
            }
            else
            {
                // On-screen: position directly above the target
                float offsetToUse = _currentOffsetOverride >= 0f ? _currentOffsetOverride : _onScreenOffset;
                Vector3 screenPosWithOffset = screenPosition + Vector3.up * offsetToUse;
                _pointerRect.position = screenPosWithOffset;
                
                // Point straight down (flipped by 180 degrees from 180f to 0f as requested)
                _pointerRect.rotation = Quaternion.Euler(0, 0, 0f);
            }
        }
    }
}
