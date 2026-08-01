using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class TutorialManager : NetworkBehaviour
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

    [HideInInspector] [Networked, OnChangedRender(nameof(OnTutorialStateChanged))]
    public TutorialState CurrentState { get; private set; }

    [HideInInspector] [Networked, OnChangedRender(nameof(OnStepChanged))]
    public int CurrentStepIndex { get; private set; }

    [Networked] private TickTimer _stateTimer { get; set; }
    [HideInInspector] [Networked] public PlayerRef MatchWinner { get; private set; }

    private readonly Dictionary<TutorialState, ITutorialState> _states = new();
    private ITutorialState _activeState;

    public int TotalSteps => _steps != null ? _steps.Length : 0;
    public TutorialStepSO CurrentStep =>
        (_steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < _steps.Length)
        ? _steps[CurrentStepIndex] : null;

    // getters
    public float CharacterSelectDuration => _settings.CharacterSelectDuration;
    public float CountdownDuration => _settings.CountdownDuration;
    public float MatchOverDuration => _settings.MatchOverBufferDuration;
    public bool IsStateTimerExpired => _stateTimer.ExpiredOrNotRunning(Runner);
    public RoundEndDisplayController RoundEndDisplay => _roundEndDisplay;
    public TutorialUIController TutorialUI => _tutorialUI;

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
        if (Object.HasStateAuthority)
            CurrentState = TutorialState.CharacterSelect;

        // force first local enter (OnChangedRender doesn't fire for the initial value)
        EnterStateLocal(CurrentState);
    }

    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        if (Instance == this) Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
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
            _tutorialUI.ShowStep(CurrentStep, CurrentStepIndex, TotalSteps, GetCompletionCountForCurrentStep(), GetActivePlayerCount());
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

#region CHARACTER SELECT
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
    #endregion

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
                    _tutorialUI.UpdateTracker(GetCompletionCountForCurrentStep(), GetActivePlayerCount());
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

    public int GetActivePlayerCount() => Runner.ActivePlayers.Count();

    public bool HaveAllPlayersCompletedCurrentStep() => GetCompletionCountForCurrentStep() >= GetActivePlayerCount();

    public void AdvanceStep()
    {
        if (!Object.HasStateAuthority) return;
        CurrentStepIndex++;  // networked -> OnStepChanged updates UI everywhere
    }

    public bool IsOnLastStep => CurrentStepIndex >= TotalSteps - 1;

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

    public float GetRemainingStateTime()
    {
        return _stateTimer.RemainingTime(Runner) ?? 0f;
    }

    //public int GetLivingPlayerCount()
    //{
    //    // <<< VERIFY: copy GameManager.GetLivingPlayerCount() logic.
    //    // Likely: count players whose Stats say they're alive / not eliminated.
    //    int living = 0;
    //    foreach (PlayerRef p in Runner.ActivePlayers)
    //    {
    //        if (Runner.TryGetPlayerObject(p, out NetworkObject obj))
    //        {
    //            PlayerComponentRegistry reg = obj.GetComponent<PlayerComponentRegistry>();
    //            // if (reg != null && reg.Stats != null && reg.Stats.IsAlive) living++;
    //        }
    //    }
    //    return living;
    //}
}
