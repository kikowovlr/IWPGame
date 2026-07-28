using UnityEngine;

public class GooRainVFXController : MonoBehaviour
{
    [SerializeField] private ParticleSystem _raindropSystem;

    public void SetAreaSize(Vector2 size)
    {
        if (_raindropSystem == null) return;

        var shape = _raindropSystem.shape;
        Vector3 currentScale = shape.scale;

        // only change X and Z
        shape.scale = new Vector3(size.x, size.y, currentScale.z);
    }

    public void Play()
    {
        if (_raindropSystem != null)
            _raindropSystem.Play();
    }

    public void Stop()
    {
        if (_raindropSystem != null)
            _raindropSystem.Stop();
    }
}
