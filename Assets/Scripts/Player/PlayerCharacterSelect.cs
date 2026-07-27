using UnityEngine;
using Fusion;

/// <summary>
/// handles character-select phase: which character is chosen and selected
/// only relevant during character select phase
/// </summary>
public class PlayerCharacterSelect : NetworkBehaviour
{
    [HideInInspector][Networked] public NetworkBool IsReadyToStart { get; private set; }
    private NetworkPlayerController _controller;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _controller = registry.Controller;
        }
    }

    /// <summary>
    /// client w Input authority req changing of their OWN character
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestCharacterChange(int newIndex)
    {
        if (!Object.HasStateAuthority) return;
        if (IsReadyToStart) return; // locked - cant change when ready
        if (newIndex < 0 || newIndex >= _controller.CharacterPackageCount) return;

        _controller.CharacterIndex = newIndex;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReady(bool ready)
    {
        if (!Object.HasStateAuthority) return;
        IsReadyToStart = ready;
    }

    /// <summary>
    /// server-initiated lock (auto-lock when timer is up)
    /// </summary>
    public void ForceLock()
    {
        if (!Object.HasStateAuthority) return;
        IsReadyToStart = true;
    }

    public void ResetSelectState()
    {
        if (!Object.HasStateAuthority) return;
        IsReadyToStart = false;
    }
}
