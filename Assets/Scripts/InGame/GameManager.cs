using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// centralised game manager script to handle rounds and games
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public const int MAX_PLAYERS = 4;
    [Networked, Capacity(MAX_PLAYERS)]
    private NetworkArray<PlayerRef> _activePlayersInRound => default;
    private HashSet<PlayerRef> _localLivingPlayers = new HashSet<PlayerRef>(); // local tracking
    [Networked] public InputRestrictions GlobalRestrictions { get; private set; } = InputRestrictions.None;

    // match
    [SerializeField] private MatchSettings _matchSettings;

    // rounds
    [Networked] public int CurrentRoundNumber { get; private set; } = 0;
    [Networked, OnChangedRender(nameof(OnRoundStateChanged))] public RoundState CurrentRoundState { get; private set; }
    private RoundState _lastTrackedState = RoundState.Setup;
    [Networked] private TickTimer StateTimer { get; set; }
    [Networked, OnChangedRender(nameof(OnSetupUIStateChanged))] public NetworkBool IsSetupUIActive { get; private set; }

    // state machine tracking dictionary
    private Dictionary<RoundState, IRoundState> _stateMachine = new Dictionary<RoundState, IRoundState>();
    public bool IsStateTimerExpired => StateTimer.Expired(Runner); // helper for state classes to check if time is up

    // getters
    public MatchSettings Settings => _matchSettings;
    public RoundState GetCurrentRoundState() => CurrentRoundState;
    public int GetLivingPlayerCount() => _localLivingPlayers.Count;


    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        InitRoundStateMachine();
    }

    private void InitRoundStateMachine()
    {
        _stateMachine.Add(RoundState.Setup, new SetupState());
        _stateMachine.Add(RoundState.Countdown, new CountdownState());
        _stateMachine.Add(RoundState.RoundActive, new RoundActiveState());
        _stateMachine.Add(RoundState.RoundOver, new RoundOverState());
        _stateMachine.Add(RoundState.MatchOver, new MatchOverState());
    }

    private void OnEnable()
    {
        PlayerEliminationHandler.OnPlayerEliminated += HandlePlayerEliminated;
    }

    private void OnDisable()
    {
        PlayerEliminationHandler.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    /// <summary>
    /// populate on startup
    /// </summary>
    public override void Spawned()
    {
        _localLivingPlayers.Clear();
        for (int i = 0; i < _activePlayersInRound.Length; i++)
        {
            if (_activePlayersInRound[i] != PlayerRef.None)
            {
                _localLivingPlayers.Add(_activePlayersInRound[i]);
            }
        }

        _lastTrackedState = CurrentRoundState;
        if (_stateMachine.TryGetValue(CurrentRoundState, out IRoundState initialRoundState))
        {
            Debug.Log($"[MATCH LOCAL] -> Initializing First Frame State: {CurrentRoundState}");
            initialRoundState.OnStateEnter(this);
        }

        // force UI update if server has activated round UI screen
        if (IsSetupUIActive)
        {
            OnSetupUIStateChanged();
        }
        // start game in setup state
        if (Object.HasStateAuthority)
        {
            TransitionToState(RoundState.Setup);
        }

        //// TODO: FOR DEBUGGING - SKIPS SETUP
        //_lastTrackedState = RoundState.RoundActive;

        //if (Object.HasStateAuthority)
        //{
        //    SetGlobalInputRestrictions(InputRestrictions.None);
        //    TransitionToState(RoundState.RoundActive);
        //}
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // update state
        if (_stateMachine.TryGetValue(CurrentRoundState, out IRoundState currState))
        {
            currState.OnStateUpdate(this);
        }
    }

    /// <summary>
    /// sets new state, ticks state timer down
    /// </summary>
    public void TransitionToState(RoundState newState, float duration = 0f)
    {
        if (!Object.HasStateAuthority) return;

        CurrentRoundState = newState;
        StateTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;
    }

    private void OnRoundStateChanged()
    {
        if (_lastTrackedState != CurrentRoundState)
        {
            if (_stateMachine.TryGetValue(_lastTrackedState, out IRoundState oldState))
            {
                Debug.Log($"[MATCH LOCAL] -> Exiting state: {_lastTrackedState}");
                oldState.OnStateExit(this);
            }

            if (_stateMachine.TryGetValue(CurrentRoundState, out IRoundState nextState))
            {
                Debug.Log($"[MATCH LOCAL] -> Entering state: {CurrentRoundState}");
                nextState.OnStateEnter(this);
            }

            _lastTrackedState = CurrentRoundState;
        }
    }

    /// <summary>
    /// registers player to tracked list when they spawn
    /// called by server/host
    /// </summary>
    /// <param name="handler"></param>
    public void TrackPlayer(PlayerRef playerRef)
    {
        _localLivingPlayers.Add(playerRef);

        if (!Object.HasStateAuthority) return;

        // find empty slot in network array and assign player
        for (int i = 0; i < _activePlayersInRound.Length; i++)
        {
            if (_activePlayersInRound[i] == playerRef) return; // alrdy tracking

            if (_activePlayersInRound[i] == PlayerRef.None)
            {
                _activePlayersInRound.Set(i, playerRef);
                return;
            }
        }
    }

    /// <summary>
    /// remove player from active players array
    /// </summary>
    private void HandlePlayerEliminated(PlayerEliminationHandler handler)
    {
        // extract network id
        PlayerRef deadPlayerId = handler.Object.InputAuthority;

        // remove locally
        if (_localLivingPlayers.Contains(deadPlayerId))
            _localLivingPlayers.Remove(deadPlayerId);

        if (!Object.HasStateAuthority) return;

        // remove from list
        for (int i = 0; i < _activePlayersInRound.Length; i++)
        {
            if (_activePlayersInRound[i] == deadPlayerId)
            {
                _activePlayersInRound.Set(i, PlayerRef.None);
                break;
            }
        }
    }

    /// <summary>
    /// returns a list of all raw network IDs currently marked as alive
    /// </summary>
    public List<PlayerRef> GetLivingPlayerIDs()
    {
        return new List<PlayerRef>(_localLivingPlayers);
    }

    /// <summary>
    /// helper to increment round outside of game manager
    /// </summary>
    public void IncrementRoundCounter()
    {
        if (Object.HasStateAuthority)
        {
            CurrentRoundNumber++;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void ResetRoundEntities()
    {
        // TODO: set spawn positions
        // TODO: randomise spawn positions between players
    }

    public int GetTotalRegisteredCount()
    {
        if (Runner != null)
        {
            return Runner.ActivePlayers.Count();
        }
        return 0;
    }

    public void SetGlobalInputRestrictions(InputRestrictions restrictions)
    {
        if (Object.HasStateAuthority)
            GlobalRestrictions = restrictions;
    }

    public void ResetStateTimer(float duration)
    {
        if (Object.HasStateAuthority)
            StateTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;
    }

    private void OnSetupUIStateChanged()
    {
        if (TransitionUIManager.Instance == null) return;

        if (IsSetupUIActive)
        {
            string currentRoundName = $"ROUND {CurrentRoundNumber}";
            TransitionUIManager.Instance.ShowRoundSetupScreen(currentRoundName);
        }
        else
        {
            TransitionUIManager.Instance.ClearAllOverlays();
        }
    }

    public void SetSetupUIActive(bool active)
    {
        if (Object.HasStateAuthority)
            IsSetupUIActive = active;
    }

    /// <summary>
    /// returns remaining time of StateTimer in seconds
    /// </summary>
    public float GetRemainingStateTime()
    {
        if (StateTimer.IsRunning && Runner != null)
        {
            return StateTimer.RemainingTime(Runner) ?? 0f; // ?? means if timer has time left, return left of ??, if timer is null, return right of ??
        }
        return 0f;
    }

    /// <summary>
    /// awards one crown
    /// called on server when there is only one player alive during roundactive state
    /// </summary>
    public void AwardCrownToPlayer(PlayerRef winner)
    {
        if (!Object.HasStateAuthority) return;

        Utils.DebugLog($"[SERVER] -> Round complete! Winner identified: Player {winner}");

        NetworkPlayerStats winningStats = null;

        if (Runner.TryGetPlayerObject(winner, out NetworkObject netObj))
        {
            PlayerComponentRegistry registry = netObj.GetComponent<PlayerComponentRegistry>();

            if (registry != null)
            {
                winningStats = registry.Stats;
            }
        }

        // found winner, add crown to their count
        if (winningStats != null)
        {
            winningStats.IncrementCrowns();

            // check if shld end game or not
            if (winningStats.CrownCount >= _matchSettings.CrownsToWinMatch)
            {
                Utils.DebugLog($"[MATCH END] -> Player {winner} achieved ultimate victory! Ending match.");
                TransitionToState(RoundState.MatchOver, _matchSettings.MatchOverBufferDuration);
            }
            else
            {
                TransitionToState(RoundState.RoundOver, _matchSettings.RoundOverBufferDuration);
            }
        }
        else
        {
            Utils.DebugLog($"[SERVER] -> Cannot find winning player");
        }
    }
}
