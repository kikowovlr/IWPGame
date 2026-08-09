using Fusion;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUIController : MonoBehaviour
{
    [Header("Network References")]
    [SerializeField] private NetworkLauncher _networkLauncher;
    [SerializeField] private LobbyManager _lobbyManagerPrefab;
    [SerializeField] private LobbyUIController _lobbyUIController;

    [Header("Main Menu Panel")]
    [SerializeField] private GameObject _menuSelectionPanel;
    [SerializeField] private Button _createLobbyButton; // create a lobby
    [SerializeField] private Button _joinLobbyButton; // shows popup menu for entering code
    [SerializeField] private TMP_InputField _nameInputField;

    [Header("Join Lobby Panel")]
    [SerializeField] private GameObject _joinLobbyPanel;
    [SerializeField] private Button _joinConfirmButton; // join lobby if code is valid
    [SerializeField] private Button _joinBackButton; // exit popup
    [SerializeField] private TMP_InputField _joinCodeInputField; // place to enter room code
    [SerializeField] private TMP_Text _joinErrorText;

    [Header("Controls Panel")]
    [SerializeField] private Button _controlsButton;
    [SerializeField] private Button _controlsBackButton;
    [SerializeField] private GameObject _controlsPanel;

    [Header("Quit Button")]
    [SerializeField] private Button _quitButton;
    [SerializeField] private GameObject _quitPanel;
    [SerializeField] private float _delayUntilQuit = 4.0f;

    private NetworkRunner _runner => NetworkLauncher.Instance != null ? NetworkLauncher.Instance.Runner : null;

    private const string NAME_PREFS_KEY = "SavedPlayerName";
    private const int ROOM_CODE_LENGTH = 5;
    private const string ROOM_CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        // initial UI
        ReturnToMainMenu();

        // load their previously typed name if they played b4, if not dont fill
        if (_nameInputField != null)
        {
            _nameInputField.characterLimit = 9;

            if (PlayerPrefs.HasKey(NAME_PREFS_KEY))
                _nameInputField.text = PlayerPrefs.GetString(NAME_PREFS_KEY);
        }

        // assign button callbacks
        if (_createLobbyButton != null)
            _createLobbyButton.onClick.AddListener(CreateLobby);

        if (_joinLobbyButton != null)
            _joinLobbyButton.onClick.AddListener(ShowJoinLobbyPanel);

        if (_joinConfirmButton != null)
            _joinConfirmButton.onClick.AddListener(ConfirmJoinLobby);

        if (_joinBackButton != null)
            _joinBackButton.onClick.AddListener(ShowMainMenuPanel);

        if (_controlsButton != null)
            _controlsButton.onClick.AddListener(ShowControlsPanel);

        if (_controlsBackButton != null)
            _controlsBackButton.onClick.AddListener(ShowMainMenuPanel);

        if (_quitButton != null)
            _quitButton.onClick.AddListener(ShowQuitPanel);

        if (IsReturningFromMatch())
        {
            ShowLobbyOnReturn();
        }
        else
        {
            ShowMainMenuPanel();
        }

        SoundManager.Instance?.PlayMusic(MusicID.Menu);
    }

    /// <summary>
    /// create lobby btn - generates room code and hosts lobby immediately
    /// </summary>
    private void CreateLobby()
    {
        // save name input into input field
        SaveCurrentName();

        string roomCode = GenerateRoomCode();

        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(false);

        StartMatchmaking(GameMode.Host, roomCode, onFailure: (message) =>
        {
            if (TransitionUIManager.Instance != null)
                TransitionUIManager.Instance.UpdateLoadingStatus(message);

            Invoke(nameof(ReturnToMainMenu), 2.5f);
        });
    }

    /// <summary>
    /// join Lobby button — swaps to the code-entry panel
    /// </summary>
    private void ShowJoinLobbyPanel()
    {
        if (_joinErrorText != null)
            _joinErrorText.text = "";

        if (_joinLobbyPanel != null)
            _joinLobbyPanel.SetActive(true);
    }

    /// <summary>
    /// back btn on join + controls     panel - return to main menu
    /// </summary>
    private void ShowMainMenuPanel()
    {
        if (_joinLobbyPanel != null)
            _joinLobbyPanel.SetActive(false);

        if (_controlsPanel != null) 
            _controlsPanel.SetActive(false);

        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// confirm btn on join panel - attempt to connect
    /// </summary>
    private void ConfirmJoinLobby()
    {
        string code = _joinCodeInputField != null ? _joinCodeInputField.text.Trim().ToUpperInvariant() : "";

        if (string.IsNullOrEmpty(code))
        {
            if (_joinErrorText != null)
                _joinErrorText.text = "Please enter a valid lobby code.";
            return;
        }

        SaveCurrentName();

        if (_joinLobbyPanel != null)
            _joinLobbyPanel.SetActive(false);

        StartMatchmaking(GameMode.Client, code, onFailure: (message) =>
        {
            // stay on the join panel, show the error there, no scene/panel transition happened
            if (_joinErrorText != null)
                _joinErrorText.text = message;
        });
    }

    private async void StartMatchmaking(GameMode mode, string sessionName, System.Action<string> onFailure)
    {
        if (_networkLauncher == null) return;

        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ShowGenericTransitionScreen("Connecting..");

        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        var result = await _networkLauncher.LaunchSession(mode, currentSceneIndex, sessionName);

        if (!result.Ok)
        {
            string friendlyMessage = result.ShutdownReason == ShutdownReason.GameNotFound
                ? "Lobby not found. Check the code and try again."
                : $"Connection Failed: {result.ShutdownReason}";

            onFailure?.Invoke(friendlyMessage);

            if (TransitionUIManager.Instance != null)
                TransitionUIManager.Instance.ClearAllOverlays();

            return;
        }

        // only reaches here on a genuinely successful connection —
        // safe to close the join panel and show the lobby now
        if (_joinLobbyPanel != null)
            _joinLobbyPanel.SetActive(false);

        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(false);

        if (mode == GameMode.Host && _lobbyManagerPrefab != null)
        {
            _runner.Spawn(_lobbyManagerPrefab.gameObject, Vector3.zero, Quaternion.identity);
        }

        if (_lobbyUIController != null)
            _lobbyUIController.ShowLobby(sessionName);

        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ClearAllOverlays();
    }

    private string GenerateRoomCode()
    {
        var chars = new char[ROOM_CODE_LENGTH];
        for (int i = 0; i < ROOM_CODE_LENGTH; i++)
        {
            chars[i] = ROOM_CODE_CHARS[Random.Range(0, ROOM_CODE_CHARS.Length)];
        }
        return new string(chars);
    }


    private void SaveCurrentName()
    {
        if (_nameInputField == null) return;

        string finalName = _nameInputField.text.Trim();

        if (finalName.Length > 9)
            finalName = finalName.Substring(0, 9);

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

    public void ReturnToMainMenu()
    {
        if (_joinLobbyPanel != null) _joinLobbyPanel.SetActive(false);
        if (_menuSelectionPanel != null) _menuSelectionPanel.SetActive(true);
        if (_controlsPanel != null) _controlsPanel.SetActive(false);
    }

    private void ShowControlsPanel()
    {
        if (_controlsPanel != null)
            _controlsPanel.SetActive(true);
    }

    private void ShowQuitPanel()
    {
        if (_quitPanel != null)
            _quitPanel.SetActive(true);

        SoundManager.Instance?.StopMusic(_delayUntilQuit * 0.9f); //fade out music before quitting

        StartCoroutine(QuitRoutine());
    }

    private IEnumerator QuitRoutine()
    {
        yield return new WaitForSeconds(_delayUntilQuit);

        QuitGame();
    }

    private void QuitGame()
    {
        Application.Quit();

        // for testing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowLobbyOnReturn()
    {
         if (_menuSelectionPanel != null) _menuSelectionPanel.SetActive(false);

        string lobbyCode = "";
        var runner = NetworkLauncher.Instance.Runner;
        if (runner.SessionInfo != null && runner.SessionInfo.IsValid)
            lobbyCode = runner.SessionInfo.Name;

        if (_lobbyUIController != null)
            _lobbyUIController.ShowLobby(lobbyCode);

        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ClearAllOverlays();
    }

    private bool IsReturningFromMatch()
    {
        return NetworkLauncher.Instance != null
            && NetworkLauncher.Instance.Runner != null
            && NetworkLauncher.Instance.Runner.IsRunning;
    }
}
