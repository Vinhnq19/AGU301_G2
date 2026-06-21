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
        }
    }
}
