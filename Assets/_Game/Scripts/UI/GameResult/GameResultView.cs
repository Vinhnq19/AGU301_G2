using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.GameResult
{
    public sealed class GameResultView : BaseView<GameResultPresenter>
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _returnButton;

        [Header("Combat Stats")]
        [SerializeField] private TMP_Text _enemiesKilledText;
        [SerializeField] private TMP_Text _bossesKilledText;
        [SerializeField] private TMP_Text _wavesCompletedText;
        [SerializeField] private TMP_Text _towersBuiltText;

        [Header("Resources")]
        [SerializeField] private TMP_Text _woodText;
        [SerializeField] private TMP_Text _stoneText;
        [SerializeField] private TMP_Text _ironText;
        [SerializeField] private TMP_Text _copperText;
        [SerializeField] private TMP_Text _blueGemsText;
        [SerializeField] private TMP_Text _purpleGemsText;
        [SerializeField] private TMP_Text _tokenText;
        [SerializeField] private TMP_Text _coinText;

        [Header("Skills")]
        [SerializeField] private TMP_Text _miningSkillText;
        [SerializeField] private TMP_Text _forgingSkillText;

        private void OnDestroy()
        {
            Presenter?.Dispose();
        }

        protected override void OnPresenterSet()
        {
            _returnButton?.onClick.AddListener(() => Presenter.ReturnToLobby());
        }

        public override void Render()
        {
            if (Presenter == null) return;

            gameObject.SetActive(Presenter.IsVisible);

            SetText(_titleText, Presenter.IsWin ? "Victory! Core Defended!" : "Defeat! Core Destroyed!");
            SetText(_enemiesKilledText, $"Enemies Killed: {Presenter.EnemyKillCount}");
            SetText(_bossesKilledText,  $"Bosses Killed: {Presenter.BossKillCount}");
            SetText(_wavesCompletedText,$"Waves Completed: {Presenter.WavesCompleted}");
            SetText(_towersBuiltText,   $"Towers Built: {Presenter.TowersBuilt}");

            var res = Presenter.FinalResources;
            SetRes(_woodText,        "Wood",              res, ResourceType.Wood);
            SetRes(_stoneText,       "Stone",             res, ResourceType.Stone);
            SetRes(_ironText,        "Iron",              res, ResourceType.Iron);
            SetRes(_copperText,      "Copper",            res, ResourceType.Copper);
            SetRes(_blueGemsText,    "Blue Gems",         res, ResourceType.BlueGems);
            SetRes(_purpleGemsText,  "Purple Gems",       res, ResourceType.PurpleGems);
            SetRes(_tokenText,       "Token",             res, ResourceType.Token);
            SetRes(_coinText,        "Coin",              res, ResourceType.Coin);
            SetRes(_miningSkillText, "Mining Skill Lv",   res, ResourceType.MiningSkill);
            SetRes(_forgingSkillText,"Forging Skill Lv",  res, ResourceType.ForgingSkill);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }

        private static void SetRes(TMP_Text label, string name,
            System.Collections.Generic.IReadOnlyDictionary<ResourceType, int> res, ResourceType type)
        {
            if (label == null) return;
            int val = 0;
            res?.TryGetValue(type, out val);
            label.text = $"{name}: {val}";
        }
    }
}
