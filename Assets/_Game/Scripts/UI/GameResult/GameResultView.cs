using System.Text;
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

        [Header("Stats")]
        [SerializeField] private TMP_Text _enemiesKilledText;
        [SerializeField] private TMP_Text _bossesKilledText;
        [SerializeField] private TMP_Text _wavesCompletedText;
        [SerializeField] private TMP_Text _towersBuiltText;
        [SerializeField] private TMP_Text _resourcesText;
        [SerializeField] private TMP_Text _skillsText;

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

            if (_titleText != null)
                _titleText.text = Presenter.IsWin
                    ? "Victory! Core Defended!"
                    : "Defeat! Core Destroyed!";

            if (_enemiesKilledText != null)
                _enemiesKilledText.text = $"Enemies Killed: {Presenter.EnemyKillCount}";

            if (_bossesKilledText != null)
                _bossesKilledText.text = $"Bosses Killed: {Presenter.BossKillCount}";

            if (_wavesCompletedText != null)
                _wavesCompletedText.text = $"Waves Completed: {Presenter.WavesCompleted}";

            if (_towersBuiltText != null)
                _towersBuiltText.text = $"Towers Built: {Presenter.TowersBuilt}";

            if (_resourcesText != null)
                _resourcesText.text = BuildResourcesText();

            if (_skillsText != null)
                _skillsText.text = BuildSkillsText();
        }

        private string BuildResourcesText()
        {
            var res = Presenter.FinalResources;
            if (res == null) return "Resources: -";

            var sb = new StringBuilder("Resources:\n");
            AppendIfNonZero(sb, res, ResourceType.Wood,      "Wood");
            AppendIfNonZero(sb, res, ResourceType.Stone,     "Stone");
            AppendIfNonZero(sb, res, ResourceType.Ore,       "Ore");
            AppendIfNonZero(sb, res, ResourceType.Iron,      "Iron");
            AppendIfNonZero(sb, res, ResourceType.Copper,    "Copper");
            AppendIfNonZero(sb, res, ResourceType.Crystal,   "Crystal");
            AppendIfNonZero(sb, res, ResourceType.BlueGems,  "Blue Gems");
            AppendIfNonZero(sb, res, ResourceType.PurpleGems,"Purple Gems");
            AppendIfNonZero(sb, res, ResourceType.Token,     "Token");
            AppendIfNonZero(sb, res, ResourceType.Coin,      "Coin");
            return sb.ToString().TrimEnd();
        }

        private string BuildSkillsText()
        {
            var res = Presenter.FinalResources;
            if (res == null) return "Skills: -";

            res.TryGetValue(ResourceType.MiningSkill,  out int mining);
            res.TryGetValue(ResourceType.ForgingSkill, out int forging);
            return $"Skills:\nMining: Lv.{mining}  Forging: Lv.{forging}";
        }

        private static void AppendIfNonZero(StringBuilder sb, System.Collections.Generic.IReadOnlyDictionary<ResourceType, int> res, ResourceType type, string label)
        {
            res.TryGetValue(type, out int val);
            if (val > 0) sb.AppendLine($"  {label}: {val}");
        }
    }
}
