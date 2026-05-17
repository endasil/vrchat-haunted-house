using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDKBase;
using Assets._3DStealthGame.Scripts;

public class PlayerUI : Resettable
{
    public PlayerInvenory playerInventory;
    public Snowball snowball;
    public VRC_Pickup snowballPickup;
    public Canvas canvas;

    public Image greenPillIcon;
    public Image redPillIcon;
    public Image bluePillIcon;
    public Image brownPillIcon;

    private VRCPlayerApi localPlayer;
    private Collider snowballCollider;

    public override void Start()
    {
        base.Start();
        localPlayer = Networking.LocalPlayer;

        if (snowball)
        {
            snowballCollider = snowball.GetComponent<Collider>();
            snowballPickup = snowball.GetComponent<VRC_Pickup>();
        }

        _FindPlayerInventory();
    }

    private Image _GetIcon(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Green: return greenPillIcon;
            case KeyType.Red:   return redPillIcon;
            case KeyType.Blue:  return bluePillIcon;
            case KeyType.Brown: return brownPillIcon;
            default:
                Debug.LogError($"Unknown KeyType: {keyType}");
                return null;
        }
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
            Debug.Log("playerObjects[0] not valid");
            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }

        playerInventory = playerObjects[0].GetComponent<PlayerInvenory>();
        if (playerInventory == null)
        {
            Debug.LogError("Could not find any PlayerInventory script on local player!");
            SendCustomEventDelayedFrames(nameof(_FindPlayerInventory), 1);
            return;
        }
    }

    private void LateUpdate()
    {
        if (playerInventory)
        {
            int typeCount = (int)KeyType.LastEnum;
            for (int i = 0; i < typeCount; i++)
            {
                KeyType kt = (KeyType)i;
                Image icon = _GetIcon(kt);
                if (icon == null)
                {
                    Debug.LogError($"Pill icon for {kt} is not assigned in the inspector.");
                    continue;
                }
                icon.gameObject.SetActive(playerInventory.HasPill(kt));
            }
        }
        else
        {
            Debug.LogError("No playerInventory.");
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
        int typeCount = (int)KeyType.LastEnum;
        for (int i = 0; i < typeCount; i++)
        {
            Image icon = _GetIcon((KeyType)i);
            if (icon != null)
                icon.gameObject.SetActive(false);
        }
    }
}
