using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.Collections.Unicode;

public class NetworkPodiumArranger : NetworkBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform _firstPlaceSpawn;
    [SerializeField] private Transform _secondPlaceSpawn;
    [SerializeField] private Transform _thirdPlaceSpawn;
    [SerializeField] private Collider _crowdZone;

    private bool _arranged;
    // re-assert for a few ticks in case sim tries to drift them (cheap safety)
    private int _reassertTicks;

    private struct Placement { public Vector3 pos; public Quaternion rot; }
    private readonly Dictionary<PlayerRef, Placement> _placements = new();

    public static NetworkPodiumArranger Instance { get; private set; }
    public override void Spawned() 
    { 
        Instance = this;

        if (Runner != null)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out var obj))
                {
                    var nrb = obj.GetComponent<NetworkRigidbody3D>();
                    if (nrb != null)
                    {
                        nrb.InterpolationTarget = obj.transform; 
                        nrb.enabled = true;
                    }
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (_arranged)
        {
            ReassertPlacements();
            return;
        }

        // wait until all leaderboard players are resolvable
        if (LeaderboardManager.Instance == null) return;
        var leaderboard = LeaderboardManager.Instance.GetSortedLeaderboard;
        if (leaderboard == null || leaderboard.Count == 0) return;

        // ensure every player object exists before arranging
        foreach (var item in leaderboard)
            if (!Runner.TryGetPlayerObject(item.PlayerReference, out _))
                return;   // not ready yet, try next tick

        ArrangePlayers(leaderboard);
        _arranged = true;
    }

    private void ArrangePlayers(List<LeaderboardItemData> leaderboard)
    {
        Vector3 cameraPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        for (int i = 0; i < leaderboard.Count; i++)
        {
            PlayerRef playerRef = leaderboard[i].PlayerReference;
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj)) continue;

            PlayerComponentRegistry registry = playerObj.GetComponent<PlayerComponentRegistry>();
            if (registry == null || registry.RespawnHandler == null) continue;

            Vector3 targetPosition;
            Quaternion targetRotation = Quaternion.identity;

            if (i == 0) targetPosition = _firstPlaceSpawn.position;
            else if (i == 1) targetPosition = _secondPlaceSpawn.position;
            else if (i == 2) targetPosition = _thirdPlaceSpawn.position;
            else targetPosition = GetRandomPointInBoxCollider(_crowdZone);

            if (i < 3)
            {
                Vector3 lookDir = cameraPos - targetPosition;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                    targetRotation = Quaternion.LookRotation(lookDir.normalized);
            }
            else targetRotation = _firstPlaceSpawn.rotation;

            // recover + zero velocity so physics doesn't drag them
            if (registry.Controller != null)
            {
                registry.Controller.StopAllCoroutines();
                registry.Controller.Recover(playAnim: false);
                var rb = registry.Controller.NetworkedRb;
                if (rb != null && rb.Rigidbody != null)
                {
                    rb.Rigidbody.linearVelocity = Vector3.zero;
                    rb.Rigidbody.angularVelocity = Vector3.zero;
                }
            }
            if (registry.Health != null) registry.Health.ResetHealthToMax();
            if (registry.Elimination != null) registry.Elimination.ResetLivesToMax();
            if (registry.Drowning != null) registry.Drowning.ResetDrownState();

            // teleport — now INSIDE FixedUpdateNetwork, so it's authoritative
            registry.RespawnHandler.TeleportToSpawnPoint(targetPosition, targetRotation);

            _placements[playerRef] = new Placement { pos = targetPosition, rot = targetRotation };
        }
    }

    private void ReassertPlacements()
    {
        if (_reassertTicks > 20) return;   // ~20 ticks then stop
        _reassertTicks++;
        foreach (var kvp in _placements)
        {
            if (Runner.TryGetPlayerObject(kvp.Key, out NetworkObject obj)
                && obj.TryGetComponent(out PlayerComponentRegistry reg)
                && reg.RespawnHandler != null)
            {
                reg.RespawnHandler.TeleportToSpawnPoint(kvp.Value.pos, kvp.Value.rot);
            }
        }
    }

    private Vector3 GetRandomPointInBoxCollider(Collider col)
    {
        Bounds b = col.bounds;
        return new Vector3(Random.Range(b.min.x, b.max.x), b.min.y, Random.Range(b.min.z, b.max.z));
    }
}