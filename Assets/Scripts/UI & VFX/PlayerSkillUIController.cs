using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.GraphView;

/// <summary>
/// ability icon
/// keybind label below
/// during cooldown, icon is darkened + shows radial cooldown wipe + countdown text on top of icon
/// </summary>
public class PlayerSkillUIController : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private Image _skillIcon;
    [SerializeField] private TMP_Text _keybindText;
    [SerializeField] private string _keybindLabel = "[E]";

    [Header("Cooldown")]
    [SerializeField] private Image _cooldownOverlay;    // filled, dark semi-transparent
    [SerializeField] private TMP_Text _cooldownText;    // seconds remaining, on top
    [SerializeField] private Color _readyTint = Color.white;
    [SerializeField] private Color _cooldownTint = new Color(0.7f, 0.7f, 0.7f, 1f); // darkened

    private NetworkPlayerController _player;

    private void OnEnable()
    {
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        TryBindLocal();
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
    }

    private void HandleLocalPlayerSpawned(Transform _) => TryBindLocal();

    private void TryBindLocal()
    {
        _player = NetworkPlayerController.Local;
        RefreshStaticVisuals();
    }

    private void RefreshStaticVisuals()
    {
        if (_keybindText != null) _keybindText.text = _keybindLabel;

        if (_player != null && _player.EquippedAbility != null && _skillIcon != null)
            _skillIcon.sprite = _player.EquippedAbility._icon;
    }

    private void Update()
    {
        if (_player == null)
        {
            TryBindLocal();
            return;
        }

        if (_player.EquippedAbility == null) return;

        // keep icon in sync if character changes
        if (_skillIcon != null && _skillIcon.sprite != _player.EquippedAbility._icon)
            _skillIcon.sprite = _player.EquippedAbility._icon;

        float cd = _player.AbilityCooldownRemaining;
        float baseCd = _player.EquippedAbility._baseCooldown;

        bool onCooldown = cd > 0.01f && baseCd > 0.01f;

        if (onCooldown)
        {
            if (_skillIcon != null) _skillIcon.color = _cooldownTint;

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.gameObject.SetActive(true);
                _cooldownOverlay.fillAmount = Mathf.Clamp01(cd / baseCd); // wipes as it recovers
            }

            if (_cooldownText != null)
            {
                _cooldownText.gameObject.SetActive(true);
                // show 1 decimal under 1s, whole seconds above
                _cooldownText.text = cd >= 1f ? Mathf.CeilToInt(cd).ToString() : cd.ToString("0.0");
            }
        }
        else
        {
            if (_skillIcon != null) _skillIcon.color = _readyTint;
            if (_cooldownOverlay != null) _cooldownOverlay.gameObject.SetActive(false);
            if (_cooldownText != null) _cooldownText.gameObject.SetActive(false);
        }
    }
}
