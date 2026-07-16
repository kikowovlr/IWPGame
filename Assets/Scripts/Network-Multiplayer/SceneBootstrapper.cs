using Fusion;
using UnityEngine;

public abstract class SceneBootstrapper : MonoBehaviour
{
    public abstract void InitializeScene(NetworkRunner runner);
}
