using Assets._3DStealthGame.Scripts;

public class PlayerInventory : Resettable
{
    // One count per PillColor, indexed by (int)PillColor.
    private int[] _pillCounts = new int[(int)PillColor.LastEnum];

    public bool HasPill(PillColor pillColor)
    {
        return GetPillCount(pillColor) > 0;
    }

    public void AddPill(PillColor pillColor)
    {
        _pillCounts[(int)pillColor]++;
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
