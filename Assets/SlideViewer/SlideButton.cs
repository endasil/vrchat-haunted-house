using Assets._3DStealthGame.Scripts;
using UdonSharp;

using UnityEngine;
using VRC.SDKBase;

public enum SlideButtonAction
{
    Previous,
    Next,
    First,
    Refresh,
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SlideButton : UdonSharpBehaviour
{
    [Tooltip("The slide player that the button should control.")]
    public SlidePlayer player;

    [Tooltip("Action for the button.")]
    public SlideButtonAction action = SlideButtonAction.Next;

    [Tooltip("Only players on this list can use the button.")]
    public AdminList adminList;

    private bool _isAdmin;

    // Dims the button during the cooldown. VRChat allows a new image download every 5 seconds.
    private Renderer _buttonRenderer;

    private const float CooldownBrightness = 0.35f;

    private Color _readyColor;

    void Start()
    {
        _buttonRenderer = GetComponent<Renderer>();

        if (_buttonRenderer == null)
        {
            ReportError("No renderer on " + gameObject.name);
            return;
        }

        _readyColor = _buttonRenderer.material.color;

        if (adminList == null)
        {
            ReportError("adminList needs to be assigned in the inspector on " + gameObject.name);
            DisableInteractive = true;
            return;
        }

        _isAdmin = adminList.IsAdmin(Networking.LocalPlayer);
        DisableInteractive = !_isAdmin;
    }

    public override void Interact()
    {
        if (!_isAdmin) return;

        if (player == null)
        {
            Debug.LogError("[SlideButton] No slide player assigned on " + gameObject.name);
            return;
        }

        if (action == SlideButtonAction.Next)
        {
            player._Next();
        }
        else if (action == SlideButtonAction.First)
        {
            player._First();
        }
        else if (action == SlideButtonAction.Refresh)
        {
            player._Refresh();
        }
        else
        {
            player._Prev();
        }
    }

    private void ReportError(string message)
    {
        if (player != null)
        {
            player.LogButtonError("Button: " + message);
            return;
        }

        Debug.LogError("[SlideButton] " + message);
    }

    public void SetAvailable(bool available)
    {
        DisableInteractive = !available || !_isAdmin;

        if (available)
        {
            _buttonRenderer.material.color = _readyColor;
        }
        else
        {
            _buttonRenderer.material.color = new Color(
                _readyColor.r * CooldownBrightness,
                _readyColor.g * CooldownBrightness,
                _readyColor.b * CooldownBrightness,
                _readyColor.a);
        }
    }
}
