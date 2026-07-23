using Fusion;
using UnityEngine;

/// <summary>
/// Drown sink + camera detach sequence
/// </summary>
public class PlayerDrowning : NetworkBehaviour
{
    [Header("Sink Settings")]
    [SerializeField] private float _restDepth = 3f; // how low below surface
    [SerializeField] private float _springStrength = 15f;
    [SerializeField] private float _damping = 6f;
    [SerializeField] private float _tumbleTorque = 3f; // yaw only spin while sinking
    [SerializeField] private float _maxSinkSpeed = 0.8f;
    [SerializeField] private Transform _cameraFollowProxy;
    private float _frozenY;
    private Rigidbody _rb;
    private PlayerBuoyancy _buoyancy;
    private NetworkPlayerController _controller;

    public Transform CameraFollowProxy => _cameraFollowProxy;


    [HideInInspector] [Networked, OnChangedRender(nameof(OnSinkingStateChanged))] public NetworkBool IsSinking { get; private set; }

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _rb = registry.Controller.NetworkedRb.Rigidbody;
            _buoyancy = registry.Buoyancy;
            _controller = registry.Controller;
        }
    }

    public override void Spawned()
    {
        if (_controller != null)
            PlayerRegistry.RegisterDrowning(_controller.CameraTarget, this);
    }

    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        if (_controller != null)
            PlayerRegistry.UnregisterDrowning(_controller.CameraTarget);
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsSinking) return;
        if (GameManager.Instance != null && GameManager.Instance.IsCurrentlyOnPodiumScene())
            return;

        if (Object.HasStateAuthority)
        {
            _buoyancy.ApplyDrownSink(_restDepth, _springStrength, _damping);
            _rb.AddTorque(Vector3.up * _tumbleTorque, ForceMode.Force); // Y only

            // hard clamp so it never sinks faster than this
            if (_rb.linearVelocity.y < -_maxSinkSpeed)
            {
                Vector3 v = _rb.linearVelocity;
                v.y = -_maxSinkSpeed;
                _rb.linearVelocity = v;
            }
        }

        // runs on every client, keeps pruxy tracking XZ but not Y
        if (_cameraFollowProxy != null)
            _cameraFollowProxy.position = new Vector3(_rb.position.x, _frozenY, _rb.position.z);
    }

    public void BeginSink()
    {
        if (!Object.HasStateAuthority) return;
        IsSinking = true;
    }

    /// <summary>
    /// called on round reset/respawn
    /// </summary>
    public void ResetDrownState()
    {
        if (!Object.HasStateAuthority) return;
        IsSinking = false;
    }

    private void OnSinkingStateChanged()
    {
        if (IsSinking)
        {
            _frozenY = _rb.position.y;
            if (CameraManager.Instance != null)
                CameraManager.Instance.SwapCameraFollowTarget(_controller.CameraTarget, _cameraFollowProxy);
        }
        else
        {
            // fires when ResetState() flips it back on respawn
            if (CameraManager.Instance != null)
                CameraManager.Instance.SwapCameraFollowTarget(_cameraFollowProxy, _controller.CameraTarget);
        }
    }
}
