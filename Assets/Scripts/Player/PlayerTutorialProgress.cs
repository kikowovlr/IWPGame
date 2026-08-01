using Fusion;
using UnityEngine;

/// <summary>
/// tracks the highest step index this player has completed
/// </summary>
public class PlayerTutorialProgress : NetworkBehaviour
{
    [HideInInspector] [Networked] public int HighestCompletedStep { get; private set; }

    [Networked] private int _currentStepProgress { get; set; } // track if step needs to be done multiple times to count as tutorial step done
    [Networked] private int _trackedStepIndex { get; set; }

    public override void Spawned()
    {
        ResetProgress();
    }

    /// <summary>
    /// host sided: adds one unit of progress towards a step (handles multi-count)
    /// </summary>
    public void RegisterProgress(int stepIndex, int requiredCount)
    {
        if (!Object.HasStateAuthority) return;
        if (stepIndex <= HighestCompletedStep) return; // already done

        // increment tutorial step if user has finished with this step
        if (_trackedStepIndex != stepIndex)
        {
            _trackedStepIndex = stepIndex;
            _currentStepProgress = 0;
        }

        _currentStepProgress++;
        if (_currentStepProgress >= Mathf.Max(1, requiredCount))
            HighestCompletedStep = stepIndex;
    }

    public bool HasCompletedStep(int stepIndex) => HighestCompletedStep >= stepIndex;

    public void ResetProgress()
    {
        if (!Object.HasStateAuthority) return;
        HighestCompletedStep = -1;
        _currentStepProgress = 0;
        _trackedStepIndex = -1;
    }
}
