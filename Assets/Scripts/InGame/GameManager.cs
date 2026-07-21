using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// centralised game manager script to handle rounds and games
/// </summary>
public class GameManager : NetworkBehaviour, IPlayerJoined, ICleanup
{
    public static GameManager Instance { get; private set; }

    public const int MAX_PLAYERS = 4;
    [Networked, Capacity(MAX_PLAYERS)]
    private NetworkArray<PlayerRef> _activePlayersInRound => default;
    private HashSet<PlayerRef> _localLivingPlayers = new HashSet<PlayerRef>(); // local tracking
    [HideInInspector] [Networked] public InputRestrictions GlobalRestrictions { get; private set; } = InputRestrictions.None;

    // match
    [SerializeField] private MatchSettings _matchSettings;

    // rounds
    [Networked] public int CurrentRoundNumber { get; private set; } = 0;
    [Networked, OnChangedRender(nameof(OnRoundStateChanged))] public RoundState CurrentRoundState { get; private set; }
    private RoundState _lastTrackedState = RoundState.Setup;
    [Networked] private TickTimer StateTimer { get; set; }
    [HideInInspector] [Networked, OnChangedRender(nameof(OnSetupUIStateChanged))] public NetworkBool IsSetupUIActive { get; private set; }
    private bool _firstRoundInitialised = false;

    // state machine tracking dictionary
    private Dictionary<RoundState, IRoundState> _stateMachine = new Dictionary<RoundState, IRoundState>();
    [HideInInspector] public bool IsStateTimerExpired => StateTimer.Expired(Runner); // helper for state classes to check if time is up

    // end of round
    [SerializeField] private RoundEndDisplayController _roundEndDisplayController;
    [HideInInspector] [Networked] public PlayerRef LastRoundWinner { get; private set; } = PlayerRef.None;

    [SerializeField] private IslandBreakManager _islandBreakManager;

    // getters
    public MatchSettings Settings => _matchSettings;
    public RoundState GetCurrentRoundState() => CurrentRoundState;
    public int GetLivingPlayerCount() => _localLivingPlayers.Count;
    public RoundEndDisplayController RoundEndDisplay => _roundEndDisplayController;


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
        DontDestroyOnLoad(gameObject);

