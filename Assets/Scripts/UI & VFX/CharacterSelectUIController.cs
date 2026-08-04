using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectUIController : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private GameObject _startingButton;
    [SerializeField] private Image _timerPanel;
    [SerializeField] private int _characterCount = 3;
    [SerializeField] private Image _prevCharacterIcon; // small icon, left side
    [SerializeField] private Image _currCharacterIcon; // large icon, center, highlighted
    [SerializeField] private Image _nextCharacterIcon; // small icon, right side
    [SerializeField] private GameObject _stageRoot;

    [Header("Character Info Popup")]
    [SerializeField] private Button _characterInfoButton;
    [SerializeField] private CharacterInfoPopupController _characterInfoPopup;

    private void Start()
    {
        _leftArrowButton.onClick.AddListener(() => CycleSelection(-1));
        _rightArrowButton.onClick.AddListener(() => CycleSelection(1));
        _readyButton.onClick.AddListener(ToggleReady);
        _cancelButton.onClick.AddListener(ToggleReady);

        _startingButton.SetActive(false);
        _cancelButton.gameObject.SetActive(false);

        if (_characterInfoButton != null)
            _characterInfoButton.onClick.AddListener(OpenCharacterInfoPopup);
    }

    private void Update()
    {
        if (MatchContext.Current == null || !MatchContext.Current.IsSpawned) return;
        bool shouldShow = MatchContext.Current.IsInCharacterSelect;

        // dont show ui and stage if not character select
        if (_panel != null) _panel.SetActive(shouldShow);
        if (_stageRoot != null && _stageRoot.activeSelf != shouldShow) _stageRoot.SetActive(shouldShow);

        if (!shouldShow) return;

        if (CameraManager.Instance != null)
            CameraManager.Instance.FocusCharacterSelectCameraOnLocalPlayer();

        if (_timerText != null)
            _timerText.text = Mathf.CeilToInt(MatchContext.Current.GetRemainingStateTime()).ToString();

        PlayerCharacterSelect localSelect = GetLocalCharacterSelect();
        bool isFinalCountdown = MatchContext.Current.IsInFinalCharacterSelectCountdown;
        bool isReady = localSelect != null && localSelect.IsReadyToStart;

        if (_readyButton != null) _readyButton.gameObject.SetActive(!isFinalCountdown && !isReady);
        if (_cancelButton != null) _cancelButton.gameObject.SetActive(!isFinalCountdown && isReady);
        if (_startingButton != null) _startingButton.SetActive(isFinalCountdown);

        bool canScroll = !isReady && !isFinalCountdown;
        _leftArrowButton.interactable = canScroll;
        _rightArrowButton.interactable = canScroll;

        UpdateLocalPreview();
    }

    private void UpdateLocalPreview()
    {
        if (NetworkPlayerController.Local == null) return;

        int currIndex = NetworkPlayerController.Local.CharacterIndex;
        int prevIndex = (currIndex - 1 + _characterCount) % _characterCount;
        int nextIndex = (currIndex + 1) % _characterCount;

        SetIconSprite(_prevCharacterIcon, prevIndex);
        SetIconSprite(_currCharacterIcon, currIndex);
        SetIconSprite(_nextCharacterIcon, nextIndex);
    }

    private void SetIconSprite(Image targetImage, int characterIndex)
    {
        if (targetImage == null || NetworkPlayerController.Local == null) return;

        CharacterDataSO data = NetworkPlayerController.Local.GetCharacterData(characterIndex);
        if (data != null && data.CharacterIcon != null)
            targetImage.sprite = data.CharacterIcon;
    }

    private PlayerCharacterSelect GetLocalCharacterSelect()
    {
        if (NetworkPlayerController.Local == null) return null;
        return NetworkPlayerController.Local.Registry.CharacterSelect;
    }

    private void CycleSelection(int direction)
    {
        PlayerCharacterSelect localSelect = GetLocalCharacterSelect();
        if (localSelect == null || localSelect.IsReadyToStart) return;
        if (GameManager.Instance == null && GameManager.Instance.IsInFinalCharacterSelectCountdown) return;
        if (NetworkPlayerController.Local == null) return;

        int currentIndex = NetworkPlayerController.Local.CharacterIndex;
        int newIndex = (currentIndex + direction + _characterCount) % _characterCount;

        localSelect.Rpc_RequestCharacterChange(newIndex);
    }

    private void ToggleReady()
    {
        PlayerCharacterSelect localSelect = GetLocalCharacterSelect();
        if (localSelect == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsInFinalCharacterSelectCountdown) return;

        localSelect.Rpc_SetReady(!localSelect.IsReadyToStart);
    }

    private void OpenCharacterInfoPopup()
    {
        if (NetworkPlayerController.Local == null || _characterInfoPopup == null) return;

        int currentIndex = NetworkPlayerController.Local.CharacterIndex;
        CharacterComponentLinker linker = NetworkPlayerController.Local.GetCharacterLinker(currentIndex);
        _characterInfoPopup.Show(linker);
    }
}
