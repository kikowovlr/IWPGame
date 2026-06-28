using Fusion.LagCompensation;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Press and hold to charge up (up to 100%) -> takes less damage during charging but enough damage taken causes skill to be canceled and goat to be stunned
/// Quickly move towards facing direction (can be aimed)
/// Knockbacks enemy (AOE)
/// stop after hitting something in hit layer
/// 
/// If no floor ahead, try halting by reducing speed
/// show arrow pointing for aimdireciton + rotate character
/// car stop sound + air puff when halting
/// </summary>
[CreateAssetMenu(fileName = "Ability_GoatRam", menuName = "Abilities/Goat Ram")]
public class RamAbilitySO : AbilitySO
{
    [Header("Charge Settings")]
    [SerializeField] private float _maxChargeTime = 3.0f; // time until full power charge
    [Range(0f, 1f)]
    [SerializeField] private float _damageTakenMultiplier = 0.5f; // take 50% less than dmg when charging
    [SerializeField] private float _maxDamageBeforeCancel = 30f; // cancel threshold

    [Header("Ram Settings")]
    [SerializeField] private float _maxRamDuration = 2.5f;
    [SerializeField] private float _baseRamSpeed = 6f;
    [SerializeField] private float _maxRamSpeedMultiplier = 2.5f; // how much charge up time affects speed

    [Header("Impact")]
    [SerializeField] private float _baseKnockbackForce = 7f;
    [SerializeField] private LayerMask _hitLayer; // targets to hit

    [Header("Braking")]
    [SerializeField] private LayerMask _floorLayer;
    [SerializeField] private float _edgeCheckDistance = 1.5f; // how far ahead to look for ledge

    [Header("Collision Settings")]
    [SerializeField] private float _range = 2f;
    [SerializeField] private float _boxHeight = 1.0f;
    [SerializeField] private float _boxDepth = 0.6f;

    private readonly Collider[] _hitBuffer = new Collider[20];
    private readonly HashSet<NetworkPlayerController> _hitTargetsThisDash = new HashSet<NetworkPlayerController>(); // ensure hits are not repeated

    /// <summary>
    /// start charging
    /// </summary>
    public override void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;
        if (state._isDashing) return; // cannot use skill again while dashing

        state._isCharging = true;
        state._isDashing = false;
        state._chargeTime = 0f;

        player.Registry.Health.ResetAccumulatedDamageCounter();

        player.Animator.SetTrigger(_skillTrigger);
        player.Animator.SetInteger(_skillTypeString, _skillType);
        player.Animator.SetBool(_activeBool, true);

