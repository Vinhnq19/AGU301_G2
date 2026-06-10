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
        [SerializeField] private TMP_Text _ironText;
        [SerializeField] private TMP_Text _blueGemsText;
        [SerializeField] private TMP_Text _copperText;

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
            SetText(_ironText, Presenter.GetResource(ResourceType.Iron).ToString());
            SetText(_blueGemsText, Presenter.GetResource(ResourceType.BlueGems).ToString());
            SetText(_copperText, Presenter.GetResource(ResourceType.Copper).ToString());
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
