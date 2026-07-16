using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour, ICleanup
{
    public static LeaderboardManager Instance { get; private set; }

    private List<LeaderboardItemData> _sortedLeaderboard = new List<LeaderboardItemData>();
    public List<LeaderboardItemData> GetSortedLeaderboard => _sortedLeaderboard;

    // events
    public static Action OnLeaderboardUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else 
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        NetworkPlayerStats.OnPlayerCrownsChanged += RecalculateLeaderboardPlacements;
        NetworkPlayerStats.OnPlayerNameSynchronized += TriggerLeaderboardRecalculation;
    }

    private void OnDisable()
    {
        NetworkPlayerStats.OnPlayerCrownsChanged -= RecalculateLeaderboardPlacements;
        NetworkPlayerStats.OnPlayerNameSynchronized -= TriggerLeaderboardRecalculation;
    }


    /// <summary>
    /// sorts leaderboard everytime a player's crown count changes
    /// </summary>
    private void RecalculateLeaderboardPlacements(PlayerRef alteredPlayer, int totalCrownsCount)
    {
        if (GameManager.Instance == null || GameManager.Instance.Runner == null) return;

        var runner = GameManager.Instance.Runner;
        List<LeaderboardItemData> newLeaderboard = new List<LeaderboardItemData>();

        // collect statistics across all registered players in lobby
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            string pName = $"Player {player.PlayerId}";
            Sprite pIcon = null;
            int crowns = 0;

            // connected players
            if (runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            {
                PlayerComponentRegistry registry = playerObj.GetComponent<PlayerComponentRegistry>();
                if (registry != null && registry.Stats != null)
                {
                    crowns = registry.Stats.CrownCount;

                    if (!string.IsNullOrEmpty(registry.Stats.PlayerName))
                    {
                        pName = registry.Stats.PlayerName;
                    }
                    // in case network property hasnt travelled to the server and back yet, local player can view their name directly from memory
                    else if (player == runner.LocalPlayer && PlayerPrefs.HasKey("SavedPlayerName"))
                    {
                        pName = PlayerPrefs.GetString("SavedPlayerName");
                    }
                }

                // get character component
                if (registry.ActiveCharacterLinker != null && registry.ActiveCharacterLinker._characterData != null)
                {
                    pIcon = registry.ActiveCharacterLinker._characterData.CharacterIcon;
                }
            }

            newLeaderboard.Add(new LeaderboardItemData(player, pName, pIcon, crowns));
        }

        // sort leaderboard by crown count, if crowncount is the same for 2 players, sort by player id
        _sortedLeaderboard = newLeaderboard
            .OrderByDescending(item => item.CrownCount)     // order by crown count
            .ThenBy(item => item.PlayerReference.PlayerId)
            .ToList();

        OnLeaderboardUpdated?.Invoke();
    }

    public void InitialiseLeaderboard()
    {
        RecalculateLeaderboardPlacements(PlayerRef.None, 0);
    }

    private void TriggerLeaderboardRecalculation()
    {
        // force manager to recalculate using current parameters
        RecalculateLeaderboardPlacements(PlayerRef.None, 0);
    }

    public static void ResetInstance()
    {
        Instance = null;
    }

    public void Cleanup()
    {
    }
}
