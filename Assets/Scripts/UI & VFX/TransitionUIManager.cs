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
    [SerializeField] private GameObject _loadingTitleGO;
    [SerializeField] private TMP_Text _loadingStatusText;
    [SerializeField] private GameObject _roundPanel;
    [SerializeField] private TMP_Text _roundText;

    [SerializeField] private GameObject _defaultBackgroundGO;   // connecting / fallback background
    [SerializeField] private GameObject _mapBackgroundGO;       // shown when loading a specific map
    [SerializeField] private Image _mapBackgroundImage;

    [SerializeField] private GameObject _mapNameSpriteGO;       
    [SerializeField] private Image _mapNameImage;              

    [SerializeField] private GameObject _loadingBarGO;
    [SerializeField] private Image _loadingBar;
    [SerializeField] private float _mapLoadingMinDuration = 5.0f;
    [SerializeField] private MapCatalog _catalog;

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

    public void ShowMapLoadingScreenTimed(string status, int sceneBuildIndex = -1)
    {
        Sprite preview = ResolvePreview(sceneBuildIndex);
        string mapName = ResolveMapName(sceneBuildIndex);
        Sprite nameSprite = ResolveNameSprite(sceneBuildIndex);
        StartCoroutine(MapLoadingRoutine(status, preview, mapName, nameSprite));
    }

    private Sprite ResolvePreview(int sceneBuildIndex)
    {
        if (_catalog != null && sceneBuildIndex != -1)
        {
            Sprite p = _catalog.GetPreviewForSceneIndex(sceneBuildIndex);
            if (p != null) return p;
        }
        return null; // fallback to default background
    }

    private string ResolveMapName(int sceneBuildIndex)
    {
        if (_catalog != null && sceneBuildIndex != -1)
        {
            string n = _catalog.GetDisplayNameForSceneIndex(sceneBuildIndex);
            if (!string.IsNullOrEmpty(n)) return n;
        }
        return null;
    }

    private Sprite ResolveNameSprite(int sceneBuildIndex)
    {
        if (_catalog != null && sceneBuildIndex != -1)
            return _catalog.GetNameSpriteForSceneIndex(sceneBuildIndex);
        return null;
    }

    private IEnumerator MapLoadingRoutine(string status, Sprite mapPreview, string mapName, Sprite nameSprite)
    {
        ShowMapLoadingScreen(status, mapPreview, mapName, nameSprite);

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

    public void ShowMapLoadingScreen(string status, Sprite mapPreview = null, string mapName = null, Sprite nameSprite = null)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(true);
        UpdateLoadingStatus(status);
        if (_roundPanel != null) _roundPanel.SetActive(false);

        bool hasPreview = mapPreview != null;

        // toggle backgrounds: map background if we have a preview, else default
        SetBackground(useMap: hasPreview);
        if (hasPreview && _mapBackgroundImage != null)
            _mapBackgroundImage.sprite = mapPreview;

        // Title: styled name art if available, else fall back to word
        if (nameSprite != null)
        {
            ShowNameArt(nameSprite);
        }
        else
        {
            ShowTitleText(!string.IsNullOrEmpty(mapName)
                ? $"GOING TO MAP - {mapName.ToUpper()}..."
                : "MAP LOADING...");
        }

        if (_loadingBarGO != null)
            _loadingBarGO.SetActive(true);
    }

    public void ShowGenericTransitionScreen(string status)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(true);
        UpdateLoadingStatus(status);
        if (_roundPanel != null) _roundPanel.SetActive(false);

        // connecting / generic always uses the default background
        SetBackground(useMap: false);

        ShowTitleText("LOADING...");
    }

    private void ShowNameArt(Sprite nameSprite)
    {
        if (_mapNameImage != null)
        {
            _mapNameImage.sprite = nameSprite;
            _mapNameImage.SetNativeSize();   // size to the sprite's real dimensions 
        }
        if (_mapNameSpriteGO != null) _mapNameSpriteGO.SetActive(true);
        if (_loadingTitleGO != null) _loadingTitleGO.SetActive(false);
    }

    private void ShowTitleText(string text)
    {
        if (_loadingTitle != null) _loadingTitle.text = text;
        if (_loadingTitleGO != null) _loadingTitleGO.SetActive(true);
        if (_mapNameSpriteGO != null) _mapNameSpriteGO.SetActive(false);
    }

    private void SetBackground(bool useMap)
    {
        if (_mapBackgroundGO != null) _mapBackgroundGO.SetActive(useMap);
        if (_defaultBackgroundGO != null) _defaultBackgroundGO.SetActive(!useMap);
    }

    public void UpdateLoadingStatus(string status)
    {
        if (_loadingStatusText != null) _loadingStatusText.text = "LOADING: " + status;
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

        if (_mapBackgroundGO != null) _mapBackgroundGO.SetActive(false);
        if (_defaultBackgroundGO != null) _defaultBackgroundGO.SetActive(false);
        if (_mapNameSpriteGO != null) _mapNameSpriteGO.SetActive(false);
    }
}
