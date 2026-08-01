using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private TMP_Text _lobbyNameText;
    [SerializeField] private TMP_Text _lobbyCodeText;
    [SerializeField] private Transform _playerRowContainer;
    [SerializeField] private LobbyPlayerRowUI _playerRowPrefab;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private TMP_Text _startGameHintText;
    [SerializeField] private Button _leaveButton;
    [SerializeField] private int _gameplaySceneBuildIndex = 1;
    [SerializeField] private int _minPlayersToStart = 2;

    private NetworkRunner _runner => NetworkLauncher.Instance != null ? NetworkLauncher.Instance.Runner : null;
    private List<LobbyPlayerRowUI> _spawnedRows = new List<LobbyPlayerRowUI>();
    private float _refreshTimer;
    private const float REFRESH_INTERVAL = 0.5f;

    public void ShowLobby(string lobbyCode)
    {
        gameObject.SetActive(true);
        if (_lobbyPanel != null) _lobbyPanel.SetActive(true);
        if (_lobbyCodeText != null) _lobbyCodeText.text = lobbyCode;
        if (_lobbyNameText != null) _lobbyNameText.text = "LOBBY";

        if (_startGameButton != null)
        {
            _startGameButton.onClick.RemoveAllListeners();
            _startGameButton.onClick.AddListener(StartGame);
        }

        if (_leaveButton != null)
        {
            _leaveButton.onClick.RemoveAllListeners();
            _leaveButton.onClick.AddListener(LeaveLobby);
        }

        RefreshRoster();
    }

    private void Update()
    {
        if (_lobbyPanel == null || !_lobbyPanel.activeSelf) return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= REFRESH_INTERVAL)
        {
            _refreshTimer = 0f;
            RefreshRoster();
        }

        bool isHost = _runner != null && _runner.IsServer;
        int currentPlayerCount = LobbyManager.Instance != null ? LobbyManager.Instance.GetLobbyRoster().Count : 0;
        bool hasEnoughPlayers = currentPlayerCount >= _minPlayersToStart;

        if (_startGameButton != null)
        {
            _startGameButton.gameObject.SetActive(isHost); //  host-only visibility
            _startGameButton.interactable = hasEnoughPlayers; // greyed out until enough players
        }

        if (_startGameHintText != null)
        {
            _startGameHintText.gameObject.SetActive(isHost && !hasEnoughPlayers);
            _startGameHintText.text = $"Need at least {_minPlayersToStart} players ({currentPlayerCount}/{_minPlayersToStart})";
        }
    }


    private void RefreshRoster()
    {
        if (LobbyManager.Instance == null)
            return;

        var roster = LobbyManager.Instance.GetLobbyRoster();

        while (_spawnedRows.Count < roster.Count)
            _spawnedRows.Add(Instantiate(_playerRowPrefab, _playerRowContainer));

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (i < roster.Count)
            {
                _spawnedRows[i].gameObject.SetActive(true);
                _spawnedRows[i].SetData(roster[i].name, roster[i].isHost);
            }
            else
            {
                _spawnedRows[i].gameObject.SetActive(false);
            }
        }
    }

    private async void StartGame()
    {
        if (_runner == null || !_runner.IsServer) return;

        int currentPlayerCount = LobbyManager.Instance != null ? LobbyManager.Instance.GetLobbyRoster().Count : 0;
        if (currentPlayerCount < _minPlayersToStart) return;

        int sceneIndex = _gameplaySceneBuildIndex; // fallback

        if (LobbyMapSelection.Instance != null)
        {
            LobbyMapSelection.Instance.CommitResolvedScene(); // roll
            int resolved = LobbyMapSelection.Instance.ResolvedSceneIndex;
            if (resolved != -1)
                sceneIndex = resolved;

            if (PendingMapCache.Instance != null)
                PendingMapCache.Instance.Set(sceneIndex);

            await System.Threading.Tasks.Task.Delay(250);
        }

        await _runner.LoadScene(SceneRef.FromIndex(sceneIndex));
    }

    private async void LeaveLobby()
    {
        if (_runner != null)
            await _runner.Shutdown();

        if (_lobbyPanel != null) _lobbyPanel.SetActive(false);

        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();

        MainMenuUIController mainMenu = FindAnyObjectByType<MainMenuUIController>();
        if (mainMenu != null) mainMenu.ReturnToMainMenu();
    }

    public void HandleForcedDisconnect()
    {
        if (_lobbyPanel != null) _lobbyPanel.SetActive(false);

        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();

        MainMenuUIController mainMenu = FindAnyObjectByType<MainMenuUIController>();
        if (mainMenu != null) mainMenu.ReturnToMainMenu();
    }
}
