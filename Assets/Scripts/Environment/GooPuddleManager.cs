using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// spawns goo puddles  at random intervals from pre-placed candidate spots
/// randomises between puddle prefabs
/// </summary>
public class GooPuddleManager : NetworkBehaviour
{
    public static GooPuddleManager Instance { get; private set; }

    [System.Serializable]
    public class PuddleSpawnPoint
    {
        public Transform point;
        public FallingIslandPiece parentTile; // leave null if this spot is on permanent ground
    }

    [Header("Puddle Prefabs")]
    [SerializeField] private NetworkObject[] _puddlePrefabs;

    [Header("Spawn Points")]
    [SerializeField] private PuddleSpawnPoint[] _candidateSpawnPoints;

    [Header("Timing")]
    [SerializeField] private float _initialDelay = 20f;
    [SerializeField] private float _minSpawnInterval = 15f;
    [SerializeField] private float _maxSpawnInterval = 30f;
    [SerializeField] private int _maxActivePuddles = 4;

    [SerializeField] private float _telegraphDuration = 1f;

    [Networked] private NetworkBool _sequenceStarted { get; set; }
    [Networked] private TickTimer _nextSpawnTimer { get; set; }
    private List<NetworkObject> _activePuddles = new List<NetworkObject>();

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }


    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (GameManager.Instance == null) return;

        RoundState currentState = GameManager.Instance.CurrentRoundState;

        // start sequence only when round starts
        if (!_sequenceStarted && currentState == RoundState.RoundActive)
        {
            _sequenceStarted = true;
            _nextSpawnTimer = TickTimer.CreateFromSeconds(Runner, _initialDelay);
        }

        if (!_sequenceStarted) return;
        if (!_nextSpawnTimer.Expired(Runner)) return;

        // clean up any despawned puddles
        _activePuddles.RemoveAll(p => p == null);

        if (_activePuddles.Count < _maxActivePuddles)
            SpawnRandomPuddle();

        _nextSpawnTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(_minSpawnInterval, _maxSpawnInterval));
    }

    private void SpawnRandomPuddle()
    {
        if (_puddlePrefabs.Length == 0 || _candidateSpawnPoints.Length == 0) return;

        PuddleSpawnPoint spawnPoint = _candidateSpawnPoints[Random.Range(0, _candidateSpawnPoints.Length)];
        NetworkObject chosenPrefab = _puddlePrefabs[Random.Range(0, _puddlePrefabs.Length)];

        NetworkObject spawned = Runner.Spawn(chosenPrefab, spawnPoint.point.position, spawnPoint.point.rotation);
        _activePuddles.Add(spawned);

        if (spawned.TryGetComponent(out GooPuddle puddle))
        {
            puddle.BeginTelegraph(_telegraphDuration);

            if (spawnPoint.parentTile != null)
                puddle.AttachToTile(spawnPoint.parentTile);
        }
    }

    public void ResetForNewRound()
    {
        if (!Object.HasStateAuthority) return;

        foreach (var puddle in _activePuddles)
        {
            if (puddle != null)
                Runner.Despawn(puddle);
        }
        _activePuddles.Clear();

        _sequenceStarted = false;
    }
}
