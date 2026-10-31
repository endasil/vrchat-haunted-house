using UdonSharp;

using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MirrorToggle : UdonSharpBehaviour
{
    public GameObject mirror;
    public ButtonGlow buttonGlow;

    private void Start()
    {
        if (mirror == null || buttonGlow == null)
        {
            Debug.LogError($"MirrorToggle ({gameObject.name}): mirror and buttonGlow need to be assigned in the inspector.", gameObject);
            return;
        }

        buttonGlow.ShowOn(mirror.activeSelf);
    }

    public override void Interact()
    {
        mirror.SetActive(!mirror.activeSelf);
        buttonGlow.ShowOn(mirror.activeSelf);
    }
}
