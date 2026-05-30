using System;

namespace DungeonBuilder.UI.Base
{
    public abstract class BasePresenter<TView, TModel> : IDisposable
        where TModel : IModel
    {
        protected readonly TView View;
        protected readonly TModel Model;

        private bool _disposed;

        protected BasePresenter(TView view, TModel model)
        {
            View = view;
            Model = model;
            Model.OnChanged += OnModelChanged;
        }

        public virtual void Initialize()
        {
            OnModelChanged();
        }

        public virtual void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Model.OnChanged -= OnModelChanged;
            _disposed = true;
        }

        protected abstract void OnModelChanged();
    }
}
