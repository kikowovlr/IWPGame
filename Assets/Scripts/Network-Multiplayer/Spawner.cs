using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles gameplay session operations once connected
/// 
/// SimulationBehaviour
/// -> give script access to Fusion's Network Simulation Loop
/// -> e.g. FixedUpdateNetwork(): replaces Unity’s FixedUpdate()
/// allows you to use Fusion's [Networked] tags to sync variables
/// lets you run code inside the network loop (FixedUpdateNetwork), doesn't require a NetworkObject component
/// ---------------------------------------------
/// INetworkRunnerCallbacks
/// -> List of "event triggers" 
/// -> e.g. OnPlayerJoined: spawn the player prefab
/// -> e.g. OnInput: keyboard/mouse inputs (like WASD or Jump) and hand them over to Fusion so it can sync your movement smoothly
/// </summary>
public class Spawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkPlayerController _networkPlayerPrefab;

    // input is being collected by network player which is then sent to the host thru this fn
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData inputData = new NetworkInputData();
        if (NetworkPlayerController.Local != null)
            inputData = NetworkPlayerController.Local.GetNetworkInput();

        input.Set(inputData);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (NetworkLauncher.Instance != null && !NetworkLauncher.Instance.HasSkippedInitialSceneLoad)
        {
            NetworkLauncher.Instance.HasSkippedInitialSceneLoad = true;
            return;
        }

        bool isPodiumTransition = GameManager.Instance != null && GameManager.Instance.CurrentRoundState == RoundState.MatchOver;
        if (isPodiumTransition)
            return;

        // check for which map/scene is loading
        int loadingIndex = PendingMapCache.Instance != null
            ? PendingMapCache.Instance.PendingSceneIndex
            : -1;


        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ShowMapLoadingScreenTimed("Starting Match...", loadingIndex);
    }

    /// <summary>
    /// fires only after destination is reached
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;

        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Game")) return;

        foreach (var player in runner.ActivePlayers)
        {
            // If the player already exists, do NOT spawn a new one
            if (runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            {
                RespawnHandler respawnHandler = playerObj.GetComponentInChildren<RespawnHandler>();

                if (respawnHandler != null)
                    respawnHandler.TeleportToSpawnPoint(Vector3.zero, Quaternion.identity);
                else
                    playerObj.transform.position = Vector3.zero;
            }
            else
            {
                // This handles anyone who is joining the game fresh
                SpawnPlayerAvatar(runner, player);
            }
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Game")) return;

        if (runner.IsServer)
            SpawnPlayerAvatar(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.UnregisterPlayer(player);
    }

    private void SpawnPlayerAvatar(NetworkRunner runner, PlayerRef player)
    {
        if (runner.TryGetPlayerObject(player, out _)) return;

        NetworkObject spawnedObj = runner.Spawn(_networkPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity, player);

        runner.SetPlayerObject(player, spawnedObj);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        HandleForcedReturnToMenu();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // Ok = we intentionally shut down (clean leave / our own return-to-menu) — handled elsewhere
        if (shutdownReason == ShutdownReason.Ok) return;

        HandleForcedReturnToMenu();
    }

    private void HandleForcedReturnToMenu()
    {
        // if the lobby UI exists in this scene, let it clean up; otherwise load the menu directly
        LobbyUIController lobby = FindAnyObjectByType<LobbyUIController>();
        if (lobby != null)
            lobby.HandleForcedDisconnect(); // your existing cleanup path
        else
            // mid-match / podium — no lobby UI here, so just load the menu scene locally
            SceneManager.LoadScene("MainMenuScene");

        // clean up the runner so the next create-lobby starts fresh
        if (NetworkLauncher.Instance != null)
            _ = NetworkLauncher.Instance.CleanupRunner();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }
}
