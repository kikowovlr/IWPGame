using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    private NetworkPlayerController _playerController;
    private PlayerCombatAudio _combatAudio;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
            _combatAudio = registry.CombatAudio;
        }
    }

    public void UnityEvent_OnAbilityImpact()
    {
        if (_playerController != null)
        {
            _playerController.UnityEvent_OnAbilityImpact();
        }
    }

    public void UnityEvent_OnAbilityEnd()
    {
        if (_playerController != null)
        {
            _playerController.UnityEvent_OnAbilityEnd();
        }
    }

    public void UnityEvent_OnFootstep()
    {
        if (_combatAudio != null)
            _combatAudio.UnityEvent_OnFootstep();
    }
}
