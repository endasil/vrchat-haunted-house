using Assets._3DStealthGame.Scripts;

using UnityEngine;

public class PlayerInvenory : Resettable
{
    [SerializeField] int greenPills;
    [SerializeField] int redPills;
    [SerializeField] int bluePills;
    [SerializeField] int brownPills;

    private int _GetCount(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Green: return greenPills;
            case KeyType.Red: return redPills;
            case KeyType.Blue: return bluePills;
            case KeyType.Brown: return brownPills;
            default:
                Debug.LogError($"Unknown KeyType: {keyType}");
                return 0;
        }
    }


    public bool HasPill(KeyType keyType)
    {
        return _GetCount(keyType) > 0;
    }

    public void AddKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Green: ++greenPills; break;
            case KeyType.Red: ++redPills; break;
            case KeyType.Blue: ++bluePills; break;
            case KeyType.Brown: ++brownPills; break;
            default:
                Debug.LogError($"Unknown KeyType: {keyType}");
                break;
        }
    }

    public int GetKeyCount(KeyType keyType)
    {
        return _GetCount(keyType);
    }

    public void ResetState()
    {
        greenPills = 0;
        redPills = 0;
        bluePills = 0;
        brownPills = 0;
    }
}
