using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// sits as kinematic and inert until tilting begins and it comes crashes down
/// gravity + small amts of torque for a slow to fast crash effect
/// once submerged in water, slowly sink down to bottom
/// </summary>
public class FallingIslandPiece : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private NetworkRigidbody3D _networkRb;
    [SerializeField] private MeshCollider _staticCollider;
    [SerializeField] private BoxCollider _fallingCollider; // simplified convex collider - used only when tilting

    [Header("Timing")]
    [SerializeField] private float _warningDelay = 1.5f;

    [Header("Tilt")]
    [SerializeField] private Vector3 _tiltDirection = Vector3.forward; // base lean direction, set per piece toward "outward"
    [SerializeField] private float _tiltTorque = 2f;
    [SerializeField] private float _maxTiltAngle = 15f; // leaning
    [SerializeField] private float _randomTiltVariance = 30f; // degrees of random spread each time it breaks

    [Header("Water Sinking")]
    [SerializeField] private float _sinkEngageDistance = 2f; // splash triggers, sinking begins
    [SerializeField] private float _sinkRestDepth = 4f;
    [SerializeField] private float _sinkSpeed = 0.3f;

    [HideInInspector] [Networked, OnChangedRender(nameof(OnTiltingStateChanged))] public NetworkBool IsTilting { get; private set; }
    [HideInInspector][Networked, OnChangedRender(nameof(OnSinkEngagedChanged))] public NetworkBool HasEngagedSinking { get; private set; }

    [Networked] private TickTimer _warningTimer { get; set; }

    private Quaternion _startRotation;
    private float _waterSurfaceHeight;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;

    public override void Spawned()
    {
        // capture the piece's starting transform once, so we know what to reset back to
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
    }

    public void BeginTilt() 
    {
        if (!Object.HasStateAuthority) return;
        if (IsTilting) return; // alrdy broken

        _startRotation = transform.rotation;

        // randomise lean dir
        float randomYaw = Random.Range(-_randomTiltVariance, _randomTiltVariance);
        _tiltDirection = Quaternion.Euler(0, randomYaw, 0) * _tiltDirection;

        _warningTimer = TickTimer.CreateFromSeconds(Runner, _warningDelay);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (!IsTilting)
        {
            if (_warningTimer.IsRunning && _warningTimer.Expired(Runner))
                IsTilting = true; // triggers OnTiltingStateChanged for clients

            return;
        }

        // check proximity to water, if top is close enough to surface, stop from dropping further
        bool shouldEngageSinking = CheckNearWaterSurface(out _waterSurfaceHeight);

        // apply lean torque
        if (!shouldEngageSinking)
        {
            // only tilt while still falling freely, before sinking engages
            float currentTilt = Quaternion.Angle(_startRotation, transform.rotation);
            if (currentTilt < _maxTiltAngle)
            {
                _rb.AddTorque(_tiltDirection * _tiltTorque, ForceMode.Force);
            }
        }
        else
        {
            if (!HasEngagedSinking)
            {
                HasEngagedSinking = true;

                _rb.isKinematic = true;
            }

            float targetY = _waterSurfaceHeight - _sinkRestDepth;

            if (_rb.position.y > targetY)
            {
                // move straight down at a fixed rate, no overshoot possible since we clamp below
                Vector3 newPos = _rb.position + Vector3.down * _sinkSpeed * Runner.DeltaTime;
                newPos.y = Mathf.Max(newPos.y, targetY); // clamp — can never go past the target
                _rb.MovePosition(newPos);
            }
        }
    }
        
    private bool CheckNearWaterSurface(out float waterSurfaceHeight)
    {
        Collider activeCollider = _fallingCollider.enabled ? (Collider)_fallingCollider : _staticCollider;
        float pieceTopY = activeCollider.bounds.max.y;

        if (!WaterBody.TryGetSurfaceHeight(_rb.position, out waterSurfaceHeight))
            return false;

        float distanceAboveSurface = pieceTopY - waterSurfaceHeight;
        return distanceAboveSurface <= _sinkEngageDistance;
    }

    private void OnTiltingStateChanged()
    {
        if (!IsTilting) return;

        _staticCollider.enabled = false;
        _fallingCollider.enabled = true;
        _rb.isKinematic = false;
    }

    /// </summary>
    private void OnSinkEngagedChanged()
    {
        if (!HasEngagedSinking) return;

        // TODO: splash VFX + sound + camera shake
    }

    /// <summary>
    /// call at the start of round
    /// </summary>
    public void ResetPiece()
    {
        if (!Object.HasStateAuthority) return;

        IsTilting = false;
        HasEngagedSinking = false;
        _warningTimer = TickTimer.None;

        _rb.isKinematic = true;

        _networkRb.Teleport(_originalPosition, _originalRotation);

        _staticCollider.enabled = true;
        _fallingCollider.enabled = false;

        Debug.Log($"[FallingPiece] {name}: RESET via Teleport to {_originalPosition}");
    }
}
