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

        private void Update()
        {
            if (_eventBus == null) return;
            if (Input.GetKeyDown(KeyCode.F1)) _eventBus.RaiseGameEnded(true);
            if (Input.GetKeyDown(KeyCode.F2)) _eventBus.RaiseGameEnded(false);
        }
    }
#endif
}
