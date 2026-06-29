using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Core.Debugging
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class DebugGameInput : MonoBehaviour
    {
        private EventBus _eventBus;

        private void Start()
        {
            var scope = LifetimeScope.Find<DungeonBuilder.Networking.Scopes.GameLifetimeScope>();
            _eventBus = scope?.Container.Resolve<EventBus>();
        }

        private int _debugWave = 1;

        private void Update()
        {
            if (_eventBus == null) return;
            if (Input.GetKeyDown(KeyCode.F1)) _eventBus.RaiseGameEnded(true);
            if (Input.GetKeyDown(KeyCode.F2)) _eventBus.RaiseGameEnded(false);
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _eventBus.RaiseWaveStarted(_debugWave, false);
                Debug.Log($"[Debug] RaiseWaveStarted wave={_debugWave}");
                _debugWave++;
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _eventBus.RaiseSkipBuildPhase();
                Debug.Log("[Debug] Skip build phase triggered.");
            }
        }
    }
#endif
}
