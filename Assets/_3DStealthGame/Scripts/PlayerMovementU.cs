using Assets._3DStealthGame.Scripts;

using UdonSharp;

public class PlayerMovementU : Resettable
{
    private int[] keyCounts = new int[(int)KeyType.LastEnum];

    public override void Start()
    {
        base.Start();
    }

    public void AddKey(KeyType keyType)
    {
        keyCounts[(int)keyType]++;
    }

    public int GetKeyCount(KeyType keyType)
    {
        return keyCounts[(int)keyType];
    }

    public void RemoveKey(KeyType keyType)
    {
        int i = (int)keyType;
        if (keyCounts[i] > 0)
            keyCounts[i]--;
    }

    public void ResetState()
    {
        for (int i = 0; i < keyCounts.Length; i++)
        {
            keyCounts[i] = 0;
        }
    }
}

