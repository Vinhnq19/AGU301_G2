using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Player;
using DungeonBuilder.Player.Tools;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Harvesting
{
    public sealed class HarvestableNode : NetworkBehaviour, IHarvestable, IDamageable, IPoolable
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField] private ResourceNodeDataSO _data;
        [SerializeField] private NetworkObject _resourceDropPrefab;
        [SerializeField, Min(0.1f)] private float _serverInteractionRange = 2f;
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _visualRenderer;
        [SerializeField] private Collider2D[] _colliders;

        [Header("VFX")]
        [SerializeField, Min(0f)] private float _flashDuration = 0.15f;
        [SerializeField, Min(0f)] private float _deathDuration = 0.3f;

        private readonly NetworkVariable<int> _hitsRemaining = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDepleted = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Node bị khóa (chưa tới wave mở khóa): ẩn + không khai thác được. Chỉ áp dụng cho loại hiếm (minWaveToAppear > 1).
        private readonly NetworkVariable<bool> _isLocked = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private INetworkPool _pool;
        private EventBus _eventBus;
        private IResourceService _sharedResources;
        private ResourceSpawner _owner;
        private int _slotIndex = -1;
        private int _currentWave;

        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _initialVisualScale = Vector3.one;
        private Tween _flashTween;

        public bool IsDepletable => true;
        public ResourceType NodeType => _data != null ? _data.resourceType : ResourceType.Wood;

        [Inject]
        public void Construct(INetworkPool pool, EventBus eventBus, IResourceService sharedResources)
        {
            _pool = pool;
            _eventBus = eventBus;
            _sharedResources = sharedResources;
        }

        private void Awake()
        {
            if (_visual != null)
            {
                _initialVisualScale = _visual.localScale;
            }
        }

        /// <summary>
        /// Cấu hình node khi được spawn động bởi ResourceSpawner. Server-only.
        /// </summary>
        public void Configure(ResourceNodeDataSO data, int slotIndex, ResourceSpawner owner)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (data != null)
            {
                _data = data;
            }

            _slotIndex = slotIndex;
            _owner = owner;
            ResetNode();
            DBLog.Info($"node.configure.{NetworkObjectId}", $"HarvestableNode configured. type={_data?.resourceType}, slot={slotIndex}.", 0.2f, this);
        }

        public override void OnNetworkSpawn()
        {
            _isDepleted.OnValueChanged += HandleDepletedChanged;
            _isLocked.OnValueChanged += HandleLockedChanged;

            if (IsServer)
            {
                if (_eventBus != null)
                {
                    _eventBus.OnWaveStarted += HandleWaveStarted;
                }

                if (_owner == null)
                {
                    // Node đặt sẵn trong scene (không qua spawner) vẫn hoạt động như cũ.
                    ResetNode();
                }

                ApplyWaveGate();
            }

            RefreshActiveState();
            DBLog.Info($"node.spawn.{NetworkObjectId}", $"HarvestableNode spawned. type={_data?.resourceType}, hits={_hitsRemaining.Value}, locked={_isLocked.Value}, server={IsServer}.", 0f, this);
        }

        public override void OnNetworkDespawn()
        {
            _isDepleted.OnValueChanged -= HandleDepletedChanged;
            _isLocked.OnValueChanged -= HandleLockedChanged;

            if (IsServer && _eventBus != null)
            {
                _eventBus.OnWaveStarted -= HandleWaveStarted;
            }
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            if (!IsServer) return;

            _currentWave = wave;
            ApplyWaveGate();
        }

        /// <summary>
        /// Khóa node nếu wave hiện tại chưa đạt minWaveToAppear (chỉ với loại hiếm minWaveToAppear > 1).
        /// Khi vừa mở khóa thì reset node để có thể khai thác ngay.
        /// </summary>
        private void ApplyWaveGate()
        {
            if (_data == null)
            {
                return;
            }

            bool shouldLock = _data.minWaveToAppear > 1 && _currentWave < _data.minWaveToAppear;
            bool wasLocked = _isLocked.Value;
            _isLocked.Value = shouldLock;

            if (wasLocked && !shouldLock)
            {
                // Vừa mở khóa: làm tươi node.
                ResetNode();
            }
        }

        public void OnGetFromPool()
        {
            _flashTween?.Kill();
            _flashTween = null;
            SetFlashAmount(0f);

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localScale = _initialVisualScale;
            }
        }

        public void OnReturnToPool()
        {
            _flashTween?.Kill();
            _flashTween = null;
            SetFlashAmount(0f);

            _owner = null;
            _slotIndex = -1;

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localScale = _initialVisualScale;
            }
        }

        public void OnInteract(PlayerController interactor)
        {
            if (IsServer && interactor != null && IsPlayerInRange(interactor.NetworkObject))
            {
                HarvestOnce(null);
            }
        }

        public void TakeDamageFrom(ITool tool)
        {
            if (!IsServer)
            {
                return;
            }

            HarvestOnce(tool);
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer)
            {
                return;
            }

            HarvestOnce(null);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractWithNodeServerRpc(RpcParams rpcParams = default)
        {
            if (!TryGetSenderPlayer(rpcParams.Receive.SenderClientId, out NetworkObject playerObject))
            {
                return;
            }

            if (!IsPlayerInRange(playerObject))
            {
                return;
            }

            HarvestOnce(null);
        }

        private void HarvestOnce(ITool sourceTool)
        {
            ResolvePool();

            if (_data == null || _resourceDropPrefab == null || _pool == null || _isDepleted.Value || _isLocked.Value)
            {
                DBLog.Warning($"node.harvest.blocked.{NetworkObjectId}", $"Harvest blocked. dataNull={_data == null}, dropPrefabNull={_resourceDropPrefab == null}, poolNull={_pool == null}, depleted={_isDepleted.Value}, locked={_isLocked.Value}.", 0.5f, this);
                return;
            }

            if (_hitsRemaining.Value <= 0)
            {
                _hitsRemaining.Value = Mathf.Max(1, _data.hitsToBreak);
            }

            ToolType toolType = sourceTool != null ? sourceTool.ToolType : ToolType.None;
            int skill = ResolveSkillForTool(toolType);
            int damage = SkillDamageCalculator.Calculate(skill);
            int hitsBefore = _hitsRemaining.Value;
            _hitsRemaining.Value = Mathf.Max(0, _hitsRemaining.Value - damage);
            DBLog.Info($"node.harvest.{NetworkObjectId}", $"Harvested node. type={_data.resourceType}, tool={toolType}, skill={skill}, damage={damage}, hitsRemaining={_hitsRemaining.Value}, amount={_data.amountPerHit}.", 0.2f, this);
            PlayDamageFlashClientRpc();
            SpawnResourceDrop();

            if (_hitsRemaining.Value <= 0)
            {
                _isDepleted.Value = true;
                DBLog.Info($"node.depleted.{NetworkObjectId}", $"Node depleted. type={_data.resourceType}, slot={_slotIndex}, hitsBefore={hitsBefore}, damage={damage}.", 0f, this);
                DepleteAsync().Forget();
            }
        }

        private int ResolveSkillForTool(ToolType toolType)
        {
            if (_sharedResources == null)
            {
                return SkillDamageCalculator.BaseSkill;
            }

            ResourceType skillType = SkillDamageCalculator.SkillForTool(toolType);
            int skill = _sharedResources.GetAmount(skillType);
            return skill < SkillDamageCalculator.BaseSkill ? SkillDamageCalculator.BaseSkill : skill;
        }

        private void SpawnResourceDrop()
        {
            NetworkObject dropObject = _pool.Get(_resourceDropPrefab, transform.position, Quaternion.identity);
            if (dropObject == null)
            {
                DBLog.Warning($"node.drop.null.{NetworkObjectId}", $"Resource drop spawn failed. type={_data.resourceType}.", 0.5f, this);
                return;
            }

            // Spawn() TRƯỚC để NetworkVariable hợp lệ trước khi Configure() ghi vào
            if (!dropObject.IsSpawned) dropObject.Spawn();

            ResourceDrop drop = dropObject.GetComponent<ResourceDrop>();
            drop?.Configure(_data.resourceType, _data.amountPerHit);

            DBLog.Info($"node.drop.spawn.{NetworkObjectId}", $"Spawned resource drop. dropId={dropObject.NetworkObjectId}, type={_data.resourceType}, amount={_data.amountPerHit}.", 0.2f, dropObject);
        }

        /// <summary>
        /// Khi node bị phá: phát death VFX, rồi xử lý theo nguồn gốc node:
        /// - Node spawn từ wave (có owner): giải phóng slot và trả về pool; respawn do ResourceSpawner điều khiển.
        /// - Node đặt sẵn trong scene (không owner): tự respawn sau respawnTime như hành vi cũ.
        /// </summary>
        private async UniTaskVoid DepleteAsync()
        {
            PlayDeathEffectClientRpc();

            if (_owner != null)
            {
                _owner.NotifyNodeDepleted(_slotIndex);

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_deathDuration), cancellationToken: destroyCancellationToken);
                    if (IsServer)
                    {
                        _pool?.Return(NetworkObject);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                return;
            }

            // Scene-placed node: respawn tại chỗ.
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_data.respawnTime), cancellationToken: destroyCancellationToken);
                if (IsServer)
                {
                    ResetNode();
                    if (_visual != null)
                    {
                        _visual.DOKill();
                        _visual.localScale = _initialVisualScale;
                    }
                    DBLog.Info($"node.respawn.{NetworkObjectId}", $"Scene node respawned. type={_data.resourceType}.", 0f, this);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ResetNode()
        {
            if (_data == null)
            {
                return;
            }

            _hitsRemaining.Value = Mathf.Max(1, _data.hitsToBreak);
            _isDepleted.Value = false;
        }

        [ClientRpc]
        public void PlayDamageFlashClientRpc()
        {
            if (_visualRenderer == null)
            {
                return;
            }

            _flashTween?.Kill();
            SetFlashAmount(1f);
            _flashTween = DOVirtual.Float(1f, 0f, _flashDuration, SetFlashAmount).SetEase(Ease.OutQuad);
        }

        [ClientRpc]
        public void PlayDeathEffectClientRpc()
        {
            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.DOScale(Vector3.zero, _deathDuration).SetEase(Ease.InBack);
        }

        private void SetFlashAmount(float amount)
        {
            if (_visualRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _visualRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FlashAmountId, amount);
            _visualRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void HandleDepletedChanged(bool previousValue, bool newValue)
        {
            // Khi depleted, để death VFX chạy trước rồi node sẽ được trả về pool;
            // chỉ ẩn collider để chặn tương tác thêm. Khi reset (false) thì bật lại.
            if (newValue)
            {
                SetCollidersActive(false);
            }
            else
            {
                // Reset visual về trạng thái đầy đủ trên mọi client (death VFX đã scale về 0).
                if (_visual != null)
                {
                    _visual.DOKill();
                    _visual.localScale = _initialVisualScale;
                }

                SetFlashAmount(0f);
                RefreshActiveState();
            }
        }

        private void HandleLockedChanged(bool previousValue, bool newValue)
        {
            RefreshActiveState();
        }

        /// <summary>Node hiển thị/khai thác được khi không bị khóa wave và chưa cạn kiệt.</summary>
        private void RefreshActiveState()
        {
            SetNodeActive(!_isLocked.Value && !_isDepleted.Value);
        }

        private void SetNodeActive(bool active)
        {
            if (_visual != null)
            {
                _visual.gameObject.SetActive(active);
            }

            SetCollidersActive(active);
        }

        private void SetCollidersActive(bool active)
        {
            if (_colliders == null)
            {
                return;
            }

            foreach (Collider2D nodeCollider in _colliders)
            {
                if (nodeCollider != null)
                {
                    nodeCollider.enabled = active;
                }
            }
        }

        private bool TryGetSenderPlayer(ulong senderClientId, out NetworkObject playerObject)
        {
            playerObject = null;
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject;
            return true;
        }

        private bool IsPlayerInRange(NetworkObject playerObject)
        {
            if (playerObject == null)
            {
                return false;
            }

            float distance = Vector3.Distance(playerObject.transform.position, transform.position);
            bool inRange = distance <= _serverInteractionRange;
            if (!inRange)
            {
                DBLog.Warning($"node.range.reject.{NetworkObjectId}", $"Node rejected interaction: out of range. distance={distance:0.00}, max={_serverInteractionRange:0.00}, playerPos={playerObject.transform.position}, nodePos={transform.position}.", 0.5f, this);
            }

            return inRange;
        }

        private void ResolvePool()
        {
            if (_pool != null)
            {
                return;
            }

            Debug.LogError($"[{nameof(HarvestableNode)}] INetworkPool was not injected on '{gameObject.name}'. Verify GameLifetimeScope registration and that this object is spawned via the pool.", this);
        }
    }
}
