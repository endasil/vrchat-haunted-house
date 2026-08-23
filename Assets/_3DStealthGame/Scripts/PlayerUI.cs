#pragma warning disable IDE1006 // Naming Styles
using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDK3.Rendering;
using VRC.SDKBase;
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerUI : Resettable
{
    private const int PillTypeCount = (int)PillColor.LastEnum;
    private const float VrViewAspect = 1f;

    // One icon per PillColor, indexed by (int)PillColor. An icon is switched on
    // when the player holds a pill of that color.
    public Image[] pillIcons;

    public RectTransform blackDeathBackground;
    public RectTransform endScreen;
    // Where to the HUD canvas relative to the head (x = right, y = up, z = forward, in
    // metres).
    private readonly Vector3 UIOffset = new Vector3(0.3f, 0.2f, 0.5f);

    // Where the pills are in view. 0 is the middle, +-1 is the edge.
    // Used to keep them at their relative position when the window resizes.
    // In VR the edge these count against is vrHudFieldOfView.
    private const float pillsVerticalFraction = 0.7f;
    private const float pillsHorizontalFraction = -0.7f;

    public RectTransform escapeTimerTextRect;
    private const float escapeTimerVerticalFraction = 0.85f;
    private const float escapeTimerHorizontalFraction = 0f;

    // Have not found a way to get the FOV in VR the same as we can from the cam
    // on desktop, so it is set to a value that should comfortably cover the screen
    // for the blackout stuff.
    private const float vrFieldOfView = 130f;
    // vrHudFieldOfView the angle that is expected to be comfortable to read within
    // used when calculating placement of ui elements to make them comfortable to read.
    private const float vrHudFieldOfView = 60f;

    // VR only: how far in front of the head the HUD is.
    private const float vrPanelDistance = 1.4f;
    private const float vrTimerAndPanelVerticalOffset = -0.15f;
    private const float vrPillVerticalOffset = -0.3f;

    private PlayerInventory _playerInventory;
    private VRCPlayerApi _localPlayer;
    private RectTransform _hudRectTransform;
    private bool _isInVR;

    // Parent which all the pill icons are under.
    private Transform _pillRow;

    // Field of view and aspect the layout updated every time the window is resized.
    private float _fov = -1f;
    private float _aspect = -1f;

    // 
    private float HudFov => _isInVR ? vrHudFieldOfView : _fov;
    
    private float Distance => _isInVR ? vrPanelDistance : UIOffset.z;
    private float VertOffset => _isInVR ? vrTimerAndPanelVerticalOffset : 0f;

    private float PillVertOffset => _isInVR ? vrPillVerticalOffset : 0f;

    // Where the pill row and the timer go relative to the head, in metres. Worked out
    // whenever the field of view or aspect changes, then reused every frame.
    private Vector3 _pillRowOffset;
    private Vector3 _escapeTimerOffset;

    private Vector3 _pillsBaseScale;
    private Vector3 _escapeTimerBaseScale;

    public override void Start()
    {
        base.Start();
        _localPlayer = Networking.LocalPlayer;
        _hudRectTransform = (RectTransform)transform;
        _fov = -1;
        
        ValidateInit();
        _pillRow = pillIcons[0].transform.parent;
        _pillsBaseScale = _pillRow.localScale;
        _escapeTimerBaseScale = escapeTimerTextRect.localScale;
    }

    private void ValidateInit()
    {
        if (pillIcons == null || pillIcons.Length != PillTypeCount)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): pillIcons must have {PillTypeCount} entries (one per PillColor), found {(pillIcons == null ? 0 : pillIcons.Length)}.");
        }

        for (int i = 0; i < PillTypeCount; i++)
        {
            if (pillIcons[i] == null)
            {
                Debug.LogError($"PlayerUI ({gameObject.name}): pill icon for {(PillColor)i} is not assigned in the inspector.");

            }
        }

        if (escapeTimerTextRect == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): escapeTimerText is not assigned in the inspector.");
        }

        if (blackDeathBackground == null || endScreen == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): the caught/end screen panels are not both assigned in the inspector.");
        }

    }
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        _localPlayer = player;
        _fov = -1;
        _isInVR = player.IsUserInVR();

        _playerInventory = player.GetPlayerObjects()[0].GetComponent<PlayerInventory>();
        if (_playerInventory == null)
        {
            Debug.LogError($"PlayerUI ({gameObject.name}): local player object has no PlayerInventory component.");
            return;
        }

        _playerInventory.SetPlayerUI(this);
        
        for (int i = 0; i < PillTypeCount; i++)
        {
            pillIcons[i].gameObject.SetActive(_playerInventory.HasPill((PillColor)i));
        }
    }

    // Switches on the icon for one pill color. Called by PlayerInventory.AddPill.
    public void ShowPillIcon(PillColor pillColor)
    {
        pillIcons[(int)pillColor].gameObject.SetActive(true);
    }

    
    private void LateUpdate()
    {
        if (_localPlayer == null || !_localPlayer.IsValid()) return;

        VRCPlayerApi.TrackingData headData = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        _hudRectTransform.position = headData.position + headData.rotation * UIOffset;
        _hudRectTransform.rotation = headData.rotation;

        if (UpdateViewShapeIfChanged())
        {
            RecalculateHudPositions();
        }

        // The pills and the timer follow the head, so they run every frame. Their spot
        // in view is already worked out, all that is left is to put it in front of
        // wherever the head is now.
        _pillRow.position = headData.position + headData.rotation * _pillRowOffset;
        escapeTimerTextRect.position = headData.position + headData.rotation * _escapeTimerOffset;
    }
    private bool UpdateViewShapeIfChanged()
    {
        float fov = _isInVR ? vrFieldOfView : VRCCameraSettings.ScreenCamera.FieldOfView;
        float aspect = _isInVR ? VrViewAspect : VRCCameraSettings.ScreenCamera.Aspect;

        if (Mathf.Abs(fov - _fov) <= 0.001f && Mathf.Abs(aspect - _aspect) <= 0.0001f)
        {
            return false;
        }

        _fov = fov;
        _aspect = aspect;
        return true;
    }

    // Works out everything that depends on the shape of the view: where the pills and
    // the timer go, how big they are, and the size of the caught/end panels.
    private void RecalculateHudPositions()
    {
        // Half the screen's height in metres, measured one metre ahead of the head.
        // What you can see spreads out from your eye like a torch beam, in straight
        // lines, so twice as far out means twice as tall: multiply by how far out
        // something is to get the half-height there.
        float viewHalfHeightPerMetre = Mathf.Tan(0.5f * _fov * Mathf.Deg2Rad);
        float hudHalfHeightPerMetre = Mathf.Tan(0.5f * HudFov * Mathf.Deg2Rad);

        _pillRowOffset = ScreenFractionToWorldOffset(pillsHorizontalFraction, pillsVerticalFraction + PillVertOffset, hudHalfHeightPerMetre: hudHalfHeightPerMetre);
        _escapeTimerOffset = ScreenFractionToWorldOffset(escapeTimerHorizontalFraction, escapeTimerVerticalFraction + VertOffset, hudHalfHeightPerMetre: hudHalfHeightPerMetre);

        float elementScale = Distance / UIOffset.z;
        _pillRow.localScale = _pillsBaseScale * elementScale;
        escapeTimerTextRect.localScale = _escapeTimerBaseScale * elementScale;

        FitScreenPanels(hudHalfHeightPerMetre: hudHalfHeightPerMetre, viewHalfHeightPerMetre: viewHalfHeightPerMetre);
    }

    // Converts a screen-space fraction (0 = centre, ±1 = edge) into a world offset
    // in front of the head.
    //
    // VRChat has no screen overlays in VR, so the HUD is a 3D canvas tracking the
    // head. A fixed world position would drift as the window resizes, so we derive
    // the position from the current FOV and aspect ratio instead:
    //
    //     x = horizFraction * Distance * _hudHalfHeightPerMetre * _aspect
    //     y = vertFraction  * Distance * _hudHalfHeightPerMetre
    //
    // Distance * _hudHalfHeightPerMetre gives half the visible height at that
    // depth. The fraction picks a point within it, and _aspect scales x so elements
    // stay put when the window stretches. VR substitutes vrHudFieldOfView for the
    // desktop camera FOV but the maths is the same.
    private Vector3 ScreenFractionToWorldOffset(float horizFraction, float vertFraction, float hudHalfHeightPerMetre)
    {
        float x = horizFraction * Distance * hudHalfHeightPerMetre * _aspect;
        float y = vertFraction * Distance * hudHalfHeightPerMetre;
        return new Vector3(x, y, Distance);
    }

    // Sizes the caught/end panels to fill the view. Same maths as ViewFractionToHeadOffset,
    // but applied to a whole rectangle instead of one point: at a given distance the
    // half-height is the distance times the half-height per metre, so double that for the
    // full height, and multiply by the aspect for the width.
    //
    // The panel's scale controls how big the artwork on it becomes.
    // The panel's rect size controls how much of the view is covered by black.
    // On desktop both need to be the same number. In a headset the cover has to reach
    // across a much wider view than the artwork fills, so the scale comes from the HUD
    // half-height and the rect is then stretched out to the full view half-height.
    //
    // Stretching the rect rather than the scale also keeps the scale uniform.

    private void FitScreenPanels(float hudHalfHeightPerMetre, float viewHalfHeightPerMetre)
    {
        Rect hud = _hudRectTransform.rect;

        float hudFullHeight = 2f * Distance * hudHalfHeightPerMetre;
        float textWorldHeight = hudFullHeight * 1.05f;

        // Uniform scale that sets how big the text is. Because it's worked out
        // from an angle at the panel's own distance, moving the panel further out
        // doesn't change how big the text looks.
        float scale = textWorldHeight / (hud.height * _hudRectTransform.localScale.y);
        // How much taller than the text's own height the cover has to be.
        float coverHeight = hud.height * viewHalfHeightPerMetre / hudHalfHeightPerMetre;

        // These panels stretch to fill the canvas, and sizeDelta is added on top of
        // that: the panel becomes the canvas's size plus whatever we put here. So to
        // get the size we want, we subtract the canvas's own width and height.
        Vector2 sizeDelta = new Vector2(coverHeight * _aspect - hud.width,
                                        coverHeight - hud.height);
        
        // localPosition is measured from the canvas, so subtracting UIOffset gives the
        // distance from the canvas to where we want the panel, and dividing by the
        // canvas scale turns metres into its local units.
        float vertShift = VertOffset * Distance * hudHalfHeightPerMetre;
        Vector3 centre = new Vector3(-UIOffset.x / _hudRectTransform.localScale.x,
                                     (vertShift - UIOffset.y) / _hudRectTransform.localScale.y,
                                     (Distance - UIOffset.z) / _hudRectTransform.localScale.z);

        ScaleToFillView(blackDeathBackground, scale, sizeDelta, centre);
        ScaleToFillView(endScreen, scale, sizeDelta, centre);
    }

    private void ScaleToFillView(RectTransform panel, float scale, Vector2 sizeDelta, Vector3 centre)
    {
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
        for (int i = 0; i < PillTypeCount; i++)
        {
            pillIcons[i].gameObject.SetActive(false);
        }
    }
}
