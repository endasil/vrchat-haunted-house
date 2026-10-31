using UdonSharp;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CameraOnWhenVisible : UdonSharpBehaviour
{
    public Camera cameraToToggle;

    void Start()
    {
        cameraToToggle.enabled = GetComponent<Renderer>().isVisible;
    }

    void OnBecameVisible()
    {
        Debug.Log("Object became visible");
        cameraToToggle.enabled = true;
    }

    void OnBecameInvisible()
    {
        Debug.Log("Object no longer visible");
        cameraToToggle.enabled = false;
    }
}
