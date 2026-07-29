using Fusion;
using UnityEngine;

public class PlayerBoost : NetworkBehaviour
{
    [Header("Boost Settings")]
    [SerializeField] private float _boostSpeedMultiplier = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] private float _airControlFactor = 0.35f;

    private Rigidbody _rb;
    private NetworkPlayerController _controller;
    private PlayerComponentRegistry _registry;

    [HideInInspector] [Networked] public NetworkBool IsBoosting { get; private set; }
    [Networked] private TickTimer _boostTimer { get; set; }

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _rb = registry.Controller.NetworkedRb.Rigidbody;
            _controller = registry.Controller;
        }
    }

    /// <summary>
    /// launches ragdoll using trajectory vector
    /// </summary>
    public void ApplyBoost(Vector3 forwardDir, float forwardSpeed, float upwardSpeed, float duration)
    {
        if (!Object.HasStateAuthority && !Object.HasInputAuthority) return;

        forwardDir.y = 0f;
        forwardDir.Normalize();
        Vector3 launch = forwardDir * forwardSpeed + Vector3.up * upwardSpeed;

        _rb.linearVelocity = Vector3.zero;
        _rb.AddForce(launch, ForceMode.VelocityChange); // ignore mass for crisp

        IsBoosting = true;
        _boostTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public void ApplyTargetSpeedMultiplier()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsBoosting) return;

        if (_boostTimer.Expired(Runner))
        {
            IsBoosting = false;
            return;
        }

        _controller.TargetSpeedMultiplier *= _boostSpeedMultiplier;
    }

    /// <summary>
    /// 
    /// </summary>
    public float GetAirControlFactor()
    {
        return IsBoosting ? _airControlFactor : 1f;
    }
}
