using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using TMPro;
using UnityEngine;

namespace DungeonBuilder.UI.HUD
{
    public sealed class HUDView : BaseView<HUDPresenter>
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _woodText;
        [SerializeField] private TMP_Text _stoneText;
        [SerializeField] private TMP_Text _oreText;
        [SerializeField] private TMP_Text _crystalText;

        [Header("Wave")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private TMP_Text _coreHealthText;

        private void OnDestroy()
        {
            Presenter?.Dispose();
        }

        public override void Render()
        {
            if (Presenter == null)
            {
                return;
            }

            SetText(_woodText, Presenter.GetResource(ResourceType.Wood).ToString());
            SetText(_stoneText, Presenter.GetResource(ResourceType.Stone).ToString());
            SetText(_oreText, Presenter.GetResource(ResourceType.Ore).ToString());
            SetText(_crystalText, Presenter.GetResource(ResourceType.Crystal).ToString());
            SetText(_waveText, Presenter.GetWave().ToString());
            SetText(_countdownText, Mathf.CeilToInt(Presenter.GetCountdown()).ToString());
            SetText(_coreHealthText, Presenter.GetCoreHealth().ToString());
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
