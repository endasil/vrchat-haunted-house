public static class RecordCorrections
{
    private const string PenguinName = "Penguin_Fish27";
    private const float PenguinCheatedFrom = 55f;
    private const float PenguinCheatedUntil = 56f;
    private const float PenguinLegitTime = 75.62f;

    public static float Correct(string displayName, float time)
    {
        if (displayName == PenguinName && time >= PenguinCheatedFrom && time < PenguinCheatedUntil)
        {
            return PenguinLegitTime;
        }
        return time;
    }
}
