using UnityEngine;
using DG.Tweening;

namespace Assets._Game.Scripts.UI.Tutorial
{
    public class PulsingWorldUI : MonoBehaviour
    {
        [SerializeField] private float _pulseScale = 1.2f;
        [SerializeField] private float _duration = 0.8f;

        private void Start()
        {
            transform.DOScale(transform.localScale * _pulseScale, _duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}