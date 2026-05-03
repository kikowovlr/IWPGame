using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.Rendering;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float _speed; // forward + back
    [SerializeField] private float _strafeSpeed; // left + right
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [SerializeField] private float _jumpForce;
    [SerializeField] private float _cameraYOffset = 0.4f;
    private Camera _playerCamera;
    private Rigidbody _hips;
    private bool _isGrounded;

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            _playerCamera = Camera.main;
            _playerCamera.transform.position = new Vector3(transform.position.x, transform.position.y + _cameraYOffset, transform.position.z);
            _playerCamera.transform.SetParent(transform);
        }
        else
        {
            // if not owner then disable player controller - dont control other players
            gameObject.GetComponent<PlayerController>().enabled = false;
        }
    }

    private void Start()
    {
        _hips = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                _hips.AddForce(_hips.transform.forward * _speed * _runSpeedMultiplier);
            }
            else
            {
                _hips.AddForce(_hips.transform.forward * _speed);
            }
        }
    }
}
