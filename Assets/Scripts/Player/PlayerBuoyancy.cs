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

    [Header("Shore Assist")]
    [SerializeField] private LayerMask _shoreAssistLayer; // environment layer
    [SerializeField] private float _shoreAssistRadius = 1.5f; // how close a wall needs to be to trigger the boost
    [SerializeField] private float _shoreAssistMaxDepthBelowSurface = 1.5f; // only assist near-surface ledges, not deep underwater walls
    [SerializeField] private float _shoreAssistUpForce = 6f;
    [SerializeField] private float _shoreAssistForwardForce = 4f;
    [SerializeField] private float _shoreAssistSphereCastRadius = 0.25f;
    [SerializeField] private float _shoreAssistSphereCastPullback = 0.4f;
    [SerializeField] private float _maxWallNormalDot = 0.6f; // check for angle of surface

    [Header("Shore Assist - Height Scaling")]
    [SerializeField] private bool _scaleForceByLedgeHeight = true;
    [SerializeField] private float _minLedgeHeight = 0.3f;   // ledges at/below this height get the base force
    [SerializeField] private float _maxLedgeHeight = 3f;      // ledges at/above this get max force
    [SerializeField] private float _minUpForceMultiplier = 0.8f;
    [SerializeField] private float _maxUpForceMultiplier = 1.6f;
    [SerializeField] private float _ledgeProbeHeight = 5f;    // how high above the wall to start probing downward

    [Header("Goo")]
    [SerializeField] private float _waterGooRate = 0.1f;

    [HideInInspector] [Networked] public NetworkBool IsSubmerged { get; private set; }
    private float _currentSurfaceHeight;
    private Rigidbody _rb;

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
    }

    /// <summary>
    /// replaces ApplyGravity() when submerged
    /// </summary>
    public void ApplyBuoyancy()
    {
        float sinWave = Mathf.Sin((Runner.SimulationTime * _idleBobFrequency) + _bobPhaseOffset);
        float bobOffset = (sinWave - 1f) * 0.5f * _idleBobAmplitude; // remaps to 0 (at peak) down to -amplitude

        float targetY = _currentSurfaceHeight - _floatDepth + bobOffset;
        float compression = targetY - _rb.position.y;
        float springForce = (compression * _buoyancySpringStrength) - (_rb.linearVelocity.y * _buoyancyDamping);

        if (springForce < 0f) springForce = 0f; // don't fight upward movement

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

    /// <summary>
    /// settle dead body below water surface after death instead of free falling
    /// similar to ApplyBuoyancy()
    /// </summary>
    public void ApplyDrownSink(float restDepth, float springStrength, float damping)
    {
        float targetY = _currentSurfaceHeight - restDepth;
        float compression = targetY - _rb.position.y;
        float springForce = (compression * springStrength) - (_rb.linearVelocity.y * damping);

        _rb.AddForce(Vector3.up * springForce, ForceMode.Force);
        _rb.AddForce(-_rb.linearVelocity * _waterDrag, ForceMode.Force);
    }

    /// <summary>
    /// pushes player up when near shore
    /// </summary>
    public void ApplyShoreAssist(Vector3 moveInputDir, float inputMagnitude)
    {
        if (inputMagnitude < 0.1f) return;

        Vector3 flatInputDir = new Vector3(moveInputDir.x, 0f, moveInputDir.z).normalized;
        if (flatInputDir.sqrMagnitude < 0.0001f) return;

        // cast from just below the surface, forgiving sphere radius instead of a thin ray
        Vector3 nearSurfacePos = new Vector3(_rb.position.x, _currentSurfaceHeight - 0.3f, _rb.position.z);
        Vector3 castOrigin = nearSurfacePos - (flatInputDir * _shoreAssistSphereCastPullback);

        float castDistance = _shoreAssistRadius + _shoreAssistSphereCastPullback;

        bool didHit = Physics.SphereCast(castOrigin, _shoreAssistSphereCastRadius, flatInputDir, out RaycastHit hit, _shoreAssistRadius, _shoreAssistLayer);

        if (!didHit) return;

        // use actual distance from player isntead of pulled back distance to decide if close enough to shoreline
        float realDistanceToWall = hit.distance - _shoreAssistSphereCastPullback;
        if (realDistanceToWall > _shoreAssistRadius)
            return;

        float depthBelowSurface = _currentSurfaceHeight - hit.point.y;
        if (depthBelowSurface < 0f || depthBelowSurface > _shoreAssistMaxDepthBelowSurface)
            return;

        float normalDot = Vector3.Dot(hit.normal, Vector3.up);
        bool isWallLike = normalDot < _maxWallNormalDot;
        if (!isWallLike)
            return;

        float upForceMultiplier = 1f;

        if (_scaleForceByLedgeHeight)
        {
            float ledgeHeight = GetLedgeHeightAboveSurface(hit.point, flatInputDir);

            if (ledgeHeight >= 0f) // -1 means probe failed, just keep default multiplier of 1
            {
                float t = Mathf.InverseLerp(_minLedgeHeight, _maxLedgeHeight, ledgeHeight);
                upForceMultiplier = Mathf.Lerp(_minUpForceMultiplier, _maxUpForceMultiplier, t);
            }
        }

        Vector3 assistForce = (Vector3.up * _shoreAssistUpForce * upForceMultiplier) + (flatInputDir * _shoreAssistForwardForce);
        _rb.AddForce(assistForce, ForceMode.Force);
    }

    private float GetLedgeHeightAboveSurface(Vector3 wallHitPoint, Vector3 flatInputDir)
    {
        // step slightly past the wall face so we're hitting the TOP of the ledge
        Vector3 probeStart = wallHitPoint + (flatInputDir * 0.3f) + (Vector3.up * _ledgeProbeHeight);

        if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit topHit, _ledgeProbeHeight + 2f, _shoreAssistLayer))
            return topHit.point.y - _currentSurfaceHeight;

        return -1f;
    }
}
