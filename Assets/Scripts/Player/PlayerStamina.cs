using Fusion;
using UnityEngine;

/// <summary>
/// stamina drains while running AND moving
/// recovers after not sprinting for a short delay
/// empty == forced walk 
/// </summary>
public class PlayerStamina : NetworkBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _drainsPerSecond = 10f;
    [SerializeField] private float _recoverPerSecond = 25f;
    [SerializeField] private float _recoverDelay = 0.5f;
    [Range(0f, 1f)] 
    [SerializeField] private float _exhaustRecoverThreshold = 0.2f; // once empty, stamina must recover to this % before sprinting is allowed again

    [HideInInspector][Networked] public float CurrentStamina { get; private set; }
    [Networked] private NetworkBool _isExhausted { get; set; } // hit zero -> forced walk until stamina recovers to threshold
    [Networked] private TickTimer _recoverDelayTimer { get; set; }

    private bool _sprintingThisTick; // set by controller on ticks where player is sprint moving

    public float Normalized01 => _maxStamina > 0f ? Mathf.Clamp01(CurrentStamina / _maxStamina) : 0f; // for UI to read

    public bool ExhaustedForcedWalk => _isExhausted; // controller reads this to know if sprinting is allowed

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentStamina = _maxStamina;
            _isExhausted = false;
        }
    }

    /// <summary>
    /// call from controller each tick -> true player is sprinting and moving
    /// </summary>
    public void NotifySprintingThisTick(bool sprinting)
    {
        _sprintingThisTick = sprinting;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        float dt = Runner.DeltaTime;

        if (_sprintingThisTick && !_isExhausted)
        {
            // decrease stamina
            CurrentStamina = Mathf.Max(0f, CurrentStamina - _drainsPerSecond * dt);
            _recoverDelayTimer = TickTimer.CreateFromSeconds(Runner, _recoverDelay); // reset recover delay timer

            if (CurrentStamina <= 0f)
                _isExhausted = true; // forced walk
        }
        else
        {
            // recover once delay has elapsed
            if (_recoverDelayTimer.ExpiredOrNotRunning(Runner))
            {
                CurrentStamina = Mathf.Min(_maxStamina, CurrentStamina + _recoverPerSecond * dt);

                // if exhausted, check if we have recovered enough to allow sprinting again
                if (_isExhausted && Normalized01 >= _exhaustRecoverThreshold)
                    _isExhausted = false;
            }
        }

        // consume per-tick flag (controller resets it each tick)
        _sprintingThisTick = false;
    }

    public void ResetStamina()
    {
        if (!Object.HasStateAuthority) return;
        CurrentStamina = _maxStamina;
        _isExhausted = false;
    }
}
