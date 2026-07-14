using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class PodiumSceneController : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform _firstPlaceSpawn;
    [SerializeField] private Transform _secondPlaceSpawn;
    [SerializeField] private Transform _thirdPlaceSpawn;
    [SerializeField] private Collider _crowdZone; // for rest of the players not on podium

    private void Start()
    {
        ArrangePlayers();
    }

    private void ArrangePlayers()
    {
        if (GameManager.Instance == null || !GameManager.Instance.Object.HasStateAuthority) return;

        NetworkRunner runner = GameManager.Instance.Runner;

        // get leaderboard data
        List<LeaderboardItemData> leaderboard = LeaderboardManager.Instance.GetSortedLeaderboard;

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
                targetRotation = _firstPlaceSpawn.rotation;
            }
            // 2nd place
            else if (i == 1)
            {
                targetPosition = _secondPlaceSpawn.position;
                targetRotation = _secondPlaceSpawn.rotation;
            }
            // 3rd place
            else if (i == 2)
            {
                targetPosition = _thirdPlaceSpawn.position;
                targetRotation = _thirdPlaceSpawn.rotation;
            }
            // crown zone
            else
            {
                targetPosition = GetRandomPointInBoxCollider(_crowdZone);
                targetRotation = _firstPlaceSpawn.rotation;
            }

            // teleport players
            registry.RespawnHandler.TeleportToSpawnPoint(targetPosition, targetRotation);
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
}
