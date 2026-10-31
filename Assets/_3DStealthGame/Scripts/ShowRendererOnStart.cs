using UnityEngine;

using UdonSharp;

// Turns the renderer on when the world starts, so it can stay hidden in the editor.
// Used on the Roof Cover, which would otherwise hide the house in the Scene view.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ShowRendererOnStart : UdonSharpBehaviour
{
    void Start()
    {
        GetComponent<Renderer>().enabled = true;
    }
}
