using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class IslandBreakManager : NetworkBehaviour
{
    public static IslandBreakManager Instance { get; private set; }

    private const int MAX_PIECES = 8;

    [SerializeField] private FallingIslandPiece[] _pieces = new FallingIslandPiece[MAX_PIECES];
    [SerializeField] private float _initialDelay = 60f; // time until pieces start to break
    [SerializeField] private float _minInterval = 15f;
    [SerializeField] private float _maxInterval = 30f;

    [Networked] private TickTimer _nextBreakTimer { get; set; }
    [Networked] private NetworkBool _sequenceStarted { get; set; }
    [Networked, Capacity(MAX_PIECES)] private NetworkArray<NetworkBool> _hasBroken => default;

    private List<int> _availableIndices = new List<int>(); // temporary store remaining pieces left

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
            _nextBreakTimer = TickTimer.CreateFromSeconds(Runner, _initialDelay);
        }

        if (!_sequenceStarted) return;
        if (!_nextBreakTimer.Expired(Runner)) return;

        TryBreakRandomPiece();

        // set next interval timing
        float nextInterval = Random.Range(_minInterval, _maxInterval);
        _nextBreakTimer = TickTimer.CreateFromSeconds(Runner, nextInterval);
    }

    private void TryBreakRandomPiece()
    {
        _availableIndices.Clear();
        for (int i = 0; i < _pieces.Length; i++)
            if (!_hasBroken[i]) _availableIndices.Add(i);

        if (_availableIndices.Count == 0)
            return;

        int chosen = _availableIndices[Random.Range(0, _availableIndices.Count)];
        _hasBroken.Set(chosen, true);
        _pieces[chosen].BeginTilt();
    }

    public void ResetAllPieces()
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < _pieces.Length; i++)
        {
            if (_pieces[i] == null) continue;

            _pieces[i].ResetPiece();
            _hasBroken.Set(i, false);
        }

        _sequenceStarted = false;
    }
}