        // TODO: show aim indicator

    }

    public override void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;

        // holding charge
        if (state._isCharging)
        {
            // increment charge time
            state._chargeTime = Mathf.Min(state._chargeTime + player.Runner.DeltaTime, _maxChargeTime);

            // handle aiming rotations - just aim if no input
            Vector3 targetDirection = player.transform.forward;
            if (aimDir.sqrMagnitude > 0.01f)
                targetDirection = new Vector3(aimDir.x, 0f, aimDir.y).normalized;

            player.transform.rotation = Quaternion.LookRotation(targetDirection);

            // todo - disable moving in player controller script
            // TODO - can add jittering??

            return;
        }

        // ramming
        if (state._isDashing)
        {
            state._dashDurationTimer -= player.Runner.DeltaTime;

            // braking system
            Vector3 rayOrigin = player.transform.position + (player.transform.forward * _edgeCheckDistance) + (Vector3.up * 0.2f); // move up a bit to ensure not always colliding with floor below fit
            bool isFloorAhead = player.Runner.GetPhysicsScene().Raycast(rayOrigin, Vector3.down, 2.0f, _floorLayer);

            // calculate ram power
            float chargePercent = Mathf.Clamp01(state._chargeTime / _maxChargeTime);
            float targetMoveSpeed = _baseRamSpeed * Mathf.Lerp(1f, _maxRamSpeedMultiplier, chargePercent);

            // no floor ahead - try halting
            if (!isFloorAhead)
            {
                Utils.DebugLogWarning("[Goat Ram] LEDGE DETECTED! Deploying safety skids!");
                state._dashDurationTimer -= player.Runner.DeltaTime * 3f; // expire dash faster 
                targetMoveSpeed *= 0.1f; // force velocity drop
                                         // TODO - apply this move speed

                // todo - add audio & visual
                player.Animator.SetBool(_activeBool, false);
            }

            if (state._dashDurationTimer < 0f)
            {
                state._isDashing = false;
                // TODO - remove arrow
                Utils.DebugLog("[Goat Ram] Dash finished organically.");
                return;
            }

            ProcessCollisionCheck(player, ref state);
        }
    }

    public override void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir)
    {
        if (!player.Object.HasStateAuthority) return;
        if (!state._isCharging) return;

        // start ramming
        state._isCharging = false;
        state._isDashing = true;

        state._dashDurationTimer = _maxRamDuration;

        _hitTargetsThisDash.Clear();

        // TODO - hide direction arrow
    }

    private void ProcessCollisionCheck(NetworkPlayerController player, ref AbilityState state)
    {
        Vector3 boxHalfExtents = new Vector3(
            _range * 0.5f,
            _boxHeight,
            _boxDepth
        );

        // position center of box so its right infront of player's face
        Vector3 boxCenter = player.transform.position + (player.transform.forward * boxHalfExtents.z) + (Vector3.up * 1.0f);

        Quaternion boxRotation = player.transform.rotation;

        // fire box overlap
        int hitCount = player.Runner.GetPhysicsScene().OverlapBox(
            boxCenter,
            boxHalfExtents,
            _hitBuffer,
            boxRotation,
            _hitLayer,
            QueryTriggerInteraction.Ignore
        );

        bool hitSomething = false;

        for ( int i = 0; i < hitCount; i++ )
        {
            Collider hit = _hitBuffer[i];
            if (hit.transform.root == player.transform) continue; // Skip self

            if (hit.transform.root.TryGetComponent(out NetworkPlayerController enemy))
            {
                if (_hitTargetsThisDash.Contains(enemy)) continue;
                _hitTargetsThisDash.Add(enemy);

                // apply knockback
                float chargePercentage = Mathf.Clamp01(state._chargeTime / _maxChargeTime);
                float finalKnockback = _baseKnockbackForce * Mathf.Lerp(1f, _maxRamSpeedMultiplier, chargePercentage);

                Vector3 knockbackDir = (enemy.transform.position - player.transform.position).normalized;
                knockbackDir.y = 1.5f; // lift slightly

                Utils.DebugLog($"[Goat Ram] Box Impact on {enemy.name}! Applied Force: {finalKnockback}");
                // TODO: apply knockback

                hitSomething = true;
            }
            else
            {
                // hit environmental object
                hitSomething = true;
            }
        }

        if (hitSomething)
        {
            state._isDashing = false;
            player.Animator.SetTrigger(_releaseTrigger);
            player.Animator.SetBool(_activeBool, false);
            Utils.DebugLog("[Goat Ram] Ram terminated via box target layer impact.");
        }
    }

    public override float HandleIncomingDamageCheck(NetworkPlayerController player, ref AbilityState state, float rawDamage)
    {
        if (!state._isCharging) return rawDamage;

        // apply damage mitigation
        float mitigatedDamage = rawDamage * _damageTakenMultiplier;

        // find if total damage taken is enough to break out of skill
        float totalTrackedDamage = player.Registry.Health.GetAccumulatedDamage() + mitigatedDamage;
        if (totalTrackedDamage >= _maxDamageBeforeCancel)
        {
            state._isCharging = false;
            state._isDashing = false;

            player.Animator.SetTrigger(_releaseTrigger);
            Utils.DebugLogWarning("[Goat Ram] STUN CANCEL! Stance broken by posture damage.");

            // TODO - apply stunned effect on goat
        }

        return mitigatedDamage;
    }

    /// <summary>
    /// Call this from your NetworkPlayerController's OnDrawGizmos method to visualize the box bumper!
    /// </summary>
    public void DrawAbilityGizmos(NetworkPlayerController player, ref AbilityState state)
    {
        // Replicate the exact math used in your ProcessCollisionCheck method
        Vector3 boxHalfExtents = new Vector3(
            _range * 0.5f,
            _boxHeight,
            _boxDepth
        );

        Vector3 boxCenter = player.transform.position + (player.transform.forward * boxHalfExtents.z) + (Vector3.up * 1.0f);

        // Cache the old Gizmos matrix so we don't break other system visuals
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Set the Gizmos matrix to match the player's rotation and position context
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, player.transform.rotation, Vector3.one);

        // Color code based on state: Red/Orange for active ramming speed, green/white for idle setup
        if (state._isDashing)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f); // Semi-transparent Orange Fill
            Gizmos.DrawCube(Vector3.zero, boxHalfExtents * 2f); // Draws using local space via matrix

            Gizmos.color = Color.red; // Solid Red Outline
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        }
        else
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.1f); // Super faint white for setup visibility
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        }

        // Restore the scene gizmo matrix state back to standard defaults
        Gizmos.matrix = oldMatrix;
    }
}
