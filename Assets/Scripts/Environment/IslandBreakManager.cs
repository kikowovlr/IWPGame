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
    [SerializeField] private float _warningDuration = 2.5f; /// how long ! shows before island breaks

    [Networked] private TickTimer _nextBreakTimer { get; set; }
    [Networked] private NetworkBool _sequenceStarted { get; set; }
    [Networked, Capacity(MAX_PIECES)] private NetworkArray<NetworkBool> _hasBroken => default;

    // warning phase
    [Networked] private TickTimer _warningTimer { get; set; }
    [Networked] private int _warningIndex { get; set; }  // which piece is currently warned (-1 = none)
    // fires on all clients when the warned piece changes, so the indicator shows/hides everywhere
    [Networked, OnChangedRender(nameof(OnWarningChanged))] private int _warningTick { get; set; }
    private int _lastShownWarningIndex = -1;

    private List<int> _availableIndices = new List<int>(); // temporary store remaining pieces left

    public override void Spawned()
    {
        Instance = this;
        if (Object.HasStateAuthority) 
            _warningIndex = -1;
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

        // warning phase -> wait for this to finish then break
        if (_warningIndex >= 0)
        {
            if (_warningTimer.Expired(Runner))
            {
                // break warned piece
                int idx = _warningIndex;
                _pieces[idx].BeginTilt();

                // clear warning
                _warningIndex = -1;
                _warningTick++;

                // set next interval timing
                float nextInterval = Random.Range(_minInterval, _maxInterval);
                _nextBreakTimer = TickTimer.CreateFromSeconds(Runner, nextInterval);
            }
            return; // dont start another warning while active
        }

        if (!_nextBreakTimer.Expired(Runner)) return;

        BeginWarningForRandomPiece();
    }

    private void BeginWarningForRandomPiece()
    {
        _availableIndices.Clear();
        for (int i = 0; i < _pieces.Length; i++)
            if (!_hasBroken[i]) _availableIndices.Add(i);

        if (_availableIndices.Count == 0)
            return;

        int chosen = _availableIndices[Random.Range(0, _availableIndices.Count)];

        // mark broken now so it isn't chosen again, but tilt happens after the warning
        _hasBroken.Set(chosen, true);

        _warningIndex = chosen;
        _warningTimer = TickTimer.CreateFromSeconds(Runner, _warningDuration);
        _warningTick++;   // fire OnWarningChanged on all clients -> show indicator
    }

    // runs on ALL whenever _warningTick changes
    private void OnWarningChanged()
    {
        // hide the previously shown indicator
        if (_lastShownWarningIndex >= 0 && _lastShownWarningIndex < _pieces.Length)
            _pieces[_lastShownWarningIndex]?.HideBreakWarning();

        // show the new one
        if (_warningIndex >= 0 && _warningIndex < _pieces.Length)
            _pieces[_warningIndex]?.ShowBreakWarning();

        _lastShownWarningIndex = _warningIndex;
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
