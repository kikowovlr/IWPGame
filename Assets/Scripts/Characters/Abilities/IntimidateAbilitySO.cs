using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// stuns anyone in vision cone
/// flash yellow cone as visual
/// </summary>
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
        // only run on host
        if (!player.Object.HasStateAuthority) return;

        player.Animator.SetTrigger(_skillTrigger);
        player.Animator.SetInteger(_skillType, _skillType);
    }

    /// <summary>
    /// called explicitly via Unity Animation Event down on the player controller
    /// </summary
    public override void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

        // prevent duplicate hits
        HashSet<NetworkPlayerController> hitEnemiesThisFrame = new HashSet<NetworkPlayerController>();

        // query all players within radius
        int hitCount = player.Runner.GetPhysicsScene().OverlapSphere(player.transform.position, _range, _hitBuffer, _affectedLayer, QueryTriggerInteraction.Ignore);
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
                {
                    if (hitEnemiesThisFrame.Contains(enemy)) continue;
                    hitEnemiesThisFrame.Add(enemy);

                    Utils.DebugLog($"[Intimidate] Stunned {enemy.name} exactly on the animation's impact frame!");
                }
            }
        }
    }

    public override void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void UpdateAbilityState(NetworkPlayerController player, ref AbilityState state)
    {
    }
}
