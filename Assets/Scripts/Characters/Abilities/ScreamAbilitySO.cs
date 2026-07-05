using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Unleashes a scream that damages and slightly knocks back enemies in a cone in front of the player
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

    public override void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;

        state._hitCount = 0;

        player.Animator.SetTrigger(_skillTrigger);
        player.Animator.SetInteger(_skillTypeString, _skillType);

        state._isCasting = true;
    }

    public override void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

        // show soundwave + distance
        ref AbilityState state = ref player.AbilityStateRef;
        state._isVisualShown = true; // PlayerAbilityVisuals script will handle the configuring of the visual effect based on the ability data

        // use persistant hit buffer attached to player
        Collider[] hitBuffer = player.HitBuffer;

        // query all players within radius
        int hitCount = player.Runner.GetPhysicsScene().OverlapSphere(player.transform.position, _range, hitBuffer, _affectedLayer, QueryTriggerInteraction.Ignore);
        Vector3 forwardDir = player.transform.forward;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null) continue;
            if (hit.transform.root == player.transform) continue;

            Vector3 dirToTarget = (hit.transform.position - player.transform.position).normalized;

            // check for cone
            if (Vector3.Angle(forwardDir, dirToTarget) < _coneAngle * 0.5f)
            {
                if (hit.transform.root.TryGetComponent(out NetworkPlayerController enemy))
                {
                    NetworkId enemyId = enemy.Object.Id;
                    bool alreadyHit = false;

                    // manually check for duplicated
                    for (int j = 0; j < state._hitCount; j++)
                    {
                        if (state._abilityHitHistory[j] == enemyId)
                        {
                            alreadyHit = true;
                            break;
                        }
                    }

                    // skip if already hit
                    if (alreadyHit) continue;

                    // add to history if we have space (max 8 hits stored)
                    if (state._hitCount < 8)
                    {
                        state._abilityHitHistory.Set(state._hitCount, enemyId);
                        state._hitCount++; // move the pointer forward
                    }

                    // apply slight knockback w damage
                    Vector3 forceDirection = new Vector3(dirToTarget.x, 0f, dirToTarget.z).normalized;
                    Vector3 impactForce = forceDirection * _knockbackForce;
                    enemy.Registry.Health.Rpc_TakeDamage(_damage, impactForce);
                }
            }
        }
    }

    public override void OnAnimationEndTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

        // hide when done
        ref AbilityState state = ref player.AbilityStateRef;
        state._isVisualShown = false;

        player.ClearActiveCastingState();
    }

    public override void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
    }

    public override void UpdateAbilityState(NetworkPlayerController player, ref AbilityState state)
    {
        // apply input restrictions
        if (state._isCasting)
        {
            player.ActiveRestrictions |= InputRestrictions.BlockMovement | InputRestrictions.BlockRotation | InputRestrictions.BlockCombat;
        }
    }

    public override void InitIndicatorVisual(AbilityIndicatorController indicator)
    {
        if (_indicatorData != null)
            indicator.ConfigureIndicator(_indicatorData, _range, _coneAngle);
    }
}
