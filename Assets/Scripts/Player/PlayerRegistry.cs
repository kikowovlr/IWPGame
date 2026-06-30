using System;
using UnityEngine;
using Unity.Cinemachine;

public static class PlayerRegistry
{
    public static event Action<Transform> OnLocalPlayerSpawned;
    public static CinemachineBrain SceneBrain { get; private set; }
    public static CinemachineCamera SceneVirtualCamera { get; private set; } 

    public static void RegisterLocalPlayerTransform(Transform transform)
    {
        OnLocalPlayerSpawned?.Invoke(transform);
    }

    public static void RegisterCameraSystem(CinemachineBrain brain, CinemachineCamera vcam)
    {
        SceneBrain = brain;
        SceneVirtualCamera = vcam;
    }
}
