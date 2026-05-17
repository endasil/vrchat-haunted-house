using Assets._3DStealthGame.Scripts;

public class PlayerMovementU : Resettable
{
    private int[] keyCounts = new int[(int)KeyType.LastEnum];

    public override void Start()
    {
        base.Start();
    }

    public bool HasPill(KeyType keyType)
    {
        return keyCounts[(int)keyType] > 0;
    }
    public void AddKey(KeyType keyType)
    {
        keyCounts[(int)keyType]++;
    }

    public int GetKeyCount(KeyType keyType)
    {
        return keyCounts[(int)keyType];
    }

    public bool UseKey(KeyType keyType)
    {
        int i = (int)keyType;
        if (keyCounts[i] > 0)
        {
            keyCounts[i]--;
            return true;
        }
        return false;
    }

    public string GetKeyCountsString()
    {
        string result = "";
        for (int i = 0; i < keyCounts.Length; i++)
        {
            if (i > 0) result += " ";
            result += KeyTypeHelper.GetName((KeyType)i) + ": " + keyCounts[i];
        }
        return result;
    }

    public void ResetState()
    {
        for (int i = 0; i < keyCounts.Length; i++)
        {
            keyCounts[i] = 0;
        }
    }
}

