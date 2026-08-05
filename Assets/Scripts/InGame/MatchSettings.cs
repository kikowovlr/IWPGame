using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SceneReference
{
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset _sceneAsset;
#endif

    [HideInInspector] [SerializeField] private string _scenePath;
    [HideInInspector] [SerializeField] private string _sceneName;
    public string SceneName => _sceneName;
    public string ScenePath => _scenePath;

#if UNITY_EDITOR
    public void OnValidate()
    {
        if (_sceneAsset != null)
        {
            _sceneName = _sceneAsset.name;
            _scenePath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
        }
        else
        {
            _sceneName = string.Empty;
            _scenePath = string.Empty;
        }
    }
#endif
}

[CreateAssetMenu(fileName = "NewMatchSettings", menuName = "Match/MatchSettings")]
public class MatchSettings : ScriptableObject
{
    [Header("Match Durations")]
    [SerializeField] private float _setupDuration = 4.0f;
    [SerializeField] private float _countdownDuration = 3.5f;
    [SerializeField] private float _roundOverBufferDuration = 4.0f;
    [SerializeField] private float _matchOverBufferDuration = 4.0f;
    [SerializeField] private float _characterSelectDuration = 60.0f;
    [SerializeField] private float _finalCharacterSelectCountdown = 5.0f;

    [Header("Rules")]
    [SerializeField] private int _crownsToWinMatch = 3;

    [Header("Podium Scene")]
    [SerializeField] private SceneReference _podiumSceneRef;

    [Header("Tutorial Settings")]
    [SerializeField] private float _tutorialIntroDuration = 3.0f;       // TUTORIAL STAGE popup
    [SerializeField] private float _stepCompleteDisplayDuration = 2f;   // step done, moving on.. popup
    [SerializeField] private float _narrationCharsPerSecond = 2f;       // typewriter speed for narration text
    [SerializeField] private float _narrationHoldAfterType = 1f;        // extra hold after typing finishes before tracking starts
    [SerializeField] private float _suddenDeathBannerDuration = 3f;

    public float SetUpDuration => _setupDuration;
    public float CountdownDuration => _countdownDuration;
    public float RoundOverBufferDuration => _roundOverBufferDuration;
    public float MatchOverBufferDuration => _matchOverBufferDuration;
    public float CharacterSelectDuration => _characterSelectDuration;
    public float FinalCharacterSelectCountdown => _finalCharacterSelectCountdown;
    public int CrownsToWinMatch => _crownsToWinMatch;
    public SceneReference PodiumScene => _podiumSceneRef;

    public float TutorialIntroDuration => _tutorialIntroDuration;
    public float StepCompleteDisplayDuration => _stepCompleteDisplayDuration;
    public float NarrationCharsPerSecond => _narrationCharsPerSecond;
    public float NarrationHoldAfterType => _narrationHoldAfterType;
    public float SuddenDeathBannerDuration => _suddenDeathBannerDuration;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_podiumSceneRef != null)
        {
            _podiumSceneRef.OnValidate();
        }
    }
#endif
}
