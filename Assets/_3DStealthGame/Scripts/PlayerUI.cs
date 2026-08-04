#pragma warning disable IDE1006 // Naming Styles
using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDKBase;
using VRC.SDK3.Rendering;
using Assets._3DStealthGame.Scripts;
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerUI : Resettable
{
    public PlayerInventory playerInventory;
    public Snowball snowball;
    public VRC_Pickup snowballPickup;
    public Canvas canvas;

    // One icon per PillColor, indexed by (int)PillColor. An icon is switched on
    // when the player holds at least one pill of that color.
    public Image[] pillIcons;

    // The full-screen caught/end panels on this canvas. On desktop, FitScreenPanels
    // sizes them to exactly match the camera's view whenever the window's shape
    // changes, so they behave like a screen overlay at any window size or shape. In
    // VR the values saved in the scene are used as-is (a headset's field of view
    // never changes).
    public RectTransform caughtScreen;
    public RectTransform endScreen;
    // Where to put the HUD canvas relative to the head (x = right, y = up, z = forward, in
    // metres). In VR we use this as-is. On desktop we only keep the z (how far in front) and
    // work out x and y ourselves each frame in GetDesktopOffset. See the long note there.
    public Vector3 UIOffset = new Vector3(0.3f, 0.2f, 0.5f);

    // Desktop only: where the pills sit on the screen. 0 is the middle, +-1 is the edge, and
    // the sign picks which side or corner. They stay in that spot no matter how the window is
    // sized or shaped, just set both in the inspector.
    public float desktopVerticalFraction = 0.7f;
    public float desktopHorizontalFraction = -0.7f;

    // The escape-run timer text. On desktop it's pinned to a fixed screen spot
    // the same way the pills are, because only a small strip of the canvas is on
    // screen. 0 is the middle, +-1 is the edge.
    public RectTransform escapeTimerText;
    public float escapeTimerVerticalFraction = 0.85f;
    public float escapeTimerHorizontalFraction = 0f;

    // The FPS counter text, pinned to a fixed screen spot on desktop the same
    // way the escape timer is.
    public RectTransform fpsText;
    public float fpsVerticalFraction = 0.85f;
    public float fpsHorizontalFraction = 0.85f;
    private VRCPlayerApi localPlayer;
    private Collider snowballCollider;

    // Field of view and aspect the caught/end panels were last sized for. -1 means
    // they have never been sized, so the first frame always sizes them.
    private float _lastFov = -1f;
    private float _lastAspect = -1f;

    public override void Start()
    {
        base.Start();
        localPlayer = Networking.LocalPlayer;

        if (snowball)
        {
            snowballCollider = snowball.GetComponent<Collider>();
            snowballPickup = snowball.GetComponent<VRC_Pickup>();
        }

        // Validate the icon wiring once here instead of logging every frame.
        int typeCount = (int)PillColor.LastEnum;
        if (pillIcons == null || pillIcons.Length != typeCount)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): pillIcons must have {typeCount} entries (one per PillColor), found {(pillIcons == null ? 0 : pillIcons.Length)}.");
        }
        else
        {
            for (int i = 0; i < typeCount; i++)
                if (pillIcons[i] == null)
                    Debug.LogError($"PlayerUI ({gameObject.name}): pill icon for {(PillColor)i} is not assigned in the inspector.");
        }

        
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        GameObject[] playerObjects = player.GetPlayerObjects();
        if (playerObjects == null || playerObjects.Length == 0) return;

        playerInventory = playerObjects[0].GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): local player object has no PlayerInventory component.");
            return;
        }

        // The inventory calls back into ShowPillIcon when a pill is picked up, so
        // the icons don't have to be polled every frame.
        playerInventory.SetPlayerUI(this);
        RefreshPillIcons();
    }

    // Switches on the icon for one pill color. Called by PlayerInventory.AddPill.
    public void ShowPillIcon(PillColor pillColor)
    {
        Image icon = pillIcons[(int)pillColor];
        if (icon == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): pill icon for {pillColor} is not assigned in the inspector.");
            return;
        }

        icon.gameObject.SetActive(true);
    }

    // Matches every icon to what the inventory already holds. Only needed when the
    // UI first hooks up to the inventory; after that AddPill keeps them in step.
    private void RefreshPillIcons()
    {
        int typeCount = (int)PillColor.LastEnum;
        for (int i = 0; i < typeCount; i++)
        {
            if (playerInventory.HasPill((PillColor)i))
                ShowPillIcon((PillColor)i);
        }
    }

    private void LateUpdate()
    {
        if (localPlayer == null || !localPlayer.IsValid()) return;

        VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        canvas.transform.position = headData.position + headData.rotation * UIOffset;
        canvas.transform.rotation = headData.rotation;

        // A headset's field of view never changes, so everything below is desktop only.
        if (localPlayer.IsUserInVR()) return;

        // Read the camera once and share it: all four helpers below need the same
        // two numbers and the same tan.
        float fov = VRCCameraSettings.ScreenCamera.FieldOfView;
        float aspect = VRCCameraSettings.ScreenCamera.Aspect;
        float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);

        // Nudge the pills, timer and FPS text so they hold a fixed screen corner at
        // any window shape. These follow the head, so they run every frame.
        PinPillsToCorner(headData, tanV, aspect);
        PinEscapeTimer(headData, tanV, aspect);
        PinFps(headData, tanV, aspect);

        // The panels sit dead ahead, so the canvas follow above already carries them
        // with the head. Their size depends only on the camera's shape, and resizing
        // them dirties the canvas, so only do it when the window shape actually moves.
        if (Mathf.Abs(fov - _lastFov) > 0.001f || Mathf.Abs(aspect - _lastAspect) > 0.0001f)
        {
            _lastFov = fov;
            _lastAspect = aspect;
            FitScreenPanels(tanV, aspect);
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
    private void PinPillsToCorner(VRCPlayerApi.TrackingData headData, float tanV, float aspect)
    {
        float z = UIOffset.z;                                    // distance out in front of the head

        // The screen spot we want, turned back into a point in front of the head.
        float x = desktopHorizontalFraction * z * tanV * aspect;
        float y = desktopVerticalFraction * z * tanV;

        // Move only the pill container. The first icon's parent IS that container, and we get it
        // this way rather than by child index because the caught/end screens are also children
        // of the canvas and the child order isn't something we want to depend on.
        Transform pills = pillIcons[0].transform.parent;
        pills.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Pins the escape timer to a fixed screen spot on desktop, same trick as the
    // pills. See the long note in PinPillsToCorner.
    private void PinEscapeTimer(VRCPlayerApi.TrackingData headData, float tanV, float aspect)
    {
        if (escapeTimerText == null) return;

        float z = UIOffset.z;

        float x = escapeTimerHorizontalFraction * z * tanV * aspect;
        float y = escapeTimerVerticalFraction * z * tanV;

        escapeTimerText.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Pins the FPS counter to a fixed screen spot on desktop, same trick as the
    // pills. See the long note in PinPillsToCorner.
    private void PinFps(VRCPlayerApi.TrackingData headData, float tanV, float aspect)
    {
        if (fpsText == null) return;

        float z = UIOffset.z;

        float x = fpsHorizontalFraction * z * tanV * aspect;
        float y = fpsVerticalFraction * z * tanV;

        fpsText.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Sizes the caught/end panels to fill the camera's view on desktop. Same maths
    // as PinPillsToCorner, but applied to a whole rectangle instead of one point:
    // at distance z the camera sees a rectangle 2 * z * tan(fov/2) tall, and that
    // times the aspect ratio wide.
    //
    // The panel always ends up filling the screen, no matter how big the canvas
    // rect is, because the scale and the rect size cancel out. A child with a fixed
    // font size doesn't get that cancellation, so its size relative to the panel
    // depends on the canvas rect's height. The width is matched by widening the
    // rect (sizeDelta) rather than scaling, so the scale stays uniform and text
    // isn't stretched. Children with fractional anchors follow along automatically.
    private void FitScreenPanels(float tanV, float aspect)
    {
        // 5% extra so the panel's edges never peek into view.
        float worldHeight = 2f * UIOffset.z * tanV * 1.05f;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        Rect cr = canvasRect.rect;

        // Uniform scale that makes the panel exactly the visible height.
        float scale = worldHeight / (cr.height * canvasRect.localScale.y);
        // Widen (or narrow) the rect so width/height matches the window's shape.
        float widthDelta = cr.height * aspect - cr.width;
        // Cancel the canvas's off-centre offset so the panel sits dead ahead.
        // (UIOffset.x/y are 0 in this scene, but don't assume that stays true.)
        Vector2 centre = new Vector2(-UIOffset.x / canvasRect.localScale.x,
                                     -UIOffset.y / canvasRect.localScale.y);

        FitOnePanel(caughtScreen, scale, widthDelta, centre);
        FitOnePanel(endScreen, scale, widthDelta, centre);
    }

    private void FitOnePanel(RectTransform panel, float scale, float widthDelta, Vector2 centre)
    {
        if (panel == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): caught/end screen panel is not wired up");
            return;
        }
        panel.localScale = new Vector3(scale, scale, scale);
        panel.sizeDelta = new Vector2(widthDelta, 0f);
        panel.anchoredPosition = centre;
    }

    public override void ResetState()
    {
        int typeCount = (int)PillColor.LastEnum;
        for (int i = 0; i < typeCount; i++)
        {
            Image icon = pillIcons[i];
            if (icon != null)
                icon.gameObject.SetActive(false);
        }
    }
}
