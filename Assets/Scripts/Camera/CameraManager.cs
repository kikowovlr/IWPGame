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
    [SerializeField] private CinemachineCamera _characterSelectCam; // static character select cam
    [SerializeField] private Camera _myLocalCamera;

    [Header("Camera Settings")]
    [SerializeField] private CinemachineBlendDefinition _gameplayIntroBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.5f);
    [SerializeField] private CinemachineBlendDefinition _cutCameraBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    public CinemachineBlendDefinition GameplayIntroBlend => _gameplayIntroBlend;
    public CinemachineBlendDefinition CutCameraBlend => _cutCameraBlend;

    private int _spectatorSlot = 0;
    private CameraMode _currentMode = CameraMode.StaticOverview;
    private HashSet<string> _cursorRequests = new HashSet<string>(); // store requests for cursor -> cursor only disappears if all stop requesting
    public bool IsCursorVisible => _cursorRequests.Count > 0;

    // events
    public static event Action OnCameraSwapRequested; // flag to signal spectator input for camera swap
    public static event Action OnCameraCutExecuted; // flag to signal the actual camera swap
    private Action _pendingCameraCutAction; // holds onto context until screen goes black from blink
    public static event Action<string> OnSpectatorTargetChanged; // update 

    public enum CameraMode
    {
        Gameplay,
        StaticOverview,
        SpectatingPlayer,
        CharacterSelect
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
        HandleGameplayCameraLock();
    }

    /// <summary>
    /// swaps priority
    /// </summary>
    /// <param name="mode"></param>
    public void SetCameraState(CameraMode mode)
    {
        _currentMode = mode;
        if (_gameplayCam == null || _spectatorCam == null || _staticSpectatorCam == null || _characterSelectCam == null) return;

        // swap priority
        _gameplayCam.Priority = (mode == CameraMode.Gameplay) ? 10 : 0;
        _spectatorCam.Priority = (mode == CameraMode.SpectatingPlayer) ? 10 : 0;
        _staticSpectatorCam.Priority = (mode == CameraMode.StaticOverview) ? 10 : 0;
        _characterSelectCam.Priority = (mode == CameraMode.CharacterSelect) ? 10 : 0;

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
            case CameraMode.CharacterSelect:
                activeCam = _characterSelectCam; break;
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

        if (GameManager.Instance == null)
        {
            SetCameraState(CameraMode.Gameplay);
            return;
        }

        switch (GameManager.Instance.CurrentRoundState)
        {
            case RoundState.Setup:
                SetCameraState(CameraMode.StaticOverview);
                break;
            case RoundState.CharacterSelect:
                SetCameraState(CameraMode.CharacterSelect);
                break;
            default:
                SetCameraState(CameraMode.Gameplay);
                break;
        }
    }

    private void HandlePlayerSpectatorReady(PlayerEliminationHandler handler)
    {
        // default to static overview when first entered spectator mode
        if (!handler.Object.HasInputAuthority) return;

        OnCameraSwapRequested?.Invoke();

        _pendingCameraCutAction = () =>
        {
            _spectatorSlot = 0;
            SetCameraState(CameraMode.StaticOverview);
            OnSpectatorTargetChanged?.Invoke("OVERVIEW");
        };
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
            SpectateTarget(targetTransform);
        }
        else
        {
            SetCameraState(CameraMode.StaticOverview);
        }
    }

    private void SpectateTarget(Transform target)
    {
        if (target == null || _spectatorCam == null) return;

        Transform effectiveTarget = ResolveCameraTarget(target);

        _spectatorCam.Follow = target;
        _spectatorCam.LookAt = target;

        SetCameraState(CameraMode.SpectatingPlayer);
    }

    private Transform ResolveCameraTarget(Transform playerPivot)
    {
        PlayerDrowning drowning = PlayerRegistry.GetDrowning(playerPivot);
        if (drowning != null && drowning.IsSinking && drowning.CameraFollowProxy != null)
            return drowning.CameraFollowProxy;

        return playerPivot;
    }

    private void HandleTargetEliminated(PlayerEliminationHandler handler)
    {
        if (_currentMode != CameraMode.SpectatingPlayer || _spectatorCam.Follow == null) return;

        PlayerRef deadPlayerId = handler.Object.InputAuthority;
        Transform deadPlayerTransform = PlayerRegistry.GetAvatarTransform(deadPlayerId);

        // check if the player that js died is the one that our camera is following
        if (_spectatorCam.Follow == deadPlayerTransform)
        {
            OnCameraSwapRequested?.Invoke();

            _pendingCameraCutAction = () =>
            {
                ApplySpectatorSlot(_spectatorSlot);
                //CycleThroughLivingPlayers();
            };
        }
    }

    private void HandleGameplayCameraLock()
    {
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
        if (isLocked && _currentMode == CameraMode.Gameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            UpdateCursorState();
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

    /// <summary>
    /// swap camera to track new target
    /// used to hand off from sinking body to camera proxy
    /// </summary>
    public void SwapCameraFollowTarget(Transform oldTarget, Transform newTarget)
    {
        if (_gameplayCam != null && _gameplayCam.Follow == oldTarget)
        {
            _gameplayCam.Follow = newTarget;
            _gameplayCam.LookAt = newTarget;
        }

        if (_spectatorCam != null && _spectatorCam.Follow == oldTarget)
        {
            _spectatorCam.Follow = newTarget;
            _spectatorCam.LookAt = newTarget;
        }
    }

    /// <summary>
    /// local only - points this client's character select cam at this client's own player
    /// </summary>
    public void FocusCharacterSelectCameraOnLocalPlayer()
    {
        if (_characterSelectCam == null)
            return;

        Transform localTarget = PlayerRegistry.LocalPlayerTransform;
        if (localTarget == null)
            return;

        if (_characterSelectCam.Follow != localTarget)
        {
            _characterSelectCam.Follow = localTarget;
            _characterSelectCam.LookAt = localTarget;
        }
    }

    /// <summary>
    /// total slots = overview cams + total alive players
    /// </summary>
    private int GetSpectatorSlotCount()
    {
        var living = GameManager.Instance != null ? GameManager.Instance.GetLivingPlayerIDs() : null;
        int livingCount = living != null ? living.Count : 0;
        return 1 + livingCount; // +1 for overview
    }

    public void SpectateNext()
    {
        if (ScreenFXManager.Instance != null && ScreenFXManager.Instance.IsBlinking) return;

        int count = GetSpectatorSlotCount();
        if (count <= 1) 
        { 
            GoToSlotViaBlink(0); 
            return; 
        } // only overview available

        int next = (_spectatorSlot + 1) % count;
        GoToSlotViaBlink(next);
    }

    public void SpectatePrevious()
    {
        if (ScreenFXManager.Instance != null && ScreenFXManager.Instance.IsBlinking) return;

        int count = GetSpectatorSlotCount();
        if (count <= 1) 
        { 
            GoToSlotViaBlink(0); 
            return; 
        }

        int prev = (_spectatorSlot - 1 + count) % count;
        GoToSlotViaBlink(prev);
    }

    private void GoToSlotViaBlink(int targetSlot)
    {
        OnCameraSwapRequested?.Invoke(); // start the blink (close eye)

        _pendingCameraCutAction = () =>
        {
            ApplySpectatorSlot(targetSlot);
        };
    }

    private void ApplySpectatorSlot(int slot)
    {
        var living = GameManager.Instance != null ? GameManager.Instance.GetLivingPlayerIDs() : null;
        int livingCount = living != null ? living.Count : 0;

        // clamp in case players died since the arrow press
        int maxSlot = livingCount;
        if (slot > maxSlot) slot = 0;

        _spectatorSlot = slot;

        if (slot == 0 || livingCount == 0)
        {
            // overview
            _spectatorSlot = 0;
            SetCameraState(CameraMode.StaticOverview);
            OnSpectatorTargetChanged?.Invoke("OVERVIEW");
            return;
        }

        // player slot (1..N -> living index 0..N-1)
        int livingIndex = slot - 1;
        Fusion.PlayerRef targetId = living[livingIndex];
        Transform targetTransform = PlayerRegistry.GetAvatarTransform(targetId);

        if (targetTransform != null)
        {
            SpectateTarget(targetTransform);
            OnSpectatorTargetChanged?.Invoke(ResolveSpectatorName(targetId));
        }
        else
        {
            // target vanished -> fall back to overview
            _spectatorSlot = 0;
            SetCameraState(CameraMode.StaticOverview);
            OnSpectatorTargetChanged?.Invoke("OVERVIEW");
        }
    }

    private string ResolveSpectatorName(Fusion.PlayerRef playerId)
    {
        if (GameManager.Instance != null && GameManager.Instance.Runner != null
            && GameManager.Instance.Runner.TryGetPlayerObject(playerId, out NetworkObject obj))
        {
            PlayerComponentRegistry reg = obj.GetComponent<PlayerComponentRegistry>();
            if (reg != null && reg.Stats != null && !string.IsNullOrEmpty(reg.Stats.PlayerName))
                return reg.Stats.PlayerName;
        }
        return $"Player {playerId.PlayerId}";
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
