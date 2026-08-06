using UnityEngine;
using Fusion;

public class PlayerFallHandler : NetworkBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float _fallDeathY = -6f;
    [SerializeField] private float _knockoutToDeathDelay = 2f;
    [SerializeField] private Transform _cameraFollowProxy;

    [Networked, OnChangedRender(nameof(OnFallingStateChanged))] private NetworkBool IsFalling { get; set; }
    [Networked] private Vector3 _frozenFallPos { get; set; }
    [Networked] private TickTimer _deathTimer { get; set; }

    private PlayerComponentRegistry _registry;
    private NetworkPlayerController _controller;
    private PlayerEliminationHandler _elimination;
    private Rigidbody _rb;

    private void Awake()
    {
        _registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (_registry != null)
        {
            _controller = _registry.Controller;
            _elimination = _registry.Elimination;
            _rb = _controller.NetworkedRb.Rigidbody;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && _controller != null)
        {
            // already falling -> wait for the death timer, then eliminate
            if (IsFalling)
            {
                if (_deathTimer.Expired(Runner))
                {
                    _deathTimer = TickTimer.None;
                    if (_elimination != null)
                        _elimination.Eliminate();
                }
                return;
            }

            // don't re-trigger on an already-eliminated body that happens to be below the line
            if (_elimination != null && _elimination.IsEliminated) return;

            // detect crossing below the kill line
            if (_rb.position.y < _fallDeathY)
                BeginFall();
        }

        if (IsFalling && _cameraFollowProxy != null)
            _cameraFollowProxy.position = _frozenFallPos;
    }

    private void BeginFall()
    {
        Debug.Log($"[FALL] BeginFall (host) pos={_controller.CameraTarget.position}");

        Vector3 p = _controller.CameraTarget.position;
        _frozenFallPos = p;

        IsFalling = true;
        _deathTimer = TickTimer.CreateFromSeconds(Runner, _knockoutToDeathDelay);

        _controller.Knockout();   // ragdoll so they can't cling to the sides
    }

    // camera swap on every client
    private void OnFallingStateChanged()
    {
        Debug.Log($"[FALL] OnFallingStateChanged fired: IsFalling={IsFalling}, cam={(CameraManager.Instance != null)}, proxy={(_cameraFollowProxy != null)}");
        if (CameraManager.Instance == null || _controller == null) return;

        if (IsFalling)
        {
            if (_cameraFollowProxy != null)
                _cameraFollowProxy.position = _frozenFallPos;   // fully frozen, no live tracking
            CameraManager.Instance.SwapCameraFollowTarget(_controller.CameraTarget, _cameraFollowProxy);
            Debug.Log($"[FALL] Swapped camera to proxy at {_frozenFallPos}");
        }
        else
        {
            CameraManager.Instance.SwapCameraFollowTarget(_cameraFollowProxy, _controller.CameraTarget);
        }
    }

    public void ResetFallState()
    {
        if (!Object.HasStateAuthority) return;
        IsFalling = false;
        _deathTimer = TickTimer.None;
    }
}
