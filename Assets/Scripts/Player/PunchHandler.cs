using Fusion;
using UnityEngine;

public class PunchHandler : NetworkBehaviour
{
    [Header("Punch Settings")]
    [SerializeField] private Animator _animator;
    [SerializeField] private DamageDealer _punchDamageDealer;

    public void UpdatePunchState()
    {

    }
}
