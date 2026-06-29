using System;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SummonBeacon : NetworkBehaviour, DungeonBuilder.Core.Interfaces.IPoolable
    {
        [SerializeField] private SpriteRenderer _visual;
        [SerializeField] private SpriteRenderer _rangeVisual;
        [SerializeField, Min(1)] private int _summonCount = 10;
        [SerializeField, Min(0.1f)] private float _summonInterval = 0.5f;

        private INetworkPool _pool;
        private NetworkObject _runnerPrefab;
        private Transform _coreTarget;
        private Transform[] _pathWaypoints;
        private bool _isFinished;

        private Tween _pulseTween;

        public void Initialize(INetworkPool pool, NetworkObject runnerPrefab, Transform coreTarget, Transform[] pathWaypoints)
        {
            _pool = pool;
            _runnerPrefab = runnerPrefab;
            _coreTarget = coreTarget;
            _pathWaypoints = pathWaypoints;
        }

        public override void OnNetworkSpawn()
        {
            PlaySpawnVisualClientRpc();
            if (IsServer)
                SummonRunnersAsync().Forget();
        }

        // Người chơi click chuột vào beacon để phá huỷ
        private void OnMouseDown()
        {
            RequestDestroyServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDestroyServerRpc()
        {
            if (_isFinished) return;
            DestroyBeacon();
        }

        private void DestroyBeacon()
        {
            if (_isFinished) return;
            _isFinished = true;
            PlayDestroyVisualClientRpc();
            _pool?.Return(NetworkObject);
        }

        private async UniTaskVoid SummonRunnersAsync()
        {
            // Delay nhỏ để beacon xuất hiện trước khi spawn runner
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: destroyCancellationToken);

            for (int i = 0; i < _summonCount; i++)
            {
                if (_isFinished || !IsServer || !IsSpawned) return;

                if (_pool != null && _runnerPrefab != null)
                {
                    NetworkObject runnerObj = _pool.Get(_runnerPrefab, transform.position, Quaternion.identity);
                    if (runnerObj != null)
                    {
                        BaseEnemy enemy = runnerObj.GetComponent<BaseEnemy>();
                        if (enemy != null)
                        {
                            enemy.SetCoreTarget(_coreTarget);
                            if (_pathWaypoints != null)
                                enemy.SetPath(_pathWaypoints);
                        }

                        if (!runnerObj.IsSpawned)
                            runnerObj.Spawn();
                    }
                }

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_summonInterval), cancellationToken: destroyCancellationToken);
                }
                catch (OperationCanceledException) { return; }
            }

            DestroyBeacon();
        }

        [ClientRpc]
        private void PlaySpawnVisualClientRpc()
        {
            if (_visual != null)
            {
                _visual.color = new Color(1f, 0.3f, 0.1f, 0f);
                _visual.DOFade(1f, 0.4f);
                _pulseTween?.Kill();
                _pulseTween = _visual.transform
                    .DOScale(Vector3.one * 1.15f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            if (_rangeVisual != null)
            {
                _rangeVisual.color = new Color(1f, 0.2f, 0.1f, 0f);
                _rangeVisual.DOFade(0.18f, 0.4f);
            }
        }

        [ClientRpc]
        private void PlayDestroyVisualClientRpc()
        {
            _pulseTween?.Kill();

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack);
            }

            if (_rangeVisual != null)
            {
                _rangeVisual.DOKill();
                _rangeVisual.DOFade(0f, 0.25f);
            }
        }

        public void OnGetFromPool()
        {
            _isFinished = false;

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.transform.localScale = Vector3.one;
                _visual.color = new Color(1f, 0.3f, 0.1f, 0f);
            }

            if (_rangeVisual != null)
            {
                _rangeVisual.DOKill();
                _rangeVisual.color = new Color(1f, 0.2f, 0.1f, 0f);
            }
        }

        public void OnReturnToPool()
        {
            _isFinished = false;
            _pulseTween?.Kill();

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.transform.localScale = Vector3.one;
            }
        }
    }
}
