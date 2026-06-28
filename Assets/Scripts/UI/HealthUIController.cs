using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : NetworkBehaviour
{
    [SerializeField] private PlayerHealthHandler _healthHandler;
    [SerializeField] private Image _healthbarSprite;
    [SerializeField] private float _lerpSpeed = 10f; // speed of change when health changes
    private float _targetFill;

    private void OnEnable()
    {
        if (_healthHandler != null)
            _healthHandler.OnHealthChangedEvent += UpdateHealthUI;
    }

    private void OnDisable()
    {
        if (_healthHandler != null)
            _healthHandler.OnHealthChangedEvent -= UpdateHealthUI;
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (max <= 0) return;
        _targetFill = current / max;
    }

    public override void Render()
    {
        _healthbarSprite.fillAmount = Mathf.Lerp(_healthbarSprite.fillAmount, _targetFill, Time.deltaTime * _lerpSpeed);
        _healthbarSprite.color = Color.Lerp(Color.red, Color.green, _healthbarSprite.fillAmount);
    }
}
