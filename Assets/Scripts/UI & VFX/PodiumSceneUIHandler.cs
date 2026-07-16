using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PodiumSceneUIHandler : MonoBehaviour
{
    [SerializeField] private Button _exitBtn;
    [SerializeField] private Button _backToLobbyBtn;
    [SerializeField] private string _mainMenuSceneName = "MainMenuScene";

    private void Awake()
    {
        if (_exitBtn != null)
            _exitBtn.onClick.AddListener(OnExitClicked);
        if (_backToLobbyBtn != null)
            _backToLobbyBtn.onClick.AddListener(OnBackToLobbyBtn);
    }

    private void OnExitClicked()
    {
        SceneTransitioner.Instance.PerformTransition(_mainMenuSceneName);
    }

    private void OnBackToLobbyBtn()
    {
        // TODO: after lobby system made
    }
}
