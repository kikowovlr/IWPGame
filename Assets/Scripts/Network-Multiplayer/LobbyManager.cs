using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// tracks connected players' names + host identity while sitting in lobby before gameplay scene
/// spawned after connecting
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public const int MAX_LOBBY_PLAYERS = 4;

    [Networked] public PlayerRef HostPlayerRef { get; private set; }
    [Networked, Capacity(MAX_LOBBY_PLAYERS)] private NetworkArray<PlayerRef> _playerSlots => default;
    [Networked, Capacity(MAX_LOBBY_PLAYERS)] private NetworkArray<NetworkString<_16>> _playerNames => default;

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LobbyManager] Duplicate instance detected — despawning this one.");
            if (Object.HasStateAuthority)
                Runner.Despawn(Object);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[LobbyManager] Spawned on {(Object.HasStateAuthority ? "HOST" : "CLIENT")} — LocalPlayer={Runner.LocalPlayer}");

        if (Object.HasStateAuthority)
        {
            HostPlayerRef = Runner.LocalPlayer;
            RegisterPlayer(Runner.LocalPlayer, GetLocalDisplayName());
        }
        else
        {
            Debug.Log($"[LobbyManager] CLIENT sending Rpc_SubmitName for {GetLocalDisplayName()}");
            Rpc_SubmitName(Runner.LocalPlayer, GetLocalDisplayName());
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;

        if (!hasState)
        {
            LobbyUIController lobbyUI = FindAnyObjectByType<LobbyUIController>();
            if (lobbyUI != null)
                lobbyUI.HandleForcedDisconnect();
        }
    }

    private string GetLocalDisplayName()
    {
        string savedName = PlayerPrefs.GetString("SavedPlayerName", "");
        return string.IsNullOrWhiteSpace(savedName) ? $"Player{Random.Range(100, 999)}" : savedName;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_SubmitName(PlayerRef player, string playerName)
    {
        Debug.Log($"[LobbyManager] HOST received Rpc_SubmitName from {player}: {playerName}");
        RegisterPlayer(player, playerName);
    }

    /// <summary>
    /// saved joined player's ref and name
    /// </summary>
    private void RegisterPlayer(PlayerRef player, string playerName)
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < MAX_LOBBY_PLAYERS; i++)
        {
            if (_playerSlots[i] == player)
            {
                _playerNames.Set(i, playerName);
                Debug.Log($"[LobbyManager] Updated existing slot {i} for {player}: {playerName}");
                return;
            }

            if (_playerSlots[i] == PlayerRef.None)
            {
                _playerSlots.Set(i, player);
                _playerNames.Set(i, playerName);
                Debug.Log($"[LobbyManager] Registered NEW slot {i} for {player}: {playerName}");
                return;
            }
        }
        Debug.LogWarning("[LobbyManager] RegisterPlayer failed — no free slots found!");
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < MAX_LOBBY_PLAYERS; i++)
        {
            if (_playerSlots[i] == player)
            {
                _playerSlots.Set(i, PlayerRef.None);
                _playerNames.Set(i, default);
                return;
            }
        }
    }

    public List<(PlayerRef player, string name, bool isHost)> GetLobbyRoster()
    {
        var list = new List<(PlayerRef, string, bool)>();

        for (int i = 0; i < MAX_LOBBY_PLAYERS; i++)
        {
            if (_playerSlots[i] == PlayerRef.None) continue;
            list.Add((_playerSlots[i], _playerNames[i].ToString(), _playerSlots[i] == HostPlayerRef));
        }

        return list;
    }
}
