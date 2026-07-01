using System.Collections.Generic;
using Fusion;
using Fusion.LagCompensation;
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

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[20];
    private readonly List<NetworkId> _hitTargetIds = new List<NetworkId>(20);

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

        player.RawCanMove = false;
        player.RawCanRotate = true;

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

            // TODO - can add jittering??

            //if 
            return;
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

        player.RawCanMove = false;
        player.RawCanRotate = false;

        _hitTargetIds.Clear();

        // TODO - hide direction arrow
    }

    private void ProcessCollisionCheck(NetworkPlayerController player, ref AbilityState state)
    {
        Vector3 boxHalfExtents = new Vector3(
            _range * 0.5f,
            _boxHeight * 0.5f,
            _boxDepth * 0.5f
        );

        // use boxcast -> prvent teleporting or missing collision detection
        // start boxcast from players mouth
        Vector3 castStart = player.transform.position + (Vector3.up * 0.75f);
        Vector3 castDirection = player.transform.forward;

        // calculate how far we are sweeping this tick based on curr frame velocity
        float currentFrameSpeed = _baseRamSpeed * Mathf.Lerp(1f, _maxRamSpeedMultiplier, state._chargeTime / _maxChargeTime);
        float castDistance = (currentFrameSpeed * player.Runner.DeltaTime) + 0.2f; // margin buffer in case of gaps

        Quaternion boxRotation = player.transform.rotation;

        // fire box overlap
        int hitCount = player.Runner.GetPhysicsScene().BoxCast(
            castStart,
            boxHalfExtents,
            castDirection,
            _hitBuffer,
            boxRotation,
            castDistance,
            _hitLayer,
            QueryTriggerInteraction.Ignore
        );

        bool hitSomething = false;

        for ( int i = 0; i < hitCount; i++ )
        {
            RaycastHit hitInfo = _hitBuffer[i];
            Collider hitCollider = hitInfo.collider;
            if (hitCollider == null) return;
            if (hitCollider.transform.root == player.transform) continue; // Skip self
            if (hitCollider.transform.root.TryGetComponent(out NetworkPlayerController enemy))
            {
                NetworkId enemyId = enemy.Object.Id;
                if (_hitTargetIds.Contains(enemyId)) continue;
                _hitTargetIds.Add(enemyId);

                // apply knockback
                float chargePercentage = Mathf.Clamp01(state._chargeTime / _maxChargeTime);
                float finalKnockback = _baseKnockbackForce * Mathf.Lerp(1f, _maxRamSpeedMultiplier, chargePercentage);

                Vector3 knockbackDir = (enemy.transform.position - player.transform.position).normalized;
                knockbackDir.y = 1.5f; // lift slightly

                Utils.DebugLog($"[Goat Ram] Box Impact on {enemy.name}! Applied Force: {finalKnockback}");

                Vector3 finalForceVector = knockbackDir * finalKnockback;
                enemy.ApplyKnockback(finalForceVector, ForceMode.Impulse);

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
            player.RawCanMove = true;
            player.RawCanRotate = true;
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
        Utils.DebugLog($"[Damage Check] Hit! IsCharging: {state._isCharging}, TotalTracked: {totalTrackedDamage}");
        if (totalTrackedDamage >= _maxDamageBeforeCancel)
        {
            state._isCharging = false;
            state._isDashing = false;

            player.RawCanMove = true;
            player.RawCanRotate = true;

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
        Vector3 boxHalfExtents = new Vector3(_range * 0.5f, _boxHeight, _boxDepth);
        Vector3 localCenterOffset = (Vector3.forward * boxHalfExtents.z) + (Vector3.up * 1.0f);

        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Bind the matrix root strictly to the real transform positions
        Gizmos.matrix = Matrix4x4.TRS(player.transform.position, player.transform.rotation, Vector3.one);

        if (state._isDashing)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawCube(localCenterOffset, boxHalfExtents * 2f); // Draw using the calculated offset

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(localCenterOffset, boxHalfExtents * 2f);
        }
        else
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawWireCube(localCenterOffset, boxHalfExtents * 2f);
        }

        Gizmos.matrix = oldMatrix;

        // ray
        Vector3 rayOrigin = player.transform.position + (player.transform.forward * _edgeCheckDistance) + (Vector3.up * 0.2f);
        Vector3 rayDirection = Vector3.down * 2.0f;
        if (state._isDashing)
        {
            Gizmos.color = Color.yellow; // High visibility neon yellow warning ray while active!
        }
        else
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f); // Soft, faded yellow when parked
        }
        Gizmos.DrawLine(rayOrigin, rayOrigin + rayDirection);
        Gizmos.DrawWireSphere(rayOrigin + rayDirection, 0.05f);
    }

    /// <summary>
    /// runs every tick from controller, manages active states automatically
    /// </summary>
    /// <param name="player"></param>
    /// <param name="state"></param>
    public override void UpdateAbilityState(NetworkPlayerController player, ref AbilityState state)
    {
        if (!player.Object.HasStateAuthority) return;

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

                // todo - add audio & visual
                player.Animator.SetBool(_activeBool, false);
            }

            // apply velocity
            Vector3 dashVelocity = player.transform.forward * targetMoveSpeed;
            state._customVelocity = dashVelocity;

            if (state._dashDurationTimer < 0f)
            {
                state._isDashing = false;
                player.RawCanMove = true;
                player.RawCanRotate = true;
                // TODO - remove arrow
                Utils.DebugLog("[Goat Ram] Dash finished organically.");
                player.Animator.SetBool(_activeBool, false);
                return;
            }

            ProcessCollisionCheck(player, ref state);
        }
    }
}
