// Turns a number of seconds into mm:ss.hh.

public static class TimeFormatter
{
    // 99:59.99, the slowest run that still fits two digits of minutes.
    private const int MaxHundredths = 599999;

    // The running clock, which is free to grow past two digits of minutes.
    public static string Format(float seconds)
    {
        return FormatHundredths((int)(seconds * 100f));
    }

    // For a fixed-width column, where a third digit of minutes would make one
    // row wider than the rest. Anything slower reads as the highest time that
    // still fits. Callers keep the real value for sorting and saving.
    public static string FormatCapped(float seconds)
    {
        int totalHundredths = (int)(seconds * 100f);
        if (totalHundredths > MaxHundredths)
        {
            totalHundredths = MaxHundredths;
        }
        return FormatHundredths(totalHundredths);
    }

    private static string FormatHundredths(int totalHundredths)
    {
        int minutes = totalHundredths / 6000;
        int secs = (totalHundredths / 100) % 60;
        int hundredths = totalHundredths % 100;
        return TwoDigits(minutes) + ":" + TwoDigits(secs) + "." + TwoDigits(hundredths);
    }

    // Udon has no ToString("D2").
    private static string TwoDigits(int value)
    {
        if (value < 10) return "0" + value;
        return "" + value;
    }
}
