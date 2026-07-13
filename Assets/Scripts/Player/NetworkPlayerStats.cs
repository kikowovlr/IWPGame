using Fusion;
using System;
using UnityEngine;

/// <summary>
/// keeps track of persistant match statistics per player
/// e.g. crowns
/// </summary>
public class NetworkPlayerStats : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnCrownsCountChanged))] public int CrownCount { get; private set; }
    [Networked, OnChangedRender(nameof(OnNameReplicated))] public string PlayerName { get; private set; }

    // events
    public static Action<PlayerRef, int> OnPlayerCrownsChanged;
    public static Action OnPlayerNameSynchronized;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CrownCount = 0;
        }

        if (Object.HasInputAuthority)
        {
            string fallbackName = $"Player {Object.InputAuthority.PlayerId}";
            string loadedName = PlayerPrefs.GetString("SavedPlayerName", fallbackName);

#if UNITY_EDITOR
            // if this execution engine is running inside a ParrelSync editor window node
            string projectPath = Application.dataPath;
            if (projectPath.Contains("Clone"))
            {
                // This appends the exact network player ID so Clone 1 and Clone 2 have different names!
                loadedName = $"{loadedName} (Clone_{Object.InputAuthority.PlayerId})"; loadedName += $" (Clone_{Object.InputAuthority.PlayerId})";
            }
#endif

            // send to server authority
            RPC_SetPlayerName(loadedName);
        }
    }

    /// <summary>
    /// server-authoritative method to add crowns
    /// </summary>
    public void IncrementCrowns()
    {
        if (!Object.HasStateAuthority) return;
        CrownCount++;
        Utils.DebugLog($"[STATS] -> Player {Object.InputAuthority} now has {CrownCount} crowns.");
    }

    private void OnCrownsCountChanged()
    {
        // fires event to when crown count changes to alert UI elements
        OnPlayerCrownsChanged?.Invoke(Object.InputAuthority, CrownCount);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string nameToRegister)
    {
        // server writes to networked property, updating name for all other players 
        PlayerName = nameToRegister;
    }

    private void OnNameReplicated()
    {
        // to alert leaderboard
        OnPlayerNameSynchronized?.Invoke();
    }
}
