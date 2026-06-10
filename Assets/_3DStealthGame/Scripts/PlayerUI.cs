#pragma warning disable IDE1006 // Naming Styles
using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDKBase;
using VRC.SDK3.Rendering;
using Assets._3DStealthGame.Scripts;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)] 
public class PlayerUI : Resettable
{
    public PlayerInventory playerInventory;
    public Snowball snowball;
    public VRC_Pickup snowballPickup;
    public Canvas canvas;

    public Image greenPillIcon;
    public Image redPillIcon;
    public Image bluePillIcon;
    public Image brownPillIcon;
    // Where to put the HUD canvas relative to the head (x = right, y = up, z = forward, in
    // metres). In VR we use this as-is. On desktop we only keep the z (how far in front) and
    // work out x and y ourselves each frame in GetDesktopOffset — see the long note there.
    public Vector3 UIOffset = new Vector3(0.3f, 0.2f, 0.5f);

    // Desktop only: where the pills sit on the screen. 0 is the middle, +-1 is the edge, and
    // the sign picks which side or corner. They stay in that spot no matter how the window is
    // sized or shaped — just set both in the inspector. 
    public float desktopVerticalFraction = 0.7f;
    public float desktopHorizontalFraction = -0.7f;
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

    private Image _GetIcon(PillColor pillColor)
    {
        switch (pillColor)
        {
            case PillColor.Green: return greenPillIcon;
            case PillColor.Red:   return redPillIcon;
            case PillColor.Blue:  return bluePillIcon;
            case PillColor.Brown: return brownPillIcon;
            default:
                Debug.LogError($"Unknown KeyType: {pillColor}");
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

        playerInventory = playerObjects[0].GetComponent<PlayerInventory>();
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
            int typeCount = (int)PillColor.LastEnum;
            for (int i = 0; i < typeCount; i++)
            {
                PillColor kt = (PillColor)i;
                Image icon = _GetIcon(kt);
                if (!icon)
                {
                    Debug.LogError($"Pill icon for {kt} is not assigned in the inspector.");
                    continue;
                }

                if (playerInventory.HasPill(kt))
                {
                    icon.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            Debug.LogError("No playerInventory.");
        }

        if (localPlayer != null && localPlayer.IsValid())
        {
            VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

            canvas.transform.position = headData.position + headData.rotation * UIOffset;
            canvas.transform.rotation = headData.rotation;

            // On desktop, nudge the pills so they hold a fixed screen corner at any window
            // shape. The other screens on the canvas are untouched.
            if (!localPlayer.IsUserInVR())
            {
                PinPillsToCorner(headData);
            }
        }
    }

    // Slides the pill row to a fixed corner of the screen on desktop, whatever size or shape the
    // window is.
    //
    // The HUD is a canvas floating in the world, not a flat overlay glued to the screen. It has
    // VRChat doesn't draw overlay canvases inside a headset. The canvas sits a
    // short distance in front of the head, and the pills are one corner of it.
    //
    // The trouble is that the same point in front of you lands in a different spot on screen
    // depending on the camera's field of view. On desktop the up-down field of view stays put,
    // but the left-right one gets wider as you widen the window. So the pills' fixed spot on the
    // canvas slides around on screen, fine in one window shape, off the edge in another. (A
    // headset never changes its field of view, so in VR we don't touch any of this.)
    //
    // To undo that you need to know how the camera turns a 3D point into a screen position. For a
    // point at (x, y, z) in front of the head it works out to (0 = middle, +-1 = edge):
    //     across the screen: (x / z) / (tan(fov/2) * aspect)
    //     up the screen:     (y / z) /  tan(fov/2)
    // The left-right one is divided by the aspect ratio and the up-down one isn't, that one
    // difference is the whole problem. We want to pick the screen spot ourselves, so we run
    // those backwards to get the point in front of the head that lands there:
    //     x = (spot across) * z * tan(fov/2) * aspect
    //     y = (spot up)      * z * tan(fov/2)
    // desktopHorizontalFraction and desktopVerticalFraction are those two screen spots. The extra
    // "* aspect" on x cancels out the aspect the camera divides back in, so the pills hold still
    // as the window stretches. Then we just park the pill container on that point directly.
    private void PinPillsToCorner(VRCPlayerApi.TrackingData headData)
    {
        // The desktop camera's up-down field of view (in degrees) and its width-to-height ratio.
        // VRCCameraSettings is how you read these in Udon — Unity's own Screen and Camera classes
        // aren't allowed in here.
        float fov = VRCCameraSettings.ScreenCamera.FieldOfView;
        float aspect = VRCCameraSettings.ScreenCamera.Aspect;
        float z = UIOffset.z;                                    // distance out in front of the head
        float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);   // tan of half the up-down field of view

        // The screen spot we want, turned back into a point in front of the head.
        float x = desktopHorizontalFraction * z * tanV * aspect;
        float y = desktopVerticalFraction * z * tanV;

        // Move only the pill container. greenPillIcon's parent IS that container — we get it this
        // way rather than by child index because the caught/end screens are also children of the
        // canvas and the child order isn't something we want to depend on.
        Transform pills = greenPillIcon.transform.parent;
        pills.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    public void ResetState()
    {
        int typeCount = (int)PillColor.LastEnum;
        for (int i = 0; i < typeCount; i++)
        {
            Image icon = _GetIcon((PillColor)i);
            if (icon != null)
                icon.gameObject.SetActive(false);
        }
    }
}
