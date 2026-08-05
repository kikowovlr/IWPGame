using TMPro;
using UnityEngine;

public class CountdownUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _countdownPanel;
    [SerializeField] private TMP_Text _countdownText;

    [Header("Settings")]
    [SerializeField] private float _goDisappearTime = 0.5f;

    private string _lastDisplayedWord = "";
    private bool _hasTriggeredGo = false;
    private float _goDisplayTimer = 0f;

    private void Start()
    {
        if (_countdownPanel != null)
            _countdownPanel.SetActive(false);
    }

    private void Update()
    {
        ICountdownSource source = CountdownSourceLocator.Current;
        if (source == null)
        {
            // no active countdown source in this scene
            if (!_hasTriggeredGo)
            {
                SetCountdownPanelActive(false);
                _lastDisplayedWord = " ";
            }
            return;
        }

        // READY / SET phase
        if (source.IsCountdownActive)
        {
            _hasTriggeredGo = false; // reset GO flag
            SetCountdownPanelActive(true);
            UpdateCountdownWords(source);
        }
        // GO! phase (countdown ended, go not yet flushed)
        else if (source.ShouldShowGo && !_hasTriggeredGo)
        {
            SetCountdownPanelActive(true);
            UpdateWordDisplay("GO!");

            _goDisplayTimer += Time.deltaTime;
            if (_goDisplayTimer >= _goDisappearTime)
            {
                _hasTriggeredGo = true;
                _goDisplayTimer = 0f;
                SetCountdownPanelActive(false);
            }
        }
        // everywhere else
        else
        {
            if (!_hasTriggeredGo)
            {
                SetCountdownPanelActive(false);
                _lastDisplayedWord = " ";
            }
        }
    }

    private void SetCountdownPanelActive(bool active)
    {
        if (_countdownPanel != null && _countdownPanel.activeSelf != active)
            _countdownPanel.SetActive(active);
    }

    /// <summary>
    /// dynamically uses countdown timer set in MatchSettings SO to show words with equal duration
    /// </summary>
    private void UpdateCountdownWords(ICountdownSource source)
    {
        float remainingTime = source.CountdownRemaining;
        float totalDuration = source.CountdownTotal;
        float segementDuration = totalDuration / 2f;

        if (remainingTime > segementDuration)
            UpdateWordDisplay("READY");
        else if (remainingTime > 0f)
            UpdateWordDisplay("SET");
    }

    private void UpdateWordDisplay(string newWord)
    {
        if (_lastDisplayedWord == newWord) return; // dont update if same word
        _lastDisplayedWord = newWord;

        _countdownText.text = newWord;
    }
}
