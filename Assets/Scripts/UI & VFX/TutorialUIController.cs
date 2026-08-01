using System.Collections;
using TMPro;
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
    [SerializeField] private TMP_Text _trackerText; // e.g. "2/4"

    [SerializeField] private GameObject _countdownPanel;
    [SerializeField] private TMP_Text _countdownText;

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
    /// updates N/total completion tracker
    /// </summary>
    public void UpdateTracker(int completed, int total)
    {
        if (_trackerText != null)
            _trackerText.text = $"{completed}/{total}";
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
}
