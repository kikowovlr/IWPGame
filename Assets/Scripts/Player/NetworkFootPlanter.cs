using UnityEngine;
using UnityEngine.Animations.Rigging;

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
    [SerializeField] private float _plantedYOffset = 0.05f; // to prevent foot clipping into ground
    [SerializeField] private LayerMask _environmentLayer;

    private void LateUpdate()
    {
        if (_footIK == null || _footBone == null || _ikTarget == null || _characterRoot == null) return;

        // reset weight to default to raw animation clip unless ground is hit
        _footIK.weight = 0f;

        // capture where raw walking animation wants foot to be rn
        Vector3 animatedFootPos = _footBone.position;

        // cast ray down from slightly above the animated foot
        Vector3 rayOrigin = animatedFootPos + Vector3.up * _rayYOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, _rayDistance, _environmentLayer))
        {
            // absolute ground height 
            float groundTargetY = hitInfo.point.y + _plantedYOffset;

            // is walking anim trying to step below the actual ground level?
            if (animatedFootPos.y < groundTargetY)
            {
                // activate IK
                _footIK.weight = 1f;

                // pos target cleanly on the ground
                Vector3 targetPos = hitInfo.point;
                targetPos.y += _plantedYOffset;
                _ikTarget.position = targetPos;

                // tilt foot to match slope, but keep facing the player's clean root forward heading
                Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
                _ikTarget.rotation = slopeRotation * _characterRoot.rotation;

                return; // grounding complete, skip airborne logic
            }
        }

        // if ray missed OR walk animation lifts foot off ground
        // keep IK turned off so walking anim can play
        _footIK.weight = 0f;
        _ikTarget.position = animatedFootPos;
        _ikTarget.rotation = _footBone.rotation;
    }
}
