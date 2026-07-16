using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System;
using Fusion;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Camera references")]
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private CinemachineCamera _gameplayCam; // follow player cam
    [SerializeField] private CinemachineCamera _staticSpectatorCam; // static cam looking down at the arena
    [SerializeField] private CinemachineCamera _spectatorCam; // reusable orbital cam for spectating specific players
    [SerializeField] private Camera _myLocalCamera;
    [SerializeField] private CinemachineInputAxisController _axisController;

    [Header("Camera Settings")]
    [SerializeField] private CinemachineBlendDefinition _gameplayIntroBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.5f);
    [SerializeField] private CinemachineBlendDefinition _cutCameraBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    public CinemachineBlendDefinition GameplayIntroBlend => _gameplayIntroBlend;
    public CinemachineBlendDefinition CutCameraBlend => _cutCameraBlend;

    private int _currentSpectatorIndex = -1;
    private CameraMode _currentMode = CameraMode.StaticOverview;
    private SpectatorViewMode _currentViewMode = SpectatorViewMode.Overview;

    private HashSet<string> _cursorRequests = new HashSet<string>(); // store requests for cursor -> cursor only disappears if all stop requesting
    public bool IsCursorVisible => _cursorRequests.Count > 0;

    // events
    public static event Action OnCameraSwapRequested; // flag to signal spectator input for camera swap
    public static event Action OnCameraCutExecuted; // flag to signal the actual camera swap
    private Action _pendingCameraCutAction; // holds onto context until screen goes black from blink

    public enum CameraMode
    {
        Gameplay,
        StaticOverview,
        SpectatingPlayer
    }

    private enum SpectatorViewMode
    {
        Overview,
        Player
    }


    public Vector3 GetCameraForward()
    {
        if (_myLocalCamera != null)
        {
            Vector3 forward = _myLocalCamera.transform.forward;
            forward.y = 0;
            return forward.normalized;
        }

        return Vector3.forward;
    }

    public Vector3 GetCameraRight()
    {
        if (_myLocalCamera != null)
        {
            Vector3 right = _myLocalCamera.transform.right;
            right.y = 0; 
            return right.normalized;
        }

        return Vector3.right;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (_brain == null)
        {
            _brain = FindAnyObjectByType<CinemachineBrain>();
        }

        if (_brain != null && _myLocalCamera == null)
        {
            _myLocalCamera = _brain.GetComponent<Camera>();
        }

        // init camera
        SetCameraState(CameraMode.StaticOverview);
    }

    private void OnEnable()
    {
        // listen to local player spawn
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        PlayerEliminationHandler.OnPlayerSpectatorReady += HandlePlayerSpectatorReady;
        ScreenFXManager.OnPeakDarknessReached += ExecutePendingCameraCut;
        PlayerEliminationHandler.OnPlayerEliminated += HandleTargetEliminated;
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        PlayerEliminationHandler.OnPlayerSpectatorReady -= HandlePlayerSpectatorReady;
        ScreenFXManager.OnPeakDarknessReached -= ExecutePendingCameraCut;
        PlayerEliminationHandler.OnPlayerEliminated -= HandleTargetEliminated;
    }

    private void Update()
    {
        // handle spectator input
        if (_currentMode == CameraMode.StaticOverview || _currentMode == CameraMode.SpectatingPlayer)
        {
            HandleSpectatorInput();
        }

        HandleGameplayCameraLock();
    }

    /// <summary>
    /// swaps priority
    /// </summary>
    /// <param name="mode"></param>
    public void SetCameraState(CameraMode mode)
    {
        _currentMode = mode;
        if (_gameplayCam == null || _spectatorCam == null || _staticSpectatorCam == null) return;

        // swap priority
        _gameplayCam.Priority = (mode == CameraMode.Gameplay) ? 10 : 0;
        _spectatorCam.Priority = (mode == CameraMode.SpectatingPlayer) ? 10 : 0;
        _staticSpectatorCam.Priority = (mode == CameraMode.StaticOverview) ? 10 : 0;

        //// handle mouse cursor locking based on mode
        //if (mode == CameraMode.StaticOverview)
        //{
        //    // free cursor
        //    Cursor.lockState = CursorLockMode.None;
        //    Cursor.visible = true;
        //}
        //else
        //{
        //    // lock cursor n hide to allow for free camera movement
        //    Cursor.lockState = CursorLockMode.Locked;
        //    Cursor.visible = false;
        //}

        UpdateCursorState();

        // identify active cam target
        CinemachineCamera activeCam = null;
        switch (mode)
        {
            case CameraMode.StaticOverview:
                activeCam = _staticSpectatorCam; break;
            case CameraMode.SpectatingPlayer:
                activeCam = _spectatorCam; break;
            case CameraMode.Gameplay:
                activeCam = _gameplayCam; break;
        }

        // update player registry
        if (_brain != null && activeCam != null)
        {
            PlayerRegistry.RegisterCameraSystem(_brain, activeCam);
        }
    }

    private void HandleLocalPlayerSpawned(Transform target)
    {
        _gameplayCam.Follow = target;
        _gameplayCam.LookAt = target;

        // if in setup state, start in static overview
        if (GameManager.Instance != null && GameManager.Instance.CurrentRoundState == RoundState.Setup)
            SetCameraState(CameraMode.StaticOverview);
        else
            SetCameraState(CameraMode.Gameplay);
    }

    private void HandlePlayerSpectatorReady(PlayerEliminationHandler handler)
    {
        // default to static overview when first entered spectator mode
        if (!handler.Object.HasInputAuthority) return;

        OnCameraSwapRequested?.Invoke();

        _pendingCameraCutAction = () =>
        {
            _currentViewMode = SpectatorViewMode.Overview;
            SetCameraState(CameraMode.StaticOverview);
        };
    }

    private void HandleSpectatorInput()
    {
        if (GameManager.Instance == null || ScreenFXManager.Instance.IsBlinking) return; // dont allow toggle if blinking is active

        var livingPlayerIDs = GameManager.Instance.GetLivingPlayerIDs();
        int livingCount = livingPlayerIDs != null ? livingPlayerIDs.Count : 0;

        // right click -> toggle spectator mode
        if (Input.GetMouseButtonDown(1))
        {
            // if trying to switch but no players alive, dont switch
            if (_currentViewMode == SpectatorViewMode.Overview && livingCount == 0) return;

            OnCameraSwapRequested?.Invoke(); // signal to close eye/blink

            // holds context until this event is called
            _pendingCameraCutAction = () =>
            {
                if (_currentViewMode == SpectatorViewMode.Overview)
                {
                    // switch to player tracking mode
                    _currentViewMode = SpectatorViewMode.Player;
                    _currentSpectatorIndex = 0; // reset loop tracking
                    SpectateFirstAvailablePlayer();
                }
                else
                {
                    // switch back to standard bird's eye view
                    _currentViewMode = SpectatorViewMode.Overview;
                    SetCameraState(CameraMode.StaticOverview);
                }
            };

            return; // prevent execution cross-over on this frame
        }

        // left click -> cycling through cams
        if (Input.GetMouseButtonDown(0))
        {
            if (_currentViewMode == SpectatorViewMode.Player)
            {
                if (livingCount <= 1) return;

                OnCameraSwapRequested?.Invoke(); // start closing

                // holds context until this event is called
                _pendingCameraCutAction = () =>
                {
                    CycleThroughLivingPlayers();
                };
            }
            else if (_currentViewMode == SpectatorViewMode.Overview)
            {
                if (_currentMode == CameraMode.StaticOverview) return; // if alrdy looking at overview cam, dont change

                OnCameraSwapRequested?.Invoke(); // start closing

                // holds context until this event is called
                _pendingCameraCutAction = () =>
                {
                    // if more static cams added, cycle them here
                    SetCameraState(CameraMode.StaticOverview);
                };
            }
        }
    }

    /// <summary>
    /// called when blink covers 100% of viewport (blink progress == 1)
    /// </summary>
    private void ExecutePendingCameraCut()
    {
        if (_pendingCameraCutAction != null)
        {
            _pendingCameraCutAction.Invoke(); // execute camera switch
            _pendingCameraCutAction = null; // clear container

            OnCameraCutExecuted?.Invoke(); // tells shader to open eye (blink progress to 0)
        }
    }

    private void CycleThroughLivingPlayers()
    {
        var livingPlayerIDs = GameManager.Instance.GetLivingPlayerIDs();

        // if no one is alive, fall back to overview
        if (livingPlayerIDs == null || livingPlayerIDs.Count == 0)
        {
            // if everyone is dead, use static view
            _currentViewMode = SpectatorViewMode.Overview;
            SetCameraState(CameraMode.StaticOverview);
            return;
        }

        // cycle through spectator cams
        // loop through to find valid transform
        int currentIndex = -1;

        // look up current target pos in new list
        if (_spectatorCam.Follow != null)
        {
            // find wat player we are spectating ow
            for (int i = 0; i < livingPlayerIDs.Count; i++)
            {
                if (PlayerRegistry.GetAvatarTransform(livingPlayerIDs[i]) == _spectatorCam.Follow)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // loop forward from current spot to find next valid target
        int nextIndex = (currentIndex + 1) % livingPlayerIDs.Count;
        Fusion.PlayerRef nextTargetID = livingPlayerIDs[nextIndex];

        Transform targetTransform = PlayerRegistry.GetAvatarTransform(nextTargetID);
        if (targetTransform != null)
        {
            _currentSpectatorIndex = nextIndex; 
            SpectateTarget(targetTransform);
        }
        else
        {
            _currentViewMode = SpectatorViewMode.Overview;
            SetCameraState(CameraMode.StaticOverview);
        }
    }

    private void SpectateFirstAvailablePlayer()
    {
        var livingPlayerIDs = GameManager.Instance.GetLivingPlayerIDs();
        if (livingPlayerIDs == null || livingPlayerIDs.Count == 0)
        {
            _currentViewMode = SpectatorViewMode.Overview;
            SetCameraState(CameraMode.StaticOverview);
            return;
        }

        _currentSpectatorIndex = -1;
        CycleThroughLivingPlayers();
    }

    private void SpectateTarget(Transform target)
    {
        if (target != null && _spectatorCam != null)
        {
            _spectatorCam.Follow = target;
            _spectatorCam.LookAt = target;

            SetCameraState(CameraMode.SpectatingPlayer);
        }
    }

    private void HandleTargetEliminated(PlayerEliminationHandler handler)
    {
        if (_currentMode != CameraMode.SpectatingPlayer || _spectatorCam.Follow == null) return;

        PlayerRef deadPlayerId = handler.Object.InputAuthority;
        Transform deadPlayerTransform = PlayerRegistry.GetAvatarTransform(deadPlayerId);

        // check if the player that js died is the one that our camera is following
        if (_spectatorCam.Follow == deadPlayerTransform)
        {
            Debug.Log($"[CameraManager] The player we are currently spectating ({deadPlayerId}) was eliminated! Triggering auto-swap.");
            OnCameraSwapRequested?.Invoke();

            _pendingCameraCutAction = () =>
            {
                CycleThroughLivingPlayers();
            };
        }
    }

    private void HandleGameplayCameraLock()
    {
        if (_axisController == null) return;

        bool shouldLockCamera = false;

            Transform localTransform = PlayerRegistry.LocalPlayerTransform;

            if (localTransform != null)
            {
                // check interface in parent
                ICameraLockable lockableEntity = localTransform.GetComponentInParent<ICameraLockable>();

                if (lockableEntity != null)
                {
                    shouldLockCamera = lockableEntity.IsCameraRotationLocked;
                }
            }

        SetCinemachineInputLocked(shouldLockCamera);
    }

    private void SetCinemachineInputLocked(bool isLocked)
    {
        // ensure input is enabled if we arent in gameplay
        _axisController.enabled = !isLocked;

        if (isLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (_currentMode == CameraMode.Gameplay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void SetDefaultBlendStyle(CinemachineBlendDefinition blend)
    {
        if (_brain != null)
            _brain.DefaultBlend = blend;
    }

    public void RequestCursorVisible(string requesterId, bool visible)
    {
        if (visible)
            _cursorRequests.Add(requesterId);
        else
            _cursorRequests.Remove(requesterId);

        UpdateCursorState();
    }

    private void UpdateCursorState()
    {
        bool showCursor = IsCursorVisible || _currentMode != CameraMode.Gameplay;

        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = showCursor;
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
