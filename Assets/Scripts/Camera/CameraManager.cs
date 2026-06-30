using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CameraManager : MonoBehaviour
{
    private CinemachineBrain _brain;
    private CinemachineCamera _vcam;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineCamera>();

        if (Camera.main != null)
        {
            if (Camera.main.TryGetComponent<CinemachineBrain>(out CinemachineBrain brain))
            {
                _brain = brain;
                PlayerRegistry.RegisterCameraSystem(_brain, _vcam);
                Utils.DebugLog("CameraManager: Successfully found Brain on Main Camera and registered system!");
            }
        }
    }

    private void OnEnable()
    {
        // listen to local player spawn
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
    }

    private void HandleLocalPlayerSpawned(Transform target)
    {
        _vcam.Follow = target;
    }
}
