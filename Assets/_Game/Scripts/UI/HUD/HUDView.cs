using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.HUD
{
    public sealed class HUDView : BaseView<HUDPresenter>
    {
        [Header("Resources")]
        [SerializeField] private TMP_Text _woodText;
        [SerializeField] private TMP_Text _stoneText;
        [SerializeField] private TMP_Text _ironText;
        [SerializeField] private TMP_Text _copperText;
        [SerializeField] private TMP_Text _blueGemsText;
        [SerializeField] private TMP_Text _purpleGemsText;
        [SerializeField] private TMP_Text _coinText;
        [SerializeField] private TMP_Text _tokenText;

        [Header("Skills")]
        [SerializeField] private TMP_Text _miningSkillText;
        [SerializeField] private TMP_Text _forgingSkillText;

        [Header("Wave")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private TMP_Text _coreHealthText;
        [SerializeField] private Button _skipButton;

        [Header("Minimap")]
        [SerializeField] private UnityEngine.UI.RawImage _minimapRawImage;

        private void Awake()
        {
            _skipButton?.onClick.AddListener(OnSkipClicked);
        }

        private void OnDestroy()
        {
            _skipButton?.onClick.RemoveListener(OnSkipClicked);
            Presenter?.Dispose();
        }

        private void OnSkipClicked()
        {
            Presenter?.SkipBuildPhase();
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
            SetText(_copperText, Presenter.GetResource(ResourceType.Copper).ToString());
            SetText(_blueGemsText, Presenter.GetResource(ResourceType.BlueGems).ToString());
            SetText(_purpleGemsText, Presenter.GetResource(ResourceType.PurpleGems).ToString());
            SetText(_coinText, Presenter.GetResource(ResourceType.Coin).ToString());
            SetText(_tokenText, Presenter.GetResource(ResourceType.Token).ToString());
            SetText(_miningSkillText, Presenter.GetResource(ResourceType.MiningSkill).ToString());
            SetText(_forgingSkillText, Presenter.GetResource(ResourceType.ForgingSkill).ToString());
            SetText(_waveText, Presenter.GetWave().ToString() + "/10");
            SetText(_countdownText, Mathf.CeilToInt(Presenter.GetCountdown()).ToString());
            SetText(_coreHealthText, Presenter.GetCoreHealth().ToString());

            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(Presenter.CanSkipBuildPhase());
            }
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
