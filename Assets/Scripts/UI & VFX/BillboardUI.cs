using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform _cameraTransform;

    private void LateUpdate()
    {
        // re-acquire if we lost the camera (e.g. after a scene load)
        if (_cameraTransform == null)
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
            else
                return;   // no camera yet this frame
        }

        transform.LookAt(
            transform.position + _cameraTransform.rotation * Vector3.forward,
            _cameraTransform.rotation * Vector3.up);
    }
}
