using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// world space nametag above player's head
/// billboard facing
/// "YOU" for the local player, dimmer for opponents
/// </summary>
public class PlayerNametagController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkObject _playerObj;
    [SerializeField] private Transform _canvasTransform;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Gameplay Variant")]
    [SerializeField] private GameObject _gameplayVariantRoot;
    [SerializeField] private Image _gameplayBackgroundImage;
    [SerializeField] private TMP_Text _gameplayNameText;

    [Header("Character Select Variant")]
    [SerializeField] private GameObject _characterSelectVariantRoot;
    [SerializeField] private TMP_Text _characterSelectNameText;
    [SerializeField] private GameObject _readyCheckmark;

    [Header("Opacity")]
    [SerializeField] private float _localPlayerAlpha = 1f;
    [SerializeField] private float _opponentAlpha = 0.6f;

    private PlayerComponentRegistry _registry;
    private Camera _mainCamera;

    private void Awake()
    {
        _registry = transform.root.GetComponent<PlayerComponentRegistry>();
    }

    private void LateUpdate()
    {
        if (_playerObj == null || !_playerObj.IsValid) return;

        bool isLocalPlayer = _playerObj.HasInputAuthority;

        bool isCharacterSelect = GameManager.Instance != null
            && GameManager.Instance.IsSpawned
            && GameManager.Instance.CurrentRoundState == RoundState.CharacterSelect;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = (!isCharacterSelect && !isLocalPlayer) ? _opponentAlpha : _localPlayerAlpha;
        }

        if (_gameplayVariantRoot != null) _gameplayVariantRoot.SetActive(!isCharacterSelect);
        if (_characterSelectVariantRoot != null) _characterSelectVariantRoot.SetActive(isCharacterSelect);

        // if local player, display YOU, if not display their name
        string displayName = isLocalPlayer ? "YOU" : GetPlayerDisplayName();

        if (isCharacterSelect)
        {
            if (_characterSelectNameText != null) _characterSelectNameText.text = displayName;

            if (_readyCheckmark != null && _registry != null && _registry.CharacterSelect != null)
                _readyCheckmark.SetActive(_registry.CharacterSelect.IsReadyToStart);
        }
        else
        {
            if (_gameplayNameText != null) _gameplayNameText.text = displayName;

            if (_registry != null && _registry.Controller != null && _gameplayBackgroundImage != null)
                _gameplayBackgroundImage.color = _registry.Controller.NametagColor;
        }
    }

    private string GetPlayerDisplayName()
    {
        if (_registry != null && _registry.Stats != null && !string.IsNullOrEmpty(_registry.Stats.PlayerName))
            return _registry.Stats.PlayerName;

        return "Player";
    }
}
