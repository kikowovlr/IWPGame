using Fusion;
using UnityEngine;
using System.Collections.Generic;

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
    [Networked] InputRestrictions GlobalRestrictions { get; private set; } = InputRestrictions.None;

    // match
    [SerializeField] private MatchSettings _matchSettings;
    public MatchSettings Settings => _matchSettings;

    // rounds
    [Networked] public int CurrentRoundNumber { get; private set; }
    [Networked, OnChangedRender(nameof(OnRoundStateChanged))] public RoundState CurrentRoundState { get; private set; }

    [Networked] private TickTimer StateTimer { get; set; }

    // state machine tracking dictionary
    private Dictionary<RoundState, IRoundState> _stateMachine = new Dictionary<RoundState, IRoundState>();
    public bool IsStateTimerExpired => StateTimer.Expired(Runner); // helper for state classes to check if time is up

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

        // start game in setup state
        if (Object.HasStateAuthority)
        {
            TransitionToState(RoundState.Setup, _matchSettings.SetUpDuration);
        }
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
    public void TransitionToState(RoundState newState, float duration)
    {
        if (!Object.HasStateAuthority) return;

        if (CurrentRoundState != newState)
        {
            if (_stateMachine.TryGetValue(CurrentRoundState, out IRoundState oldState))
            {
                oldState.OnStateExit(this);
            }
        }

        CurrentRoundState = newState;
        StateTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;

        if (_stateMachine.TryGetValue(CurrentRoundState, out IRoundState nextState))
        {
            nextState.OnStateEnter(this);
        }
    }

    private void OnRoundStateChanged()
    {
        Debug.Log($"[CLIENT] State changed to: {CurrentRoundState}");
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
        int count = 0;
        for (int i = 0; i < _activePlayersInRound.Length; i++)
        {
            if (_activePlayersInRound[i] != PlayerRef.None) count++;
        }
        return count;
    }

    public void SetGlobalInputRestrictions(InputRestrictions restrictions)
    {
        if (Object.HasStateAuthority)
            GlobalRestrictions = restrictions;
    }
}
