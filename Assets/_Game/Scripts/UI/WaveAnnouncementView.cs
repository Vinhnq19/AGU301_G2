using UnityEngine;
using TMPro;
using DG.Tweening;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using VContainer;

namespace Assets._Game.Scripts.UI
{
    public class WaveAnnouncementView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;

        [Header("Animation Settings")]
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _displayDuration = 2.0f;
        [SerializeField] private float _punchAmount = 0.15f;

        [Header("Colors")]
        [SerializeField] private Color _normalWaveColor = Color.white;
        [SerializeField] private Color _bossWaveColor = Color.red;
        [SerializeField] private Color _waveClearColor = new Color(0.12f, 0.75f, 0.16f); // A nice gold/green color

        private EventBus _eventBus;
        private Sequence _announcementSequence;
        private GamePhase _lastPhase = GamePhase.Build;

        [Inject]
        public void Construct(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.OnWaveStarted += HandleWaveStarted;
            _eventBus.OnGamePhaseChanged += HandleGamePhaseChanged;
        }

        private void Awake()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.OnWaveStarted -= HandleWaveStarted;
                _eventBus.OnGamePhaseChanged -= HandleGamePhaseChanged;
            }
            KillActiveSequence();
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            _lastPhase = GamePhase.Combat;

            if (_canvasGroup == null || _titleText == null || _subtitleText == null) return;

            KillActiveSequence();

            _titleText.text = isBossWave ? $"BOSS WAVE {wave} START" : $"WAVE {wave} START";
            _titleText.color = isBossWave ? _bossWaveColor : _normalWaveColor;
            _subtitleText.text = "PREPARE TO DEFEND!";

            PlayAnnouncement();
        }

        private void HandleGamePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Build && _lastPhase == GamePhase.Combat)
            {
                if (_canvasGroup == null || _titleText == null || _subtitleText == null)
                {
                    _lastPhase = phase;
                    return;
                }

                KillActiveSequence();

                _titleText.text = "WAVE CLEAR!";
                _titleText.color = _waveClearColor;
                _subtitleText.text = "SHOP OPEN & RESOURCES GRANTED";

                PlayAnnouncement();
            }

            _lastPhase = phase;
        }

        private void PlayAnnouncement()
        {
            _canvasGroup.alpha = 0f;
            transform.localScale = Vector3.one;

            _announcementSequence = DOTween.Sequence();
            _announcementSequence.Append(_canvasGroup.DOFade(1f, _fadeDuration));
            _announcementSequence.Join(transform.DOPunchScale(new Vector3(_punchAmount, _punchAmount, 0f), _fadeDuration, 5, 1f));
            _announcementSequence.AppendInterval(_displayDuration);
            _announcementSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
            _announcementSequence.OnComplete(() =>
            {
                _canvasGroup.alpha = 0f;
                transform.localScale = Vector3.one;
            });
        }

        private void KillActiveSequence()
        {
            if (_announcementSequence != null)
            {
                _announcementSequence.Kill();
                _announcementSequence = null;
            }
        }
    }
}
