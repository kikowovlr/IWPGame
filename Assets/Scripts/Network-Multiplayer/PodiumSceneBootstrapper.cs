using Fusion;
using UnityEngine;

public class PodiumSceneBootstrapper : SceneBootstrapper
{
    public override void InitializeScene(NetworkRunner runner)
    {
        PodiumSceneController podiumController = FindAnyObjectByType<PodiumSceneController>();
        if (podiumController != null)
        {
            podiumController.InitializePodium(runner);
        }
    }
}
