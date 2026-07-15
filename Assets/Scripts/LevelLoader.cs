using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance { get; private set; }

    [SerializeField] private Animator transition;
    [SerializeField] private float transitionTime = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Starts the fade-out to black. Call this before loading the next scene.
    /// </summary>
    public IEnumerator FadeToBlack()
    {
        transition.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
    }

    /// <summary>
    /// Fades the black screen back to clear. Call this after teleporting is complete.
    /// </summary>
    public void FadeToClear()
    {
        transition.SetTrigger("End");
    }
}
