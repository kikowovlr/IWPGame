using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TutorialManager : NetworkBehaviour, IMatchContext, ICountdownSource
{
    public static TutorialManager Instance { get; private set; }

    [Header("Tutorial Steps")]
    [SerializeField] private TutorialStepSO[] _steps;

    [Header("Sudden Death")]
    [SerializeField] private Transform[] _suddenDeathSpawnPoints;

    [Header("Durations")]
    [SerializeField] private MatchSettings _settings;

    [Header("References")]
    [SerializeField] private TutorialUIController _tutorialUI;
    [SerializeField] private RoundEndDisplayController _roundEndDisplay;

    [Header("Scenes")]
    [SerializeField] private string _mainMenuScenePath;
    [SerializeField] private int _mainMenuSceneBuildIndex;


    [HideInInspector][Networked, OnChangedRender(nameof(OnStepChanged))]
    public int CurrentStepIndex { get; private set; }

    [Networked] private TickTimer _stateTimer { get; set; }
    [HideInInspector][Networked] public PlayerRef MatchWinner { get; private set; }

    private readonly Dictionary<TutorialState, ITutorialState> _states = new();
    private ITutorialState _activeState;

    public int TotalSteps => _steps != null ? _steps.Length : 0;
    public TutorialStepSO CurrentStep =>
        (_steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < _steps.Length)
        ? _steps[CurrentStepIndex] : null;

    public const int MAX_PLAYERS = 4;

    [Networked, Capacity(MAX_PLAYERS), OnChangedRender(nameof(OnActivePlayersChanged))]
    private NetworkArray<PlayerRef> _livingPlayers => default;
    private readonly HashSet<PlayerRef> _localLiving = new HashSet<PlayerRef>();
    [Networked] private NetworkBool _isInFinalCountdown { get; set; }

    [Header("Character Select")]
    [SerializeField] private Transform[] _characterSelectStagePoints;
    [Networked] private NetworkBool _hasCompletedCharacterSelect { get; set; }

    // state tracker
    [HideInInspector] [Networked, OnChangedRender(nameof(OnTutorialStateChanged))] public TutorialState CurrentState { get; private set; }
    [HideInInspector] [Networked, OnChangedRender(nameof(OnStepPhaseChanged))] public TutorialStepPhase CurrentStepPhase { get; private set; }
    [Networked] private TickTimer _phaseTimer { get; set; }
    private bool _firstRoundInitialised;
    private bool _transitionInProgress;

    // sudden death
    [Header("Sudden Death")]
    [SerializeField] private GameObject _tutorialWalls;
    [HideInInspector] [Networked, OnChangedRender(nameof(OnSuddenDeathPhaseChanged))] public SuddenDeathPhase CurrentSuddenDeathPhase { get; private set; }
    public bool IsSuddenDeathCountdownPhase => CurrentSuddenDeathPhase == SuddenDeathPhase.Countdown;
    public bool IsSuddenDeathGoMoment => CurrentSuddenDeathPhase == SuddenDeathPhase.Fighting;

    [Header("Tutorial Spawning - Boost Pads")]
    [SerializeField] private BoostPad _boostPadPrefab;
    [SerializeField] private Transform[] _boostPadSpawnPoints;
    private readonly List<NetworkObject> _spawnedBoostPads = new List<NetworkObject>();

    [Header("Tutorial Spawning - Bots")]
    [SerializeField] private NetworkObject _botPrefab;
    [SerializeField] private Transform[] _botSpawnPoints;
    private readonly List<NetworkObject> _spawnedBots = new List<NetworkObject>();
    private bool _botsSpawned;

    [HideInInspector] [Networked] public NetworkBool WasSoloTutorial { get; private set; }

    // getters
    public float CharacterSelectDuration => _settings.CharacterSelectDuration;
    public float CountdownDuration => _settings.CountdownDuration;
    public float MatchOverDuration => _settings.MatchOverBufferDuration;
    public bool IsStateTimerExpired => _stateTimer.ExpiredOrNotRunning(Runner);
    public RoundEndDisplayController RoundEndDisplay => _roundEndDisplay;
    public TutorialUIController TutorialUI => _tutorialUI;
    public MatchSettings Settings => _settings;
    public bool IsInGameplayPhase =>
        CurrentState == TutorialState.TutorialActive || CurrentState == TutorialState.SuddenDeath;
    public bool IsInCharacterSelect => CurrentState == TutorialState.CharacterSelect;
    public bool IsInFinalCharacterSelectCountdown => _isInFinalCountdown;
    public float GetRemainingStateTime() => _stateTimer.RemainingTime(Runner) ?? 0f;
    public List<PlayerRef> GetLivingPlayerIDs() => new List<PlayerRef>(_localLiving);
    public int GetLivingPlayerCount() => _localLiving.Count;
    public bool IsSpawned { get; private set; }
    public bool IsCountdownActive => CurrentSuddenDeathPhase == SuddenDeathPhase.Countdown;
    public bool ShouldShowGo => CurrentSuddenDeathPhase == SuddenDeathPhase.Fighting;
    public float CountdownRemaining => GetRemainingStateTime();
    public float CountdownTotal => Settings.CountdownDuration;
    public bool IsIntroPhase => CurrentStepPhase == TutorialStepPhase.Intro;
    public bool IsNarratingPhase => CurrentStepPhase == TutorialStepPhase.Narrating;
    public bool IsTrackingPhase => CurrentStepPhase == TutorialStepPhase.Tracking;
    public bool IsStepCompletePhase => CurrentStepPhase == TutorialStepPhase.StepComplete;
    public bool IsPhaseTimerExpired => _phaseTimer.ExpiredOrNotRunning(Runner);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _states[TutorialState.CharacterSelect] = new TutorialCharacterSelectState();
        _states[TutorialState.TutorialActive] = new TutorialActiveState();
        _states[TutorialState.SuddenDeath] = new TutorialSuddenDeathState();
        _states[TutorialState.TutorialStageOver] = new TutorialStageOverState();
    }

    public override void Spawned()
    {
        Instance = this;

        // register
        IsSpawned = true;
        MatchContext.Register(this);
        CountdownSourceLocator.Register(this);

        if (Object.HasStateAuthority)
        {
            foreach (var player in Object.Runner.ActivePlayers)
            {
                TrackPlayer(player);
            }
        }

        RebuildLocalLiving();

        // catch up if only needed
        if (CurrentState != TutorialState.None && _states.TryGetValue(CurrentState, out ITutorialState initialRoundState))
        {
            Debug.Log($"[MATCH LOCAL] -> Initializing First Frame State: {CurrentState}");
            initialRoundState.OnStateEnter(this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        MatchContext.Unregister(this);
        CountdownSourceLocator.Unregister(this);
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        PlayerEliminationHandler.OnPlayerEliminated += HandlePlayerEliminated;
    }
    private void OnDisable()
    {
        PlayerEliminationHandler.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F9))   
        {
            if (Object == null || !Object.IsValid) return;

            if (Object.HasStateAuthority)
                DebugSkipCurrentStep();    
            else
                Rpc_RequestSkipStep();   
        }

        // F10 = skip all steps -> SuddenDeath
        if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
        {
            if (Object.HasStateAuthority) DebugSkipToSuddenDeath();
            else Rpc_RequestSkipToSuddenDeath();
        }
#endif
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (!_firstRoundInitialised)
        {
            int activeNetworkPlayers = Object.Runner.ActivePlayers.Count();
            int fullyTrackedCount = GetTotalTrackedPlayerCount();

            if (activeNetworkPlayers > 0 && fullyTrackedCount >= activeNetworkPlayers && AreAllPlayerAvatarsSpawned())
            {
                _firstRoundInitialised = true;
                TransitionToState(TutorialState.CharacterSelect, CharacterSelectDuration);
            }
            else
            {
                return;   // not ready yet — wait for next tick
            }
        }

        _activeState?.OnStateUpdate(this);
    }

