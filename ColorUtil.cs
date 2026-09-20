namespace Glyphore;

internal static class ColorUtil
{
    internal static Color ParseHtmlOrWhite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Color.White;

        var text = value.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if (text.Length == 3)
            text = string.Concat(text.Select(c => $"{c}{c}"));
        if (text.Length == 6 &&
            byte.TryParse(text[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(text[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            return Color.FromArgb(r, g, b);
        return Color.White;
    }

    internal static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
