using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PodiumSceneUIHandler : MonoBehaviour
{
    [SerializeField] private Button _exitBtn;
    [SerializeField] private Button _backToLobbyBtn;
    [SerializeField] private string _mainMenuSceneName = "MainMenuScene";
    [SerializeField] private int _mainMenuSceneBuildIndex = 0;

    private NetworkRunner _runner => NetworkLauncher.Instance != null ? NetworkLauncher.Instance.Runner : null;
    private bool IsHost => _runner != null && _runner.IsServer;

    private void Awake()
    {
        if (_exitBtn != null)
            _exitBtn.onClick.AddListener(OnExitClicked);
        if (_backToLobbyBtn != null)
            _backToLobbyBtn.onClick.AddListener(OnBackToLobbyBtn);
    }

    private void Update()
    {
        // return-to-lobby is host-only -> only the host can bring everyone back
        if (_backToLobbyBtn != null)
        {
            bool shouldShow = IsHost;
            if (_backToLobbyBtn.gameObject.activeSelf != shouldShow)
                _backToLobbyBtn.gameObject.SetActive(shouldShow);
        }
    }

    private void OnExitClicked()
    {
        // exit = leave the session entirely, back to title (shuts down runner)
        SceneTransitioner.Instance.PerformTransition(_mainMenuSceneName);
    }

    private void OnBackToLobbyBtn()
    {
        if (!IsHost) return; // safety

        DespawnAllPlayers();

        // host-authoritative Fusion scene load: brings the WHOLE session back to
        // the main menu scene together, where the lobby UI re-shows.
        _runner.LoadScene(SceneRef.FromIndex(_mainMenuSceneBuildIndex), LoadSceneMode.Single);
    }

    private void DespawnAllPlayers()
    {
        if (!IsHost) return;
        foreach (PlayerRef p in _runner.ActivePlayers)
        {
            if (_runner.TryGetPlayerObject(p, out NetworkObject playerObj))
                _runner.Despawn(playerObj);
        }
    }
}
