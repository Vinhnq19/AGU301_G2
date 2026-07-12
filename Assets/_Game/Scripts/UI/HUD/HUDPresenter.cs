using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.UI.Base;
using DungeonBuilder.Wave;
using UnityEngine;
using VContainer.Unity;

namespace DungeonBuilder.UI.HUD
{
    public sealed class HUDPresenter : BasePresenter<HUDView, HUDModel>, IInitializable
    {
        private readonly EventBus _eventBus;
        private readonly IResourceService _resources;

        public HUDPresenter(HUDView view, HUDModel model, EventBus eventBus, IResourceService resources) : base(view, model)
        {
            _eventBus = eventBus;
            _resources = resources;
            _resources.ResourceChanged += HandleResourceChanged;
            _eventBus.OnWaveStarted += HandleWaveStarted;
            _eventBus.OnCoreHealthChanged += HandleCoreHealthChanged;
            _eventBus.OnPhaseCountdownChanged += HandlePhaseCountdownChanged;
            _eventBus.OnGamePhaseChanged += HandleGamePhaseChanged;
        }

        public override void Initialize()
        {
            foreach (var pair in _resources.GetSnapshot())
            {
                Model.SetResource(pair.Key, pair.Value);
            }

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

        public int GetTotalWaves()
        {
            return Model.TotalWaves;
        }

        public int GetCoreHealth()
        {
            return Model.CoreHealth;
        }

        public float GetCountdown()
        {
            return Model.Countdown;
        }

        public bool CanSkipBuildPhase()
        {
            return Model.Phase == GamePhase.Build;
        }

        public GamePhase GetPhase()
        {
            return Model.Phase;
        }

        public void SkipBuildPhase()
        {
            WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
            waveManager?.RequestSkipBuildPhaseServerRpc();
        }

        public override void Dispose()
        {
            _resources.ResourceChanged -= HandleResourceChanged;
            _eventBus.OnWaveStarted -= HandleWaveStarted;
            _eventBus.OnCoreHealthChanged -= HandleCoreHealthChanged;
            _eventBus.OnPhaseCountdownChanged -= HandlePhaseCountdownChanged;
            _eventBus.OnGamePhaseChanged -= HandleGamePhaseChanged;
            base.Dispose();
        }

        protected override void OnModelChanged()
        {
            View.Render();
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            Model.SetResource(change.Type, change.CurrentAmount);
        }

        private void HandleWaveStarted(int currentWave, bool isBossWave)
        {
            Model.SetWave(currentWave);

            WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
            if (waveManager != null)
            {
                Model.SetTotalWaves(waveManager.TotalWavesNetVar.Value);
            }
        }

        private void HandleCoreHealthChanged(int coreHealth)
        {
            Model.SetCoreHealth(coreHealth);
        }

        private void HandlePhaseCountdownChanged(float secondsRemaining)
        {
            // Tổng wave chưa có trước khi wave 1 bắt đầu (WaveManager spawn sau presenter)
            // — lấy một lần từ NetworkVariable ngay tick countdown đầu tiên.
            if (Model.TotalWaves <= 0)
            {
                WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
                if (waveManager != null)
                {
                    Model.SetTotalWaves(waveManager.TotalWavesNetVar.Value);
                }
            }

            Model.SetCountdown(secondsRemaining);
        }

        private void HandleGamePhaseChanged(GamePhase phase)
        {
            Model.SetPhase(phase);
        }
    }
}
