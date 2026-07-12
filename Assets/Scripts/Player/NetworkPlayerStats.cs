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

    // events
    public static Action<PlayerRef, int> OnPlayerCrownsChanged;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CrownCount = 0;
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
}
