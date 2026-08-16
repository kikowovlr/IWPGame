using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour, ICleanup
{
    public static LevelLoader Instance { get; private set; }

    [SerializeField] private Animator _transition;
    [SerializeField] private float _transitionTime = 1f;
    public float TransitionTime => _transitionTime;

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
    public IEnumerator TransitionOut()
    {
        _transition.SetTrigger("Start");
        yield return new WaitForSeconds(_transitionTime);
    }

    /// <summary>
    /// Fades the black screen back to clear. Call this after teleporting is complete.
    /// </summary>
    public void TransitionIn()
    {
        _transition.SetTrigger("End");
    }

    /// <summary>
    /// plays wipe to COVER screen (no screen load)
    /// <summary>
    public IEnumerator PlayWipeCover()
    {
        _transition.SetTrigger("Start");
        yield return new WaitForSeconds(_transitionTime);
    }

    /// <summary>
    /// Plays the wipe to REVEAL the screen (open from black).
    /// </summary>
    public void PlayWipeReveal()
    {
        _transition.SetTrigger("End");
    }

    public static void ResetInstance()
    {
        Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Cleanup()
    {
    }
}
