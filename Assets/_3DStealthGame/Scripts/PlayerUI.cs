using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDKBase;
using Assets._3DStealthGame.Scripts;

public class PlayerUI : Resettable
{
    public PlayerMovementU playerInventory;
    public Snowball snowball;
    public VRC_Pickup snowballPickup;
    public Canvas canvas;

    public Image[] pillIcons;       // length 4: Green=0, Red=1, Blue=2, Black=3
    private VRCPlayerApi localPlayer;
    private Collider snowballCollider;

    public override void  Start()
    {
        base.Start();
        localPlayer = Networking.LocalPlayer;
       
        if (!snowball)
        {
           // Debug.LogWarning("No snowball found in UIDisplay.");
        }
        else
        {
            snowballCollider = snowball.GetComponent<Collider>();
            snowballPickup = snowball.GetComponent<VRC_Pickup>();
        }

        _FindPlayerInventory();
    }


    private void _FindPlayerInventory()
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
            string keyCount = playerInventory.GetKeyCountsString();
            int typeCount = (int)KeyType.LastEnum;

            for (int i = 0; i < typeCount; i++)
            {
                if (i < pillIcons.Length && pillIcons[i] != null)
                    pillIcons[i].enabled = playerInventory.GetKeyCount((KeyType)i) > 0;
            }
        }
        else
        {
            Debug.Log("No playerInventory.");
        }
        if (localPlayer != null && localPlayer.IsValid())
        {
            VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 offset = headData.rotation * new Vector3(0.3f, 0.2f, 0.5f);
            canvas.transform.position = headData.position + offset;
            canvas.transform.rotation = headData.rotation;
        }
    }

    public void ResetState()
    {
        for (int i = 0; i < pillIcons.Length; i++)
        {
            if (pillIcons[i] != null)
                pillIcons[i].enabled = false;
        }
    }


}
