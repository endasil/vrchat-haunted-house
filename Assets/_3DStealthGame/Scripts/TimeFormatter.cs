// Turns a number of seconds into text such as 4m 32.17s.

public static class TimeFormatter
{
    // 99m 59.99s, limit max time to cap nr of digits to two.
    private const int MaxHundredths = 599999;

	// Used to format time as text like 2m 08.10s
    public static string Format(float seconds)
    {
        return FormatHundredths((int)(seconds * 100f));
    }

 
	// Format the text to limit the number of digits to
	// two for minutes. So a time > 99m 59.99s
	// turns into 99m 59.99s
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
        return minutes + "m " + TwoDigits(secs) + "." + TwoDigits(hundredths) + "s";
    }

    // Udon has no ToString("D2") to make sure single
	//  nrs are written as 01 instead of 1. .
    private static string TwoDigits(int value)
    {
        if (value < 10) return "0" + value;
        return "" + value;
    }
}
