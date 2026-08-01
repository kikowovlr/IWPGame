using Fusion;
using UnityEngine;

/// <summary>
/// put ONE of these in every playable scene
/// </summary>
public class MatchRestrictionsProvider : NetworkBehaviour
{
    public static MatchRestrictionsProvider Instance { get; private set; }
    [Networked] public InputRestrictions GlobalRestrictions { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public override void Spawned()
    {
        // re-assert singleton on the networked side (Awake may run before this on clients)
        Instance = this;
        if (Object.HasStateAuthority)
            GlobalRestrictions = InputRestrictions.None;
    }

    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        if (Instance == this) Instance = null;
    }

    public void SetRestrictions(InputRestrictions restrictions)
    {
        if (!Object.HasStateAuthority) return;
        GlobalRestrictions = restrictions;
    }

    public static InputRestrictions Current => Instance != null ? Instance.GlobalRestrictions : InputRestrictions.None;
}
