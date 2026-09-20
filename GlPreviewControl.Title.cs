using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace Glyphore;

internal sealed partial class GlPreviewControl
{
    private unsafe void BindLayerTitleTexture(SceneEffectLayer layer, EffectSettings settings, float titleTime)
    {
        if (!layer.Effect.Equals("ASCII Title", StringComparison.OrdinalIgnoreCase))
        {
            Ui(_captureProgram, "u_title_enabled", 0);
            Ui(_captureProgram, "u_title_animate", 0);
            U1(_captureProgram, "u_title_time", 0f);
            return;
        }

        string text = string.IsNullOrEmpty(layer.TitleText) ? "GLYPHORÉ" : layer.TitleText;
        string fontName = string.IsNullOrWhiteSpace(layer.TitleFont) ? "monospace" : layer.TitleFont;
        string prefab = string.IsNullOrWhiteSpace(layer.TitlePrefab) ? "System Font" : layer.TitlePrefab;
        bool generatedPrefab = AsciiTitlePrefabGenerator.IsGeneratedPrefab(prefab);
        string signature = string.Join('|',
            settings.Width,
            settings.Height,
            text,
            fontName,
            prefab,
            layer.TitleBold,
            layer.TitleItalic,
            settings.Get("title_letter_spacing").ToString("R", CultureInfo.InvariantCulture),
            settings.Get("title_size").ToString("R", CultureInfo.InvariantCulture),
            settings.Get("title_x").ToString("R", CultureInfo.InvariantCulture),
            settings.Get("title_y").ToString("R", CultureInfo.InvariantCulture));

        if (!_titleTextures.TryGetValue(layer.Id, out var resource))
        {
            resource = new TitleTextureResource();
            _titleTextures[layer.Id] = resource;
        }

        if (resource.Texture == 0 || !string.Equals(resource.Signature, signature, StringComparison.Ordinal))
        {
            int w = Math.Max(2, settings.Width);
            int h = Math.Max(2, settings.Height);
            const float safeWidth = .92f;
            const float safeHeight = .84f;
            float titleScale = (float)Math.Clamp(settings.Get("title_size"), .15, 2.5);
            int letterSpacing = Math.Max(0, (int)Math.Round(settings.Get("title_letter_spacing")));
            float titleX = (float)Math.Clamp(settings.Get("title_x"), -1.0, 1.0);
            float titleY = (float)Math.Clamp(settings.Get("title_y"), -1.0, 1.0);

            byte[] pixels = generatedPrefab
                ? BuildGeneratedPrefabGrid(text, prefab, w, h, safeWidth, safeHeight, titleScale, letterSpacing, titleX, titleY)
                : BuildSystemFontGrid(text, fontName, layer.TitleBold, layer.TitleItalic, w, h, safeWidth, safeHeight, titleScale, letterSpacing, titleX, titleY);

            if (resource.Texture == 0)
            {
                uint texture;
                NativeGl.GenTextures(1, &texture);
                resource.Texture = texture;
            }
            NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 4);
            NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, resource.Texture);
            NativeGl.PixelStorei(NativeGl.GL_UNPACK_ALIGNMENT, 1);
            NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, generatedPrefab ? (int)NativeGl.GL_NEAREST : (int)NativeGl.GL_LINEAR);
            NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, generatedPrefab ? (int)NativeGl.GL_NEAREST : (int)NativeGl.GL_LINEAR);
            fixed (byte* pixelPtr = pixels)
            {
                NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, w, h, 0,
                    NativeGl.GL_BGRA, NativeGl.GL_UNSIGNED_BYTE, (IntPtr)pixelPtr);
            }
            resource.Signature = signature;
        }

        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 4);
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, resource.Texture);
        Ui(_captureProgram, "u_title_mask", 4);
        Ui(_captureProgram, "u_title_enabled", 1);
        Ui(_captureProgram, "u_title_animate", layer.TitleAnimate ? 1 : 0);
        U1(_captureProgram, "u_title_time", layer.TitleAnimate ? titleTime : 0f);
    }

    private static byte[] BuildSystemFontGrid(
        string text,
        string fontName,
        bool bold,
        bool italic,
        int width,
        int height,
        float safeWidth,
        float safeHeight,
        float titleScale,
        int letterSpacing,
        float titleX,
        float titleY)
    {
        const int renderScale = 2;
        int renderW = checked(width * renderScale);
        int renderH = checked(height * renderScale);
        using var bitmap = new SKBitmap(renderW, renderH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        SKFontStyle style = bold && italic ? new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic)
            : bold ? new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            : italic ? new SKFontStyle(SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic)
            : SKFontStyle.Normal;
        using var typeface = SKTypeface.FromFamilyName(fontName, style) ?? SKTypeface.FromFamilyName("monospace", style) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, Math.Max(2f, renderH * .72f)) { Edging = SKFontEdging.SubpixelAntialias };
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true, FilterQuality = SKFilterQuality.High };

        var lines = text.Replace("\r", string.Empty).Split('\n');
        float lineHeight = Math.Max(1f, font.Size * 1.15f);
        float maxWidth = 0;
        foreach (string line in lines)
        {
            float measured = MeasureTracked(font, paint, line, letterSpacing * renderScale);
            maxWidth = Math.Max(maxWidth, measured);
        }
        float totalHeight = Math.Max(lineHeight, lineHeight * Math.Max(1, lines.Length));
        float targetW = renderW * safeWidth;
        float targetH = renderH * safeHeight;
        float fit = Math.Min(1f, Math.Min(targetW / Math.Max(1f, maxWidth), targetH / Math.Max(1f, totalHeight)));
        float finalSize = Math.Max(2f, font.Size * fit * titleScale);
        using var finalFont = new SKFont(typeface, finalSize) { Edging = SKFontEdging.SubpixelAntialias };
        float finalLineHeight = Math.Max(1f, finalSize * 1.15f);
        float finalTracking = letterSpacing * renderScale * (finalSize / Math.Max(1f, font.Size));
        float finalTotalHeight = finalLineHeight * Math.Max(1, lines.Length);

        float y = (renderH - finalTotalHeight) * .5f - finalFont.Metrics.Ascent;
        foreach (string line in lines)
        {
            float lineWidth = MeasureTracked(finalFont, paint, line, finalTracking);
            float x = (renderW - lineWidth) * .5f;
            foreach (var element in EnumerateTextElements(line))
            {
                canvas.DrawText(element, x, y, finalFont, paint);
                x += paint.MeasureText(element);
                if (x < renderW + finalTracking) x += finalTracking;
            }
            y += finalLineHeight;
        }

        var ink = FindVisibleInkBounds(bitmap);
        using var centered = new SKBitmap(renderW, renderH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var cc = new SKCanvas(centered))
        {
            cc.Clear(SKColors.Black);
            float recenterX = ink.Width > 0 ? renderW * .5f - (ink.Left + ink.Width * .5f) : 0;
            float recenterY = ink.Height > 0 ? renderH * .5f - (ink.Top + ink.Height * .5f) : 0;
            float travelX = ink.Width > 0 ? Math.Max(0, (renderW - ink.Width) / 2f) : renderW / 2f;
            float travelY = ink.Height > 0 ? Math.Max(0, (renderH - ink.Height) / 2f) : renderH / 2f;
            using var image = SKImage.FromBitmap(bitmap);
            cc.DrawImage(image, recenterX + titleX * travelX, recenterY - titleY * travelY);
        }

        using var final = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var fc = new SKCanvas(final))
        {
            fc.Clear(SKColors.Black);
            using var image = SKImage.FromBitmap(centered);
            fc.DrawImage(image, new SKRect(0, 0, width, height));
        }
        return BitmapToRgbaBytes(final);
    }

    private static float MeasureTracked(SKFont font, SKPaint paint, string text, float tracking)
    {
        var elements = EnumerateTextElements(text);
        if (elements.Count == 0) return 0;
        float width = 0;
        foreach (string element in elements) width += paint.MeasureText(element);
        return width + tracking * Math.Max(0, elements.Count - 1);
    }

    private static List<string> EnumerateTextElements(string text)
    {
        var result = new List<string>();
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text.Normalize(NormalizationForm.FormC));
        while (enumerator.MoveNext()) result.Add(enumerator.GetTextElement());
        return result;
    }

    private static SKRect FindVisibleInkBounds(SKBitmap bitmap)
    {
        int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            SKColor pixel = bitmap.GetPixel(x, y);
            if (pixel.Red <= 3 && pixel.Green <= 3 && pixel.Blue <= 3) continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        return maxX < minX || maxY < minY ? SKRect.Empty : new SKRect(minX, minY, maxX + 1, maxY + 1);
    }

    private static byte[] BitmapToRgbaBytes(SKBitmap bitmap)
    {
        int count = checked(bitmap.Width * bitmap.Height * 4);
        byte[] result = new byte[count];
        IntPtr pixels = bitmap.GetPixels();
        Marshal.Copy(pixels, result, 0, count);
        // The bitmap is BGRA, while the field historically remains named pixels and the GL upload
        // explicitly uses GL_BGRA. Keep that exact representation for zero-copy texture upload.
        return result;
    }

    private static byte[] BuildGeneratedPrefabGrid(
        string text,
        string prefab,
        int width,
        int height,
        float safeWidth,
        float safeHeight,
        float titleScale,
        int letterSpacing,
        float titleX,
        float titleY)
    {
        string art = AsciiTitlePrefabGenerator.Generate(text, prefab, letterSpacing).Replace("\r", string.Empty);
        Rune[][] rawLines = art.Split('\n').Select(line => line.EnumerateRunes().ToArray()).ToArray();
        if (rawLines.Length == 0) rawLines = [new[] { new Rune(' ') }];

        int minInkX = int.MaxValue, maxInkX = -1, minInkY = int.MaxValue, maxInkY = -1;
        for (int y = 0; y < rawLines.Length; y++)
        {
            bool rowHasInk = false;
            for (int x = 0; x < rawLines[y].Length; x++)
            {
                if (Rune.IsWhiteSpace(rawLines[y][x])) continue;
                rowHasInk = true;
                minInkX = Math.Min(minInkX, x);
                maxInkX = Math.Max(maxInkX, x);
            }
            if (rowHasInk)
            {
                minInkY = Math.Min(minInkY, y);
                maxInkY = Math.Max(maxInkY, y);
            }
        }

        Rune[][] lines;
        if (maxInkX >= minInkX && maxInkY >= minInkY)
        {
            int cropWidth = maxInkX - minInkX + 1;
            lines = rawLines.Skip(minInkY).Take(maxInkY - minInkY + 1)
                .Select(line => line.Length <= minInkX ? Array.Empty<Rune>() : line.Skip(minInkX).Take(Math.Min(cropWidth, line.Length - minInkX)).ToArray())
                .ToArray();
        }
        else lines = rawLines;

        int artWidth = Math.Max(1, lines.Max(line => line.Length));
        int artHeight = Math.Max(1, lines.Length);
        int safeCellsW = Math.Max(1, (int)Math.Floor(width * safeWidth));
        int safeCellsH = Math.Max(1, (int)Math.Floor(height * safeHeight));
        double fitScale = Math.Min(safeCellsW / (double)artWidth, safeCellsH / (double)artHeight);
        double scale = Math.Clamp(fitScale * titleScale, .08, 8.0);
        int drawW = Math.Max(1, (int)Math.Round(artWidth * scale));
        int drawH = Math.Max(1, (int)Math.Round(artHeight * scale));
        int travelX = Math.Max(0, (width - drawW) / 2);
        int travelY = Math.Max(0, (height - drawH) / 2);
        int startX = (width - drawW) / 2 + (int)Math.Round(titleX * travelX);
        int startY = (height - drawH) / 2 - (int)Math.Round(titleY * travelY);
        var mask = new byte[width * height * 4];

        for (int dy = 0; dy < drawH; dy++)
        {
            int sy = Math.Clamp((int)Math.Floor(dy / scale), 0, artHeight - 1);
            Rune[] sourceLine = lines[sy];
            for (int dx = 0; dx < drawW; dx++)
            {
                int sx = Math.Clamp((int)Math.Floor(dx / scale), 0, artWidth - 1);
                if (sx >= sourceLine.Length) continue;
                Rune rune = sourceLine[sx];
                if (rune.Value == ' ' || rune.Value == '\t') continue;
                int x = startX + dx, y = startY + dy;
                if ((uint)x >= (uint)width || (uint)y >= (uint)height) continue;
                int offset = (y * width + x) * 4;
                mask[offset] = mask[offset + 1] = mask[offset + 2] = mask[offset + 3] = 255;
            }
        }
        return mask;
    }
}
