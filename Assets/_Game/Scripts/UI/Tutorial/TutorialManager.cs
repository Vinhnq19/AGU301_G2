using UnityEngine;
using TMPro;
using DG.Tweening;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Harvesting;
using DungeonBuilder.Building;
using DungeonBuilder.Networking;
using System.Collections.Generic;
using VContainer;

namespace Assets._Game.Scripts.UI.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField] private CanvasGroup _tutorialCanvasGroup;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private ScreenSpacePointer _pointer;

        [Header("Step 2 - Building Prompt")]
        [SerializeField] private GameObject _gridPromptFPrefab; // Pulses in World Space

        private IResourceService _resourceService;
        private EventBus _eventBus;
        private GridManager _gridManager;
        private CoreManager _coreManager;

        private int _currentStep = 1;
        private int _startingWood = 0;
        private List<GameObject> _activePrompts = new List<GameObject>();
        private HarvestableNode _targetWoodNode = null;
        private Shop _shop = null;

        // Blinking variables for the wood node
        private SpriteRenderer _targetNodeRenderer = null;
        private Color _originalNodeColor = Color.white;
        private float _blinkTimer = 0f;

        // Step 2 waiting variables
        private bool _towerPlaced = false;
        private int _currentWaveCount = 0;
        private bool _isPrepTimeOfPhase2 = false;

        [Inject]
        public void Construct(IResourceService resourceService, EventBus eventBus, GridManager gridManager, CoreManager coreManager)
        {
            _resourceService = resourceService;
            _eventBus = eventBus;
            _gridManager = gridManager;
            _coreManager = coreManager;

            if (_eventBus != null)
            {
                _eventBus.OnWaveStarted += HandleWaveStarted;
                _eventBus.OnGamePhaseChanged += HandleGamePhaseChanged;
            }
        }

        private void Start()
        {
            // If injection did not happen (e.g. offline/testing), fallback to finding them in scene
            if (_resourceService == null)
                _resourceService = FindFirstObjectByType<SharedResourceManager>();
            if (_gridManager == null)
                _gridManager = FindFirstObjectByType<GridManager>();
            if (_coreManager == null)
                _coreManager = FindFirstObjectByType<CoreManager>();

            _shop = FindFirstObjectByType<Shop>();

            _currentStep = 1;
            _towerPlaced = false;
            _isPrepTimeOfPhase2 = false;
            _currentWaveCount = 0;

            if (_resourceService != null)
            {
                _startingWood = _resourceService.GetAmount(ResourceType.Wood);
            }
            SetupStep(_currentStep);
        }

        private void OnDestroy()
        {
            RestoreBlinkingNode();
            if (_eventBus != null)
            {
                _eventBus.OnWaveStarted -= HandleWaveStarted;
                _eventBus.OnGamePhaseChanged -= HandleGamePhaseChanged;
            }
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            _currentWaveCount = wave;
        }

        private void HandleGamePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Build && _currentWaveCount >= 1)
            {
                _isPrepTimeOfPhase2 = true;
                if (_currentStep == 2 && _towerPlaced)
                {
                    SetupStep(3);
                }
            }
        }

        private void Update()
        {
            UpdateStepLogic();
        }

        private void SetupStep(int step)
        {
            _currentStep = step;
            _pointer?.ClearTarget();
            ClearActivePrompts();
            RestoreBlinkingNode();

            if (step == 1)
            {
                if (_titleText != null) _titleText.text = "HƯỚNG DẪN CHƠI - BƯỚC 1";
                if (_bodyText != null) _bodyText.text = "DI CHUYỂN & KHAI THÁC: Di chuyển bằng phím WASD đến mỏ gỗ gần nhất và khai thác 5 Gỗ.";
                FindAndTargetNearestWoodNode();
            }
            else if (step == 2)
            {
                if (_titleText != null) _titleText.text = "HƯỚNG DẪN CHƠI - BƯỚC 2";
                if (_bodyText != null) _bodyText.text = "XÂY DỰNG PHÒNG THỦ: Di chuyển đến khu vực Core và nhấn chuột trái vào các điểm đặt tháp xung quanh Core để xây 1 tháp Arrow Tower.";
                
                // Point specifically to the single tower spot's world coordinates: (-5.50, 30.50, 3.46)
                _pointer?.SetTarget(new Vector3(-5.50f, 30.50f, 3.46f));

                SpawnPredefinedSpotPrompts();
            }
            else if (step == 3)
            {
                if (_titleText != null) _titleText.text = "HƯỚNG DẪN CHƠI - BƯỚC 3";
                if (_bodyText != null) _bodyText.text = "GIAO THƯƠNG & CỬA HÀNG: Di chuyển đến khu vực Shop, đi vào vùng kích hoạt của Shop để mở giao diện cửa hàng.";
                
                // Point specifically to the shop position as requested
                _pointer?.SetTarget(new Vector3(-7.13000011f, 7.23999977f, 0f));
            }
        }

        private void UpdateStepLogic()
        {
            if (_currentStep == 1)
            {
                // Wood resource check
                if (_resourceService != null)
                {
                    int currentWood = _resourceService.GetAmount(ResourceType.Wood);
                    if (currentWood >= _startingWood + 5 || currentWood >= 5)
                    {
                        SetupStep(2);
                        return;
                    }
                }

                // Node tracking and blinking
                if (_targetWoodNode == null || !_targetWoodNode.gameObject.activeInHierarchy)
                {
                    RestoreBlinkingNode();
                    FindAndTargetNearestWoodNode();
                }

                BlinkTargetNode();
            }
            else if (_currentStep == 2)
            {
                if (!_towerPlaced)
                {
                    // Check if any tower is placed
                    var tower = FindFirstObjectByType<BaseTower>();
                    if (tower != null)
                    {
                        _towerPlaced = true;
                        ClearActivePrompts();
                        _pointer?.ClearTarget();

                        // Stay in Step 2 visual state but instruct the player to defend
                        if (_titleText != null) _titleText.text = "HƯỚNG DẪN CHƠI - PHÒNG THỦ";
                        if (_bodyText != null) _bodyText.text = "PHÒNG THỦ SÓNG QUÁI: Hãy sẵn sàng chiến đấu và bảo vệ Core khỏi Wave 1!";
                    }
                    else
                    {
                        // Update active prompts: hide if a spot becomes occupied
                        UpdatePredefinedSpotPromptsVisibility();
                    }
                }
                else
                {
                    // If tower is placed, we wait until Wave 1 is cleared (we check the _isPrepTimeOfPhase2 flag set by EventBus)
                    if (_isPrepTimeOfPhase2)
                    {
                        SetupStep(3);
                    }
                }
            }
            else if (_currentStep == 3)
            {
                if (_shop == null)
                {
                    _shop = FindFirstObjectByType<Shop>();
                }

                if (_shop != null && _shop.IsOpen)
                {
                    CompleteTutorial();
                }
            }
        }

        private void FindAndTargetNearestWoodNode()
        {
            Vector3 targetPos = new Vector3(-34f, 29f, 0f);
            HarvestableNode targetNode = null;

            foreach (var node in FindObjectsByType<HarvestableNode>(FindObjectsSortMode.None))
            {
                if (node.NodeType == ResourceType.Wood && node.gameObject.activeInHierarchy)
                {
                    if (Vector3.Distance(node.transform.position, targetPos) < 1.5f)
                    {
                        targetNode = node;
                        break;
                    }
                }
            }

            _targetWoodNode = targetNode;
            if (_targetWoodNode != null)
            {
                _pointer?.SetTarget(_targetWoodNode.transform.position);
                _targetNodeRenderer = _targetWoodNode.GetComponentInChildren<SpriteRenderer>();
                if (_targetNodeRenderer != null)
                {
                    _originalNodeColor = _targetNodeRenderer.color;
                }
            }
            else
            {
                // Fallback to exact coordinate pointer if node is missing/destroyed
                _pointer?.SetTarget(targetPos);
            }
        }

        private void BlinkTargetNode()
        {
            if (_targetNodeRenderer == null) return;

            _blinkTimer += Time.deltaTime * 5f;
            float t = (Mathf.Sin(_blinkTimer) + 1f) / 2f;
            _targetNodeRenderer.color = Color.Lerp(_originalNodeColor, Color.yellow, t);
        }

        private void RestoreBlinkingNode()
        {
            if (_targetNodeRenderer != null)
            {
                _targetNodeRenderer.color = _originalNodeColor;
                _targetNodeRenderer = null;
            }
        }

        private DungeonBuilder.Player.PlayerController GetLocalPlayer()
        {
            var players = FindObjectsByType<DungeonBuilder.Player.PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p.IsOwner) return p;
            }
            return players.Length > 0 ? players[0] : null;
        }

        private void SpawnPredefinedSpotPrompts()
        {
            if (_gridPromptFPrefab == null) return;

            // Find GridPosition (1) in GridPositions in scene to position perfectly
            var spotsParent = GameObject.Find("GridPositions");
            Transform targetSpot = null;
            if (spotsParent != null)
            {
                targetSpot = spotsParent.transform.Find("GridPosition (1)");
            }

            Vector3 spawnPos;
            if (targetSpot != null)
            {
                spawnPos = targetSpot.position + Vector3.up * 0.5f;
            }
            else
            {
                // Fallback to the actual world coordinates corresponding to GridPosition (1)
                spawnPos = new Vector3(-5.50f, 30.50f, 3.46f) + Vector3.up * 0.5f;
            }

            GameObject promptObj = Instantiate(_gridPromptFPrefab, spawnPos, Quaternion.identity);
            if (targetSpot != null)
            {
                promptObj.transform.SetParent(targetSpot, true);
            }

            // Customize text to English and remove "[F]" as requested
            var textComp = promptObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = "CLICK TO BUILD";
            }
            
            _activePrompts.Add(promptObj);
        }

        private void UpdatePredefinedSpotPromptsVisibility()
        {
            if (_gridManager == null) return;

            for (int i = _activePrompts.Count - 1; i >= 0; i--)
            {
                GameObject prompt = _activePrompts[i];
                if (prompt == null)
                {
                    _activePrompts.RemoveAt(i);
                    continue;
                }

                // If grid spot is occupied, clear the prompt
                Vector3 spotPos = new Vector3(-5.50f, 30.50f, 3.46f);
                Vector2Int gridPos = _gridManager.WorldToGrid(spotPos);
                if (!_gridManager.IsValidPlacement(gridPos))
                {
                    Destroy(prompt);
                    _activePrompts.RemoveAt(i);
                }
            }
        }

        private void ClearActivePrompts()
        {
            foreach (var prompt in _activePrompts)
            {
                if (prompt != null)
                {
                    Destroy(prompt);
                }
            }
            _activePrompts.Clear();
        }

        private void CompleteTutorial()
        {
            _currentStep = 4; // Complete state
            _pointer?.ClearTarget();
            ClearActivePrompts();
            RestoreBlinkingNode();

            if (_titleText != null) _titleText.text = "TUTORIAL HOÀN THÀNH!";
            if (_bodyText != null) _bodyText.text = "Chúc mừng bạn đã hoàn thành hướng dẫn cơ bản!";

            if (_tutorialCanvasGroup != null)
            {
                _tutorialCanvasGroup.DOFade(0f, 1f).SetDelay(3f).OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });
            }
            else
            {
                Destroy(gameObject, 3f);
            }
        }
    }
}
