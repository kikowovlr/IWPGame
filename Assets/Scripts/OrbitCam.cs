using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x; use "Cinemachine" namespace in 2.x

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class AutoOrbit : MonoBehaviour
{
    public float speed = 20f; // deg/sec
    CinemachineOrbitalFollow _orbital;
    void Awake() => _orbital = GetComponent<CinemachineOrbitalFollow>();
    void Update() => _orbital.HorizontalAxis.Value += speed * Time.deltaTime;
}