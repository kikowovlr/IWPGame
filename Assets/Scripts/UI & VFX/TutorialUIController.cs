using System.Collections;
using TMPro;
using UnityEditor.Rendering.LookDev;
using UnityEngine;

/// <summary>
/// manages side panel that shows -> objective text + N/total completion tracker
/// manages ready/set/go overlay for sudden death
/// </summary>
public class TutorialUIController : MonoBehaviour
{
    [Header("Objective Panel")]
    [SerializeField] private GameObject _objectivePanel;
    [SerializeField] private TMP_Text _objectiveText;
    [SerializeField] private TMP_Text _objectiveDescription;

    [SerializeField] private RectTransform _sidePanel;
    [SerializeField] private GameObject _trackerContainer; // toggled off when 
    [SerializeField] private TMP_Text _trackerText; // e.g. "2/4"

    [SerializeField] private GameObject _countdownPanel;
    [SerializeField] private TMP_Text _countdownText;

    [Header("Personal Status")]
    [SerializeField] private TMP_Text _personalStatusText;  // "PENDING..." / "DONE"
    [SerializeField] private Color _pendingColor = new Color(1f, 0.8f, 0.2f); 
    [SerializeField] private Color _doneColor = new Color(0.3f, 1f, 0.4f);

    [Header("Narrator")]
    [SerializeField] private TutorialNarratorController _narrator;

    [Header("Intro Popup")]
    [SerializeField] private GameObject _introPopup;
    [SerializeField] private float _introPopupDuration = 3f;

    [Header("Transition Settings")]
    [SerializeField] private float _slideDuration = 0.4f;
    [SerializeField] private float _sidePanelHiddenX = 600f;  // off-screen right
    [SerializeField] private float _sidePanelShownX = 0f;     // on-screen anchored 
    private Coroutine _slideRoutine;

    private void Awake()
    {
        if (_introPopup != null) _introPopup.SetActive(false);
        if (_sidePanel != null)
            _sidePanel.anchoredPosition = new Vector2(_sidePanelHiddenX, _sidePanel.anchoredPosition.y);
    }

    private void Update()
    {
        if (MatchContext.Current == null || !MatchContext.Current.IsSpawned) return;

        var tm = TutorialManager.Instance;
        // If there's no tutorial or we're not in the active tutorial state, keep everything hidden.
        if (tm == null || tm.CurrentState != TutorialState.TutorialActive)
        {
            HideAllChildren();
            return;
        }

        // in tutorial active phase
        if (tm.IsTrackingPhase)
        {
            SetPersonalStatus(tm.HasLocalPlayerCompletedCurrentStep());
            UpdateTracker(tm.GetCompletionCountForCurrentStep(), tm.GetRealPlayerCount());
        }
    }

    /// <summary>
    /// called when step changes/first shown
    /// updates objective + player completion tracker
    /// </summary>
    public void ShowStep(TutorialStepSO step, int stepIndex, int totalSteps, int completed, int total)
    {
        if (_objectivePanel != null)
            _objectivePanel.SetActive(step != null);

        if (step == null) return;

        if (_objectiveText != null)
            _objectiveText.text = step.ObjectiveText;
        if (_objectiveDescription != null) 
            _objectiveDescription.text = step.ObjectiveDescription;

        UpdateTracker(completed, total);
    }

    /// <summary>
    /// plays countdown sequence
    /// </summary>
    public void ShowCountdownPanel(float duration)
    {
        if (_countdownPanel != null)
            _countdownPanel.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(CountdownRoutine(duration));
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        // split window into 3 intervals
        float time = duration / 3f;

        if (_countdownText != null) _countdownText.text = "READY";
        yield return new WaitForSeconds(time);

        if (_countdownText != null) _countdownText.text = "SET";
        yield return new WaitForSeconds(time);

        if (_countdownText != null) _countdownText.text = "GO!";
        yield return new WaitForSeconds(time);

        if (_countdownPanel != null) _countdownPanel.SetActive(false);
    }

