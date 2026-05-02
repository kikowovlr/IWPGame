using UnityEngine;
using FishNet.Connection;
using FishNet.Object;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float cameraYOffset = 0.4f;
    private Camera playerCamera;

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            playerCamera = Camera.main;
            playerCamera.transform.position = new Vector3(transform.position.x, transform.position.y + cameraYOffset, transform.position.z);
            playerCamera.transform.SetParent(transform);
        }
        else
        {
            // if not owner then disable player controller - dont control other players
            gameObject.GetComponent<PlayerController>().enabled = false;
        }
    }
}
