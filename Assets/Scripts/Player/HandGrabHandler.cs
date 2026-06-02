using UnityEngine;

enum GrabState
{
    None, // grab not pressed
    Searching, // grab is pressed - searching for grab target
    Reaching, // grab target found, hands reaching
    Attached // hands attached to the target
}

public class HandGrabHandler : MonoBehaviour
{
    
}