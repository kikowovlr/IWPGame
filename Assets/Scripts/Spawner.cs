using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

/// <summary>
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

    // input is being collected by network player which is then sent to the host thru this fn
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData inputData = new NetworkInputData();

        bool isCloneInstance = Application.dataPath.Contains("Clone");
        if (!isCloneInstance)
        {
            if (NetworkPlayerController.Local != null)
                inputData = NetworkPlayerController.Local.GetNetworkInput();
        }
        else
        {
            Vector2 arrowInput = Vector2.zero;
            if (Input.GetKey(KeyCode.UpArrow)) arrowInput.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) arrowInput.y -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) arrowInput.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) arrowInput.x -= 1f;

            inputData._movementInput = arrowInput.normalized;

            // Map alternative Jump (Right Shift) and Sprint (Right Control) for Player 2
            inputData._isJumpPressed = Input.GetKey(KeyCode.RightShift);
            inputData._isSprintPressed = Input.GetKey(KeyCode.RightControl);
        }

        input.Set(inputData);
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

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // true for server or host 
        if (runner.IsServer)
        {
            Utils.DebugLog("OnPlayerJoined this is the server/host, spawning network player");

            runner.Spawn(_networkPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity, player);
        }
        else
            Utils.DebugLog("OnPlayerJoined this is the client");
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

    public void OnSceneLoadDone(NetworkRunner runner)
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
