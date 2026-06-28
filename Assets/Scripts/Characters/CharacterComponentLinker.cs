using UnityEngine;

/// <summary>
/// links handlers for specific CHARACTERS in their rig
/// </summary>
public class CharacterComponentLinker : MonoBehaviour
{
    public GameObject animatedModelRoot;
    public GameObject physicsPackageRoot;

    [Header("Punch Elements")]
    public DamageDealer _leftHandDamageDealer;
    public DamageDealer _rightHandDamageDealer;
    public Rigidbody _leftHandRb;
    public Rigidbody _rightHandRb;

    [Header("Kick Elements")]
    public DamageDealer _rightFootDamageDealer;

    [Header("Headbutt Elements")]
    public DamageDealer _headDamageDealer;
    public Rigidbody _headRb;

    [Header("Grab Elements")]
    public HandGrabHandler[] _grabHandlers;

    [Header("Animation")]
    public Animator characterAnimator;

    [Header("Ability")]
    public AbilitySO ability;
}
