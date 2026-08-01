using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// lobby mode selector
/// - "CURRENT: X" label shows for EVERYONE (host and clients).
/// - The "Change Map" button that opens the popup shows ONLY for the host.
/// - Inside the popup: two buttons (Random / Tutorial). Host clicks to change mode.
/// </summary>
public class MapSelectPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _currentModeLabel;
    [SerializeField] private Button _changeModeButton;

    [Header("Popup")]
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _randomModeButton;
    [SerializeField] private Button _tutorialModeButton;
    [SerializeField] private GameObject _randomSelectedHighlight;
    [SerializeField] private GameObject _tutorialSelectedHighlight;   
    
    private bool IsHost =>
        NetworkLauncher.Instance != null &&
        NetworkLauncher.Instance.Runner != null &&
        NetworkLauncher.Instance.Runner.IsServer;

    private void Start()
    {
        if (_changeModeButton != null) _changeModeButton.onClick.AddListener(OpenPopup);
        if (_closeButton != null) _closeButton.onClick.AddListener(ClosePopup);
        if (_randomModeButton != null) _randomModeButton.onClick.AddListener(() => PickMode(LobbyMode.Random));
        if (_tutorialModeButton != null) _tutorialModeButton.onClick.AddListener(() => PickMode(LobbyMode.Tutorial));

        LobbyMapSelection.OnSelectionChanged += OnModeChanged;

        if (_popupRoot != null) _popupRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        LobbyMapSelection.OnSelectionChanged -= OnModeChanged;
    }

    private void Update()
    {
        // change button is host-only, label is for everyone
        if (_changeModeButton != null)
            _changeModeButton.gameObject.SetActive(IsHost);

        if (_currentModeLabel != null && LobbyMapSelection.Instance != null)
            _currentModeLabel.text = $"MODE: {LobbyMapSelection.Instance.GetModeLabel()}";
    }

    private void OpenPopup()
    {
        if (!IsHost) return; // safety — only host opens
        if (_popupRoot != null) _popupRoot.SetActive(true);

        RefreshHighlight(LobbyMapSelection.Instance != null ? LobbyMapSelection.Instance.SelectedMode : LobbyMode.Random);
    }

    private void ClosePopup()
    {
        if (_popupRoot != null) _popupRoot.SetActive(false);
    }

    private void PickMode(LobbyMode mode)
    {
        if (!IsHost) return;
        if (LobbyMapSelection.Instance == null) return;

        LobbyMapSelection.Instance.SetModeAsHost(mode);
        // highlight updates via OnSelectionChanged for everyone; close popup for the host
        ClosePopup();
    }

    private void OnModeChanged(LobbyMode mode)
    {
        RefreshHighlight(mode);
        if (_currentModeLabel != null && LobbyMapSelection.Instance != null)
            _currentModeLabel.text = $"CURRENT: {LobbyMapSelection.Instance.GetModeLabel()}";
    }

    private void RefreshHighlight(LobbyMode mode)
    {
        if (_randomSelectedHighlight != null)
            _randomSelectedHighlight.SetActive(mode == LobbyMode.Random);
        if (_tutorialSelectedHighlight != null)
            _tutorialSelectedHighlight.SetActive(mode == LobbyMode.Tutorial);
    }
}
