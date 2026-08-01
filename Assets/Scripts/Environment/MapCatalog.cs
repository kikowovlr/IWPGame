using UnityEngine;

/// <summary>
/// - Random mode: picks one map at random from _randomModeMaps at start time
/// - Tutorial mode: always loads _tutorialMap
/// </summary>
[CreateAssetMenu(fileName = "MapCatalog", menuName = "Match/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    [System.Serializable]
    public class MapEntry
    {
        public string DisplayName;
        public Sprite NameSprite;
        public Sprite LoadPreview;
        public int SceneBuildIndex;
    }

    [SerializeField] private MapEntry[] _randomModeMaps;
    [SerializeField] private MapEntry _tutorialMap;

    public int RandomMapCount => _randomModeMaps != null ? _randomModeMaps.Length : 0;

    public int GetRandomMapSceneIndex()
    {
        if (_randomModeMaps == null || _randomModeMaps.Length == 0) return -1;
        int pick = Random.Range(0, _randomModeMaps.Length);
        return _randomModeMaps[pick].SceneBuildIndex;
    }

    public int GetTutorialSceneIndex()
    {
        return _tutorialMap != null ? _tutorialMap.SceneBuildIndex : -1;
    }

    /// <summary>
    /// look up map load preview based on build index
    /// </summary>
    public Sprite GetPreviewForSceneIndex(int sceneBuildIndex)
    {
        MapEntry e = FindBySceneIndex(sceneBuildIndex);
        return e != null ? e.LoadPreview : null;
    }

    public string GetDisplayNameForSceneIndex(int sceneBuildIndex)
    {
        MapEntry e = FindBySceneIndex(sceneBuildIndex);
        return e != null ? e.DisplayName : null;
    }

    public Sprite GetNameSpriteForSceneIndex(int sceneBuildIndex)
    {
        MapEntry e = FindBySceneIndex(sceneBuildIndex);
        return e != null ? e.NameSprite : null;
    }

    private MapEntry FindBySceneIndex(int sceneBuildIndex)
    {
        if (_randomModeMaps != null)
        {
            foreach (var m in _randomModeMaps)
                if (m != null && m.SceneBuildIndex == sceneBuildIndex)
                    return m;
        }

        if (_tutorialMap != null && _tutorialMap.SceneBuildIndex == sceneBuildIndex)
            return _tutorialMap;

        return null;
    }

    public string RandomModeLabel => "RANDOM";
    public string TutorialModeLabel => "TUTORIAL";
}
