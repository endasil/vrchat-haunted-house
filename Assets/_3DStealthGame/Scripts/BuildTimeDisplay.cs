using TMPro;

using UdonSharp;

using UnityEngine;

// Shows when the world was last built and uploaded. The build time is stamped
// into buildTime by BuildTimeStamper (an editor script) right before the SDK
// build, so at runtime we just copy it onto the label.
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BuildTimeDisplay : UdonSharpBehaviour
{
    // Filled in at build time by BuildTimeStamper. Format: "yyyy-MM-dd HH:mm UTC".
    public string buildTime = "";

    public TextMeshPro label;

    void Start()
    {
        if (label == null)
        {
            label = GetComponent<TextMeshPro>();
        }
        if (label == null)
        {
            Debug.LogError($"BuildTimeDisplay ({gameObject.name}): label text is not assigned in the inspector.");
            return;
        }
        label.text = "Built " + buildTime;
    }
}
