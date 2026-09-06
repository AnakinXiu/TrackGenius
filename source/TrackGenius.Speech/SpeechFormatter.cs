using System;
using System.Globalization;
using System.Linq;

namespace TrackGenius.Speech;

/// <summary>Pure spoken-English formatting for numbers, ordinals and lap times.</summary>
public static class SpeechFormatter
{
    private static readonly string[] Ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
    private static readonly string[] Teens = { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
    private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    public static string NumberToSpokenWords(int number)
    {
        if (number is < 0 or > 999)
            return number.ToString(CultureInfo.InvariantCulture);
        if (number < 10) return Ones[number];
        if (number < 20) return Teens[number - 10];
        if (number < 100)
            return Tens[number / 10] + (number % 10 == 0 ? "" : " " + Ones[number % 10]);
        return Ones[number / 100] + " hundred" + (number % 100 == 0 ? "" : " " + NumberToSpokenWords(number % 100));
    }

    public static string Ordinal(int position) => position switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        9 => "ninth",
        10 => "tenth",
        _ => position.ToString(CultureInfo.InvariantCulture) + "th",
    };

    public static string LapTimeToSpoken(TimeSpan time)
    {
        var secondsSpoken = SecondsWithDigits(time.TotalSeconds % 60);
        if (time.TotalMinutes < 1)
            return secondsSpoken + " seconds";
        var minutes = (int)time.TotalMinutes;
        return $"{NumberToSpokenWords(minutes)} minute{(minutes == 1 ? "" : "s")} {secondsSpoken} seconds";
    }

    // "twelve point four three eight" — whole seconds as words, decimal digits as words.
    private static string SecondsWithDigits(double seconds)
    {
        var whole = (int)seconds;
        var digits = ((int)Math.Round((seconds - whole) * 1000)).ToString("000", CultureInfo.InvariantCulture);
        return $"{NumberToSpokenWords(whole)} point {string.Join(" ", digits.ToCharArray().Select(ToSpokenDigit))}";
    }

    private static string ToSpokenDigit(char digit) => Ones[digit - '0'];
}
