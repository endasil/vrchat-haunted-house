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

    
    // FitScreenPanels sizes them at runtime on both platforms, so whatever size they
    // have in the scene doesn't matter. On desktop that happens whenever the window's
    // shape changes; in VR the numbers never change, so only happens once at startup there.
    public RectTransform blackDeathBackground;
    public RectTransform endScreen;
    // Where to put the HUD canvas relative to the head (x = right, y = up, z = forward, in
    // metres). The z is also how far out the pills, timer, FPS text and the caught/end panels
    // sit, since they all get placed from the head themselves. See the long note in
    // PinPillsToCorner.
    public Vector3 UIOffset = new Vector3(0.3f, 0.2f, 0.5f);

    // Where the pills sit in view. 0 is the middle, +-1 is the edge, and the sign picks which
    // side or corner. They stay in that spot no matter how the window is sized or shaped. In VR
    // the edge these count against is vrHudFieldOfView, not the window's.
    public float desktopVerticalFraction = 0.7f;
    public float desktopHorizontalFraction = -0.7f;

    // The escape-run timer text, pinned to a fixed spot the same way the pills are.
    // 0 is the middle, +-1 is the edge.
    public RectTransform escapeTimerText;
    public float escapeTimerVerticalFraction = 0.85f;
    public float escapeTimerHorizontalFraction = 0f;

    // The FPS counter text, pinned to a fixed spot the same way the escape timer is.
    public RectTransform fpsText;
    public float fpsVerticalFraction = 0.85f;
    public float fpsHorizontalFraction = 0.85f;

    // VR only. A headset doesn't report a field of view the way the desktop camera
    // does, so we say what to aim for. vrFieldOfView is how much of the view the
    // caught/end panels have to cover: raise it if the world shows at the edges.
    // vrHudFieldOfView is the narrower angle everything meant to be read is measured
    // against, so the pills, timer and FPS text sit in comfortable view rather than
    // out at the periphery, and the artwork on the caught/end panels stays a sane
    // size instead of being blown up to match the cover. Around 60 puts all of it at
    // desktop size.
    public float vrFieldOfView = 130f;
    public float vrHudFieldOfView = 60f;

    // VR only: how far in front of the head the caught/end panels sit, in metres.
    // The pills stay up at UIOffset.z, but the panels can't. Two reasons, both about
    // the eyes being about 3 cm either side of the point the panel is centred on:
    // up close each eye looks at the panel off-axis and the far edge slides out of
    // its view, and nobody can focus on text 10 cm away. A metre out both go away.
    // Nothing can hide the panels at that distance because their materials draw on
    // top of everything (ZTest Always).
    public float vrPanelDistance = 1.4f;

    // VR only: shifts the HUD up or down, in the same units as the fraction fields
    // above (negative is down). In a headset "straight ahead" is wherever the head is
    // pointing, which sits higher than where you actually look, so the fractions that
    // are right on desktop end up reading too high. The pills get their own value
    // because they sit near the top of the view, where being too high bites hardest.
    public float vrVerticalOffset = -0.15f;
    public float vrPillVerticalOffset = -0.3f;

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

        // The two numbers the placing and sizing below is all built on. On desktop
        // they come from the camera and move with the window. In VR the headset
        // doesn't hand them out, so we use what the inspector says, and a square
        // view (aspect 1) so the panels cover left-right as much as up-down.
        float fov;
        float aspect;
        float hudFov;
        float panelDistance;
        float vertOffset;
        float pillVertOffset;
        if (localPlayer.IsUserInVR())
        {
            fov = vrFieldOfView;
            aspect = 1f;
            hudFov = vrHudFieldOfView;
            panelDistance = vrPanelDistance;
            vertOffset = vrVerticalOffset;
            pillVertOffset = vrPillVerticalOffset;
        }
        else
        {
            fov = VRCCameraSettings.ScreenCamera.FieldOfView;
            aspect = VRCCameraSettings.ScreenCamera.Aspect;
            hudFov = fov;
            panelDistance = UIOffset.z;
            vertOffset = 0f;
            pillVertOffset = 0f;
        }

        // Half the screen's height in metres, measured one metre ahead of the head.
        // What you can see spreads out from your eye like a torch beam, in straight
        // lines, so twice as far out means twice as tall: multiply by how far out
        // something sits to get the half-height there.
        float halfViewHeightOneMetreAhead = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float hudHalfViewHeightOneMetreAhead = Mathf.Tan(0.5f * hudFov * Mathf.Deg2Rad);

        // Nudge the pills, timer and FPS text so they hold a fixed spot in view at
        // any window shape. These follow the head, so they run every frame.
        PinPillsToCorner(headData, hudHalfViewHeightOneMetreAhead, aspect, pillVertOffset);
        PinEscapeTimer(headData, hudHalfViewHeightOneMetreAhead, aspect, vertOffset);
        PinFps(headData, hudHalfViewHeightOneMetreAhead, aspect, vertOffset);

        // Update the size of the black death background and depending on field of view and 
        // aspect ratio. Will be called once at start and then if the screen is resized.
        if (Mathf.Abs(fov - _lastFov) > 0.001f || Mathf.Abs(aspect - _lastAspect) > 0.0001f)
        {
            Debug.Log("FitScreenPanels");
            _lastFov = fov;
            _lastAspect = aspect;
            FitScreenPanels(halfViewHeightOneMetreAhead, hudHalfViewHeightOneMetreAhead, aspect, panelDistance, vertOffset);
        }

    }

    // Puts the pill row in a fixed corner of the view, whatever shape the window is.
    //
    // VRChat can't draw screen overlays in a headset, so we can't just use a screen space
    // canvas painted flat on the screen, instead it's treated like a 3D canvas 
    // hanging in front of the players head in 3D making stuff messier.
    // How much of the world fits on screen changes with the window shape. 
    // A pill placed at a fixed spot in front of you would sit in the corner in one
    // window size and slide off the edge in another. 
    //
    // So instead of a fixed spot in the world, we pick the spot on screen we want
    // (desktopHorizontalFraction and desktopVerticalFraction, where 0 is the middle
    // and 1 is the edge) and work out where in front of the head that lands right now:
    //
    //     x = across * z * halfViewHeightOneMetreAhead * aspect     // sideways
    //     y = up     * z * halfViewHeightOneMetreAhead              // up and down
    //
    // z is how far out in front the canvas is, so z * halfViewHeightOneMetreAhead is half
    // the screen's height in metres at that distance, and the fraction picks how far up
    // that half to go. aspect is the width-to-height of the window. Multiplying x by aspect
    // is what keeps the pills still when the window stretches: the wider the window, the
    // further out we push them, which exactly matches the extra width you can see.
    //
    // VR uses vrHudFieldOfView for the angle instead of the desktop camera, but the
    // same sums run there.
    private void PinPillsToCorner(VRCPlayerApi.TrackingData headData, float halfViewHeightOneMetreAhead, float aspect, float vertOffset)
    {
        float z = UIOffset.z;                                    // distance out in front of the head

        // The screen spot we want, turned back into a point in front of the head.
        float x = desktopHorizontalFraction * z * halfViewHeightOneMetreAhead * aspect;
        float y = (desktopVerticalFraction + vertOffset) * z * halfViewHeightOneMetreAhead;

        // Move only the pill container. The first icon's parent IS that container, and we get it
        // this way rather than by child index because the caught/end screens are also children
        // of the canvas and the child order isn't something we want to depend on.
        Transform pills = pillIcons[0].transform.parent;
        pills.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Pins the escape timer to a fixed spot in view, same trick as the pills.
    // See the long note in PinPillsToCorner.
    private void PinEscapeTimer(VRCPlayerApi.TrackingData headData, float halfViewHeightOneMetreAhead, float aspect, float vertOffset)
    {
        if (escapeTimerText == null) return;

        float z = UIOffset.z;

        float x = escapeTimerHorizontalFraction * z * halfViewHeightOneMetreAhead * aspect;
        float y = (escapeTimerVerticalFraction + vertOffset) * z * halfViewHeightOneMetreAhead;

        escapeTimerText.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Pins the FPS counter to a fixed spot in view, same trick as the pills.
    // See the long note in PinPillsToCorner.
    private void PinFps(VRCPlayerApi.TrackingData headData, float halfViewHeightOneMetreAhead, float aspect, float vertOffset)
    {
        if (fpsText == null) return;

        float z = UIOffset.z;

        float x = fpsHorizontalFraction * z * halfViewHeightOneMetreAhead * aspect;
        float y = (fpsVerticalFraction + vertOffset) * z * halfViewHeightOneMetreAhead;

        fpsText.position = headData.position + headData.rotation * new Vector3(x, y, z);
    }

    // Sizes the caught/end panels to fill the view. Same maths as PinPillsToCorner,
    // but applied to a whole rectangle instead of one point: at a given distance the
    // half-height is the distance times halfViewHeightOneMetreAhead, so double that
    // for the full height, and multiply by the aspect for the width.
    //
    // Two separate jobs here, and they pull apart in VR. The panel's scale decides
    // how big the artwork on it comes out. The panel's rect size decides how much of
    // the view the black cover reaches. On desktop both want the same number. In a
    // headset the cover has to reach across a much wider view than the artwork wants
    // to fill, so the scale comes from the text half-height and the rect is then
    // stretched out to whatever the full half-height asks for.
    //
    // Stretching the rect rather than the scale also keeps the scale uniform, so
    // nothing is squashed. Children with fractional anchors follow the rect on their
    // own.
    private void FitScreenPanels(float halfViewHeightOneMetreAhead, float textHalfViewHeightOneMetreAhead, float aspect, float distance, float vertOffset)
    {
        RectTransform canvasRect = (RectTransform)canvas.transform;
        Rect cr = canvasRect.rect;

        // 5% extra so the panel's edges never peek into view.
        float textWorldHeight = 2f * distance * textHalfViewHeightOneMetreAhead * 1.05f;

        // Uniform scale that sets how big the text reads. Because it's worked out
        // from an angle at the panel's own distance, moving the panel further out
        // doesn't change how big the text looks.
        float scale = textWorldHeight / (cr.height * canvasRect.localScale.y);
        // How much taller than the text's own height the cover has to be.
        float coverHeight = cr.height * halfViewHeightOneMetreAhead / textHalfViewHeightOneMetreAhead;

        // These panels stretch to fill the canvas, and sizeDelta is added on top of
        // that: the panel ends up the canvas's size plus whatever we put here. So to
        // land on the size we want, we subtract the canvas's own width and height.
        Vector2 sizeDelta = new Vector2(coverHeight * aspect - cr.width,
                                        coverHeight - cr.height);
        // Put the panel ahead of the head at its own distance, whatever offset the
        // canvas itself carries, then apply the same up-down nudge the rest of the
        // HUD gets. (UIOffset.x/y are 0 in this scene, but don't assume that stays
        // true.)
        float vertNudge = vertOffset * distance * textHalfViewHeightOneMetreAhead;
        Vector3 centre = new Vector3(-UIOffset.x / canvasRect.localScale.x,
                                     (vertNudge - UIOffset.y) / canvasRect.localScale.y,
                                     (distance - UIOffset.z) / canvasRect.localScale.z);

        FitOnePanel(blackDeathBackground, scale, sizeDelta, centre);
        FitOnePanel(endScreen, scale, sizeDelta, centre);
    }

    private void FitOnePanel(RectTransform panel, float scale, Vector2 sizeDelta, Vector3 centre)
    {
        if (panel == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): caught/end screen panel is not wired up");
            return;
        }
        panel.localScale = new Vector3(scale, scale, scale);
        panel.sizeDelta = sizeDelta;
        panel.anchoredPosition = new Vector2(centre.x, centre.y);

        // anchoredPosition only writes x and y, so push it out to its distance here.
        Vector3 local = panel.localPosition;
        local.z = centre.z;
        panel.localPosition = local;
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
