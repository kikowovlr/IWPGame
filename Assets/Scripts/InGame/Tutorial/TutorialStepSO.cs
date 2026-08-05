using UnityEngine;

public enum TutorialActionType
{
    None,
    Move,
    Sprint,
    Jump,
    Punch,
    StrongPunch,
    Grab,
    Throw,
    Headbutt,
    AirKick,
    UseSkill,
    Environment, // hit a boost pad?
}

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Tutorial Step")]
public class TutorialStepSO : ScriptableObject
{
    [TextArea] public string ObjectiveText;  // shown on side panel in UI
    [TextArea] public string ObjectiveDescription;  // shown on side panel in UI
    [TextArea] public string NarrationText;  // shown in typewriter narration UI
    public TutorialActionType RequiredAction;
    public int RequiredCount = 1; // how many times action must be done
}
