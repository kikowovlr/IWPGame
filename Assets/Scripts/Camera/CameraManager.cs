using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

[RequireComponent(typeof(CinemachineCamera))]
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Camera references")]
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private CinemachineCamera _gameplayCam; // follow player cam
    [SerializeField] private CinemachineCamera _staticSpectatorCam; // static cam looking down at the arena
    [SerializeField] private CinemachineCamera _spectatorCam; // reusable orbital cam for spectating specific players

    private int _currentSpectatorIndex = -1;

    public enum CameraMode
    {
        Gameplay,
        StaticOverview,
        SpectatingPlayer
    }

    private CameraMode _currentMode = CameraMode.StaticOverview;

    private void Awake()
    {
        if (Instance == null) 
            Instance = this;
        else 
            Destroy(gameObject);

        if (_brain == null && Camera.main != null)
        {
            Camera.main.TryGetComponent(out _brain);
        }

        // init camera
        SetCameraState(CameraMode.StaticOverview);

        //if (Camera.main != null)
        //{
        //    if (Camera.main.TryGetComponent<CinemachineBrain>(out CinemachineBrain brain))
        //    {
        //        _brain = brain;
        //        PlayerRegistry.RegisterCameraSystem(_brain, _vcam);
        //    }
        //}
    }

    private void OnEnable()
    {
        // listen to local player spawn
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        PlayerEliminationHandler.OnPlayerSpectatorReady += HandlePlayerSpectatorReady;
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        PlayerEliminationHandler.OnPlayerSpectatorReady -= HandlePlayerSpectatorReady;
    }

    private void Update()
    {
        // restrict spectator mouse tracking input
    }

    private void HandleLocalPlayerSpawned(Transform target)
    {
        _vcam.Follow = target;
    }
}
