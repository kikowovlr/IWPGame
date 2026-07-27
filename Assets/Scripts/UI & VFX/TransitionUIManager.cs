using System.Collections;
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

    [SerializeField] private GameObject _loadingBarGO;
    [SerializeField] private Image _loadingBar;
    [SerializeField] private float _mapLoadingMinDuration = 5.0f;

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

    private void Start()
    {
        if (_loadingBarGO != null)
            _loadingBarGO.SetActive(false);
    }

    public void ShowMapLoadingScreenTimed(string status)
    {
        StartCoroutine(MapLoadingRoutine(status));
    }

    private IEnumerator MapLoadingRoutine(string status)
    {
        ShowMapLoadingScreen(status);

        float elapsed = 0f;
        while (elapsed < _mapLoadingMinDuration)
        {
            elapsed += Time.deltaTime;
            if (_loadingBar != null)
                _loadingBar.fillAmount = Mathf.Clamp01(elapsed / _mapLoadingMinDuration);
            yield return null;
        }

        if (_loadingBarGO != null)
            _loadingBarGO.SetActive(false);
        ClearAllOverlays();
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

        if (_loadingBarGO != null)
            _loadingBarGO.SetActive(true);
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
