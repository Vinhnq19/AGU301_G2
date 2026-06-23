using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Audio
{
    public sealed class GameAudioController : MonoBehaviour
    {
        private EventBus _eventBus;
        private int _currentWave = 0;
        private bool _isCurrentWaveBoss = false;

        [Inject]
        public void Construct(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        private void Start()
        {
            if (_eventBus != null)
            {
                _eventBus.OnGamePhaseChanged += HandleGamePhaseChanged;
                _eventBus.OnWaveStarted += HandleWaveStarted;
                _eventBus.OnGameEnded += HandleGameEnded;
            }

            // Play Prep Phase BGM by default when scene starts
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(SoundType.BGM_Prep_Phase, 2f);
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.OnGamePhaseChanged -= HandleGamePhaseChanged;
                _eventBus.OnWaveStarted -= HandleWaveStarted;
                _eventBus.OnGameEnded -= HandleGameEnded;
            }
        }

        private void HandleGamePhaseChanged(GamePhase phase)
        {
            Debug.Log($"[GameAudioController] HandleGamePhaseChanged: {phase}, Wave: {_currentWave}");
            if (AudioManager.Instance == null) return;

            if (phase == GamePhase.Build)
            {
                AudioManager.Instance.PlayBGM(SoundType.BGM_Prep_Phase, 2f);
            }
            else if (phase == GamePhase.Combat)
            {
                if (_isCurrentWaveBoss)
                {
                    AudioManager.Instance.PlayBGM(SoundType.BGM_Boss_Fight, 2f);
                }
                else
                {
                    AudioManager.Instance.PlayBGM(SoundType.BGM_Combat_Phase, 2f);
                }
            }
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            Debug.Log($"[GameAudioController] HandleWaveStarted: {wave}, IsBoss: {isBossWave}");
            _currentWave = wave;
            _isCurrentWaveBoss = isBossWave;
        }

        private void HandleGameEnded(bool isWin)
        {
            if (AudioManager.Instance == null) return;

            if (isWin)
            {
                AudioManager.Instance.PlayBGM(SoundType.BGM_Victory, 1.5f);
            }
            else
            {
                AudioManager.Instance.PlayBGM(SoundType.BGM_Defeat, 1.5f);
            }
        }
    }
}
