
using System;
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class UIDisplay : UdonSharpBehaviour
{
    public PlayerMovementU keyManager;
    public Snowball snowball;
    public VRC_Pickup snowballPickup;
    public TMP_Text keyText;
    public Canvas canvas;
    private VRCPlayerApi localPlayer;
    private Collider snowballCollider;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (!snowball)
        {
            // Debug.LogError("No snowball found in UIDisplay.");
        }
        else
        {
            snowballCollider = snowball.GetComponent<Collider>();
            snowballPickup = snowball.GetComponent<VRC_Pickup>();
            Debug.Log($"Snowball:  {snowball.name}");
            Debug.Log($"Snowball collider enabled:  {snowballCollider.enabled}");
        }
        
        
        Debug.Log($"keyText: {keyText}");
        Debug.Log($"keyManager: {keyManager}");
        Debug.Log($"canvas: {canvas}");
        
        
    }
    private void LateUpdate()
    {
        if (keyText && snowball)
        {
            // keyText.text = $"Keys: {keyManager.keyCount}";
            keyText.text = "Snowball pos: "+ snowball.transform.position + "\n";
            keyText.text += "Snowball collider: " + snowballCollider.enabled;
        }
        
        if (localPlayer != null && localPlayer.IsValid())
        {
            VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 offset = headData.rotation * new Vector3(0.3f, 0.2f, 0.5f);
            var prevPo = canvas.transform.position;
            canvas.transform.position = headData.position + offset;
            canvas.transform.rotation = headData.rotation;
        }
    }
}
