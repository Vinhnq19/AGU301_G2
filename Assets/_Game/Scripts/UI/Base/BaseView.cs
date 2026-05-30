using UnityEngine;

namespace DungeonBuilder.UI.Base
{
    public abstract class BaseView<TPresenter> : MonoBehaviour
    {
        protected TPresenter Presenter { get; private set; }

        public void SetPresenter(TPresenter presenter)
        {
            Presenter = presenter;
            OnPresenterSet();
        }

        public abstract void Render();

        protected virtual void OnPresenterSet()
        {
        }
    }
}
