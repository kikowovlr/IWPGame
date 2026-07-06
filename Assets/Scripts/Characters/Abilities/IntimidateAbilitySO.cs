using System.Collections.Generic;
using Fusion;
using UnityEngine;

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
    [SerializeField] private float _stunDuration = 3f;
    
    // flash settings
    [SerializeField] private float _flashDuration = 0.15f;

    public override void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        // only run on host
        if (!player.Object.HasStateAuthority) return;

        state._hitCount = 0;

        state._visualTime = 0f;
        state._isVisualShown = false;

        player.Animator.SetTrigger(_skillTrigger);
        player.Animator.SetInteger(_skillTypeString, _skillType);

        state._isCasting = true;
    }

    /// <summary>
    /// called explicitly via Unity Animation Event down on the player controller
    /// </summary
    public override void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

        ref AbilityState state = ref player.AbilityStateRef;
        state._visualTime = _flashDuration;
        state._isVisualShown = true;

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

                    for (int j = 0; j < state._hitCount; j++)
                    {
                        if (state._abilityHitHistory[j] == enemyId)
                        {
                            alreadyHit = true;
                            break;
                        }
                    }

                    if (alreadyHit) continue;

                    if (state._hitCount < 8)
                    {
                        state._abilityHitHistory.Set(state._hitCount, enemyId);
                        state._hitCount++;
                    }

                    if (enemy.Registry.Status != null)
                        enemy.Registry.Status.InflictStatus(StatusEffectType.Stunned, _stunDuration);
                }
            }
        }
    }

    public override void OnAnimationEndTriggered(NetworkPlayerController player)
    {
        if (!player.Object.HasStateAuthority) return;

        // safety fallback to ensure the indicator is turned off after the animation ends
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

        // only state authority can update timer
        // manually set flash inactive after duration
        if (player.Object.HasStateAuthority && state._isVisualShown)
        {
            state._visualTime -= player.Runner.DeltaTime;

            if (state._visualTime <= 0f)
            {
                state._isVisualShown = false;
            }
        }
    }

    public override void InitIndicatorVisual(AbilityIndicatorController indicator)
    {
        if (_indicatorData != null)
            indicator.ConfigureIndicator(_indicatorData, _range, _coneAngle);
    }
}
