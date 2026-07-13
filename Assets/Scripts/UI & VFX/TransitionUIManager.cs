using TMPro;
using UnityEngine;

/// <summary>
/// attached to NetworkRunnerPrefab
/// facilitates seamless scene transitions by overlaying a persistant canvas
/// </summary>
public class TransitionUIManager : MonoBehaviour
{
    public static TransitionUIManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject _mapLoadingPanel; // preview map loading panel
    [SerializeField] private TMP_Text _loadingStatusText;
    [SerializeField] private GameObject _roundPanel;
    [SerializeField] private TMP_Text _roundText;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // hide everything on startup
            ClearAllOverlays();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowMapLoadingScreen(string status)
    {
        if (_mapLoadingPanel != null) _mapLoadingPanel.SetActive(true);
        UpdateLoadingStatus(status);
        if (_roundPanel != null) _roundPanel.SetActive(false);
    }

    public void UpdateLoadingStatus(string status)
    {
        if (_loadingStatusText != null) _loadingStatusText.text = "Status: " + status;
    }

    public void ShowRoundSetupScreen(string roundTitle)
    {
        if (_mapLoadingPanel != null) _mapLoadingPanel.SetActive(false);
        if (_roundPanel != null) _roundPanel.SetActive(true);
        if (_roundText != null) _roundText.text = roundTitle;
    }

    public void ClearAllOverlays()
    {
        if (_mapLoadingPanel != null) _mapLoadingPanel.SetActive(false);
        if (_roundPanel != null) _roundPanel.SetActive(false);
    }
}
