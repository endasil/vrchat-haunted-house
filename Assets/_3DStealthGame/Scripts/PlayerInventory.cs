using Assets._3DStealthGame.Scripts;

using UnityEngine;

public class PlayerInventory : Resettable
{
    // One count per PillColor, indexed by (int)PillColor.
    private int[] _pillCounts = new int[(int)PillColor.LastEnum];

    // The HUD, handed to us by PlayerUI once it finds the local player's inventory.
    private PlayerUI _playerUI;

    public void SetPlayerUI(PlayerUI playerUI)
    {
        _playerUI = playerUI;
    }

    public bool HasPill(PillColor pillColor)
    {
        return GetPillCount(pillColor) > 0;
    }

    public void AddPill(PillColor pillColor)
    {
        _pillCounts[(int)pillColor]++;

        if (_playerUI == null)
        {
            Debug.LogError($"PlayerInventory ({gameObject.name}): no PlayerUI is hooked up, the pill icon won't light up.");
            return;
        }

        _playerUI.ShowPillIcon(pillColor);
    }

    public int GetPillCount(PillColor pillColor)
    {
        return _pillCounts[(int)pillColor];
    }

    public override void ResetState()
    {
        for (int i = 0; i < _pillCounts.Length; i++)
            _pillCounts[i] = 0;
    }
}