        if (Object.HasStateAuthority)
        {
            foreach (var player in Object.Runner.ActivePlayers)
            {
                TrackPlayer(player);
            }
        }

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
    }

    public override void FixedUpdateNetwork()
    {
        if (IsCurrentlyOnPodiumScene()) return;

        if (!Object.HasStateAuthority) return;

        // wait until fusion generates player network wrappers before starting
        if (!_firstRoundInitialised)
        {
            int activeNetworkPlayers = Object.Runner.ActivePlayers.Count();
            int fullyTrackedCount = GetTotalTrackedPlayerCount();

            if (activeNetworkPlayers > 0 && fullyTrackedCount >= activeNetworkPlayers)
            {
                Debug.Log($"[MATCH ENGINE] -> Network arrays synced ({fullyTrackedCount}/{activeNetworkPlayers}). Commencing Setup state safely.");
                _firstRoundInitialised = true;

                ResetRoundEntities();
                TransitionToState(RoundState.Setup, _matchSettings.SetUpDuration);
            }
            else
                return;
        }

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
                if (_lastTrackedState == RoundState.RoundOver && CurrentRoundState == RoundState.MatchOver)
                {
                    // Guard the display if we are specifically moving to the final match presentation screen
                    Debug.Log("[MATCH ENGINE UI GUARD] -> Preserving end display for match screen.");
                }
                else
                {
                    oldState.OnStateExit(this);
                }
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
    /// returns the total number of players who have completely transitioned into spectators
    /// </summary>
    public int GetActiveSpectatorCount()
    {
        int spectatorCount = 0;
        var runner = Object.Runner;

        foreach (var playerRef in runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
            {
                PlayerComponentRegistry registry = playerObj.GetComponentInParent<PlayerComponentRegistry>();
                if (registry != null && registry.Elimination != null && registry.Elimination.IsSpectatorTransitionComplete)
                {
                    spectatorCount++;
                }
            }
        }

        return spectatorCount;
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

    public void ResetRoundEntities()
    {
        if (!Object.HasStateAuthority) return;

        var activePlayers = Object.Runner.ActivePlayers.ToList();
        _localLivingPlayers.Clear(); // clear old list

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

        if (_islandBreakManager != null)
            _islandBreakManager.ResetAllPieces();
    }

    private void TeleportAndResetPlayer(NetworkObject playerObj, Transform targetTransform)
    {
        if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
        {
            if (registry.RespawnHandler != null)
            {
                registry.RespawnHandler.TeleportToSpawnPoint(targetTransform.position, targetTransform.rotation);
            }

            if (registry.Health != null)
            {
                registry.Health.ResetHealthToMax();
            }

            if (registry.Elimination != null)
            {
                registry.Elimination.ResetLivesToMax();
            }

            if (registry.Drowning != null)
            {
                registry.Drowning.ResetDrownState();
            }
        }
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
        if (TransitionUIManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] OnSetupUIStateChanged fired but TransitionUIManager.Instance is NULL");
            return;
        }

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
        }
        
        TransitionToState(RoundState.RoundOver, _matchSettings.RoundOverBufferDuration);
    }

    public void SetRoundWinner(PlayerRef winnerId)
    {
        if (Object.HasStateAuthority)
            LastRoundWinner = winnerId;
    }

    /// <summary>
    /// checks if any player has reached the max crowns to win
    /// </summary>
    public bool IsMatchOver()
    {
        if (Object.Runner == null) return false;

        // loop thru every player currently connected
        foreach(PlayerRef playerRef in Object.Runner.ActivePlayers)
        {
            if (Object.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
            {
                // Reach through your Service Locator pattern to check their stats
                if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
                {
                    if (registry.Stats != null && registry.Stats.CrownCount >= _matchSettings.CrownsToWinMatch)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// retrieve the player that won the match (most crowns)
    /// </summary>
    public PlayerRef GetOverallMatchWinner()
    {
        if (Object.Runner == null) return PlayerRef.None;

        foreach (PlayerRef playerRef in Object.Runner.ActivePlayers)
        {
            if (Object.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
            {
                if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
                {
                    if (registry.Stats != null && registry.Stats.CrownCount >= _matchSettings.CrownsToWinMatch)
                        return playerRef;
                }
            }
        }

        return PlayerRef.None;
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (CurrentRoundState == RoundState.Setup)
        {
            StartCoroutine(TeleportLateJoinerDelayed(player));
        }

        TrackPlayer(player);
    }

    private IEnumerator TeleportLateJoinerDelayed(PlayerRef player)
    {
        float timeout = 2f;
        float elapsed = 0f;
        NetworkObject playerObj = null;

        // Wait until the spawned Player Object is linked and registered by Fusion
        while (playerObj == null && elapsed < timeout)
        {
            Runner.TryGetPlayerObject(player, out playerObj);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerObj != null)
        {
            // find this player's specific stable index in the match
            int playerIndex = -1;
            for (int i = 0; i < _activePlayersInRound.Length; i++)
            {
                if (_activePlayersInRound[i] == player)
                {
                    playerIndex = i;
                    break;
                }
            }

            // get the master list of spawn points
            List<Transform> allSpawnPoints = SpawnManager.Instance.GetAllSpawnPoints();

            // teleport them to their dedicated index spawn point
            if (playerIndex != -1 && playerIndex < allSpawnPoints.Count)
            {
                Transform spawnPoint = allSpawnPoints[playerIndex];
                TeleportAndResetPlayer(playerObj, spawnPoint);
            }
        }
    }

    public bool IsCurrentlyOnPodiumScene()
    {
        if (_matchSettings == null || _matchSettings.PodiumScene == null) return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.name == _matchSettings.PodiumScene.SceneName;
    }

    private int GetTotalTrackedPlayerCount()
    {
        int count = 0;
        for (int i = 0; i < _activePlayersInRound.Length; i++)
        {
            if (_activePlayersInRound[i] != PlayerRef.None)
                count++;
        }

        return count;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayTransitionOut()
    {
        if (LevelLoader.Instance != null)
            StartCoroutine(PlayLocalTransition());
    }

    private IEnumerator PlayLocalTransition()
    {
        yield return LevelLoader.Instance.TransitionOut();

        if (Runner != null)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out NetworkObject playerObj))
                {
                    var nrb = playerObj.GetComponent<NetworkRigidbody3D>();
                    if (nrb != null)
                    {
                        nrb.enabled = false;
                    }
                }
            }
        }
    }

    public static void ResetInstance()
    {
        Instance = null;
    }

    public void Cleanup()
    {
    }
}
