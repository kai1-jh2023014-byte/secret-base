namespace SecretBase.Core.Themes;

/// <summary>Named accents. Status colors stay independent so warnings/errors never tint with fashion.</summary>
public static class AccentPalette
{
    public const string Jade = "Jade";
    public const string Blue = "Blue";
    public const string Purple = "Purple";
    public const string Green = "Green";
    public const string Orange = "Orange";
    public const string Pink = "Pink";
    public const string Red = "Red";
    public const string Cyan = "Cyan";
    public const string Custom = "Custom";

    public static IReadOnlyList<string> Names { get; } =
        [Jade, Blue, Purple, Green, Orange, Pink, Red, Cyan, Custom];

    public static IReadOnlyDictionary<string, string> Colors { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Jade] = "#FF7A9E86",
            [Blue] = "#FF6C8CFF",
            [Purple] = "#FF9B7ED9",
            [Green] = "#FF6B9F7A",
            [Orange] = "#FFC4894A",
            [Pink] = "#FFC9849A",
            [Red] = "#FFC47A72",
            [Cyan] = "#FF5AAFBF"
        };

    public static bool TryGet(string? name, out string hex)
    {
        hex = Colors[Jade];
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, Custom, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Colors.TryGetValue(name.Trim(), out hex!);
    }

    public static string Secondary(string accentHex)
    {
        if (!ThemeColor.TryParse(accentHex, out var a, out var r, out var g, out var b))
        {
            return "#FF5E7A68";
        }

        r = (byte)Math.Clamp((int)(r * 0.78), 0, 255);
        g = (byte)Math.Clamp((int)(g * 0.78), 0, 255);
        b = (byte)Math.Clamp((int)(b * 0.78), 0, 255);
        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    public static bool HexMatchesName(string? name, string? hex)
    {
        if (string.Equals(name, Custom, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryGet(name, out var named) && string.Equals(named, hex?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string MatchName(string? accentHex)
    {
        if (string.IsNullOrWhiteSpace(accentHex))
        {
            return Jade;
        }

        foreach (var pair in Colors)
        {
            if (string.Equals(pair.Value, accentHex.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return Custom;
    }
}
