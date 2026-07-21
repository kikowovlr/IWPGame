using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// attached to NetworkRunnerPrefab
/// facilitates seamless scene transitions by overlaying a persistant canvas
/// </summary>
public class TransitionUIManager : MonoBehaviour
{
    public static TransitionUIManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private TMP_Text _loadingTitle;
    [SerializeField] private TMP_Text _loadingStatusText;
    [SerializeField] private GameObject _roundPanel;
    [SerializeField] private TMP_Text _roundText;
    [SerializeField] private Image _loadingBackgroundImage;
    [SerializeField] private Sprite _connectingBackground;
    [SerializeField] private Sprite _mapLoadingBackground;

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
        if (_loadingPanel != null) _loadingPanel.SetActive(true);
        UpdateLoadingStatus(status);
        if (_roundPanel != null) _roundPanel.SetActive(false);

        if (_loadingBackgroundImage != null && _mapLoadingBackground != null)
            _loadingBackgroundImage.sprite = _mapLoadingBackground;
        if (_loadingTitle != null)
            _loadingTitle.text = "MAP LOADING SCREEN";
    }

    public void ShowGenericTransitionScreen(string status)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(true);
        UpdateLoadingStatus(status);
        if (_roundPanel != null) _roundPanel.SetActive(false);

        if (_loadingBackgroundImage != null && _connectingBackground != null)
            _loadingBackgroundImage.sprite = _connectingBackground;

        if (_loadingTitle != null)
            _loadingTitle.text = "LOADING...";
    }

    public void UpdateLoadingStatus(string status)
    {
        if (_loadingStatusText != null) _loadingStatusText.text = "Status: " + status;
    }

    public void ShowRoundSetupScreen(string roundTitle)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(false);
        if (_roundPanel != null) _roundPanel.SetActive(true);
        if (_roundText != null) _roundText.text = roundTitle;
    }

    public void ClearAllOverlays()
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(false);
        if (_roundPanel != null) _roundPanel.SetActive(false);
    }
}
