using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Unleashes a 
/// </summary>
[CreateAssetMenu(fileName = "Ability_MonkeyScream", menuName = "Abilities/Monkey Scream")]
public class ScreamAbilitySO : AbilitySO
{
    [Header("Ability Settings")]
    [SerializeField] private float _range = 5f;
    [SerializeField] private float _coneAngle = 50f;
    [SerializeField] private LayerMask _affectedLayer;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _knockbackForce = 1.5f;

    private readonly Collider[] _hitBuffer = new Collider[20];
    private readonly List<NetworkId> _hitTargetIds = new List<NetworkId>(20);

    public override void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;

        player.Animator.SetTrigger(_skillTrigger);
        player.Animator.SetInteger(_skillTypeString, _skillType);

        // TODO: show cone of AOE
    }

    public override void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

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
                    NetworkId enemyId = enemy.Object.Id;
                    if (_hitTargetIds.Contains(enemyId)) continue;
                    _hitTargetIds.Add(enemyId);

                    Utils.DebugLog($"[Scream] Damaged {enemy.name} for {_damage}!");
                    
                    // apply slight knockback w damage
                    Vector3 forceDirection = new Vector3(dirToTarget.x, 0f, dirToTarget.z).normalized;
                    Vector3 impactForce = forceDirection * _knockbackForce;
                    enemy.Registry.Health.Rpc_TakeDamage(_damage, impactForce);

                    // TODO: apply VFX sound wave
                }
            }
        }
    }

    public override void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;
        _hitTargetIds.Clear();
    }

    public override void UpdateAbilityState(NetworkPlayerController player, ref AbilityState state)
    {
    }
}
