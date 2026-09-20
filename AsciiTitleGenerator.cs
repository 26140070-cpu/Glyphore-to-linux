using Figgle;
using Figgle.Fonts;

namespace Glyphore;

internal static class AsciiTitleGenerator
{
    public static readonly string[] Fonts = ["Standard", "Slant", "Big", "Block", "Doom", "Small", "Mini", "Banner"];

    public static string Generate(string? text, string? font)
    {
        var source = string.IsNullOrWhiteSpace(text) ? " " : text;
        return font switch
        {
            "Slant" => FiggleFonts.Slant.Render(source),
            "Big" => FiggleFonts.Big.Render(source),
            "Block" => FiggleFonts.Block.Render(source),
            "Doom" => FiggleFonts.Doom.Render(source),
            "Small" => FiggleFonts.Small.Render(source),
            "Mini" => FiggleFonts.Mini.Render(source),
            "Banner" => FiggleFonts.Banner.Render(source),
            _ => FiggleFonts.Standard.Render(source)
        };
    }
}
