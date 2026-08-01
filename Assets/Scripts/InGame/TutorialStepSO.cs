using UnityEngine;

public enum TutorialActionType
{
    None,
    Move,
    Jump,
    UseSkill,
    Punch,
    Grab,
    Boost, // hit a boost pad?
}

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Tutorial Step")]
public class TutorialStepSO : ScriptableObject
{
    [TextArea] public string ObjectiveText;  // shown on side panel in UI
    [TextArea] public string ObjectiveDescription;  // shown on side panel in UI
    public TutorialActionType RequiredAction;
    public int RequiredCount = 1; // how many times action must be done
}
