using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ConfigurableJoint _mainJoint;

    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 3f;
    [SerializeField] private float _jumpForce = 10f;

    //Input
    Vector2 _moveInputVector = Vector2.zero;
    bool _isJumpButtonPressed = false;

    //States
    private bool _isGrounded;
    private bool _isRunning;

    //Raycasts
    RaycastHit[] _raycastHits = new RaycastHit[10];

    //Syncing of ragdoll parts
    ActiveRagdollMember[] _activeRagdollMembers;

    private void Awake()
    {
        _activeRagdollMembers = GetComponentsInChildren<ActiveRagdollMember>();
    }

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner)
        {
            // if not owner then disable player controller - dont control other players
            gameObject.GetComponent<NetworkPlayerController>().enabled = false;
        }
    }

    private void Update()
    {
        // move input
        _moveInputVector.x = Input.GetAxis("Horizontal");
        _moveInputVector.y = Input.GetAxis("Vertical");

        if (Input.GetKeyDown(KeyCode.Space))
            _isJumpButtonPressed = true;
    }

    private void FixedUpdate()
    {
        // assume we are not grounded
        _isGrounded = false;

        // check if we are grounded
        int numOfHits = Physics.SphereCastNonAlloc(_rb.position, 0.1f, transform.up * -1, _raycastHits, 0.5f);

        // check for valid results
        for (int i = 0; i < numOfHits; i++)
        {
            //ignore self hits
            if (_raycastHits[i].transform.root == transform)
                continue;

            _isGrounded = true;
            break;
        }

        // apply more gravity to make character less floaty
        if (!_isGrounded)
            _rb.AddForce(Vector3.down * 10);

        float inputMagnitued = _moveInputVector.magnitude;

        if (inputMagnitued != 0)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(new Vector3(_moveInputVector.x, 0, _moveInputVector.y * -1), transform.up);

            // rotate towards target dir
            _mainJoint.targetRotation = Quaternion.RotateTowards(_mainJoint.targetRotation, desiredRotation, Time.fixedDeltaTime * 300);

            Vector3 localVelocityVsForward = transform.forward * Vector3.Dot(transform.forward, _rb.linearVelocity);

            float localForwardVelocity = localVelocityVsForward.magnitude;

            if (localForwardVelocity < _maxSpeed)
            {
                // move character in the dir they're facing
                _rb.AddForce(transform.forward * inputMagnitued * 30);
            }
        }

        if (_isGrounded && _isJumpButtonPressed)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _isJumpButtonPressed = false;
        }

        // update joints rotation based on animation
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].UpdateJointFromAnimation();
        }
    }
}
