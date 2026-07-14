using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
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

    /// <summary>
    /// fires only after destination is reached
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsServer)
            Utils.DebugLog("[SPAWNER] -> Scene load complete. Spawning player avatars into arena.");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
            SpawnPlayerAvatar(runner, player);
    }

    private void SpawnPlayerAvatar(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject spawnedObj = runner.Spawn(_networkPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity, player);

        runner.SetPlayerObject(player, spawnedObj);
    }

    //public void OnSceneLoadDone(NetworkRunner runner)
    //{
    //    if (!runner.IsServer) return;

    //    Utils.DebugLog("[SPAWNER] -> Scene load complete. Spawning player avatars into arena.");
    //    _sceneLoadingComplete = true;

    //    CheckAndStartGameEngine(runner);
    //}

    //public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    //{
    //    if (!runner.IsServer) return;

    //    SpawnPlayerAvatar(runner, player);

    //    _spawnedPlayers.Add(player);

    //    if (_sceneLoadingComplete)
    //    {
    //        CheckAndStartGameEngine(runner);
    //    }
    //}

    //private void SpawnPlayerAvatar(NetworkRunner runner, PlayerRef player)
    //{
    //    NetworkObject spawnedObj = runner.Spawn(_networkPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity, player);
    //    runner.SetPlayerObject(player, spawnedObj);
    //}

    //private void CheckAndStartGameEngine(NetworkRunner runner)
    //{
    //    if (GameManager.Instance == null) return;

    //    int activeConnections = runner.ActivePlayers.Count();
    //    int spawnedAvatarsCount = _spawnedPlayers.Count;

    //    // We only start the match if the scene is open AND every connected player has a spawned avatar body
    //    if (_sceneLoadingComplete && spawnedAvatarsCount >= activeConnections)
    //    {
    //        Debug.Log($"[SPAWNER] -> All connections satisfied ({spawnedAvatarsCount}/{activeConnections}). Signaling GameManager to start match!");

    //        Invoke(nameof(ExecuteDeferredEngineStart), 0.05f);
    //    }
    //}

    //private void ExecuteDeferredEngineStart()
    //{
    //    if (GameManager.Instance != null && Runner.IsServer)
    //    {
    //        Debug.Log("[SPAWNER] -> One-frame buffer cleared. Safely signaling GameManager to start match engine.");
    //        GameManager.Instance.StartMatchEngine();
    //    }
    //}

    //public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    //{
    //    if (runner.IsServer && _spawnedPlayers.Contains(player))
    //    {
    //        _spawnedPlayers.Remove(player);
    //    }
    //}

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

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
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

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }
}
