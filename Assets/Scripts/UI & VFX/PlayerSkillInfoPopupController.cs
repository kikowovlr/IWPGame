using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// timed skill info popup
/// pressing info keybind shows equipped ability name , description, then fades out after delay
/// </summary>
public class PlayerSkillInfoPopupController : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private CanvasGroup _popupGroup;   // for fading
    [SerializeField] private TMP_Text _skillNameText;
    [SerializeField] private TMP_Text _skillDescText;
    [SerializeField] private TMP_Text _keybindText;
    [SerializeField] private string _keybindLabel = "[I]";

    [Header("Behaviour")]
    [SerializeField] private KeyCode _infoKey = KeyCode.I;
    [SerializeField] private float _visibleDuration = 3f;
    [SerializeField] private float _fadeDuration = 0.4f;

    private NetworkPlayerController _player;
    private Coroutine _routine;
    private bool _isOpen;

    private void Awake()
    {
        if (_popupGroup != null) _popupGroup.alpha = 0f;
    }

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
    }

    private void Update()
    {
        if (Input.GetKeyDown(_infoKey))
            ShowPopup();
    }

    private void ShowPopup()
    {
        // close if alrdy running
        if (_isOpen || (_popupGroup != null && _popupGroup.alpha > 0f))
        {
            ClosePopup();
            return;
        }

        if (_player == null || _player.EquippedAbility == null) return;

        if (_skillNameText != null) _skillNameText.text = _player.EquippedAbility._name;
        if (_skillDescText != null) _skillDescText.text = _player.EquippedAbility._description;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowThenFade());
    }

    private void ClosePopup()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (_popupGroup != null)
            _popupGroup.alpha = 0f;

        _isOpen = false;
    }

    private IEnumerator ShowThenFade()
    {
        _isOpen = true;
        if (_popupGroup != null) _popupGroup.alpha = 1f;

        yield return new WaitForSeconds(_visibleDuration);

        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            if (_popupGroup != null)
                _popupGroup.alpha = Mathf.Lerp(1f, 0f, t / _fadeDuration);
            yield return null;
        }

        ClosePopup();
    }
}
