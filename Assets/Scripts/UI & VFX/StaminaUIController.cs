using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// radial stamina UI
/// reads from player stamina and updates the UI accordingly
/// </summary>
public class StaminaUIController : MonoBehaviour
{
    [SerializeField] private Image _staminaFill;
    [SerializeField] private float _lerpSpeed = 8f;

    private PlayerStamina _stamina;
    private float _displayed = 1f;

    private void OnEnable()
    {
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        TryBindLocal();
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
    }

    private void HandleLocalPlayerSpawned(Transform _)
    {
        TryBindLocal();
    }

    private void TryBindLocal()
    {
        if (NetworkPlayerController.Local != null)
            _stamina = NetworkPlayerController.Local.Registry.Stamina; // add Stamina to registry
    }

    private void Update()
    {
        if (_stamina == null)
        {
            TryBindLocal();
            return;
        }

        float target = _stamina.Normalized01;
        _displayed = Mathf.MoveTowards(_displayed, target, _lerpSpeed * Time.deltaTime);

        if (_staminaFill != null)
            _staminaFill.fillAmount = _displayed;
    }
}
