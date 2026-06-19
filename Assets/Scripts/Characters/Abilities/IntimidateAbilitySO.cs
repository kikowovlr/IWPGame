using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Ability_RedPandaIntimidate", menuName = "Abilities/Red Panda Intimidate")]
public class IntimidateAbilitySO : AbilitySO
{
    [Header("Ability Settings")]
    [SerializeField] private float _range = 5f;
    [SerializeField] private float _coneAngle = 60f;
    [SerializeField] private LayerMask _affectedLayer;

    // static reusable array buffer to hold up to 20 hits wihtout generating heap garbage
    private readonly Collider[] _hitBuffer = new Collider[20];

    public override void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        player.Animator.SetTrigger("Intimidate");

        // query all players within radius
        int hitCount =  player.Runner.GetPhysicsScene().OverlapSphere(player.transform.position, _range, _hitBuffer, _affectedLayer, QueryTriggerInteraction.Ignore);
        Vector3 forwardDir = player.transform.forward;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitBuffer[i];
            if (hit.transform.root == player.transform) continue;

            Vector3 dirToTarget = (hit.transform.position - player.transform.position).normalized;

            // check for cone
            if (Vector3.Angle(forwardDir, dirToTarget) < _coneAngle * 0.5f)
            {
                if (hit.transform.root.TryGetComponent(out NetworkPlayerController enemy))
                    enemy.Knockout();
            }
        }
    }

    public override void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }
}
