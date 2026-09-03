namespace SecretBase.Core.Themes;

/// <summary>Hex color helpers shared by Core (no WinUI/Avalonia types).</summary>
public static class ThemeColor
{
    public static bool TryParse(string? hex, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255;
        r = 18;
        g = 22;
        b = 30;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8)
        {
            return false;
        }

        try
        {
            a = Convert.ToByte(value[..2], 16);
            r = Convert.ToByte(value[2..4], 16);
            g = Convert.ToByte(value[4..6], 16);
            b = Convert.ToByte(value[6..8], 16);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static double RelativeLuminance(string? hex)
    {
        if (!TryParse(hex, out _, out var r, out var g, out var b))
        {
            return 0.1;
        }

        return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
    }

    public static bool IsLight(string? hex) => RelativeLuminance(hex) >= 0.45;

    /// <summary>Readable foreground for a filled accent surface. Picks the better of dark/light.</summary>
    public static string ContrastOn(string? fillHex, string dark = "#FF141820", string light = "#FFF6F3EC") =>
        ContrastRatio(dark, fillHex) >= ContrastRatio(light, fillHex) ? dark : light;

    public static double ContrastRatio(string? a, string? b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Channel(byte value)
    {
        var s = value / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