    public void ShowIntroPopup()
    {
        if (_introPopup == null) return;
        StopCoroutine(nameof(IntroPopupRoutine));
        StartCoroutine(IntroPopupRoutine());
    }

    private IEnumerator IntroPopupRoutine()
    {
        _introPopup.SetActive(true);
        yield return new WaitForSeconds(_introPopupDuration);
        _introPopup.SetActive(false);
    }

    /// <summary>
    /// phase-driven UI router -> Called from TutorialManager.OnStepPhaseChanged (all clients)
    /// </summary>
    public void OnStepPhaseChanged(TutorialStepPhase phase, TutorialStepSO step, int stepIndex, int totalSteps, int completed, int total)
    {
        switch (phase)
        {
            case TutorialStepPhase.Intro:
                // popup handled by ShowIntroPopup()
                // ensure panels hidden
                if (_narrator != null) _narrator.Hide();
                SlideSidePanel(false);
                break;

            case TutorialStepPhase.Narrating:
                // typewriter types narration
                // side panel hidden
                // tracking not active yet
                SlideSidePanel(false);
                if (_narrator != null && step != null)
                    _narrator.ShowAndType(step.NarrationText);
                break;

            case TutorialStepPhase.Tracking:
                // hide typewriter, populate + slide in side panel (tracking begins)
                if (_narrator != null) _narrator.SlideOut();
                PopulateStep(step, completed, total);
                if (_trackerContainer != null) _trackerContainer.SetActive(true);
                SetPersonalStatus(false);
                SlideSidePanel(true);
                break;

            case TutorialStepPhase.StepComplete:
                // deactivate tracker, show "moving on", then slide out
                if (_trackerContainer != null) _trackerContainer.SetActive(false);
                if (_objectiveText != null) _objectiveText.text = "";
                if (_objectiveDescription != null) _objectiveDescription.text = "Step done! Moving on...";
                StartCoroutine(SlideOutAfterDelay());
                break;
        }
    }

    private void SlideSidePanel(bool show)
    {
        if (_sidePanel == null) return;
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideRoutine(show ? _sidePanelShownX : _sidePanelHiddenX));
    }

    private IEnumerator SlideRoutine(float targetX)
    {
        float startX = _sidePanel.anchoredPosition.x;
        float t = 0f;
        while (t < _slideDuration)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, t / _slideDuration);
            _sidePanel.anchoredPosition = new Vector2(x, _sidePanel.anchoredPosition.y);
            yield return null;
        }
        _sidePanel.anchoredPosition = new Vector2(targetX, _sidePanel.anchoredPosition.y);
    }

    private IEnumerator SlideOutAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);
        SlideSidePanel(false);
    }

    private void PopulateStep(TutorialStepSO step, int completed, int total)
    {
        if (step == null) return;
        if (_objectiveText != null) _objectiveText.text = step.ObjectiveText;
        if (_objectiveDescription != null) _objectiveDescription.text = step.ObjectiveDescription;
        UpdateTracker(completed, total);
    }

    public void UpdateTracker(int completed, int total)
    {
        if (_trackerText != null) _trackerText.text = $"{completed}/{total}";
    }

    public void HideAll()
    {
        if (_narrator != null) _narrator.Hide();
        if (_introPopup != null) _introPopup.SetActive(false);
        SlideSidePanel(false);
    }

    public void SetPersonalStatus(bool done)
    {
        if (_personalStatusText == null) return;
        _personalStatusText.text = done ? "ACTION DONE" : "ACTION PENDING...";
        _personalStatusText.color = done ? _doneColor : _pendingColor;
    }

    private void HideAllChildren()
    {
        if (_introPopup != null && _introPopup.activeSelf) _introPopup.SetActive(false);
        if (_narrator != null) _narrator.Hide();
        if (_sidePanel != null)
            _sidePanel.anchoredPosition = new Vector2(_sidePanelHiddenX, _sidePanel.anchoredPosition.y);
        if (_trackerContainer != null && _trackerContainer.activeSelf) _trackerContainer.SetActive(false);
    }
}
