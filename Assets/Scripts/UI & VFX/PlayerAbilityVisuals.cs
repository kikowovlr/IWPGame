using Fusion;
using UnityEngine;

public class PlayerAbilityVisuals : NetworkBehaviour
{
    private NetworkPlayerController _playerController;

    public bool SuppressVisuals { get; set; }

    private void Awake()
    {
        _playerController = GetComponentInParent<NetworkPlayerController>();
    }

    public override void Render()
    {
        // grab current targets from controller
        AbilityIndicatorController activeIndicator = _playerController.ActiveAbilityIndicator;

        if (SuppressVisuals)
        {
            if (activeIndicator != null && activeIndicator.gameObject.activeSelf)
                activeIndicator.gameObject.SetActive(false);
            return;
        }

        AbilitySO currentAbility = _playerController.EquippedAbility;

        if (currentAbility == null || activeIndicator == null || currentAbility.IndicatorData == null) return;

        // evaluate visibility from network state struct
        ref AbilityState abilityState = ref _playerController.AbilityStateRef;
        bool isNetworkedIndicator = currentAbility.IndicatorData._isNetworked;
        bool shouldShowVisuals = abilityState._isVisualShown;

        // networked vs local only state check
        if (!isNetworkedIndicator && !Object.HasInputAuthority)
        {
            // if not supposed to be networked, deactivate the visuals for non-authoritative clients
            if (activeIndicator.gameObject.activeSelf)
                activeIndicator.gameObject.SetActive(false);
            return;
        }

        // local prediction - if local only and WE are controlling this player, show  
        if (!isNetworkedIndicator && Object.HasInputAuthority)
            shouldShowVisuals = abilityState._isCharging || abilityState._isCasting;

        // toggle game object
        if (activeIndicator.gameObject.activeSelf != shouldShowVisuals)
        {
            activeIndicator.gameObject.SetActive(shouldShowVisuals);

            if (shouldShowVisuals)
                currentAbility.InitIndicatorVisual(activeIndicator);
        }
    }
}
