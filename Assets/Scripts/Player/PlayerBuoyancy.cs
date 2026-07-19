using Fusion;
using UnityEngine;

/// <summary>
/// Handles water detection + buoyancy + swim movement
/// </summary>
public class PlayerBuoyancy : NetworkBehaviour
{
    [SerializeField] private float _minSubmergeDepth = 0.5f; // ignore shallow dipping

    [Header("Buoyancy Settings")]
    [SerializeField] private float _floatDepth = 0.4f;
    [SerializeField] private float _buoyancySpringStrength = 60f;
    [SerializeField] private float _buoyancyDamping = 8f;
    [SerializeField] private float _waterDrag = 2f;

    [Header("Swim Movement")]
    [SerializeField] private float _swimSpeedMultiplier = 0.5f;
    [SerializeField] private float _swimForceMultiplier = 0.6f;

    [Header("Bobbing Up and Down")]
    [SerializeField] private float _idleBobAmplitude = 0.08f;
    [SerializeField] private float _idleBobFrequency = 1.2f;
    [SerializeField] private float _bobPhaseOffset; // randomise for all players so they dont bob in sync

    [Header("Goo")]
    [SerializeField] private float _waterGooRate = 0.1f;

    [HideInInspector] [Networked] public NetworkBool IsSubmerged { get; private set; }
    private float _currentSurfaceHeight;

    [SerializeField] private Rigidbody _rb;
    [SerializeField] private bool _debugLogTransitions = true;

    public float WaterGooRate => _waterGooRate;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _rb = registry.Controller.NetworkedRb.Rigidbody;
        }
    }

    public override void Spawned()
    {
        _bobPhaseOffset = Random.Range(0f, Mathf.PI * 2f); // stagger bob timings
    }

    /// <summary>
    /// call once per tick, before movement logic
    /// </summary>
    public void UpdateSubmersionState()
    {
        bool inWater = WaterBody.TryGetSurfaceHeight(_rb.position, out float surfaceY);
        _currentSurfaceHeight = surfaceY;
        IsSubmerged = inWater && (surfaceY - _rb.position.y > _minSubmergeDepth);


        //if (_debugLogTransitions && IsSubmerged != _previousSubmergedState)
        //{
        //    if (IsSubmerged)
        //        Debug.Log($"[Buoyancy] {name} ENTERED water — surfaceY={surfaceY:F2}, playerY={_rb.position.y:F2}, depth={(surfaceY - _rb.position.y):F2}", this);
        //    else
        //        Debug.Log($"[Buoyancy] {name} EXITED water", this);

        //    _previousSubmergedState = IsSubmerged;
        //}
    }

    /// <summary>
    /// replaces ApplyGravity() when submerged
    /// </summary>
    public void ApplyBuoyancy()
    {
        float bobOffset = Mathf.Sin((Runner.SimulationTime * _idleBobFrequency) + _bobPhaseOffset) * _idleBobAmplitude;

        float targetY = _currentSurfaceHeight - _floatDepth + bobOffset;
        float compression = targetY - _rb.position.y;
        float springForce = (compression * _buoyancySpringStrength) - (_rb.linearVelocity.y * _buoyancyDamping);

        _rb.AddForce(Vector3.up * springForce, ForceMode.Force);
        _rb.AddForce(-_rb.linearVelocity * _waterDrag, ForceMode.Force); // sluggish drag on all axes
    }

    /// <summary>
    /// replaces ProcessInputMovement() when submerged
    /// </summary>
    public void ApplySwimMovement(Vector3 moveDir, float inputMagnitude, float baseMaxSpeed, float baseForce, float speedMultiplier)
    {
        float dynamicMaxSpeed = baseMaxSpeed * speedMultiplier * _swimSpeedMultiplier;

        if (_rb.linearVelocity.magnitude < dynamicMaxSpeed * inputMagnitude)
        {
            _rb.AddForce(moveDir * (baseForce * speedMultiplier * _swimForceMultiplier), ForceMode.Force);
        }
    }

    public void
}
