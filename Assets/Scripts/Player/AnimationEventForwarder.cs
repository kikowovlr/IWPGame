using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    private NetworkPlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponentInParent<NetworkPlayerController>();
    }

    public void UnityEvent_OnAbilityImpact()
    {
        if (_playerController != null)
        {
            _playerController.UnityEvent_OnAbilityImpact();
        }
    }
}
