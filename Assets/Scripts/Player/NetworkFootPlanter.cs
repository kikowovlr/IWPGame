using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(-10)]
public class NetworkFootPlanter : MonoBehaviour
{
    [Header("Rig Components")]
    [SerializeField] private TwoBoneIKConstraint _footIK;
    [SerializeField] private Transform _ikTarget;

    [Header("Character References")]
    [SerializeField] private Transform _footBone; 
    [SerializeField] private Transform _characterRoot; // for clean forward dir

    [Header("Raycast Settings")]
    [SerializeField] private float _rayYOffset = 1f;
    [SerializeField] private float _rayDistance = 1.5f;
    [SerializeField] private float _sphereRadius = 0.05f;
    [SerializeField] private float _plantedYOffset = 0.05f; // to prevent foot clipping into ground
    [SerializeField] private LayerMask _environmentLayer;

    [SerializeField] private Vector3 _boneRotationFix;
    [SerializeField] private float _transformSmoothSpeed = 15f;
    [SerializeField] private float _weightSmoothSpeed = 10f;

    private Vector3 _currentPosTarget;
    private Quaternion _currentRotTarget;
    private float _targetWeight;

    [Header("--- DEBUGGING ---")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _printStateChanges = true;
    private bool _wasGroundedLastFrame;
    private Vector3 _gizmoRayOrigin;
    private Vector3 _gizmoRayHitPoint;
    private bool _gizmoDidHit;
    private string _footName;

    private void Awake()
    {
        _footName = gameObject.name;
    }

    private IEnumerator Start()
    {
        yield return null;

        if (_footBone != null)
        {
            _currentPosTarget = _footBone.position;
            _currentRotTarget = _footBone.rotation;
        }
    }

    private void LateUpdate()
    {
        if (_footIK == null || _footBone == null || _ikTarget == null || _characterRoot == null) return;

        // reset weight to default to raw animation clip unless ground is hit
        //_footIK.weight = 0f;

        // capture where raw walking animation wants foot to be rn
        Vector3 animatedFootPos = _footBone.position;

        // cast ray down from slightly above the animated foot
        Vector3 rayOrigin = new Vector3(animatedFootPos.x, _characterRoot.position.y + _rayYOffset, animatedFootPos.z);

        // Save for Gizmos
        _gizmoRayOrigin = rayOrigin;
        _gizmoDidHit = false;
        bool grounded = false;

        if (Physics.SphereCast(rayOrigin, _sphereRadius, Vector3.down, out RaycastHit hitInfo, _rayDistance, _environmentLayer))
        {
            // Save for Gizmos
            _gizmoDidHit = true;
            _gizmoRayHitPoint = hitInfo.point;

            // absolute ground height 
            float floorHeight = hitInfo.point.y;

            // is walking anim trying to step below the actual ground level?
            if (animatedFootPos.y <= floorHeight + 0.1f)
            {
                grounded = true;
                // Save for Gizmos
                //if (_printStateChanges && !_wasGroundedLastFrame)
                    //Debug.Log($"<color=green>[IK DEBUG] {_footName} is now GROUNDED.</color> Ray hit: {hitInfo.collider.name}");
                _wasGroundedLastFrame = true;

                // pos target cleanly on the ground
                Vector3 targetPos = hitInfo.point;
                targetPos.y += _plantedYOffset;
                _currentPosTarget = targetPos;

                Vector3 slopeForward = Vector3.ProjectOnPlane(_characterRoot.forward, hitInfo.normal).normalized;
                if (slopeForward.sqrMagnitude < 0.001f)
                    slopeForward = Vector3.ProjectOnPlane(_characterRoot.up, hitInfo.normal).normalized;

                Quaternion slopeRotation = Quaternion.LookRotation(slopeForward, hitInfo.normal);
                // Apply bone-space correction LAST, in local space
                _currentRotTarget = slopeRotation * Quaternion.Euler(_boneRotationFix);

                // activate IK
                _targetWeight = 1f;
            }
        }

        if (!grounded)
        {
            //// Save for Gizmos
            //if (_printStateChanges && _wasGroundedLastFrame)
            //    Debug.Log($"<color=orange>[IK DEBUG] {_footName} is now AIRBORNE.</color>");

            // if ray missed OR walk animation lifts foot off ground
            // keep IK turned off so walking anim can play
            _targetWeight = 0f;
            _currentPosTarget = animatedFootPos;
            _currentRotTarget = _footBone.rotation;
        }

        _wasGroundedLastFrame = grounded;
        ApplyTargetTransforms();
    }

    private void ApplyTargetTransforms()
    {
        //_footIK.weight = Mathf.MoveTowards(_footIK.weight, _targetWeight, Time.deltaTime * _weightSmoothSpeed);
        _footIK.weight = _targetWeight;

        _ikTarget.position = Vector3.Lerp(_ikTarget.position, _currentPosTarget, Time.deltaTime * _transformSmoothSpeed);
        _ikTarget.rotation = Quaternion.Slerp(_ikTarget.rotation, _currentRotTarget, Time.deltaTime * _transformSmoothSpeed);
    }

    //private void OnDrawGizmos()
    //{
    //    if (!_drawGizmos || !Application.isPlaying) return;

    //    // Draw the raycast laser
    //    Gizmos.color = _gizmoDidHit ? Color.cyan : Color.red;
    //    Gizmos.DrawLine(_gizmoRayOrigin, _gizmoRayOrigin + Vector3.down * _rayDistance);

    //    // Draw the exact spot the ray is hitting
    //    if (_gizmoDidHit)
    //    {
    //        Gizmos.color = Color.yellow;
    //        Gizmos.DrawSphere(_gizmoRayHitPoint, 0.03f);
    //    }

    //    // Draw where the green box SHOULD be going
    //    Gizmos.color = Color.magenta;
    //    Gizmos.DrawWireCube(_currentPosTarget, new Vector3(0.1f, 0.02f, 0.1f));
    //}
}
