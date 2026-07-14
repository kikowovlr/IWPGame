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

    [Header("Rules")]
    [SerializeField] private int _crownsToWinMatch = 3;

    [Header("Podium Scene")]
    [SerializeField] private SceneReference _podiumSceneRef;

    public float SetUpDuration => _setupDuration;
    public float CountdownDuration => _countdownDuration;
    public float RoundOverBufferDuration => _roundOverBufferDuration;
    public float MatchOverBufferDuration => _matchOverBufferDuration;
    public int CrownsToWinMatch => _crownsToWinMatch;
    public SceneReference PodiumScene => _podiumSceneRef;
}
