#pragma warning disable IDE1006 // Naming Styles
using Assets._3DStealthGame.Scripts;
using UnityEngine;

public class PlayerInventory : Resettable
{
    [SerializeField] int greenPills;
    [SerializeField] int redPills;
    [SerializeField] int bluePills;
    [SerializeField] int brownPills;

    private int _GetCount(PillColor pillColor)

    {
        switch (pillColor)
        {
            case PillColor.Green: return greenPills;
            case PillColor.Red: return redPills;
            case PillColor.Blue: return bluePills;
            case PillColor.Brown: return brownPills;
            default:
                Debug.LogError($"Unknown KeyType: {pillColor}");
                return 0;
        }
    }


    public bool HasPill(PillColor pillColor)
    {
        return _GetCount(pillColor) > 0;
    }

    public void AddKey(PillColor pillColor)
    {
        switch (pillColor)
        {
            case PillColor.Green: ++greenPills; break;
            case PillColor.Red: ++redPills; break;
            case PillColor.Blue: ++bluePills; break;
            case PillColor.Brown: ++brownPills; break;
            default:
                Debug.LogError($"Unknown KeyType: {pillColor}");
                break;
        }
    }

    public int GetKeyCount(PillColor pillColor)
    {
        return _GetCount(pillColor);
    }

    public void ResetState()
    {
        greenPills = 0;
        redPills = 0;
        bluePills = 0;
        brownPills = 0;
    }
}
