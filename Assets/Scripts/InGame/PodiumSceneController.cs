using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PodiumSceneController : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform _firstPlaceSpawn;
    [SerializeField] private Transform _secondPlaceSpawn;
    [SerializeField] private Transform _thirdPlaceSpawn;
    [SerializeField] private Collider _crowdZone; // for rest of the players not on podium

    private void Start()
    {
        // Run the initialization safely in a coroutine to prevent race conditions
        StartCoroutine(SafeNetworkInitRoutine());
        CameraManager.Instance.RequestCursorVisible("PodiumScene", true);
    }

    private IEnumerator SafeNetworkInitRoutine()
    {
        NetworkRunner runner = null;

        // 1. Loop until we find the NetworkRunner active in the scene
        while (runner == null)
        {
            runner = FindAnyObjectByType<NetworkRunner>();
            if (runner == null) yield return null;
        }
        while (!runner.IsRunning) yield return null;
        while (runner.ActivePlayers.Count() == 0) yield return null;

        InitializePodium(runner);
    }

    public void InitializePodium(NetworkRunner runner)
    {
        StartCoroutine(PodiumSequenceRoutine(runner));
    }

    private IEnumerator PodiumSequenceRoutine(NetworkRunner runner)
    {
        ArrangePlayers(runner);

        yield return StartCoroutine(WaitForNetworkToSettle(runner)); // wait for syncing

        if (runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localPlayerObj))
        {
            PlayerRegistry.RegisterLocalPlayerTransform(localPlayerObj.transform);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGlobalInputRestrictions(InputRestrictions.None);
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.Gameplay);
            Debug.Log("[PODIUM] -> Forcing CameraManager to Gameplay mode.");
        }

        if (LevelLoader.Instance != null)
            LevelLoader.Instance.TransitionIn();
    }

    private IEnumerator WaitForNetworkToSettle(NetworkRunner runner)
    {
        yield return null; // give unity a frame to register physical scene changes

        while (!runner.TryGetPlayerObject(runner.LocalPlayer, out var localPlayerObjplayer))
        {
            yield return null; // check again next frame
        }

        yield return new WaitForEndOfFrame(); // wait for extra frame to ensure interpolation finishes
    }

    private void ArrangePlayers(NetworkRunner runner)
    {
        // get leaderboard data
        List<LeaderboardItemData> leaderboard = LeaderboardManager.Instance.GetSortedLeaderboard;

        Vector3 cameraPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        for (int i = 0; i < leaderboard.Count; i++)
        {
            PlayerRef playerRef = leaderboard[i].PlayerReference;
            if (!runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj)) continue;

            PlayerComponentRegistry registry = playerObj.GetComponent<PlayerComponentRegistry>();
            if (registry == null || registry.RespawnHandler == null) continue;

            Vector3 targetPosition = Vector3.zero;
            Quaternion targetRotation = Quaternion.identity;

            // 1st place
            if (i == 0)
            {
                targetPosition = _firstPlaceSpawn.position;
            }
            // 2nd place
            else if (i == 1)
            {
                targetPosition = _secondPlaceSpawn.position;
            }
            // 3rd place
            else if (i == 2)
            {
                targetPosition = _thirdPlaceSpawn.position;
            }
            // crown zone
            else
            {
                targetPosition = GetRandomPointInBoxCollider(_crowdZone);
            }

            if (i < 3)
            {
                // Calculate flat rotation towards camera
                Vector3 lookDir = cameraPos - targetPosition;
                lookDir.y = 0f; // Keep the character upright
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    targetRotation = Quaternion.LookRotation(lookDir.normalized);
                }
            }
            else
            {
                // Default to spawn rotation for the crowd
                targetRotation = _firstPlaceSpawn.rotation;
            }

            // teleport players
            registry.RespawnHandler.TeleportToSpawnPoint(targetPosition, targetRotation);
            playerObj.transform.rotation = targetRotation;

            if (runner.IsServer)
            {
                RPC_ResetPlayerStats(playerObj);
            }

            var nrb = playerObj.GetComponent<NetworkRigidbody3D>();
            if (nrb != null)
            {
                nrb.InterpolationTarget = playerObj.transform;
                nrb.enabled = true;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ResetPlayerStats(NetworkObject playerObj)
    {
        if (playerObj.TryGetComponent(out PlayerComponentRegistry registry))
        {
            if (registry.Health != null) registry.Health.ResetHealthToMax();
            if (registry.Elimination != null) registry.Elimination.ResetLivesToMax();
            if (registry.Controller != null) registry.Controller.StopAllCoroutines();
        }
    }

    private Vector3 GetRandomPointInBoxCollider(Collider col)
    {
        Bounds bounds = col.bounds;
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomY = bounds.min.y;
        float randomZ = Random.Range(bounds.min.z, bounds.max.z);
        return new Vector3(randomX, randomY, randomZ);
    }

    private void OnDestroy()
    {
        CameraManager.Instance.RequestCursorVisible("PodiumScene", false); // release req
    }
}
