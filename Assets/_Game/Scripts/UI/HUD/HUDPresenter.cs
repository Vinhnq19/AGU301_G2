using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using VContainer.Unity;

namespace DungeonBuilder.UI.HUD
{
    public sealed class HUDPresenter : BasePresenter<HUDView, HUDModel>, IInitializable
    {
        private readonly EventBus _eventBus;

        public HUDPresenter(HUDView view, HUDModel model, EventBus eventBus) : base(view, model)
        {
            _eventBus = eventBus;
            _eventBus.OnResourceUpdated += HandleResourceUpdated;
            _eventBus.OnWaveStarted += HandleWaveStarted;
            _eventBus.OnCoreHealthChanged += HandleCoreHealthChanged;
            _eventBus.OnPhaseCountdownChanged += HandlePhaseCountdownChanged;
        }

        public override void Initialize()
        {
            View.SetPresenter(this);
            base.Initialize();
        }

        public int GetResource(ResourceType type)
        {
            return Model.GetResource(type);
        }

        public int GetWave()
        {
            return Model.Wave;
        }

        public int GetCoreHealth()
        {
            return Model.CoreHealth;
        }

        public float GetCountdown()
        {
            return Model.Countdown;
        }

        public override void Dispose()
        {
            _eventBus.OnResourceUpdated -= HandleResourceUpdated;
            _eventBus.OnWaveStarted -= HandleWaveStarted;
            _eventBus.OnCoreHealthChanged -= HandleCoreHealthChanged;
            _eventBus.OnPhaseCountdownChanged -= HandlePhaseCountdownChanged;
            base.Dispose();
        }

        protected override void OnModelChanged()
        {
            View.Render();
        }

        private void HandleResourceUpdated(ResourceType type, int value)
        {
            Model.SetResource(type, value);
        }

        private void HandleWaveStarted(int wave)
        {
            Model.SetWave(wave);
        }

        private void HandleCoreHealthChanged(int coreHealth)
        {
            Model.SetCoreHealth(coreHealth);
        }

        private void HandlePhaseCountdownChanged(float secondsRemaining)
        {
            Model.SetCountdown(secondsRemaining);
        }
    }
}
