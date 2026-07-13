using Fusion;
using System;
using UnityEngine;

public class PlayerEliminationHandler : NetworkBehaviour
{
    [Header("Life Settings")]
    [SerializeField] private int _maxLives = 3;
    [SerializeField] private float _spectatorTransitionDuration = 6f;

    [Networked] public int CurrentLives { get; private set; }
    [Networked, OnChangedRender(nameof(OnEliminationStatusChanged))] public bool IsEliminated { get; private set; }
    [Networked] private TickTimer SpectatorTransitionTimer { get; set; }
    [Networked, OnChangedRender(nameof(OnSpectatorTransitionCompleteChanged))] public bool IsSpectatorTransitionComplete { get; private set; }

    private PlayerHealthHandler _healthHandler;
    private NetworkPlayerController _playerController;

    // events
    public static event Action<PlayerEliminationHandler> OnPlayerEliminated; // for showing local UI screen, triggering gray screen
    public static event Action<PlayerEliminationHandler> OnPlayerSpectatorReady; // for camera swapping, hiding body, etc

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _healthHandler = registry.Health;
            _playerController = registry.Controller;
        }
    }

    public override void Spawned()
    {
        // init hearts on spawned
        if (Object.HasStateAuthority)
        {
            CurrentLives = _maxLives;
            IsEliminated = false;
            IsSpectatorTransitionComplete = false;
        }
    }

    /// <summary>
    /// deducts one life point, only called by state authority
    /// call this after knocked out fully
    /// </summary>
    public void DeductLife()
    {
        if (!Object.HasStateAuthority || IsEliminated) return;

        if (CurrentLives > 0)
        {
            CurrentLives--;

            if (CurrentLives <= 0)
            {
                IsEliminated = true;

                // start to spectator countdown
                SpectatorTransitionTimer = TickTimer.CreateFromSeconds(Runner, _spectatorTransitionDuration); 
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsEliminated && !IsSpectatorTransitionComplete)
        {
            // check if timer is done
            if (SpectatorTransitionTimer.Expired(Runner))
            {
                IsSpectatorTransitionComplete = true;
            }
        }
    }

    /// <summary>
    /// triggered on ALL clients when IsEliminated changes
    /// </summary>
    private void OnEliminationStatusChanged()
    {
        if (IsEliminated)
        {
            OnPlayerEliminated?.Invoke(this);
            HandleLocalElimination();
        }
    }

    /// <summary>
    /// triggers elimination effects and disables controls
    /// </summary>
    private void HandleLocalElimination()
    {
        if (Object.HasStateAuthority)
        {

        }
        // TODO: show spectator UI
    }

    /// <summary>
    /// triggers on all clients ONCE 
    /// </summary>
    private void OnSpectatorTransitionCompleteChanged()
    {
        if (IsSpectatorTransitionComplete)
            OnPlayerSpectatorReady?.Invoke(this);
    }
}
