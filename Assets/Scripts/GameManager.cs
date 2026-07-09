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

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
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
        {
            _localLivingPlayers.Remove(deadPlayerId);
        }

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
}
