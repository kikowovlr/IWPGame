using UnityEngine;

public class PendingMapCache : MonoBehaviour
{
    public static PendingMapCache Instance { get; private set; }
    public int PendingSceneIndex { get; private set; } = -1;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void Set(int sceneBuildIndex)
    {
        PendingSceneIndex = sceneBuildIndex;
    }

    public void Clear()
    {
        PendingSceneIndex = -1;
    }
}
