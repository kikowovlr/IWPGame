using System.Threading;
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
        if (GameManager.Instance == null) return;

        RoundState currState = GameManager.Instance.GetCurrentRoundState();

        // ensure countdown only shows up during Countdown state
        if (currState == RoundState.Countdown)
        {
            // show countdown
            _hasTriggeredGo = false; // reset GO flag
            SetCountdownPanelActive(true);
            UpdateCountdownWords();
        }
        // round js went active but GO has not been triggered
        else if (currState == RoundState.RoundActive && !_hasTriggeredGo)
        {
            SetCountdownPanelActive(true);
            UpdateWordDisplay("GO!");

            // run local timer to handle lingering of GO text
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
    private void UpdateCountdownWords()
    {
        float remainingTime = GameManager.Instance.GetRemainingStateTime();
        float totalDuration = GameManager.Instance.Settings.CountdownDuration;
        float segementDuration = totalDuration / 2f;

        if (remainingTime > segementDuration)
        {
            UpdateWordDisplay("READY");
        }
        else if (remainingTime > 0f)
        {
            UpdateWordDisplay("SET");
        }
    }

    private void UpdateWordDisplay(string newWord)
    {
        if (_lastDisplayedWord == newWord) return; // dont update if same word
        _lastDisplayedWord = newWord;

        _countdownText.text = newWord;
    }
}
