
using System;

using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class PlayerUI : UdonSharpBehaviour
{
    public PlayerMovementU playerInventory;
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
           // Debug.LogWarning("No snowball found in UIDisplay.");
        }
        else
        {
            snowballCollider = snowball.GetComponent<Collider>();
            snowballPickup = snowball.GetComponent<VRC_Pickup>();
            // Debug.Log($"Snowball:  {snowball.name}");
            // Debug.Log($"Snowball collider enabled:  {snowballCollider.enabled}");
        }


        // Debug.Log($"keyText: {keyText}");
        // Debug.Log($"keyManager: {playerInventory}");
        // Debug.Log($"canvas: {canvas}");
        _FindPlayerInventory();
    }

    public void _FindPlayerInventory()
    {
        if (!Utilities.IsValid(localPlayer))
        {
            Debug.Log("LocalPlayer not valid");
            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }

        GameObject[] playerObjects = localPlayer.GetPlayerObjects();
        if (playerObjects == null || playerObjects.Length == 0)
        {
            Debug.Log("No playerobject found");

            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }

        if (!Utilities.IsValid(playerObjects[0]))
        {
            Debug.Log("No playerObjects[0] not valid");

            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }

        playerInventory = playerObjects[0].GetComponent<PlayerMovementU>();
        if (playerInventory == null)
        {
            Debug.LogError("Could not find any PlayerMovementU script on local player!");
            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }

        // Success - playerInventory is found
        // Continue with your initialization here
    }
    private void LateUpdate()
    {

        if (playerInventory)
        {
            keyText.text = playerInventory.GetKeyCountsString();
        }
        else
        {
            Debug.Log("No playerInventory.");
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