#region TRANSITIONS
    public void TransitionToState(TutorialState next, float timerDuration = 0f)
    {
        if (!Object.HasStateAuthority) return;

        _activeState?.OnStateExit(this);
        CurrentState = next;
        if (timerDuration > 0f)
            _stateTimer = TickTimer.CreateFromSeconds(Runner, timerDuration);

        EnterStateLocal(next);
    }

    private void EnterStateLocal(TutorialState state)
    {
        if (_states.TryGetValue(state, out var impl))
        {
            _activeState = impl;
            _activeState.OnStateEnter(this);
        }
    }

    private void OnTutorialStateChanged()
    {
        if (Object.HasStateAuthority) return;   // host already entered in TransitionToState
        EnterStateLocal(CurrentState);
    }

    private void OnStepChanged()
    {
        if (_tutorialUI != null)
            _tutorialUI.ShowStep(CurrentStep, CurrentStepIndex, TotalSteps, GetCompletionCountForCurrentStep(), GetRealPlayerCount());

        // spawn practice content based on the step's required action
        if (Object.HasStateAuthority && CurrentStep != null)
        {
            if (CurrentStep.RequiredAction == TutorialActionType.Punch)
                SpawnBotsToFill();       // bots from punch step onward (spawn once, persist)

            if (CurrentStep.RequiredAction == TutorialActionType.Environment)
                SpawnBoostPads();        // boost pads at the environment step
        }
    }

    public void ResetStateTimer(float duration)
    {
        if (!Object.HasStateAuthority) return;
        _stateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public void SetGlobalInputRestrictions(InputRestrictions restrictions)
    {
        if (!Object.HasStateAuthority) return;
        if (MatchRestrictionsProvider.Instance != null)
            MatchRestrictionsProvider.Instance.SetRestrictions(restrictions);
    }
#endregion

    public bool AreAllPlayersReady()
    {
        int active = 0;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            active++;
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                PlayerCharacterSelect sel = obj.GetComponentInChildren<PlayerCharacterSelect>();
                if (sel == null || !sel.IsReadyToStart)
                    return false;
            }
            else return false;
        }
        return active > 0;
    }

    public void AutoLockUnreadyPlayers()
    {
        if (!Object.HasStateAuthority) return;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                PlayerCharacterSelect sel = obj.GetComponentInChildren<PlayerCharacterSelect>();
                if (sel != null && !sel.IsReadyToStart)
                    sel.ForceLock();
            }
        }
    }

    public int GetRealPlayerCount() => Runner.ActivePlayers.Count();

    public bool HaveAllPlayersCompletedCurrentStep() => GetCompletionCountForCurrentStep() >= GetRealPlayerCount();

    /// <summary>
    /// disable elimination during tutorial active stage!
    /// </summary>
    public bool EliminationDisabled => CurrentState == TutorialState.TutorialActive;

    public void TeleportPlayersToSuddenDeathPositions()
    {
        if (!Object.HasStateAuthority) return;
        if (_suddenDeathSpawnPoints == null || _suddenDeathSpawnPoints.Length == 0) return;

        int i = 0;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                Transform target = _suddenDeathSpawnPoints[i % _suddenDeathSpawnPoints.Length];
                PlayerComponentRegistry registry = obj.transform.root.GetComponent<PlayerComponentRegistry>();
                if (registry.RespawnHandler != null)
                    registry.RespawnHandler.TeleportToSpawnPoint(target.position, target.rotation);
            }
            i++;
        }
    }

    public void TeleportPlayersToCharacterSelectStage()
    {
        if (!Object.HasStateAuthority) return;

        var sortedPlayers = Object.Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();

        for (int i = 0; i < sortedPlayers.Count && i < _characterSelectStagePoints.Length; i++)
        {
            if (Object.Runner.TryGetPlayerObject(sortedPlayers[i], out NetworkObject playerObj))
            {
                if (playerObj.TryGetComponent(out PlayerComponentRegistry registry) && registry.RespawnHandler != null)
                {
                    registry.RespawnHandler.TeleportToSpawnPoint(_characterSelectStagePoints[i].position, _characterSelectStagePoints[i].rotation);
                }
            }
        }
    }

    #region CHARACTER SELECT
    public void ResetAllPlayersCharacterSelectState()
    {
        if (!Object.HasStateAuthority) return;

        // build a fresh shuffled pool of color indices, once, for this match so every player this round gets a guaranteed-unique color
        var activePlayerRefs = Object.Runner.ActivePlayers.ToList();

        int paletteSize = 0;
        foreach (PlayerRef pRef in activePlayerRefs)
        {
            if (Object.Runner.TryGetPlayerObject(pRef, out NetworkObject firstObj)
                && firstObj.TryGetComponent(out PlayerComponentRegistry firstRegistry)
                && firstRegistry.Controller != null)
            {
                paletteSize = firstRegistry.Controller.NametagSpritePoolSize;
                break;
            }
        }

        List<int> availableColorIndices = Enumerable.Range(0, paletteSize)
            .OrderBy(_ => Random.value)
            .ToList();

        foreach (PlayerRef playerRef in activePlayerRefs)
        {
            if (Object.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
            {
                if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
                {
                    if (registry.CharacterSelect != null)
                        registry.CharacterSelect.ResetSelectState();

                    if (registry.Controller != null)
                    {
                        if (availableColorIndices.Count > 0)
                        {
                            int colorIndex = availableColorIndices[0];
                            availableColorIndices.RemoveAt(0);
                            registry.Controller.AssignNametagColorByIndex(colorIndex);
                        }
                    }
                }
            }
        }
    }

    public void SetFinalCharacterSelectCountdown(bool value)
    {
        if (Object.HasStateAuthority)
            _isInFinalCountdown = value;
    }

    public void BeginFinalCharacterSelectCountdown()
    {
        if (!Object.HasStateAuthority) return;

        _isInFinalCountdown = true;
        ResetStateTimer(_settings.FinalCharacterSelectCountdown);
    }

    public void MarkCharacterSelectComplete()
    {
        if (Object.HasStateAuthority)
            _hasCompletedCharacterSelect = true;
    }

    #endregion

    #region RESET
    public void ResetRoundEntities()
    {
        if (!Object.HasStateAuthority) return;

        var activePlayers = Object.Runner.ActivePlayers.ToList();
        _localLiving.Clear(); // clear old list

        // grab randomised spawn pos
        List<Transform> allSpawnPoints = SpawnManager.Instance.GetAllSpawnPoints();

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerRef playerRef = activePlayers[i];

            if (Object.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
            {
                TrackPlayer(playerRef);

                if (i < allSpawnPoints.Count)
                {
                    Transform targetTransform = allSpawnPoints[i];
                    TeleportAndResetPlayer(playerObj, targetTransform);
                }
            }
        }
    }

    private void TeleportAndResetPlayer(NetworkObject playerObj, Transform targetTransform)
    {
        if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
        {
            if (registry.RespawnHandler != null)
                registry.RespawnHandler.TeleportToSpawnPoint(targetTransform.position, targetTransform.rotation);

            if (registry.Health != null)
            {
                registry.Health.ResetKnockoutState();
                registry.Health.ResetHealthToMax();
            }

            if (registry.Elimination != null)
                registry.Elimination.ResetLivesToMax();
            
            if (registry.Drowning != null)
                registry.Drowning.ResetDrownState();

            if (registry.Fall != null)
                registry.Fall.ResetFallState();

            if (playerObj.HasInputAuthority && ScreenFXManager.Instance != null)
                ScreenFXManager.Instance.ResetLocalPlayerVisuals();
        }
    }

    public void RestoreFullHealthAllPlayers()
    {
        if (!Object.HasStateAuthority) return;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                PlayerComponentRegistry registry = obj.transform.root.GetComponent<PlayerComponentRegistry>();
                if (registry.Health != null)
                    registry.Health.ResetHealthToMax();
                registry.Controller.Recover();
            }
        }
    }
    #endregion

    private void HandlePlayerEliminated(PlayerEliminationHandler handler)
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != TutorialState.SuddenDeath) return;

        PlayerRef dead = handler.Object.InputAuthority;
        for (int i = 0; i < _livingPlayers.Length; i++)
        {
            if (_livingPlayers[i] == dead) { _livingPlayers.Set(i, PlayerRef.None); break; }
        }
        RebuildLocalLiving();
    }

    #region PLAYER COUNT
    public int GetActiveSpectatorCount()
    {
        int count = 0;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                PlayerComponentRegistry reg = obj.GetComponentInParent<PlayerComponentRegistry>();
                if (reg != null && reg.Elimination != null && reg.Elimination.IsSpectatorTransitionComplete)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// registers player to tracked list when they spawn
    /// called by server/host
    /// </summary>
    public void TrackPlayer(PlayerRef playerRef)
    {
        if (!Object.HasStateAuthority) return;

        // find empty slot in network array and assign player
        for (int i = 0; i < _livingPlayers.Length; i++)
        {
            if (_livingPlayers[i] == playerRef) return; // alrdy tracking

            if (_livingPlayers[i] == PlayerRef.None)
            {
                _livingPlayers.Set(i, playerRef);
                return;
            }
        }
    }

    private void RebuildLocalLiving()
    {
        _localLiving.Clear();
        for (int i = 0; i < _livingPlayers.Length; i++)
            if (_livingPlayers[i] != PlayerRef.None)
                _localLiving.Add(_livingPlayers[i]);
    }
    private void OnActivePlayersChanged()
    {
        RebuildLocalLiving();
    }

    private int GetTotalTrackedPlayerCount()
    {
        int count = 0;
        for (int i = 0; i < _livingPlayers.Length; i++)
        {
            if (_livingPlayers[i] != PlayerRef.None)
                count++;
        }

        return count;
    }

    private bool AreAllPlayerAvatarsSpawned()
    {
        foreach (PlayerRef playerRef in Object.Runner.ActivePlayers)
        {
            if (!Object.Runner.TryGetPlayerObject(playerRef, out _))
                return false;
        }
        return true;
    }

    #endregion

    public void SeedLivingPlayersForSuddenDeath()
    {
        if (!Object.HasStateAuthority) return;
        for (int i = 0; i < _livingPlayers.Length; i++) _livingPlayers.Set(i, PlayerRef.None);

        int idx = 0;
        foreach (PlayerRef p in Runner.ActivePlayers)
            if (idx < _livingPlayers.Length) { _livingPlayers.Set(idx, p); idx++; }

        RebuildLocalLiving();
    }

    public void SetMatchWinner(PlayerRef winner)
    {
        if (!Object.HasStateAuthority) return;
        MatchWinner = winner;
    }

    public void SetFinalCountdown(bool active)
    {
        if (!Object.HasStateAuthority) return;
        _isInFinalCountdown = active;
    }

    #region TUTORIAL STEP PHASE HELPERS

    public void SetStepPhase(TutorialStepPhase phase, float timerDuration = 0f)
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log($"[TUTORIAL PHASE] (host) EXIT {CurrentStepPhase} -> ENTER {phase} (step {CurrentStepIndex})");

        CurrentStepPhase = phase;
        if (timerDuration > 0f)
            _phaseTimer = TickTimer.CreateFromSeconds(Runner, timerDuration);
    }

    /// <summary>
    /// computes how long the narration phase should last for the current step, based on text length + hold
    /// guarantees the typewriter finishes before tracking player actions
    /// </summary>
    public float GetNarrationDuration()
    {
        TutorialStepSO step = CurrentStep;
        if (step == null || string.IsNullOrEmpty(step.NarrationText))
            return _settings.NarrationHoldAfterType;

        float typeTime = step.NarrationText.Length / Mathf.Max(1f, _settings.NarrationCharsPerSecond);
        return typeTime + _settings.NarrationHoldAfterType;
    }

    private void OnStepPhaseChanged()
    {
        Debug.Log($"[TUTORIAL PHASE] (local, auth={Object.HasStateAuthority}) now in {CurrentStepPhase}, step {CurrentStepIndex}");

        // update UI with new phase info
        if (_tutorialUI != null)
            _tutorialUI.OnStepPhaseChanged(CurrentStepPhase, CurrentStep, CurrentStepIndex, TotalSteps, GetCompletionCountForCurrentStep(), GetRealPlayerCount());
    }

    public void ResetStepIndexToStart()
    {
        if (!Object.HasStateAuthority) return;
        CurrentStepIndex = 0;   // networked -> OnStepChanged fires
    }

    public void AdvanceStep()
    {
        if (!Object.HasStateAuthority) return;
        CurrentStepIndex++;  // networked -> OnStepChanged updates UI everywhere
    }

    public bool IsOnLastStep => CurrentStepIndex >= TotalSteps - 1;

    /// <summary>
    /// host sided -> called from wherever action happens
    /// e.g. TutorialManager.Instance?.NotifyPlayerAction(Object.InputAuthority, TutorialActionType.UseSkill)
    /// </summary>
    public void NotifyPlayerAction(PlayerRef player, TutorialActionType action)
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != TutorialState.TutorialActive) return;

        TutorialStepSO step = CurrentStep;
        if (step == null || step.RequiredAction != action) return;

        if (Runner.TryGetPlayerObject(player, out NetworkObject obj))
        {
            PlayerComponentRegistry registry = obj.transform.root.GetComponent<PlayerComponentRegistry>();
            PlayerTutorialProgress progress = registry.Progress;
            if (progress != null)
            {
                int before = progress.HighestCompletedStep;
                progress.RegisterProgress(CurrentStepIndex, step.RequiredCount);

                // refresh the N/total tracker when someone newly completes
                if (progress.HighestCompletedStep != before && _tutorialUI != null)
                    _tutorialUI.UpdateTracker(GetCompletionCountForCurrentStep(), GetRealPlayerCount());
            }
        }
    }

    public int GetCompletionCountForCurrentStep()
    {
        int count = 0;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
            {
                PlayerComponentRegistry registry = obj.transform.root.GetComponent<PlayerComponentRegistry>();
                if (registry.Progress != null && registry.Progress.HasCompletedStep(CurrentStepIndex))
                    count++;
            }
        }

        return count;
    }

    public bool HasLocalPlayerCompletedCurrentStep()
    {
        if (NetworkPlayerController.Local == null) return false;
        PlayerComponentRegistry registry = NetworkPlayerController.Local.Registry;
        if (registry == null || registry.Progress == null) return false;
        return registry.Progress.HasCompletedStep(CurrentStepIndex);
    }

    #endregion

    #region TRANSITION

    public void BeginTransitionToTutorialActive()
    {
        if (!Object.HasStateAuthority) return;
        if (_transitionInProgress) return;

        _transitionInProgress = true;
        Rpc_PlayTransitionWipe();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTransitionWipe()
    {
        StartCoroutine(CoveredTransitionRoutine());
    }

    private IEnumerator CoveredTransitionRoutine()
    {
        // cover the screen locally on every client
        if (LevelLoader.Instance != null)
            yield return LevelLoader.Instance.PlayWipeCover();

        // now covered — host flips state + teleports; everyone cuts camera
        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.Gameplay);

        if (Object.HasStateAuthority)
        {
            ResetRoundEntities();
            TransitionToState(TutorialState.TutorialActive);
        }

        // brief beat, then reveal
        yield return new WaitForSeconds(0.1f);
        if (LevelLoader.Instance != null)
            LevelLoader.Instance.PlayWipeReveal();
    }

    #endregion

    #region DEBUG SKIPS
    /// <summary>
    /// DEBUG: force-skips the current step
    /// </summary>
    public void DebugSkipCurrentStep()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != TutorialState.TutorialActive) return;

        // only meaningful while actually running a step
        if (CurrentStepPhase == TutorialStepPhase.Narrating || CurrentStepPhase == TutorialStepPhase.Tracking)
           SetStepPhase(TutorialStepPhase.StepComplete, Settings.StepCompleteDisplayDuration);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestSkipStep()
    {
        DebugSkipCurrentStep();
    }

    /// <summary>
    /// DEBUG: skips the entire tutorial active phase, jumps straight to SuddenDeath.
    /// </summary>
    public void DebugSkipToSuddenDeath()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != TutorialState.TutorialActive) return;
        Debug.Log("[TUTORIAL DEBUG] Skipping all steps -> SuddenDeath");
        TransitionToState(TutorialState.SuddenDeath);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestSkipToSuddenDeath()
    {
        DebugSkipToSuddenDeath();
    }

    #endregion

    #region SUDDEN DEATH HELPERS

    public void SetSuddenDeathPhase(SuddenDeathPhase phase, float timerDuration = 0f)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[SUDDEN DEATH] EXIT {CurrentSuddenDeathPhase} -> ENTER {phase}");
        CurrentSuddenDeathPhase = phase;
        if (timerDuration > 0f)
            _stateTimer = TickTimer.CreateFromSeconds(Runner, timerDuration);
    }

    private void OnSuddenDeathPhaseChanged()
    {
        Debug.Log($"[SUDDEN DEATH] (local, auth={Object.HasStateAuthority}) now {CurrentSuddenDeathPhase}");

        if (SuddenDeathUIController.Instance != null)
            SuddenDeathUIController.Instance.OnPhaseChanged(CurrentSuddenDeathPhase);

        // walls: down once we start fighting (runs on all clients)
        if (CurrentSuddenDeathPhase == SuddenDeathPhase.Fighting)
            SetWallsActive(false);
        else if (CurrentSuddenDeathPhase == SuddenDeathPhase.Banner)
            SetWallsActive(true);   // ensure up at start
    }

    public void SetWallsActive(bool active)
    {
        if (_tutorialWalls != null)
            _tutorialWalls.SetActive(active);
    }

    #endregion

    public PlayerRef GetLastLivingPlayer()
    {
        foreach (var p in _localLiving) return p;
        return PlayerRef.None;
    }

    // same-tick death tiebreak: pick a random still-active player as winner
    public PlayerRef PickRandomWinnerFallback()
    {
        var active = Runner.ActivePlayers.ToList();
        if (active.Count == 0) return PlayerRef.None;
        return active[Random.Range(0, active.Count)];
    }

    public bool LocalPlayerIsWinner(PlayerRef winner)
    {
        return NetworkPlayerController.Local != null
            && NetworkPlayerController.Local.Object.InputAuthority == winner;
    }

    public string GetPlayerName(PlayerRef player)
    {
        // adapt to however you fetch names; fallback to player id
        if (Runner.TryGetPlayerObject(player, out NetworkObject obj)
            && obj.TryGetComponent(out PlayerComponentRegistry reg)
            && reg.Controller != null)
        {
            // if you have a name source, use it; else:
            return reg.Stats.PlayerName;
        }
        return $"Player {player.PlayerId}";
    }

    public void ReturnToLobby()
    {
        if (!Object.HasStateAuthority) return;

        DespawnBots();
        DespawnBoostPads();
        DespawnAllPlayers();

        Runner.LoadScene(SceneRef.FromIndex(_mainMenuSceneBuildIndex), LoadSceneMode.Single);
    }

    private void DespawnAllPlayers()
    {
        if (!Object.HasStateAuthority) return;
        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(p, out NetworkObject playerObj))
                Runner.Despawn(playerObj);
        }
    }

    public bool IsSoloTutorial() => GetRealPlayerCount() <= 1;

    public void MarkSoloTutorial()
    {
        if (Object.HasStateAuthority) WasSoloTutorial = true;
    }

    #region BOOST PADS
    public void SpawnBoostPads()
    {
        if (!Object.HasStateAuthority) return;
        if (_boostPadPrefab == null || _boostPadSpawnPoints == null) return;
        if (_spawnedBoostPads.Count > 0) return;   // already spawned

        foreach (Transform pt in _boostPadSpawnPoints)
        {
            if (pt == null) continue;
            // pad takes the spawn point's own rotation directly
            NetworkObject pad = Runner.Spawn(_boostPadPrefab.gameObject, pt.position, pt.rotation);
            if (pad != null) _spawnedBoostPads.Add(pad);
        }
    }

    public void DespawnBoostPads()
    {
        if (!Object.HasStateAuthority) return;
        foreach (var pad in _spawnedBoostPads)
            if (pad != null) Runner.Despawn(pad);
        _spawnedBoostPads.Clear();
    }
    #endregion

    #region BOTS

    public void SpawnBotsToFill()
    {
        if (!Object.HasStateAuthority) return;
        if (_botsSpawned) return;                  // spawn once
        if (_botPrefab == null || _botSpawnPoints == null) return;

        int realPlayers = GetRealPlayerCount();
        int botsToSpawn = Mathf.Min(MAX_PLAYERS - realPlayers, _botSpawnPoints.Length);

        for (int i = 0; i < botsToSpawn; i++)
        {
            Transform pt = _botSpawnPoints[i];
            if (pt == null) continue;
            // no input authority -> stays out of ActivePlayers
            NetworkObject bot = Runner.Spawn(_botPrefab, pt.position, pt.rotation, inputAuthority: PlayerRef.None);
            if (bot != null) _spawnedBots.Add(bot);
        }
        _botsSpawned = true;
    }

    public void DespawnBots()
    {
        if (!Object.HasStateAuthority) return;
        foreach (var bot in _spawnedBots)
            if (bot != null) Runner.Despawn(bot);
        _spawnedBots.Clear();
        _botsSpawned = false;
    }

    #endregion
}
