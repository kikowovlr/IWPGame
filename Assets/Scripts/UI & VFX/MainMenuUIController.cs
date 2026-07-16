using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUIController : MonoBehaviour
{
    [Header("Network References")]
    [SerializeField] private NetworkLauncher _networkLauncher;
    [SerializeField] private int _gameplaySceneBuildIndex = 1;

    [Header("UI References")]
    [SerializeField] private GameObject _menuSelectionPanel;

    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;

    [SerializeField] private TMP_InputField _nameInputField;
    private const string NAME_PREFS_KEY = "SavedPlayerName";

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        // initial UI
        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(true);

        // load their previously typed name if they played b4, if not dont fill
        if (_nameInputField != null && PlayerPrefs.HasKey(NAME_PREFS_KEY))
        {
            _nameInputField.text = PlayerPrefs.GetString(NAME_PREFS_KEY);
        }

        // assign button callbacks
        if (_hostButton != null)
            _hostButton.onClick.AddListener(() => StartMatchmaking(GameMode.Host));
        if (_joinButton != null)
            _joinButton.onClick.AddListener(() => StartMatchmaking(GameMode.Client));
    }

    private async void StartMatchmaking(GameMode mode)
    {
        // save name input into input field
        SaveCurrentName();

        // swap panels
        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(false);

        // use transition ui manager
        if (TransitionUIManager.Instance != null)
        {
            string statusMessage = mode == GameMode.Host ? "Initializing Server..." : "Connecting to Host...";
            TransitionUIManager.Instance.ShowMapLoadingScreen(statusMessage);
        }

        // call modular launcher helper routine
        if (_networkLauncher != null)
        {
            var result = await _networkLauncher.LaunchSession(mode, _gameplaySceneBuildIndex);

            // fallback
            if (!result.Ok)
            {
                if (TransitionUIManager.Instance != null)
                    TransitionUIManager.Instance.UpdateLoadingStatus($"Connection Failed: {result.ShutdownReason}");

                // re enable menu panel
                Invoke(nameof(ResetMenuUI), 2.5f);
            }
        }
    }  
    
    private void SaveCurrentName()
    {
        if (_nameInputField == null) return;

        string finalName = _nameInputField.text;

        // validation check - dont save it blank
        if (string.IsNullOrWhiteSpace(finalName))
        {
            // clear so system uses default fallbacks later on
            PlayerPrefs.DeleteKey(NAME_PREFS_KEY);
        }
        else
        {
            // trim
            PlayerPrefs.SetString(NAME_PREFS_KEY, finalName);
        }

        PlayerPrefs.Save();
    }


    private void ResetMenuUI()
    {
        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ClearAllOverlays();

        if (_menuSelectionPanel != null) 
            _menuSelectionPanel.SetActive(true);
    }
}
