using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private List<LeaderboardItemData> _sortedLeaderboard = new List<LeaderboardItemData>();

    // events
    public static Action OnLeaderboardUpdated;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        NetworkPlayerStats.OnPlayerCrownsChanged += RecalculateLeaderboardPlacements;
    }

    private void OnDisable()
    {
        NetworkPlayerStats.OnPlayerCrownsChanged -= RecalculateLeaderboardPlacements;
    }

    /// <summary>
    /// sorts leaderboard everytime a player's crown count changes
    /// </summary>
    private void RecalculateLeaderboardPlacements(PlayerRef alteredPlayer, int totalCrownsCount)
    {
        if (GameManager.Instance == null || GameManager.Instance.Runner == null) return;


    }
}
