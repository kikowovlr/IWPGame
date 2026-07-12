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

    private void Start()
    {
        // initial UI
        if (_menuSelectionPanel != null)
            _menuSelectionPanel.SetActive(true);

        // assign button callbacks
        if (_hostButton != null)
            _hostButton.onClick.AddListener(() => StartMatchmaking(GameMode.Host));
        if (_joinButton != null)
            _joinButton.onClick.AddListener(() => StartMatchmaking(GameMode.Client));
    }

    private async void StartMatchmaking(GameMode mode)
    {
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


    private void ResetMenuUI()
    {
        if (TransitionUIManager.Instance != null)
            TransitionUIManager.Instance.ClearAllOverlays();

        if (_menuSelectionPanel != null) 
            _menuSelectionPanel.SetActive(true);
    }
}
