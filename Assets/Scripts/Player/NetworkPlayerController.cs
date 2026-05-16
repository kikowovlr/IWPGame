using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ConfigurableJoint _mainJoint;
    [SerializeField] private Animator _animator;

    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 3f;
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _rotationSpeed = 300f;

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
    private Quaternion _initialJointRotation;

    private void Awake()
    {
        _activeRagdollMembers = GetComponentsInChildren<ActiveRagdollMember>();
        _initialJointRotation = _mainJoint.transform.localRotation;
    }

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            PlayerRegistry.RegisterLocalPlayerTransform(_rb.transform);
        }
        else
        {
            // if not owner then disable player controller - dont control other players
            enabled = false;
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

        float inputMagnitude = _moveInputVector.magnitude;

        Vector3 localVelocityVsForward = transform.forward * Vector3.Dot(transform.forward, _rb.linearVelocity);
        float localForwardVelocity = localVelocityVsForward.magnitude;

        if (inputMagnitude != 0)
        {
            // get cam dir vectors and flatten y
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // movement dir vector based on cam's POV
            Vector3 moveDir = (camForward * _moveInputVector.y) + (camRight * _moveInputVector.x);

            // based on camera dir
            Quaternion desiredWorldRotation = Quaternion.LookRotation(moveDir, transform.up);
            Quaternion jointSpaceRotation = Quaternion.Inverse(desiredWorldRotation) * _mainJoint.transform.rotation * _initialJointRotation;

            // rotate towards target dir
            _mainJoint.targetRotation = Quaternion.RotateTowards(_mainJoint.targetRotation, jointSpaceRotation, Time.fixedDeltaTime * _rotationSpeed);

            if (localForwardVelocity < _maxSpeed)
            {
                // move character in the dir they're facing
                _rb.AddForce(moveDir * inputMagnitude * 30);
            }
        }

        if (_isGrounded && _isJumpButtonPressed)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _isJumpButtonPressed = false;
        }

        _animator.SetFloat("MovementSpeed", localForwardVelocity * 0.4f);

        // update joints rotation based on animation
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].UpdateJointFromAnimation();
        }
    }
}
