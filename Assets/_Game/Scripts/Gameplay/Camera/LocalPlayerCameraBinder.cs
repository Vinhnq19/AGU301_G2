using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Gameplay.Camera
{
    public sealed class LocalPlayerCameraBinder : NetworkBehaviour
    {
        private CinemachineCamera _cinemachineCamera;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (cameras.Length == 0)
            {
                Debug.LogWarning(
                    "[LocalPlayerCameraBinder] No active CinemachineCamera was found in the scene.",
                    this);
                return;
            }

            if (cameras.Length > 1)
            {
                Debug.LogWarning(
                    $"[LocalPlayerCameraBinder] Expected one active CinemachineCamera, but found {cameras.Length}. " +
                    "The local player camera target was not assigned.",
                    this);
                return;
            }

            _cinemachineCamera = cameras[0];
            _cinemachineCamera.Follow = transform;
        }

        public override void OnNetworkDespawn()
        {
            if (_cinemachineCamera != null && _cinemachineCamera.Follow == transform)
            {
                _cinemachineCamera.Follow = null;
            }

            _cinemachineCamera = null;
        }
    }
}
