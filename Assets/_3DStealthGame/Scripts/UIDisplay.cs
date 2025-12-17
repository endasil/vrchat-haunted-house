
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

    void Start()
    {

        localPlayer = Networking.LocalPlayer;
        
        Debug.Log($"Snowball:  {snowball.name}");
        Debug.Log($"keyText: {keyText}");
        Debug.Log($"keyManager: {keyManager}");
        Debug.Log($"canvas: {canvas}");
        snowballPickup = snowball.GetComponent<VRC_Pickup>();
    }
    private void LateUpdate()
    {
        if (keyText)
        {
            // keyText.text = $"Keys: {keyManager.keyCount}";
            keyText.text = "Snowball held: "+ snowballPickup.IsHeld;
        }
        else
        {
            Debug.LogError("keyText is null!");
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
